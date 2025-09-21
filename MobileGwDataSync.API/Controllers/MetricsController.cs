using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.API.Models.Responses.Metrics;
using MobileGwDataSync.API.Models.Responses.Sync;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Entities;
using System.Diagnostics;
using System.Text;

namespace MobileGwDataSync.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController : ControllerBase
    {
        private readonly ServiceDbContext _context;
        private readonly IMetricsService? _metricsService;
        private readonly ILogger<MetricsController> _logger;

        public MetricsController(
            ServiceDbContext context,
            ILogger<MetricsController> logger,
            IMetricsService? metricsService = null)
        {
            _context = context;
            _metricsService = metricsService;
            _logger = logger;
        }

        /// <summary>
        /// Получить текущие метрики
        /// </summary>
        [HttpGet("current")]
        public ActionResult<Dictionary<string, double>> GetCurrentMetrics()
        {
            var metrics = _metricsService?.GetCurrentMetrics() ?? new Dictionary<string, double>();

            // Добавляем системные метрики
            var process = Process.GetCurrentProcess();
            metrics["process_memory_mb"] = process.WorkingSet64 / (1024.0 * 1024.0);
            metrics["process_cpu_seconds"] = process.TotalProcessorTime.TotalSeconds;
            metrics["gc_memory_mb"] = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            metrics["thread_count"] = Process.GetCurrentProcess().Threads.Count;

            return Ok(metrics);
        }

        /// <summary>
        /// Получить метрики производительности
        /// </summary>
        [HttpGet("performance")]
        public async Task<ActionResult<PerformanceMetricsDTO>> GetPerformanceMetrics(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            var metrics = await _context.PerformanceMetrics
                .Where(m => m.RecordedAt >= from && m.RecordedAt <= to)
                .GroupBy(m => m.MetricName)
                .Select(g => new
                {
                    MetricName = g.Key,
                    AverageValue = g.Average(m => m.MetricValue),
                    MinValue = g.Min(m => m.MetricValue),
                    MaxValue = g.Max(m => m.MetricValue),
                    Count = g.Count()
                })
                .ToListAsync();

            var runs = await _context.SyncRuns
                .Where(r => r.StartTime >= from && r.StartTime <= to)
                .ToListAsync();

            var performanceDTO = new PerformanceMetricsDTO
            {
                Period = new { From = from.Value, To = to.Value },
                SyncMetrics = new SyncMetricsDTO
                {
                    TotalRuns = runs.Count,
                    SuccessfulRuns = runs.Count(r => r.Status == "Completed"),
                    FailedRuns = runs.Count(r => r.Status == "Failed"),
                    AverageRecordsPerRun = runs.Average(r => (double)r.RecordsProcessed),
                    TotalRecordsProcessed = runs.Sum(r => r.RecordsProcessed),
                    AverageDurationSeconds = runs
                        .Where(r => r.EndTime.HasValue)
                        .Select(r => (r.EndTime!.Value - r.StartTime!).TotalSeconds)
                        .DefaultIfEmpty(0)
                        .Average()
                },
                SystemMetrics = metrics.ToDictionary(
                    m => m.MetricName,
                    m => new MetricSummaryDTO
                    {
                        Average = m.AverageValue,
                        Min = m.MinValue,
                        Max = m.MaxValue,
                        Count = m.Count
                    }
                )
            };

            return Ok(performanceDTO);
        }

        /// <summary>
        /// Экспорт метрик в формате Prometheus
        /// </summary>
        [HttpGet("prometheus")]
        [Produces("text/plain")]
        public async Task<IActionResult> GetPrometheusMetrics()
        {
            var sb = new StringBuilder();

            // Метрики из сервиса
            var currentMetrics = _metricsService?.GetCurrentMetrics() ?? new Dictionary<string, double>();
            foreach (var metric in currentMetrics)
            {
                sb.AppendLine($"# TYPE {metric.Key} gauge");
                sb.AppendLine($"{metric.Key} {metric.Value}");
            }

            // Системные метрики
            var process = Process.GetCurrentProcess();
            sb.AppendLine("# TYPE process_memory_bytes gauge");
            sb.AppendLine($"process_memory_bytes {process.WorkingSet64}");

            sb.AppendLine("# TYPE process_cpu_seconds_total counter");
            sb.AppendLine($"process_cpu_seconds_total {process.TotalProcessorTime.TotalSeconds}");

            // Метрики БД
            var jobCount = await _context.SyncJobs.CountAsync();
            sb.AppendLine("# TYPE sync_jobs_total gauge");
            sb.AppendLine($"sync_jobs_total {jobCount}");

            var activeRuns = await _context.SyncRuns.CountAsync(r => r.Status == "InProgress");
            sb.AppendLine("# TYPE sync_runs_active gauge");
            sb.AppendLine($"sync_runs_active {activeRuns}");

            return Content(sb.ToString(), "text/plain; version=0.0.4");
        }

        /// <summary>
        /// Получить метрики по конкретной задаче
        /// </summary>
        [HttpGet("jobs/{jobId}")]
        public async Task<ActionResult<JobMetricsDTO>> GetJobMetrics(
            string jobId,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            from ??= DateTime.UtcNow.AddDays(-30);
            to ??= DateTime.UtcNow;

            var runs = await _context.SyncRuns
                .Where(r => r.JobId == jobId && r.StartTime >= from && r.StartTime <= to)
                .Include(r => r.Metrics)
                .ToListAsync();

            if (!runs.Any())
            {
                return NotFound(new { message = $"No metrics found for job '{jobId}'" });
            }

            var metrics = new JobMetricsDTO
            {
                JobId = jobId,
                Period = new { From = from.Value, To = to.Value },
                TotalRuns = runs.Count,
                SuccessRate = runs.Count > 0
                    ? (double)runs.Count(r => r.Status == "Completed") / runs.Count * 100
                    : 0,
                AverageRecords = runs.Average(r => (double)r.RecordsProcessed),
                TotalRecords = runs.Sum(r => r.RecordsProcessed),
                AverageDuration = runs
                    .Where(r => r.EndTime.HasValue)
                    .Select(r => (r.EndTime!.Value - r.StartTime!).TotalSeconds)
                    .DefaultIfEmpty(0)
                    .Average(),
                Throughput = CalculateThroughput(runs),
                ErrorRate = runs.Count > 0
                    ? (double)runs.Count(r => r.Status == "Failed") / runs.Count * 100
                    : 0,
                Timeline = GenerateTimeline(runs)
            };

            return Ok(metrics);
        }

        private double CalculateThroughput(List<SyncRunEntity> runs)
        {
            var completedRuns = runs.Where(r => r.EndTime.HasValue && r.RecordsProcessed > 0).ToList();
            if (!completedRuns.Any()) return 0;

            var totalRecords = completedRuns.Sum(r => r.RecordsProcessed);
            var totalSeconds = completedRuns.Sum(r => (r.EndTime!.Value - r.StartTime).TotalSeconds);

            return totalSeconds > 0 ? totalRecords / totalSeconds : 0;
        }

        private List<TimelinePointDTO> GenerateTimeline(List<SyncRunEntity> runs)
        {
            return runs
                .GroupBy(r => r.StartTime.Date)
                .Select(g => new TimelinePointDTO
                {
                    Date = g.Key,
                    Runs = g.Count(),
                    SuccessfulRuns = g.Count(r => r.Status == "Completed"),
                    Records = g.Sum(r => r.RecordsProcessed),
                    AverageDuration = g
                        .Where(r => r.EndTime.HasValue)
                        .Select(r => (r.EndTime!.Value - r.StartTime).TotalSeconds)
                        .DefaultIfEmpty(0)
                        .Average()
                })
                .OrderBy(t => t.Date)
                .ToList();
        }
    }
}

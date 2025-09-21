using Asp.Versioning;
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
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
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

        /// <summary>
        /// Получить метрики в формате Prometheus
        /// </summary>
        [HttpGet("prometheus")]
        [Produces("text/plain")]
        public async Task<IActionResult> GetPrometheusMetrics()
        {
            try
            {
                // Собираем кастомные метрики из БД
                var runs = await _context.SyncRuns
                    .Where(r => r.StartTime >= DateTime.UtcNow.AddHours(-24))
                    .GroupBy(r => new { r.JobId, r.Status })
                    .Select(g => new
                    {
                        JobId = g.Key.JobId,
                        Status = g.Key.Status,
                        Count = g.Count(),
                        TotalRecords = g.Sum(r => r.RecordsProcessed)
                    })
                    .ToListAsync();

                var sb = new StringBuilder();

                // Добавляем заголовок
                sb.AppendLine("# HELP sync_runs_total Total number of sync runs by job and status");
                sb.AppendLine("# TYPE sync_runs_total counter");

                foreach (var run in runs)
                {
                    sb.AppendLine($"sync_runs_total{{job=\"{run.JobId}\",status=\"{run.Status}\"}} {run.Count}");
                }

                sb.AppendLine();
                sb.AppendLine("# HELP sync_records_processed_total Total records processed by job");
                sb.AppendLine("# TYPE sync_records_processed_total counter");

                foreach (var run in runs)
                {
                    sb.AppendLine($"sync_records_processed_total{{job=\"{run.JobId}\"}} {run.TotalRecords}");
                }

                // Активные синхронизации
                var activeCount = await _context.SyncRuns
                    .CountAsync(r => r.Status == "InProgress");

                sb.AppendLine();
                sb.AppendLine("# HELP sync_jobs_active Number of currently active sync jobs");
                sb.AppendLine("# TYPE sync_jobs_active gauge");
                sb.AppendLine($"sync_jobs_active {activeCount}");

                // Системные метрики
                var process = Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / (1024 * 1024);

                sb.AppendLine();
                sb.AppendLine("# HELP process_memory_mb Process memory usage in MB");
                sb.AppendLine("# TYPE process_memory_mb gauge");
                sb.AppendLine($"process_memory_mb {memoryMB}");

                return Content(sb.ToString(), "text/plain; version=0.0.4");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate Prometheus metrics");
                return StatusCode(500, "Failed to generate metrics");
            }
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

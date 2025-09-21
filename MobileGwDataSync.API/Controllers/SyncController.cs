using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.API.Models.Responses.Jobs;
using MobileGwDataSync.API.Models.Responses.Metrics;
using MobileGwDataSync.API.Models.Responses.Sync;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Data.Context;

namespace MobileGwDataSync.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly ISyncService _syncService;
        private readonly ServiceDbContext _context;
        private readonly ILogger<SyncController> _logger;

        public SyncController(
            ISyncService syncService,
            ServiceDbContext context,
            ILogger<SyncController> logger)
        {
            _syncService = syncService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Получить историю синхронизаций
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<IEnumerable<SyncRunDTO>>> GetSyncHistory(
            [FromQuery] string? jobId = null,
            [FromQuery] int limit = 50,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? status = null)
        {
            var query = _context.SyncRuns.AsQueryable();

            // Фильтры
            if (!string.IsNullOrEmpty(jobId))
                query = query.Where(r => r.JobId == jobId);

            if (from.HasValue)
                query = query.Where(r => r.StartTime >= from.Value);

            if (to.HasValue)
                query = query.Where(r => r.StartTime <= to.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            var runs = await query
                .OrderByDescending(r => r.StartTime)
                .Take(limit)
                .Include(r => r.Job)
                .Select(r => new SyncRunDTO
                {
                    Id = r.Id,
                    JobId = r.JobId,
                    JobName = r.Job.Name,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    Status = r.Status,
                    RecordsProcessed = r.RecordsProcessed,
                    RecordsFetched = r.RecordsFetched,
                    ErrorMessage = r.ErrorMessage,
                    Duration = r.EndTime.HasValue
                        ? (r.EndTime.Value - r.StartTime).TotalSeconds
                        : (double?)null
                })
                .ToListAsync();

            return Ok(runs);
        }

        /// <summary>
        /// Получить детальную информацию о запуске синхронизации
        /// </summary>
        [HttpGet("runs/{runId}")]
        public async Task<ActionResult<SyncRunDetailDTO>> GetSyncRun(Guid runId)
        {
            var run = await _context.SyncRuns
                .Include(r => r.Job)
                .Include(r => r.Steps)
                .Include(r => r.Metrics)
                .FirstOrDefaultAsync(r => r.Id == runId);

            if (run == null)
            {
                return NotFound(new { message = $"Sync run with ID '{runId}' not found" });
            }

            var dto = new SyncRunDetailDTO
            {
                Id = run.Id,
                JobId = run.JobId,
                JobName = run.Job.Name,
                StartTime = run.StartTime,
                EndTime = run.EndTime,
                Status = run.Status,
                RecordsProcessed = run.RecordsProcessed,
                RecordsFetched = run.RecordsFetched,
                ErrorMessage = run.ErrorMessage,
                Duration = run.EndTime.HasValue
                    ? (run.EndTime.Value - run.StartTime).TotalSeconds
                    : (double?)null,
                Steps = run.Steps.Select(s => new SyncRunStepDTO
                {
                    Id = s.Id,
                    StepName = s.StepName,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Status = s.Status,
                    Details = s.Details,
                    DurationMs = s.DurationMs
                }).OrderBy(s => s.StartTime).ToList(),
                Metrics = run.Metrics.Select(m => new MetricDTO
                {
                    Name = m.MetricName,
                    Value = m.MetricValue,
                    Unit = m.Unit,
                    RecordedAt = m.RecordedAt
                }).ToList()
            };

            return Ok(dto);
        }

        /// <summary>
        /// Запустить синхронизацию для задачи
        /// </summary>
        [HttpPost("trigger/{jobId}")]
        public async Task<ActionResult<SyncTriggerResponseDTO>> TriggerSync(string jobId)
        {
            var job = await _context.SyncJobs.FindAsync(jobId);
            if (job == null)
            {
                return NotFound(new { message = $"Job with ID '{jobId}' not found" });
            }

            // Проверяем, не выполняется ли задача сейчас
            var runningJob = await _context.SyncRuns
                .Where(r => r.JobId == jobId && r.Status == "InProgress")
                .FirstOrDefaultAsync();

            if (runningJob != null)
            {
                return Conflict(new
                {
                    message = "Job is already running",
                    runId = runningJob.Id,
                    startTime = runningJob.StartTime
                });
            }

            try
            {
                // Запускаем синхронизацию асинхронно
                var result = await _syncService.ExecuteSyncAsync(jobId);

                _logger.LogInformation("Manually triggered sync for job: {JobId}", jobId);

                return Ok(new SyncTriggerResponseDTO
                {
                    Success = result.Success,
                    Message = result.Success
                        ? "Synchronization completed successfully"
                        : "Synchronization completed with errors",
                    RecordsProcessed = result.RecordsProcessed,
                    RecordsFailed = result.RecordsFailed,
                    Duration = result.Duration.TotalSeconds,
                    Errors = result.Errors,
                    Metrics = result.Metrics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger sync for job: {JobId}", jobId);
                return StatusCode(500, new
                {
                    message = "Failed to trigger synchronization",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Отменить выполняющуюся синхронизацию
        /// </summary>
        [HttpPost("cancel/{runId}")]
        public async Task<IActionResult> CancelSync(Guid runId)
        {
            var cancelled = await _syncService.CancelSyncAsync(runId);

            if (!cancelled)
            {
                return NotFound(new
                {
                    message = "Sync run not found or not in progress",
                    runId = runId
                });
            }

            _logger.LogInformation("Cancelled sync run: {RunId}", runId);

            return Ok(new { message = "Synchronization cancelled", runId = runId });
        }

        /// <summary>
        /// Получить статистику синхронизаций
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<SyncStatisticsDTO>> GetStatistics(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var query = _context.SyncRuns.AsQueryable();

            // По умолчанию за последние 30 дней
            from ??= DateTime.UtcNow.AddDays(-30);
            to ??= DateTime.UtcNow;

            query = query.Where(r => r.StartTime >= from && r.StartTime <= to);

            var runs = await query.ToListAsync();

            var statistics = new SyncStatisticsDTO
            {
                TotalRuns = runs.Count,
                SuccessfulRuns = runs.Count(r => r.Status == "Completed"),
                FailedRuns = runs.Count(r => r.Status == "Failed"),
                CancelledRuns = runs.Count(r => r.Status == "Cancelled"),
                TotalRecordsProcessed = runs.Sum(r => r.RecordsProcessed),
                AverageDuration = runs
                    .Where(r => r.EndTime.HasValue)
                    .Select(r => (r.EndTime.Value - r.StartTime).TotalSeconds)
                    .DefaultIfEmpty(0)
                    .Average(),
                SuccessRate = runs.Count > 0
                    ? (double)runs.Count(r => r.Status == "Completed") / runs.Count * 100
                    : 0,
                JobStatistics = await GetJobStatistics(from.Value, to.Value)
            };

            return Ok(statistics);
        }

        /// <summary>
        /// Получить активные синхронизации
        /// </summary>
        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<ActiveSyncDTO>>> GetActiveSyncs()
        {
            var activeRuns = await _context.SyncRuns
                .Where(r => r.Status == "InProgress")
                .Include(r => r.Job)
                .Include(r => r.Steps)
                .Select(r => new ActiveSyncDTO
                {
                    RunId = r.Id,
                    JobId = r.JobId,
                    JobName = r.Job.Name,
                    StartTime = r.StartTime,
                    RunningFor = (DateTime.UtcNow - r.StartTime).TotalSeconds,
                    RecordsProcessed = r.RecordsProcessed,
                    CurrentStep = r.Steps
                        .Where(s => s.Status == "InProgress")
                        .Select(s => s.StepName)
                        .FirstOrDefault() ?? "Unknown"
                })
                .ToListAsync();

            return Ok(activeRuns);
        }

        private async Task<List<JobStatisticDTO>> GetJobStatistics(DateTime from, DateTime to)
        {
            return await _context.SyncRuns
                .Where(r => r.StartTime >= from && r.StartTime <= to)
                .GroupBy(r => new { r.JobId, r.Job.Name })
                .Select(g => new JobStatisticDTO
                {
                    JobId = g.Key.JobId,
                    JobName = g.Key.Name,
                    TotalRuns = g.Count(),
                    SuccessfulRuns = g.Count(r => r.Status == "Completed"),
                    FailedRuns = g.Count(r => r.Status == "Failed"),
                    TotalRecords = g.Sum(r => r.RecordsProcessed),
                    AverageDuration = g
                        .Where(r => r.EndTime.HasValue)
                        .Select(r => (r.EndTime.Value - r.StartTime).TotalSeconds)
                        .DefaultIfEmpty(0)
                        .Average()
                })
                .ToListAsync();
        }
    }
}
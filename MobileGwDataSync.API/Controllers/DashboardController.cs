using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Data.Context;
using System.Security.Claims;

namespace MobileGwDataSync.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly ServiceDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ISyncService _syncService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            ServiceDbContext context,
            IConfiguration configuration,
            ISyncService syncService,
            ILogger<DashboardController> logger)
        {
            _context = context;
            _configuration = configuration;
            _syncService = syncService;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous] // HTML страница доступна всем
        [Produces("text/html")]
        public IActionResult Index()
        {
            return Redirect("/dashboard.html");
        }

        [HttpGet("data")]
        [Authorize] // Требует JWT токен
        public async Task<IActionResult> GetDashboardData()
        {
            // Логируем пользователя
            var username = User.Identity?.Name;
            _logger.LogInformation("Dashboard data requested by user: {User}", username);

            var now = DateTime.UtcNow;
            var last24Hours = now.AddHours(-24);
            var last7Days = now.AddDays(-7);

            // Последние запуски
            var recentRuns = await _context.SyncRuns
                .Where(r => r.StartTime >= last24Hours)
                .OrderByDescending(r => r.StartTime)
                .Take(10)
                .Select(r => new
                {
                    r.Id,
                    r.JobId,
                    JobName = r.Job.Name,
                    r.StartTime,
                    r.EndTime,
                    r.Status,
                    r.RecordsProcessed,
                    Duration = r.EndTime.HasValue
                        ? (r.EndTime.Value - r.StartTime).TotalSeconds
                        : 0
                })
                .ToListAsync();

            // Статистика за 24 часа
            var stats24h = await _context.SyncRuns
                .Where(r => r.StartTime >= last24Hours)
                .GroupBy(r => r.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            // График за 7 дней
            var chartData = await _context.SyncRuns
                .Where(r => r.StartTime >= last7Days)
                .GroupBy(r => r.StartTime.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Count(),
                    Success = g.Count(r => r.Status == "Completed"),
                    Failed = g.Count(r => r.Status == "Failed"),
                    Records = g.Sum(r => r.RecordsProcessed)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            // Активные задачи
            var activeJobs = await _context.SyncJobs
                .Where(j => j.IsEnabled)
                .Select(j => new
                {
                    j.Id,
                    j.Name,
                    j.CronExpression,
                    j.LastRunAt,
                    j.NextRunAt,
                    LastRunStatus = j.Runs
                        .OrderByDescending(r => r.StartTime)
                        .Select(r => r.Status)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new
            {
                recentRuns,
                stats24h,
                chartData,
                activeJobs,
                serverTime = now,
                user = username
            });
        }

        [HttpPost("trigger/{jobId}")]
        [Authorize(Policy = "DashboardUser")] // Требует роль Dashboard пользователя
        public async Task<IActionResult> TriggerJob(string jobId)
        {
            // Проверяем роль пользователя
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Viewer")
            {
                return Forbid("Viewers cannot trigger jobs");
            }

            try
            {
                var job = await _context.SyncJobs.FindAsync(jobId);
                if (job == null)
                    return NotFound(new { message = "Job not found" });

                // Очистка зависших задач
                var staleTime = DateTime.UtcNow.AddMinutes(-30);
                var staleJobs = await _context.SyncRuns
                    .Where(r => r.JobId == jobId && r.Status == "InProgress" && r.StartTime < staleTime)
                    .ToListAsync();

                foreach (var staleJob in staleJobs)
                {
                    staleJob.Status = "Failed";
                    staleJob.EndTime = DateTime.UtcNow;
                    staleJob.ErrorMessage = "Job terminated due to timeout";
                }

                await _context.SaveChangesAsync();

                // Проверка актуальных задач
                var runningJob = await _context.SyncRuns
                    .Where(r => r.JobId == jobId && r.Status == "InProgress")
                    .AnyAsync();

                if (runningJob)
                    return BadRequest(new { message = "Job is already running" });

                _logger.LogInformation("User {User} triggered job {JobId}", User.Identity?.Name, jobId);

                // Запускаем синхронизацию
                var result = await _syncService.ExecuteSyncAsync(jobId);

                return Ok(new
                {
                    message = "Job completed",
                    success = result.Success,
                    recordsProcessed = result.RecordsProcessed,
                    errors = result.Errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger job {JobId}", jobId);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("logs/{runId}")]
        [Authorize]
        public async Task<IActionResult> GetRunLogs(Guid runId)
        {
            var steps = await _context.SyncRunSteps
                .Where(s => s.RunId == runId)
                .OrderBy(s => s.StartTime)
                .Select(s => new
                {
                    s.StepName,
                    s.StartTime,
                    s.EndTime,
                    s.Status,
                    s.Details,
                    s.DurationMs
                })
                .ToListAsync();

            return Ok(steps);
        }

        [HttpGet("health-status")]
        [Authorize]
        public async Task<IActionResult> GetHealthStatus([FromServices] IDataSource dataSource)
        {
            var checks = new Dictionary<string, object>();

            // SQLite check
            try
            {
                await _context.Database.CanConnectAsync();
                var jobCount = await _context.SyncJobs.CountAsync();
                checks["sqlite"] = new
                {
                    status = "healthy",
                    message = $"Connected, {jobCount} jobs configured"
                };
            }
            catch (Exception ex)
            {
                checks["sqlite"] = new { status = "unhealthy", message = ex.Message };
            }

            // 1C check
            try
            {
                var oneCHealthy = await dataSource.TestConnectionAsync();
                checks["onec"] = new
                {
                    status = oneCHealthy ? "healthy" : "unhealthy",
                    message = oneCHealthy ? "1C API accessible" : "Cannot connect to 1C"
                };
            }
            catch (Exception ex)
            {
                checks["onec"] = new { status = "unhealthy", message = ex.Message };
            }

            // Memory check
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            checks["memory"] = new
            {
                status = memoryMB < 500 ? "healthy" : memoryMB < 1000 ? "degraded" : "unhealthy",
                message = $"{memoryMB} MB used",
                value = memoryMB
            };

            return Ok(checks);
        }
    }
}
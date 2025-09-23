using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.API.Services;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Data.Context;
using StackExchange.Redis;
using System;
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
        private readonly ICommandQueue _commandQueue;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            ServiceDbContext context,
            IConfiguration configuration,
            ISyncService syncService,
            ICommandQueue commandQueue,
            ILogger<DashboardController> logger)
        {
            _context = context;
            _configuration = configuration;
            _syncService = syncService;
            _commandQueue = commandQueue;
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
        [Authorize(Policy = "CanTriggerJobs")]
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
                // Сначала пытаемся отправить через Redis
                try
                {
                    await _commandQueue.PublishJobTriggerAsync(jobId, User.Identity?.Name ?? "Dashboard");
                    _logger.LogInformation("Job {JobId} trigger command sent to Redis queue by {User}",
                        jobId, User.Identity?.Name);

                    return Ok(new
                    {
                        success = true,
                        message = "Задача поставлена в очередь выполнения",
                        method = "redis_queue",
                        jobId = jobId
                    });
                }
                catch (RedisConnectionException ex)
                {
                    _logger.LogWarning(ex, "Redis unavailable, falling back to direct execution for job {JobId}", jobId);

                    // Fallback: прямой вызов если Redis недоступен
                    var result = await _syncService.ExecuteSyncAsync(jobId);

                    if (result.Success)
                    {
                        return Ok(new
                        {
                            success = true,
                            message = $"Задача выполнена напрямую (Redis недоступен). " +
                                     $"Обработано: {result.RecordsProcessed} записей за {result.Duration.TotalSeconds:F2} сек",
                            method = "direct",
                            jobId = jobId,
                            details = new
                            {
                                recordsProcessed = result.RecordsProcessed,
                                recordsFailed = result.RecordsFailed,
                                duration = result.Duration.TotalSeconds,
                                metrics = result.Metrics
                            }
                        });
                    }
                    else
                    {
                        return Ok(new
                        {
                            success = false,
                            message = "Ошибка выполнения задачи",
                            method = "direct",
                            jobId = jobId,
                            errors = result.Errors,
                            details = new
                            {
                                recordsProcessed = result.RecordsProcessed,
                                recordsFailed = result.RecordsFailed,
                                duration = result.Duration.TotalSeconds
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering job {JobId}", jobId);
                return StatusCode(500, new
                {
                    success = false,
                    error = "Внутренняя ошибка при запуске задачи",
                    message = ex.Message
                });
            }
        }

        [HttpGet("status/{jobId}")]
        [Authorize]
        public async Task<IActionResult> GetJobStatus(string jobId)
        {
            try
            {
                // Проверяем, существует ли задача
                var job = await _context.SyncJobs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.Id == jobId);

                if (job == null)
                {
                    return NotFound(new { error = $"Job with ID '{jobId}' not found" });
                }

                // Получаем последний запуск
                var lastRun = await _context.SyncRuns
                    .Where(r => r.JobId == jobId)
                    .OrderByDescending(r => r.StartTime)
                    .Select(r => new
                    {
                        r.Id,
                        r.StartTime,
                        r.EndTime,
                        r.Status,
                        r.RecordsProcessed,
                        r.RecordsFetched,  // Вместо RecordsFailed
                        r.ErrorMessage,
                        Metadata = r.Metadata  // Вместо TriggeredBy можно хранить в Metadata
                    })
                    .FirstOrDefaultAsync();

                // Проверяем статус в Redis (если задача в очереди)
                string queueStatus = "idle";
                try
                {
                    var isInQueue = await _commandQueue.IsJobInQueueAsync(jobId);
                    if (isInQueue)
                    {
                        queueStatus = "queued";
                    }
                }
                catch (RedisConnectionException)
                {
                    _logger.LogDebug("Redis unavailable for queue status check");
                }

                // Определяем текущий статус
                string currentStatus;
                string statusMessage;

                if (lastRun != null && lastRun.EndTime == null && lastRun.Status == "Running")
                {
                    // Задача выполняется
                    currentStatus = "running";
                    statusMessage = $"Задача выполняется с {lastRun.StartTime:HH:mm:ss}";
                }
                else if (lastRun != null && (DateTime.UtcNow - lastRun.StartTime).TotalSeconds < 5)
                {
                    // Задача только что запущена
                    currentStatus = "starting";
                    statusMessage = "Задача запускается...";
                }
                else if (queueStatus == "queued")
                {
                    currentStatus = "queued";
                    statusMessage = "Задача в очереди на выполнение";
                }
                else
                {
                    currentStatus = "idle";
                    statusMessage = lastRun != null
                        ? $"Последний запуск: {lastRun.Status} в {lastRun.StartTime:HH:mm:ss}"
                        : "Задача еще не запускалась";
                }

                // Парсим metadata если есть
                string? triggeredBy = null;
                if (!string.IsNullOrEmpty(lastRun?.Metadata))
                {
                    try
                    {
                        var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(lastRun.Metadata);
                        triggeredBy = metadata?.GetValueOrDefault("triggeredBy")?.ToString();
                    }
                    catch { /* Игнорируем ошибки парсинга */ }
                }

                return Ok(new
                {
                    jobId = jobId,
                    jobName = job.Name,
                    status = currentStatus,
                    message = statusMessage,
                    lastRun = lastRun != null ? new
                    {
                        lastRun.Id,
                        lastRun.StartTime,
                        lastRun.EndTime,
                        lastRun.Status,
                        lastRun.RecordsProcessed,
                        lastRun.RecordsFetched,
                        lastRun.ErrorMessage,
                        TriggeredBy = triggeredBy
                    } : null,
                    nextRunAt = job.NextRunAt,
                    isEnabled = job.IsEnabled
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting status for job {JobId}", jobId);
                return StatusCode(500, new { error = "Ошибка получения статуса задачи" });
            }
        }

        [HttpGet("queue-status")]
        [Authorize]
        public async Task<IActionResult> GetQueueStatus()
        {
            try
            {
                var queueInfo = new
                {
                    isRedisAvailable = false,
                    queueLength = 0,
                    message = "Информация о очереди недоступна"
                };

                try
                {
                    // Проверяем доступность Redis
                    var redis = HttpContext.RequestServices.GetService<IConnectionMultiplexer>();
                    if (redis != null && redis.IsConnected)
                    {
                        var db = redis.GetDatabase();
                        var queueLength = await db.ListLengthAsync("job:commands");

                        queueInfo = new
                        {
                            isRedisAvailable = true,
                            queueLength = (int)queueLength,
                            message = queueLength > 0
                                ? $"В очереди {queueLength} команд"
                                : "Очередь пуста"
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get Redis queue status");
                }

                return Ok(queueInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting queue status");
                return StatusCode(500, new { error = "Ошибка получения статуса очереди" });
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
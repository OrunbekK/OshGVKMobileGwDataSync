using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.API.Models.Responses.Health;
using MobileGwDataSync.Data.Context;
using System.Diagnostics;

namespace MobileGwDataSync.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ServiceDbContext _serviceContext;
        private readonly BusinessDbContext _businessContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            ServiceDbContext serviceContext,
            BusinessDbContext businessContext,
            IHttpClientFactory httpClientFactory,
            ILogger<HealthController> logger)
        {
            _serviceContext = serviceContext;
            _businessContext = businessContext;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Общий health check
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<HealthCheckDTO>> GetHealth()
        {
            var health = new HealthCheckDTO
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Uptime = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                Checks = new Dictionary<string, ComponentHealthDTO>()
            };

            // Проверка SQLite
            var sqliteHealth = await CheckSQLiteHealth();
            health.Checks["SQLite"] = sqliteHealth;

            // Проверка SQL Server
            var sqlServerHealth = await CheckSqlServerHealth();
            health.Checks["SqlServer"] = sqlServerHealth;

            // Проверка 1C API
            var oneCHealth = await CheckOneCHealth();
            health.Checks["OneC"] = oneCHealth;

            // Проверка памяти
            var memoryHealth = CheckMemoryHealth();
            health.Checks["Memory"] = memoryHealth;

            // Определяем общий статус
            if (health.Checks.Any(c => c.Value.Status == "Unhealthy"))
            {
                health.Status = "Unhealthy";
            }
            else if (health.Checks.Any(c => c.Value.Status == "Degraded"))
            {
                health.Status = "Degraded";
            }

            return health.Status == "Healthy" ? Ok(health) : StatusCode(503, health);
        }

        /// <summary>
        /// Проверка готовности сервиса
        /// </summary>
        [HttpGet("ready")]
        public async Task<IActionResult> Ready()
        {
            try
            {
                // Быстрая проверка основных компонентов
                await _serviceContext.Database.CanConnectAsync();
                await _businessContext.Database.CanConnectAsync();

                return Ok(new { status = "Ready", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Service not ready");
                return StatusCode(503, new { status = "Not Ready", error = ex.Message });
            }
        }

        /// <summary>
        /// Проверка живости сервиса
        /// </summary>
        [HttpGet("live")]
        public IActionResult Live()
        {
            return Ok(new { status = "Alive", timestamp = DateTime.UtcNow });
        }

        private async Task<ComponentHealthDTO> CheckSQLiteHealth()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var canConnect = await _serviceContext.Database.CanConnectAsync();

                if (!canConnect)
                {
                    return new ComponentHealthDTO
                    {
                        Status = "Unhealthy",
                        Description = "Cannot connect to SQLite database",
                        ResponseTime = stopwatch.ElapsedMilliseconds
                    };
                }

                // Проверяем количество записей
                var jobCount = await _serviceContext.SyncJobs.CountAsync();
                var runCount = await _serviceContext.SyncRuns.CountAsync();

                return new ComponentHealthDTO
                {
                    Status = "Healthy",
                    Description = "SQLite is operational",
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Jobs"] = jobCount,
                        ["Runs"] = runCount,
                        ["DatabaseSize"] = GetDatabaseSize()
                    }
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealthDTO
                {
                    Status = "Unhealthy",
                    Description = $"SQLite error: {ex.Message}",
                    ResponseTime = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private async Task<ComponentHealthDTO> CheckSqlServerHealth()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var connection = _businessContext.CreateConnection();
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await Task.Run(() => command.ExecuteScalar());

                return new ComponentHealthDTO
                {
                    Status = "Healthy",
                    Description = "SQL Server is operational",
                    ResponseTime = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealthDTO
                {
                    Status = "Unhealthy",
                    Description = $"SQL Server error: {ex.Message}",
                    ResponseTime = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private async Task<ComponentHealthDTO> CheckOneCHealth()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var client = _httpClientFactory.CreateClient("OneC");
                client.Timeout = TimeSpan.FromSeconds(5);

                var response = await client.GetAsync("subscribers?limit=1");

                if (response.IsSuccessStatusCode)
                {
                    return new ComponentHealthDTO
                    {
                        Status = "Healthy",
                        Description = "1C API is accessible",
                        ResponseTime = stopwatch.ElapsedMilliseconds
                    };
                }
                else
                {
                    return new ComponentHealthDTO
                    {
                        Status = "Degraded",
                        Description = $"1C API returned status: {response.StatusCode}",
                        ResponseTime = stopwatch.ElapsedMilliseconds
                    };
                }
            }
            catch (TaskCanceledException)
            {
                return new ComponentHealthDTO
                {
                    Status = "Unhealthy",
                    Description = "1C API timeout",
                    ResponseTime = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealthDTO
                {
                    Status = "Unhealthy",
                    Description = $"1C API error: {ex.Message}",
                    ResponseTime = stopwatch.ElapsedMilliseconds
                };
            }
        }

        /// <summary>
        /// Получить системный health check (использует встроенный HealthCheckService)
        /// </summary>
        [HttpGet("system")]
        public async Task<IActionResult> GetSystemHealth(
            [FromServices] IServiceProvider serviceProvider)
        {
            // Проверяем, зарегистрирован ли HealthCheckService
            var healthCheckService = serviceProvider.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();

            if (healthCheckService == null)
            {
                return Ok(new
                {
                    message = "HealthCheckService not registered. Using custom health checks only.",
                    customHealthEndpoint = "/api/v1/health"
                });
            }

            var result = await healthCheckService.CheckHealthAsync();

            var response = new
            {
                status = result.Status.ToString(),
                totalDuration = result.TotalDuration.TotalMilliseconds,
                entries = result.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds,
                    description = e.Value.Description,
                    tags = e.Value.Tags.ToList(),
                    data = e.Value.Data
                })
            };

            return result.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
                ? Ok(response)
                : StatusCode(503, response);
        }

        private ComponentHealthDTO CheckMemoryHealth()
        {
            var process = Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            var gcMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);

            var status = memoryMB < 500 ? "Healthy" : memoryMB < 1000 ? "Degraded" : "Unhealthy";

            return new ComponentHealthDTO
            {
                Status = status,
                Description = $"Memory usage: {memoryMB} MB",
                ResponseTime = 0,
                Metadata = new Dictionary<string, object>
                {
                    ["WorkingSetMB"] = memoryMB,
                    ["GCMemoryMB"] = gcMemoryMB,
                    ["Gen0Collections"] = GC.CollectionCount(0),
                    ["Gen1Collections"] = GC.CollectionCount(1),
                    ["Gen2Collections"] = GC.CollectionCount(2)
                }
            };
        }

        private long GetDatabaseSize()
        {
            try
            {
                var dbPath = _serviceContext.Database.GetConnectionString()
                    ?.Split(';')
                    .FirstOrDefault(s => s.StartsWith("Data Source="))
                    ?.Replace("Data Source=", "");

                if (dbPath != null && System.IO.File.Exists(dbPath))
                {
                    return new FileInfo(dbPath).Length / 1024; // KB
                }
            }
            catch { }
            return 0;
        }
    }
}
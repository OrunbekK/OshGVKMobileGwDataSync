using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Configuration;
using System.Diagnostics;

namespace MobileGwDataSync.Monitoring.Services
{
    public class HealthMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<HealthMonitorService> _logger;
        private readonly AppSettings _appSettings;
        private Timer? _timer;

        public HealthMonitorService(
            IServiceProvider serviceProvider,
            IMetricsService metricsService,
            ILogger<HealthMonitorService> logger,
            AppSettings appSettings)
        {
            _serviceProvider = serviceProvider;
            _metricsService = metricsService;
            _logger = logger;
            _appSettings = appSettings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckHealthMetrics();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task CheckHealthMetrics()
        {
            try
            {
                // Проверка памяти
                var process = Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / (1024 * 1024);
                _metricsService.RecordMemoryUsage(memoryMB);

                // Проверка 1C
                using (var scope = _serviceProvider.CreateScope())
                {
                    var oneCConnector = scope.ServiceProvider.GetService<IDataSource>();
                    if (oneCConnector != null)
                    {
                        var isHealthy = await oneCConnector.TestConnectionAsync();
                        _metricsService.UpdateDataSourceHealth("1C", "HTTP", isHealthy);

                        if (!isHealthy)
                        {
                            _logger.LogWarning("1C health check failed");
                        }
                    }
                }

                // Проверка SQL Server
                using (var scope = _serviceProvider.CreateScope())
                {
                    var sqlTarget = scope.ServiceProvider.GetService<IDataTarget>();
                    if (sqlTarget != null)
                    {
                        var isHealthy = await sqlTarget.PrepareTargetAsync();
                        _metricsService.UpdateDataSourceHealth("SqlServer", "Database", isHealthy);

                        if (!isHealthy)
                        {
                            _logger.LogWarning("SQL Server health check failed");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health monitoring");
            }
        }
    }
}
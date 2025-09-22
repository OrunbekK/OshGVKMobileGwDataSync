using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Interfaces;

namespace MobileGwDataSync.Monitoring.Services
{
    public class AlertMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AlertMonitorService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public AlertMonitorService(
            IServiceProvider serviceProvider,
            ILogger<AlertMonitorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Alert Monitor Service started");

            // Ждем 30 секунд перед первой проверкой
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var alertManager = scope.ServiceProvider.GetRequiredService<IAlertManager>();

                    await alertManager.CheckAndSendAlertsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in alert monitoring");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Alert Monitor Service stopped");
        }
    }
}
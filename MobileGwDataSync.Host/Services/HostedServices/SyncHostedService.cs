using MobileGwDataSync.Core.Interfaces;
using System.Diagnostics;

namespace MobileGwDataSync.Host.Services.HostedServices
{
    public class SyncHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SyncHostedService> _logger;
        private readonly TimeSpan _syncInterval = TimeSpan.FromHours(1); // Синхронизация каждый час
        private readonly string _defaultJobId = "subscribers-sync";

        public SyncHostedService(
            IServiceProvider serviceProvider,
            ILogger<SyncHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SyncHostedService started");

            // Ждём 30 секунд перед первым запуском, чтобы все сервисы инициализировались
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunSyncAsync(stoppingToken);

                    _logger.LogInformation("Next sync scheduled at: {NextRun}",
                        DateTime.Now.Add(_syncInterval));

                    await Task.Delay(_syncInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Сервис останавливается
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in sync loop. Will retry after interval");
                    await Task.Delay(_syncInterval, stoppingToken);
                }
            }

            _logger.LogInformation("SyncHostedService stopped");
        }

        private async Task RunSyncAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Starting scheduled sync for job: {JobId}", _defaultJobId);

            using var scope = _serviceProvider.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

            try
            {
                var result = await syncService.ExecuteSyncAsync(_defaultJobId, cancellationToken);

                stopwatch.Stop();

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Scheduled sync completed successfully. " +
                        "Records: {Records}, Duration: {Duration}s",
                        result.RecordsProcessed,
                        stopwatch.Elapsed.TotalSeconds);
                }
                else
                {
                    _logger.LogWarning(
                        "Scheduled sync completed with errors. " +
                        "Records: {Records}, Errors: {Errors}",
                        result.RecordsProcessed,
                        string.Join("; ", result.Errors));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled sync failed for job: {JobId}", _defaultJobId);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SyncHostedService is stopping");
            await base.StopAsync(cancellationToken);
        }
    }
}
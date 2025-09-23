using MobileGwDataSync.Core.Interfaces;

namespace MobileGwDataSync.Host.Services.HostedServices
{
    public class CommandProcessorService : BackgroundService
    {
        private readonly ICommandQueue _commandQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CommandProcessorService> _logger;

        public CommandProcessorService(
            ICommandQueue commandQueue,
            IServiceProvider serviceProvider,
            ILogger<CommandProcessorService> logger)
        {
            _commandQueue = commandQueue;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Command processor service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var command = await _commandQueue.WaitForCommandAsync(stoppingToken);

                    if (command != null && command.Command == "TriggerNow")
                    {
                        await ProcessTriggerCommand(command);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing command");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            _logger.LogInformation("Command processor service stopped");
        }

        private async Task ProcessTriggerCommand(JobCommand command)
        {
            try
            {
                _logger.LogInformation("Processing manual trigger for job {JobId} from {TriggeredBy}",
                    command.JobId, command.TriggeredBy);

                // Используем scope для получения scoped сервисов
                using var scope = _serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

                // Запускаем синхронизацию напрямую через ISyncService
                var result = await syncService.ExecuteSyncAsync(command.JobId);

                if (result.Success)
                {
                    _logger.LogInformation("Manual job {JobId} completed. Records: {Records}",
                        command.JobId, result.RecordsProcessed);
                }
                else
                {
                    _logger.LogWarning("Manual job {JobId} failed: {Errors}",
                        command.JobId, string.Join(", ", result.Errors));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process trigger command for job {JobId}", command.JobId);
            }
        }
    }
}
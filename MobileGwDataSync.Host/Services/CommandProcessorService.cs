using MobileGwDataSync.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text.Json;

namespace MobileGwDataSync.Host.Services
{
    public class CommandProcessorService : BackgroundService
    {
        private readonly ICommandQueue _commandQueue;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<CommandProcessorService> _logger;
        private readonly ISyncService _syncService;

        public CommandProcessorService(
            ICommandQueue commandQueue,
            ISchedulerFactory schedulerFactory,
            ISyncService syncService,
            ILogger<CommandProcessorService> logger)
        {
            _commandQueue = commandQueue;
            _schedulerFactory = schedulerFactory;
            _syncService = syncService;
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
                    // Нормальное завершение при остановке сервиса
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
                _logger.LogInformation("Processing trigger command for job {JobId} from {TriggeredBy}",
                    command.JobId, command.TriggeredBy);

                // Вариант 1: Через Quartz
                try
                {
                    var scheduler = await _schedulerFactory.GetScheduler();
                    if (scheduler != null)
                    {
                        var jobKey = new JobKey(command.JobId);

                        if (await scheduler.CheckExists(jobKey))
                        {
                            await scheduler.TriggerJob(jobKey, new JobDataMap
                            {
                                ["TriggeredBy"] = command.TriggeredBy,
                                ["CommandId"] = command.Id.ToString(),
                                ["TriggerSource"] = "Dashboard"
                            });

                            _logger.LogInformation("Job {JobId} triggered via Quartz", command.JobId);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to trigger via Quartz, using direct sync service");
                }

                // Вариант 2: Прямой вызов SyncService
                var result = await _syncService.ExecuteSyncAsync(command.JobId);

                _logger.LogInformation("Job {JobId} executed directly. Success: {Success}, Records: {Records}",
                    command.JobId, result.Success, result.RecordsProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process trigger command for job {JobId}", command.JobId);
            }
        }
    }
}
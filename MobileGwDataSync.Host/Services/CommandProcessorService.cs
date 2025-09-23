using MobileGwDataSync.Host.Services;
using Quartz;

public class CommandProcessorService : BackgroundService
{
    private readonly ICommandQueue _commandQueue;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<CommandProcessorService> _logger;

    public CommandProcessorService(
        ICommandQueue commandQueue,
        ISchedulerFactory schedulerFactory,
        ILogger<CommandProcessorService> logger)
    {
        _commandQueue = commandQueue;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Command processor started");

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessTriggerCommand(JobCommand command)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(command.JobId);

        if (await scheduler.CheckExists(jobKey))
        {
            await scheduler.TriggerJob(jobKey, new JobDataMap
            {
                ["TriggeredBy"] = command.TriggeredBy,
                ["CommandId"] = command.Id.ToString()
            });

            _logger.LogInformation("Executed trigger command for job {JobId}", command.JobId);
        }
        else
        {
            _logger.LogWarning("Job {JobId} not found in scheduler", command.JobId);
        }
    }
}
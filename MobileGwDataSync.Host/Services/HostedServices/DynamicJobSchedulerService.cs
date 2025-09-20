using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Host.Jobs;
using Quartz;

namespace MobileGwDataSync.Host.Services.HostedServices
{
    /// <summary>
    /// Сервис который читает задачи из БД и динамически создаёт Quartz jobs
    /// </summary>
    public class DynamicJobSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<DynamicJobSchedulerService> _logger;

        public DynamicJobSchedulerService(
            IServiceProvider serviceProvider,
            ISchedulerFactory schedulerFactory,
            ILogger<DynamicJobSchedulerService> logger)
        {
            _serviceProvider = serviceProvider;
            _schedulerFactory = schedulerFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Ждём инициализации
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            var scheduler = await _schedulerFactory.GetScheduler(stoppingToken);

            // Загружаем задачи из БД и создаём jobs
            await LoadJobsFromDatabase(scheduler, stoppingToken);

            // Периодически проверяем изменения в БД (каждую минуту)
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                await UpdateJobsFromDatabase(scheduler, stoppingToken);
            }
        }

        private async Task LoadJobsFromDatabase(IScheduler scheduler, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();

            try
            {
                var jobs = await context.SyncJobs
                    .Where(j => j.IsEnabled)
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Loading {Count} jobs from database", jobs.Count);

                foreach (var jobEntity in jobs)
                {
                    var jobKey = new JobKey(jobEntity.Id);

                    // Проверяем, существует ли уже job
                    if (await scheduler.CheckExists(jobKey, cancellationToken))
                    {
                        _logger.LogDebug("Job {JobId} already exists in scheduler", jobEntity.Id);
                        continue;
                    }

                    // Создаём job
                    var job = JobBuilder.Create<DataSyncJob>()
                        .WithIdentity(jobKey)
                        .UsingJobData("JobId", jobEntity.Id)
                        .UsingJobData("JobName", jobEntity.Name)
                        .Build();

                    // Создаём trigger с расписанием из БД
                    var trigger = TriggerBuilder.Create()
                        .WithIdentity($"{jobEntity.Id}-trigger")
                        .WithCronSchedule(jobEntity.CronExpression)
                        .StartNow()
                        .Build();

                    await scheduler.ScheduleJob(job, trigger, cancellationToken);

                    _logger.LogInformation(
                        "Scheduled job {JobId} ({Name}) with cron: {Cron}",
                        jobEntity.Id, jobEntity.Name, jobEntity.CronExpression);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load jobs from database");
            }
        }

        private async Task UpdateJobsFromDatabase(IScheduler scheduler, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();

            try
            {
                var jobs = await context.SyncJobs.ToListAsync(cancellationToken);

                foreach (var jobEntity in jobs)
                {
                    var jobKey = new JobKey(jobEntity.Id);
                    var exists = await scheduler.CheckExists(jobKey, cancellationToken);

                    if (jobEntity.IsEnabled && !exists)
                    {
                        // Добавляем новую задачу
                        await AddJob(scheduler, jobEntity, cancellationToken);
                    }
                    else if (!jobEntity.IsEnabled && exists)
                    {
                        // Удаляем отключенную задачу
                        await scheduler.DeleteJob(jobKey, cancellationToken);
                        _logger.LogInformation("Removed disabled job {JobId}", jobEntity.Id);
                    }
                    else if (exists)
                    {
                        // Проверяем изменение расписания
                        var triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken);
                        var trigger = triggers.FirstOrDefault() as ICronTrigger;

                        if (trigger != null && trigger.CronExpressionString != jobEntity.CronExpression)
                        {
                            // Пересоздаём с новым расписанием
                            await scheduler.DeleteJob(jobKey, cancellationToken);
                            await AddJob(scheduler, jobEntity, cancellationToken);
                            _logger.LogInformation(
                                "Updated job {JobId} schedule from {OldCron} to {NewCron}",
                                jobEntity.Id, trigger.CronExpressionString, jobEntity.CronExpression);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update jobs from database");
            }
        }

        private async Task AddJob(IScheduler scheduler, Data.Entities.SyncJobEntity jobEntity, CancellationToken cancellationToken)
        {
            var job = JobBuilder.Create<DataSyncJob>()
                .WithIdentity(jobEntity.Id)
                .UsingJobData("JobId", jobEntity.Id)
                .UsingJobData("JobName", jobEntity.Name)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{jobEntity.Id}-trigger")
                .WithCronSchedule(jobEntity.CronExpression)
                .StartNow()
                .Build();

            await scheduler.ScheduleJob(job, trigger, cancellationToken);

            _logger.LogInformation(
                "Added job {JobId} ({Name}) with cron: {Cron}",
                jobEntity.Id, jobEntity.Name, jobEntity.CronExpression);
        }
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Interfaces;
using Quartz;
using System.Diagnostics;

namespace MobileGwDataSync.Core.Jobs
{
    [DisallowConcurrentExecution]
    public class DataSyncJob : IJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DataSyncJob> _logger;

        public DataSyncJob(
            IServiceProvider serviceProvider,
            ILogger<DataSyncJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var jobId = context.JobDetail.JobDataMap.GetString("JobId") ?? "subscribers-sync";
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "Quartz job started. JobId: {JobId}, FireTime: {FireTime}, NextFireTime: {NextFireTime}",
                jobId,
                context.FireTimeUtc,
                context.NextFireTimeUtc);

            using var scope = _serviceProvider.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

            try
            {
                var result = await syncService.ExecuteSyncAsync(jobId, context.CancellationToken);

                stopwatch.Stop();

                // Сохраняем результат в контексте для истории
                context.Result = new
                {
                    Success = result.Success,
                    RecordsProcessed = result.RecordsProcessed,
                    Duration = stopwatch.Elapsed,
                    Errors = result.Errors
                };

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Quartz job completed successfully. " +
                        "JobId: {JobId}, Records: {Records}, Duration: {Duration}s",
                        jobId,
                        result.RecordsProcessed,
                        stopwatch.Elapsed.TotalSeconds);
                }
                else
                {
                    _logger.LogWarning(
                        "Quartz job completed with errors. " +
                        "JobId: {JobId}, Errors: {Errors}",
                        jobId,
                        string.Join("; ", result.Errors));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quartz job failed. JobId: {JobId}", jobId);
                throw new JobExecutionException(ex, false);
            }
        }
    }
}
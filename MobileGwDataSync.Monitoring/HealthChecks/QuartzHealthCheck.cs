using Microsoft.Extensions.Diagnostics.HealthChecks;
using Quartz;
using Quartz.Impl.Matchers;

namespace MobileGwDataSync.Monitoring.HealthChecks
{
    public class QuartzHealthCheck : IHealthCheck
    {
        private readonly ISchedulerFactory _schedulerFactory;

        public QuartzHealthCheck(ISchedulerFactory schedulerFactory)
        {
            _schedulerFactory = schedulerFactory;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

                if (!scheduler.IsStarted)
                {
                    return HealthCheckResult.Unhealthy("Quartz scheduler is not started");
                }

                var executingJobs = await scheduler.GetCurrentlyExecutingJobs(cancellationToken);
                var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);

                return HealthCheckResult.Healthy(
                    $"Quartz is running with {jobKeys.Count} jobs",
                    new Dictionary<string, object>
                    {
                        ["TotalJobs"] = jobKeys.Count,
                        ["ExecutingJobs"] = executingJobs.Count,
                        ["IsStarted"] = scheduler.IsStarted,
                        ["IsShutdown"] = scheduler.IsShutdown
                    });
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Quartz check failed", ex);
            }
        }
    }
}

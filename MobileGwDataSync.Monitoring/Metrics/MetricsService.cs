using MobileGwDataSync.Core.Interfaces;

namespace MobileGwDataSync.Monitoring.Metrics
{
    /// <summary>
    /// Implements the IMetricsService interface using Prometheus for metrics collection.
    /// </summary>
    public class MetricsService : IMetricsService
    {
        public void IncrementJobsStarted(string jobName)
        {
            CustomMetrics.JobsStarted.WithLabels(jobName).Inc();
        }

        public void IncrementJobsCompleted(string jobName, string status)
        {
            CustomMetrics.JobsCompleted.WithLabels(jobName, status).Inc();
        }

        public void RecordJobDuration(string jobName, TimeSpan duration)
        {
            CustomMetrics.JobDuration.WithLabels(jobName).Observe(duration.TotalSeconds);
        }

        public void IncrementRecordsRead(string jobName, int count)
        {
            CustomMetrics.RecordsRead.WithLabels(jobName).Inc(count);
        }

        public void IncrementRecordsWritten(string jobName, int count)
        {
            CustomMetrics.RecordsWritten.WithLabels(jobName).Inc(count);
        }

        public void IncrementJobErrors(string jobName, string errorType)
        {
            CustomMetrics.JobErrors.WithLabels(jobName, errorType).Inc();
        }
    }
}

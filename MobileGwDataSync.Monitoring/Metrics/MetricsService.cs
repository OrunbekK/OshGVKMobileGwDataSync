using MobileGwDataSync.Core.Interfaces;

namespace MobileGwDataSync.Monitoring.Metrics
{
    /// <summary>
    /// Implements the IMetricsService interface using Prometheus for metrics collection.
    /// </summary>
    public class MetricsService : IMetricsService
    {
        public void RecordSyncStart(string jobName)
        {
            CustomMetrics.JobsStarted.WithLabels(jobName).Inc();
        }

        public void RecordSyncComplete(string jobName, TimeSpan duration, int recordsRead, int recordsWritten)
        {
            CustomMetrics.JobsCompleted.WithLabels(jobName, "Success").Inc();
            CustomMetrics.JobDuration.WithLabels(jobName).Observe(duration.TotalSeconds);
            CustomMetrics.RecordsRead.WithLabels(jobName).Inc(recordsRead);
            CustomMetrics.RecordsWritten.WithLabels(jobName).Inc(recordsWritten);
        }

        public void RecordSyncError(string jobName, TimeSpan duration, string errorType)
        {
            CustomMetrics.JobsCompleted.WithLabels(jobName, "Failed").Inc();
            CustomMetrics.JobDuration.WithLabels(jobName).Observe(duration.TotalSeconds);
            CustomMetrics.JobErrors.WithLabels(jobName, errorType).Inc();
        }
    }

    /// <summary>
    /// A null object implementation of IMetricsService that does nothing.
    /// Used when metrics are disabled.
    /// </summary>
    public class NullMetricsService : IMetricsService
    {
        public void RecordSyncStart(string jobName) { /* Do nothing */ }

        public void RecordSyncComplete(string jobName, TimeSpan duration, int recordsRead, int recordsWritten) { /* Do nothing */ }

        public void RecordSyncError(string jobName, TimeSpan duration, string errorType) { /* Do nothing */ }
    }
}

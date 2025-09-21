using Prometheus;

namespace MobileGwDataSync.Monitoring.Metrics
{
    /// <summary>
    /// Contains definitions for all custom Prometheus metrics used in the application.
    /// </summary>
    public static class CustomMetrics
    {
        private const string JobLabel = "job_name";
        private const string StatusLabel = "status";
        private const string ErrorLabel = "error_type";

        // Указываем полный путь: Prometheus.Counter и Prometheus.Metrics
        public static readonly Prometheus.Counter JobsStarted = Prometheus.Metrics
            .CreateCounter("sync_jobs_started_total", "Total number of synchronization jobs started.", JobLabel);

        public static readonly Prometheus.Counter JobsCompleted = Prometheus.Metrics
            .CreateCounter("sync_jobs_completed_total", "Total number of synchronization jobs completed.", JobLabel, StatusLabel);

        public static readonly Prometheus.Histogram JobDuration = Prometheus.Metrics
            .CreateHistogram("sync_job_duration_seconds", "Histogram of synchronization job duration in seconds.", JobLabel);

        public static readonly Prometheus.Counter RecordsRead = Prometheus.Metrics
            .CreateCounter("sync_records_read_total", "Total number of records read from the source.", JobLabel);

        public static readonly Prometheus.Counter RecordsWritten = Prometheus.Metrics
            .CreateCounter("sync_records_written_total", "Total number of records written to the target.", JobLabel);

        public static readonly Prometheus.Counter JobErrors = Prometheus.Metrics
            .CreateCounter("sync_job_errors_total", "Total number of errors encountered during job execution.", JobLabel, ErrorLabel);
    }
}

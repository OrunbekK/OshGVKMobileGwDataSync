using MobileGwDataSync.Core.Interfaces;
using Prometheus;

namespace MobileGwDataSync.Monitoring.Metrics
{
    public class MetricsService : IMetricsService
    {
        // Определяем метрики Prometheus
        private static readonly Counter SyncStarted = Metrics
            .CreateCounter("sync_jobs_started_total", "Total number of sync jobs started", "job_name");

        private static readonly Counter SyncCompleted = Metrics
            .CreateCounter("sync_jobs_completed_total", "Total number of sync jobs completed", "job_name", "status");

        private static readonly Counter SyncErrors = Metrics
            .CreateCounter("sync_job_errors_total", "Total number of sync job errors", "job_name", "error_type");

        private static readonly Histogram SyncDuration = Metrics
            .CreateHistogram("sync_job_duration_seconds", "Duration of sync jobs in seconds", "job_name");

        private static readonly Histogram StepDuration = Metrics
            .CreateHistogram("sync_step_duration_seconds", "Duration of sync steps in seconds", "job_name", "step_name");

        private static readonly Counter RecordsProcessed = Metrics
            .CreateCounter("sync_records_processed_total", "Total number of records processed", "job_name");

        private static readonly Gauge ActiveSyncs = Metrics
            .CreateGauge("sync_jobs_active", "Number of currently active sync jobs");

        public void RecordSyncStart(string jobName)
        {
            SyncStarted.WithLabels(jobName).Inc();
            ActiveSyncs.Inc();
        }

        public void RecordSyncComplete(string jobName, bool success, int recordsProcessed, TimeSpan duration)
        {
            var status = success ? "success" : "failed";
            SyncCompleted.WithLabels(jobName, status).Inc();
            SyncDuration.WithLabels(jobName).Observe(duration.TotalSeconds);
            RecordsProcessed.WithLabels(jobName).Inc(recordsProcessed);
            ActiveSyncs.Dec();
        }

        public void RecordSyncError(string jobName, string errorType)
        {
            SyncErrors.WithLabels(jobName, errorType).Inc();
            ActiveSyncs.Dec();
        }

        public void RecordStepDuration(string jobName, string stepName, TimeSpan duration)
        {
            StepDuration.WithLabels(jobName, stepName).Observe(duration.TotalSeconds);
        }

        public Dictionary<string, double> GetCurrentMetrics()
        {
            // Возвращаем текущие значения метрик
            return new Dictionary<string, double>
            {
                ["active_syncs"] = ActiveSyncs.Value,
                ["total_sync_started"] = SyncStarted.WithLabels("all").Value,
                ["total_sync_completed"] = SyncCompleted.WithLabels("all", "success").Value,
                ["total_sync_errors"] = SyncErrors.WithLabels("all", "all").Value
            };
        }
    }
}

// NullMetricsService.cs - пустая реализация
namespace MobileGwDataSync.Monitoring.Metrics
{
    public class NullMetricsService : IMetricsService
    {
        public void RecordSyncStart(string jobName) { }

        public void RecordSyncComplete(string jobName, bool success, int recordsProcessed, TimeSpan duration) { }

        public void RecordSyncError(string jobName, string errorType) { }

        public void RecordStepDuration(string jobName, string stepName, TimeSpan duration) { }

        public Dictionary<string, double> GetCurrentMetrics() => new Dictionary<string, double>();
    }
}
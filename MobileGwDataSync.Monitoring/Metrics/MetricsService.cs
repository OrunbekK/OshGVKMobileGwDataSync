using MobileGwDataSync.Core.Interfaces;
using Prometheus;
using System.Collections.Concurrent;

namespace MobileGwDataSync.Monitoring.Metrics
{
    public class MetricsService : IMetricsService
    {
        // Счетчики
        private static readonly Counter SyncJobsTotal = Prometheus.Metrics
            .CreateCounter("sync_jobs_total", "Total sync jobs executed",
                new[] { "job_name", "job_type", "status" });

        private static readonly Counter SyncRecordsTotal = Prometheus.Metrics
            .CreateCounter("sync_records_total", "Total records processed",
                new[] { "job_name", "operation" }); // operation: fetched, processed, failed

        private static readonly Counter SyncErrorsTotal = Prometheus.Metrics
            .CreateCounter("sync_errors_total", "Total sync errors",
                new[] { "job_name", "error_type", "step" });

        private static readonly Counter HttpRequestsTotal = Prometheus.Metrics
            .CreateCounter("http_requests_total", "Total HTTP requests",
                new[] { "method", "endpoint", "status_code" });

        // Гистограммы
        private static readonly Histogram SyncDuration = Prometheus.Metrics
            .CreateHistogram("sync_duration_seconds", "Sync job duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "job_name", "job_type" },
                    Buckets = new[] { 1.0, 5.0, 10.0, 30.0, 60.0, 120.0, 300.0, 600.0 }
                });

        private static readonly Histogram StepDuration = Prometheus.Metrics
            .CreateHistogram("sync_step_duration_seconds", "Step duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "job_name", "step_name" },
                    Buckets = new[] { 0.1, 0.5, 1, 2, 5, 10, 30 }
                });

        private static readonly Histogram HttpRequestDuration = Prometheus.Metrics
            .CreateHistogram("http_request_duration_seconds", "HTTP request duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "method", "endpoint", "status_code" },
                    Buckets = new[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5 }
                });

        // Gauges
        private static readonly Gauge ActiveSyncJobs = Prometheus.Metrics
            .CreateGauge("sync_jobs_active", "Currently active sync jobs");

        private static readonly Gauge LastSyncTimestamp = Prometheus.Metrics
            .CreateGauge("sync_last_success_timestamp", "Last successful sync timestamp",
                new[] { "job_name" });

        private static readonly Gauge DataSourceHealth = Prometheus.Metrics
            .CreateGauge("datasource_health", "Data source health status",
                new[] { "source_name", "source_type" });

        private static readonly Gauge SystemMemoryUsage = Prometheus.Metrics
            .CreateGauge("system_memory_usage_mb", "System memory usage in MB");

        private static readonly Gauge DatabaseConnections = Prometheus.Metrics
            .CreateGauge("database_connections_active", "Active database connections",
                new[] { "database_name" });

        private static readonly Gauge HttpRequestsInProgress = Prometheus.Metrics
            .CreateGauge("http_requests_in_progress", "HTTP requests currently being processed");

        // Внутреннее хранилище для текущих метрик
        private readonly ConcurrentDictionary<string, double> _currentMetrics = new();

        public void RecordSyncStart(string jobName)
        {
            ActiveSyncJobs.Inc();
            SyncJobsTotal.WithLabels(jobName, "sync", "started").Inc();

            _currentMetrics[$"active_syncs"] = ActiveSyncJobs.Value;
        }

        public void RecordSyncComplete(string jobName, bool success, int recordsProcessed, TimeSpan duration)
        {
            var status = success ? "success" : "failed";

            SyncJobsTotal.WithLabels(jobName, "sync", status).Inc();
            SyncDuration.WithLabels(jobName, "sync").Observe(duration.TotalSeconds);

            if (success)
            {
                LastSyncTimestamp.WithLabels(jobName).Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                SyncRecordsTotal.WithLabels(jobName, "processed").Inc(recordsProcessed);
            }

            ActiveSyncJobs.Dec();

            _currentMetrics[$"sync_{jobName}_last_duration"] = duration.TotalSeconds;
            _currentMetrics[$"sync_{jobName}_last_records"] = recordsProcessed;
            _currentMetrics[$"active_syncs"] = ActiveSyncJobs.Value;
        }

        public void RecordSyncError(string jobName, string errorType)
        {
            SyncErrorsTotal.WithLabels(jobName, errorType, "unknown").Inc();
            ActiveSyncJobs.Dec();

            _currentMetrics[$"sync_{jobName}_errors"] =
                (_currentMetrics.GetValueOrDefault($"sync_{jobName}_errors") + 1);
        }

        public void RecordStepDuration(string jobName, string stepName, TimeSpan duration)
        {
            StepDuration.WithLabels(jobName, stepName).Observe(duration.TotalSeconds);

            _currentMetrics[$"step_{jobName}_{stepName}_duration"] = duration.TotalSeconds;
        }

        public void RecordRecordsFetched(string jobName, int count)
        {
            SyncRecordsTotal.WithLabels(jobName, "fetched").Inc(count);
            _currentMetrics[$"sync_{jobName}_fetched"] = count;
        }

        public void RecordRecordsProcessed(string jobName, int count)
        {
            SyncRecordsTotal.WithLabels(jobName, "processed").Inc(count);
            _currentMetrics[$"sync_{jobName}_processed"] = count;
        }

        public void RecordRecordsFailed(string jobName, int count)
        {
            SyncRecordsTotal.WithLabels(jobName, "failed").Inc(count);
            _currentMetrics[$"sync_{jobName}_failed"] = count;
        }

        public void RecordMemoryUsage(long memoryMB)
        {
            SystemMemoryUsage.Set(memoryMB);
            _currentMetrics["memory_usage_mb"] = memoryMB;
        }

        public void RecordDatabaseConnections(string databaseName, int count)
        {
            DatabaseConnections.WithLabels(databaseName).Set(count);
            _currentMetrics[$"db_{databaseName}_connections"] = count;
        }

        public void UpdateDataSourceHealth(string sourceName, string sourceType, bool isHealthy)
        {
            DataSourceHealth.WithLabels(sourceName, sourceType).Set(isHealthy ? 1 : 0);
            _currentMetrics[$"health_{sourceName}"] = isHealthy ? 1 : 0;
        }

        public void RecordHttpRequest(string method, string endpoint, int statusCode, TimeSpan duration)
        {
            var statusCodeStr = statusCode.ToString();
            HttpRequestsTotal.WithLabels(method, endpoint, statusCodeStr).Inc();
            HttpRequestDuration.WithLabels(method, endpoint, statusCodeStr).Observe(duration.TotalSeconds);

            // Обновляем внутренние метрики
            var key = $"http_{method}_{endpoint}_{statusCodeStr}";
            _currentMetrics[key] = _currentMetrics.GetValueOrDefault(key) + 1;
        }

        public void IncrementHttpRequestsInProgress()
        {
            HttpRequestsInProgress.Inc();
            _currentMetrics["http_requests_in_progress"] = HttpRequestsInProgress.Value;
        }

        public void DecrementHttpRequestsInProgress()
        {
            HttpRequestsInProgress.Dec();
            _currentMetrics["http_requests_in_progress"] = HttpRequestsInProgress.Value;
        }

        public Dictionary<string, double> GetCurrentMetrics()
        {
            return new Dictionary<string, double>(_currentMetrics);
        }
    }

    // NullMetricsService для случаев когда метрики отключены
    public class NullMetricsService : IMetricsService
    {
        public void RecordSyncStart(string jobName) { }
        public void RecordSyncComplete(string jobName, bool success, int recordsProcessed, TimeSpan duration) { }
        public void RecordSyncError(string jobName, string errorType) { }
        public void RecordStepDuration(string jobName, string stepName, TimeSpan duration) { }
        public void RecordRecordsFetched(string jobName, int count) { }
        public void RecordRecordsProcessed(string jobName, int count) { }
        public void RecordRecordsFailed(string jobName, int count) { }
        public void RecordMemoryUsage(long memoryMB) { }
        public void RecordDatabaseConnections(string databaseName, int count) { }
        public void UpdateDataSourceHealth(string sourceName, string sourceType, bool isHealthy) { }
        public void RecordHttpRequest(string method, string endpoint, int statusCode, TimeSpan duration) { }
        public void IncrementHttpRequestsInProgress() { }
        public void DecrementHttpRequestsInProgress() { }
        public Dictionary<string, double> GetCurrentMetrics() => new Dictionary<string, double>();
    }
}
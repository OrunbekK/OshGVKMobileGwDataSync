// IMetricsService.cs - обновленный интерфейс
namespace MobileGwDataSync.Core.Interfaces
{
    /// <summary>
    /// Service for collecting application metrics
    /// </summary>
    public interface IMetricsService
    {
        // Sync metrics
        void RecordSyncStart(string jobName);
        void RecordSyncComplete(string jobName, bool success, int recordsProcessed, TimeSpan duration);
        void RecordSyncError(string jobName, string errorType);
        void RecordStepDuration(string jobName, string stepName, TimeSpan duration);

        // Data metrics
        void RecordRecordsFetched(string jobName, int count);
        void RecordRecordsProcessed(string jobName, int count);
        void RecordRecordsFailed(string jobName, int count);

        // System metrics
        void RecordMemoryUsage(long memoryMB);
        void RecordDatabaseConnections(string databaseName, int count);
        void UpdateDataSourceHealth(string sourceName, string sourceType, bool isHealthy);

        // HTTP metrics
        void RecordHttpRequest(string method, string endpoint, int statusCode, TimeSpan duration);
        void IncrementHttpRequestsInProgress();
        void DecrementHttpRequestsInProgress();

        // Get current metrics
        Dictionary<string, double> GetCurrentMetrics();
    }
}
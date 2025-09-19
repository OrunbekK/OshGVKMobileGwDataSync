namespace MobileGwDataSync.Core.Interfaces
{
    public interface IMetricsService
    {
        void RecordSyncStart(string jobId);
        void RecordSyncComplete(string jobId, bool success, int recordsProcessed, TimeSpan duration);
        void RecordSyncError(string jobId, string errorType);
        void RecordBatchProcessed(string jobId, int batchSize, TimeSpan duration);
        void RecordMemoryUsage(long bytes);
        Dictionary<string, double> GetCurrentMetrics();
    }
}

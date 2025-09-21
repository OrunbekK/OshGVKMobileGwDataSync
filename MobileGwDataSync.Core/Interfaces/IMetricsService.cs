// IMetricsService.cs - обновленный интерфейс
namespace MobileGwDataSync.Core.Interfaces
{
    /// <summary>
    /// Service for collecting application metrics
    /// </summary>
    public interface IMetricsService
    {
        /// <summary>
        /// Records the start of a synchronization job
        /// </summary>
        void RecordSyncStart(string jobName);

        /// <summary>
        /// Records the completion of a synchronization job
        /// </summary>
        void RecordSyncComplete(string jobName, bool success, int recordsProcessed, TimeSpan duration);

        /// <summary>
        /// Records a failure during a synchronization job
        /// </summary>
        void RecordSyncError(string jobName, string errorType);

        /// <summary>
        /// Records the duration of a specific step in the sync process
        /// </summary>
        void RecordStepDuration(string jobName, string stepName, TimeSpan duration);

        /// <summary>
        /// Gets current metrics as a dictionary
        /// </summary>
        Dictionary<string, double> GetCurrentMetrics();
    }
}
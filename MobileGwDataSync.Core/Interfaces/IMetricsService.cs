namespace MobileGwDataSync.Core.Interfaces;

/// <summary>
/// Defines a contract for a service that collects application metrics.
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Records the start of a synchronization job.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    void RecordSyncStart(string jobName);

    /// <summary>
    /// Records the successful completion of a synchronization job with all relevant metrics.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="duration">The total time taken for the job.</param>
    /// <param name="recordsRead">Number of records read from the source.</param>
    /// <param name="recordsWritten">Number of records written to the target.</param>
    void RecordSyncComplete(string jobName, TimeSpan duration, int recordsRead, int recordsWritten);

    /// <summary>
    /// Records a failure during a synchronization job.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="duration">The time elapsed before the failure.</param>
    /// <param name="errorType">The type of error (e.g., "Validation", "Database").</param>
    void RecordSyncError(string jobName, TimeSpan duration, string errorType);
}
namespace MobileGwDataSync.Core.Interfaces;

/// <summary>
/// Defines a contract for a service that collects application metrics.
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Increments the counter for started synchronization jobs.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    void IncrementJobsStarted(string jobName);

    /// <summary>
    /// Increments the counter for completed synchronization jobs, specifying the status.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="status">The final status (e.g., "Success", "Failed").</param>
    void IncrementJobsCompleted(string jobName, string status);

    /// <summary>
    /// Records the total duration of a synchronization job.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="duration">The time taken for the job to complete.</param>
    void RecordJobDuration(string jobName, TimeSpan duration);

    /// <summary>
    /// Increments the counter for the number of records processed (read from source).
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="count">The number of records processed.</param>
    void IncrementRecordsRead(string jobName, int count);

    /// <summary>
    /// Increments the counter for the number of records written to the target.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="count">The number of records written.</param>
    void IncrementRecordsWritten(string jobName, int count);

    /// <summary>
    /// Records an error event during a job execution.
    /// </summary>
    /// <param name="jobName">The name of the job where the error occurred.</param>
    /// <param name="errorType">The type of error (e.g., "Validation", "Database").</param>
    void IncrementJobErrors(string jobName, string errorType);
}
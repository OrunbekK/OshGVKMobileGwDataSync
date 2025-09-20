using MobileGwDataSync.Core.Models.Domain;

namespace MobileGwDataSync.Core.Interfaces
{
    public interface IJobSchedulingService
    {
        Task<bool> CanRunJobAsync(string jobId, CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> GetBlockedJobsAsync(string jobId, CancellationToken cancellationToken = default);
        Task<bool> AcquireJobLockAsync(string jobId, TimeSpan timeout, CancellationToken cancellationToken = default);
        Task ReleaseJobLockAsync(string jobId, CancellationToken cancellationToken = default);
        Task<IEnumerable<SyncJob>> GetPendingJobsAsync(CancellationToken cancellationToken = default);
    }
}

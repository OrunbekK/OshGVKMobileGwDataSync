using MobileGwDataSync.Core.Models.Domain;

namespace MobileGwDataSync.Core.Interfaces
{
    public interface ISyncJobRepository
    {
        Task<SyncJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);
        Task<IEnumerable<SyncJob>> GetActiveJobsAsync(CancellationToken cancellationToken = default);
        Task UpdateJobLastRunAsync(string jobId, DateTime lastRunTime, CancellationToken cancellationToken = default);
    }
}
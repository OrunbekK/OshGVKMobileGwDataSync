using MobileGwDataSync.Core.Models.Domain;
using MobileGwDataSync.Core.Models.DTO;

namespace MobileGwDataSync.Core.Interfaces
{
    public interface ISyncJobExecutor
    {
        Task<SyncResultDTO> ExecuteAsync(SyncJob job, CancellationToken cancellationToken = default);
        bool CanHandle(SyncJobType jobType);
        Task<bool> ValidateJobConfiguration(SyncJob job);
    }
}

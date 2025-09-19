using MobileGwDataSync.Core.Models.Domain;
using MobileGwDataSync.Core.Models.DTO;

namespace MobileGwDataSync.Core.Interfaces
{
    public interface ISyncService
    {
        Task<SyncResultDTO> ExecuteSyncAsync(string jobId, CancellationToken cancellationToken = default);
        Task<SyncRun> GetSyncRunAsync(Guid runId, CancellationToken cancellationToken = default);
        Task<IEnumerable<SyncRun>> GetSyncHistoryAsync(string jobId, int limit = 100, CancellationToken cancellationToken = default);
        Task<bool> CancelSyncAsync(Guid runId, CancellationToken cancellationToken = default);
    }
}

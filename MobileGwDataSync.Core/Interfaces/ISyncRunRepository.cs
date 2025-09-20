using MobileGwDataSync.Core.Models.Domain;

namespace MobileGwDataSync.Core.Interfaces
{
    public interface ISyncRunRepository
    {
        Task<SyncRun> CreateRunAsync(string jobId, CancellationToken cancellationToken = default);
        Task<SyncRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);
        Task UpdateRunAsync(SyncRun run, CancellationToken cancellationToken = default);
        Task<IEnumerable<SyncRun>> GetRunHistoryAsync(string jobId, int limit = 100, CancellationToken cancellationToken = default);

        Task<SyncRunStep> CreateStepAsync(Guid runId, string stepName, CancellationToken cancellationToken = default);
        Task UpdateStepAsync(SyncRunStep step, CancellationToken cancellationToken = default);
    }
}

using MobileGwDataSync.Core.Models.DTO;

namespace MobileGwDataSync.Core.Interfaces
{
    public interface IDataTarget
    {
        Task<bool> SaveDataAsync(DataTableDTO data, CancellationToken cancellationToken = default);
        Task<bool> PrepareTargetAsync(CancellationToken cancellationToken = default);
        Task<bool> FinalizeTargetAsync(bool success, CancellationToken cancellationToken = default);
        string TargetName { get; }
    }
}

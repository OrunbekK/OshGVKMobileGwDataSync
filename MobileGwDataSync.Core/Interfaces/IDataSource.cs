using MobileGwDataSync.Core.Models.DTO;

namespace MobileGwDataSync.Core.Interfaces
{
    public interface IDataSource
    {
        Task<DataTableDTO> FetchDataAsync(Dictionary<string, string> parameters, CancellationToken cancellationToken = default);
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
        string SourceName { get; }
    }
}

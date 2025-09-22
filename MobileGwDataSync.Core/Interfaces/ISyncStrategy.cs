using MobileGwDataSync.Core.Models.DTO;
using System.Data;

namespace MobileGwDataSync.Core.Interfaces
{
    public interface ISyncStrategy
    {
        string EntityName { get; }
        string Endpoint { get; }
        Task<DataTableDTO> FetchDataAsync(HttpClient httpClient, CancellationToken cancellationToken);
        DataTableDTO ParseResponse(string jsonResponse);
        string GetTargetProcedure();
    }
}

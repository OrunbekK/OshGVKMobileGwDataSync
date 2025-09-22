using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.DTO;
using System.Data;

namespace MobileGwDataSync.Integration.OneC.Strategies
{
    public abstract class BaseSyncStrategy : ISyncStrategy
    {
        protected readonly ILogger _logger;

        public abstract string EntityName { get; }
        public abstract string Endpoint { get; }

        protected BaseSyncStrategy(ILogger logger)
        {
            _logger = logger;
        }

        public virtual async Task<DataTableDTO> FetchDataAsync(HttpClient httpClient, CancellationToken cancellationToken)
        {
            var response = await httpClient.GetAsync(Endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Received JSON for {Entity}: {Json}", EntityName, json);

            return ParseResponse(json);
        }

        public abstract DataTableDTO ParseResponse(string jsonResponse);
        public abstract string GetTargetProcedure();
    }
}

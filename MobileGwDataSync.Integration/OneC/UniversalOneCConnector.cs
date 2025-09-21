using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.DTO;

namespace MobileGwDataSync.Integration.OneC
{
    public class UniversalOneCConnector : IDataSource
    {
        private readonly HttpClient _httpClient;
        private readonly ISyncStrategyFactory _strategyFactory;
        private readonly ILogger<UniversalOneCConnector> _logger;
        private ISyncStrategy? _currentStrategy;

        public string SourceName => _currentStrategy?.EntityName ?? "1C Universal";

        public UniversalOneCConnector(
            IHttpClientFactory httpClientFactory,
            ISyncStrategyFactory strategyFactory,
            ILogger<UniversalOneCConnector> logger)
        {
            _httpClient = httpClientFactory.CreateClient("OneC");
            _strategyFactory = strategyFactory;
            _logger = logger;
        }

        public async Task<DataTableDTO> FetchDataAsync(
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken = default)
        {
            var jobType = parameters.GetValueOrDefault("jobType", "subscribers");
            _currentStrategy = _strategyFactory.GetStrategy(jobType);

            _logger.LogInformation("Fetching data for {EntityName} from endpoint {Endpoint}",
                _currentStrategy.EntityName, _currentStrategy.Endpoint);

            return await _currentStrategy.FetchDataAsync(_httpClient, cancellationToken);
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("health", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}

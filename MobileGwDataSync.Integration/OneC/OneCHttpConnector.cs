// OneCHttpConnector.cs с улучшенной устойчивостью
using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Exceptions;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Core.Models.DTO;
using MobileGwDataSync.Integration.Models;
using Newtonsoft.Json;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using System.Diagnostics;
using System.Net;

namespace MobileGwDataSync.Integration.OneC
{
    public class OneCHttpConnector : IDataSource
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OneCHttpConnector> _logger;
        private readonly OneCSettings _settings;
        private readonly IAsyncPolicy<HttpResponseMessage> _resilientPolicy;
        private readonly IMetricsService? _metricsService;

        public string SourceName => "1C HTTP Service";

        public OneCHttpConnector(
            IHttpClientFactory httpClientFactory,
            AppSettings appSettings,
            ILogger<OneCHttpConnector> logger,
            IMetricsService? metricsService = null)
        {
            _settings = appSettings.OneC;
            _logger = logger;
            _metricsService = metricsService;
            _httpClient = httpClientFactory.CreateClient("OneC");

            // Создаем комбинированную политику: Retry + Circuit Breaker + Timeout
            _resilientPolicy = Policy.WrapAsync(
                // Circuit Breaker: открывается после 3 последовательных ошибок на 30 секунд
                HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: 3,
                        durationOfBreak: TimeSpan.FromSeconds(30),
                        onBreak: (outcome, duration) =>
                        {
                            _logger.LogWarning("Circuit breaker opened for {Duration}s", duration.TotalSeconds);
                            _metricsService?.RecordSyncError("1C", "CircuitBreakerOpen");
                        },
                        onReset: () =>
                        {
                            _logger.LogInformation("Circuit breaker reset");
                        }),

                // Retry: экспоненциальная задержка с jitter
                HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .WaitAndRetryAsync(
                        retryCount: 3,
                        sleepDurationProvider: retryAttempt =>
                            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                            TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                        onRetry: (outcome, timespan, retryCount, context) =>
                        {
                            _logger.LogWarning(
                                "Retry {RetryCount} after {TimeSpan}s. Status: {StatusCode}",
                                retryCount,
                                timespan.TotalSeconds,
                                outcome.Result?.StatusCode);
                        }),

                // Timeout: общий таймаут на операцию
                Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(_settings.Timeout))
            );
        }

        public async Task<DataTableDTO> FetchDataAsync(
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = parameters.GetValueOrDefault("endpoint", "/subscribers");

            try
            {
                _logger.LogInformation("Fetching data from 1C endpoint: {Endpoint}", endpoint);

                // Выполняем запрос с resilient policy
                var response = await _resilientPolicy.ExecuteAsync(async (ct) =>
                {
                    _logger.LogDebug("Executing GET request to: {Endpoint}", endpoint);
                    return await _httpClient.GetAsync(endpoint, ct);
                }, cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new DataSourceException("Authentication failed. Check username and password.");
                }

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                // Проверка на пустой ответ
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("Received empty response from 1C");
                    return new DataTableDTO
                    {
                        Source = SourceName,
                        FetchedAt = DateTime.UtcNow,
                        Columns = new List<string> { "Account", "Subscriber", "Address", "Balance" },
                        Rows = new List<Dictionary<string, object>>()
                    };
                }

                var result = ParseAndConvert(json);

                stopwatch.Stop();
                _logger.LogInformation(
                    "Successfully fetched {Count} records from 1C in {Duration}ms",
                    result.TotalRows,
                    stopwatch.ElapsedMilliseconds);

                // Метрики
                _metricsService?.RecordStepDuration("1C", "FetchData", stopwatch.Elapsed);

                return result;
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, "Circuit breaker is open. 1C service is unavailable");
                throw new DataSourceException("1C service is temporarily unavailable due to repeated failures", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request to 1C timed out after {Timeout}s", _settings.Timeout);
                throw new DataSourceException($"1C request timed out after {_settings.Timeout} seconds", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while connecting to 1C");
                throw new DataSourceException("Failed to connect to 1C. Check network connection and server availability.", ex);
            }
            catch (Exception ex) when (ex is not DataSourceException)
            {
                _logger.LogError(ex, "Unexpected error while fetching data from 1C");
                throw new DataSourceException("Unexpected error fetching data from 1C", ex);
            }
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var testEndpoint = "/subscribers?limit=1";

                var response = await _resilientPolicy.ExecuteAsync(async (ct) =>
                    await _httpClient.GetAsync(testEndpoint, ct),
                    cancellationToken);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection test to 1C failed");
                return false;
            }
        }

        private DataTableDTO ParseAndConvert(string json)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<OneCResponseWrapper>(json);

                if (response == null || !response.Success)
                {
                    throw new DataSourceException("1C returned unsuccessful response");
                }

                var dataTable = new DataTableDTO
                {
                    Source = SourceName,
                    FetchedAt = DateTime.UtcNow,
                    Columns = new List<string> { "Account", "Subscriber", "Address", "Balance" }
                };

                if (response.Subscribers != null)
                {
                    foreach (var subscriber in response.Subscribers.Where(s => !string.IsNullOrEmpty(s.Account)))
                    {
                        dataTable.Rows.Add(new Dictionary<string, object>
                        {
                            ["Account"] = subscriber.Account,
                            ["Subscriber"] = subscriber.FIO ?? string.Empty,
                            ["Address"] = subscriber.Address ?? string.Empty,
                            ["Balance"] = subscriber.Balance
                        });
                    }
                }

                _logger.LogInformation(
                    "Parsed {Count} subscribers. Stats: Individual={Individual}, Legal={Legal}, TotalDebt={Debt:N2}",
                    response.TotalCount,
                    response.Statistics?.Individual,
                    response.Statistics?.Legal,
                    response.Statistics?.TotalDebt);

                return dataTable;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse 1C response");
                throw new DataSourceException("Invalid JSON response from 1C", ex);
            }
        }
    }
}
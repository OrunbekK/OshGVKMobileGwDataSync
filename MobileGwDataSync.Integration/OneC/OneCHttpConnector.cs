using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Exceptions;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Core.Models.DTO;
using MobileGwDataSync.Integration.Models;
using Newtonsoft.Json;
using Polly;
using Polly.Extensions.Http;
using System.Net;

namespace MobileGwDataSync.Integration.OneC
{
    public class OneCHttpConnector : IDataSource
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OneCHttpConnector> _logger;
        private readonly OneCSettings _settings;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public string SourceName => "1C HTTP Service";

        public OneCHttpConnector(
            IHttpClientFactory httpClientFactory,
            AppSettings appSettings,
            ILogger<OneCHttpConnector> logger)
        {
            _settings = appSettings.OneC;
            _logger = logger;

            // Создаем HttpClient с аутентификацией
            var authHandler = new OneCAuthHandler(_settings.Username, _settings.Password);
            _httpClient = new HttpClient(authHandler)
            {
                BaseAddress = new Uri(_settings.BaseUrl),
                Timeout = TimeSpan.FromSeconds(_settings.Timeout)
            };

            // Настраиваем Polly retry policy
            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Retry {RetryCount} after {TimeSpan}s",
                            retryCount,
                            timespan.TotalSeconds);
                    });
        }

        public async Task<DataTableDTO> FetchDataAsync(
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var endpoint = parameters.GetValueOrDefault("endpoint", "/api/subscribers");
                _logger.LogInformation("Fetching data from 1C endpoint: {Endpoint}", endpoint);

                // Добавляем query parameters если есть
                var queryString = BuildQueryString(parameters);
                if (!string.IsNullOrEmpty(queryString))
                {
                    endpoint = $"{endpoint}?{queryString}";
                }

                // Выполняем запрос с retry policy
                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await _httpClient.GetAsync(endpoint, cancellationToken));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new DataSourceException(
                        $"1C returned error: {response.StatusCode}. Details: {error}");
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Received response from 1C: {Length} characters", json.Length);

                // Парсим ответ
                var result = ParseResponse(json);

                _logger.LogInformation(
                    "Successfully fetched {Count} records from 1C",
                    result.TotalRows);

                return result;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request to 1C timed out");
                throw new DataSourceException("1C request timed out", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while connecting to 1C");
                throw new DataSourceException("Failed to connect to 1C", ex);
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
                _logger.LogInformation("Testing connection to 1C...");

                var response = await _httpClient.GetAsync("/ping", cancellationToken);

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogInformation("Connection to 1C successful");
                    return true;
                }

                _logger.LogWarning("Connection test failed with status: {Status}", response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection test to 1C failed");
                return false;
            }
        }

        private DataTableDTO ParseResponse(string json)
        {
            try
            {
                // Пробуем разные форматы ответа

                // Формат 1: Массив объектов напрямую
                if (json.TrimStart().StartsWith("["))
                {
                    var subscribers = JsonConvert.DeserializeObject<List<OneCSubscriber>>(json);
                    return ConvertToDataTable(subscribers);
                }

                // Формат 2: Обернутый ответ
                var response = JsonConvert.DeserializeObject<OneCResponse<List<OneCSubscriber>>>(json);

                if (response == null || !response.Success)
                {
                    throw new DataSourceException($"1C returned unsuccessful response: {response?.Error}");
                }

                return ConvertToDataTable(response.Data);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse 1C response");
                throw new DataSourceException("Invalid JSON response from 1C", ex);
            }
        }

        private DataTableDTO ConvertToDataTable(List<OneCSubscriber>? subscribers)
        {
            var dataTable = new DataTableDTO
            {
                Source = SourceName,
                FetchedAt = DateTime.UtcNow,
                Columns = new List<string> { "Account", "FIO", "Address", "Balance" }
            };

            if (subscribers == null || subscribers.Count == 0)
            {
                return dataTable;
            }

            foreach (var subscriber in subscribers)
            {
                var row = new Dictionary<string, object>
                {
                    ["Account"] = subscriber.Account,
                    ["FIO"] = subscriber.FIO,
                    ["Address"] = subscriber.Address,
                    ["Balance"] = subscriber.Balance
                };

                dataTable.Rows.Add(row);
            }

            return dataTable;
        }

        private string BuildQueryString(Dictionary<string, string> parameters)
        {
            var queryParams = parameters
                .Where(p => p.Key != "endpoint")
                .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}");

            return string.Join("&", queryParams);
        }
    }
}

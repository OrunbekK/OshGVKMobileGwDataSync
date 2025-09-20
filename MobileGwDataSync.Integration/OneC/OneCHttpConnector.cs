using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Exceptions;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Core.Models.DTO;
using MobileGwDataSync.Integration.Models;
using Newtonsoft.Json;
using Polly;
using Polly.Extensions.Http;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

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

            // Создаем HttpClient через factory
            _httpClient = httpClientFactory.CreateClient("OneC");

            // Настраиваем базовые параметры
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.Timeout);

            // Добавляем Basic Authentication
            var authValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_settings.Username}:{_settings.Password}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authValue);

            // Настраиваем Polly retry policy
            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode && msg.StatusCode != HttpStatusCode.Unauthorized)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Retry {RetryCount} after {TimeSpan}s. Status: {StatusCode}",
                            retryCount,
                            timespan.TotalSeconds,
                            outcome.Result?.StatusCode);
                    });
        }

        public async Task<DataTableDTO> FetchDataAsync(
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Определяем endpoint
                var endpoint = parameters.GetValueOrDefault("endpoint", "/subscribers");

                // Детальное логирование URL
                _logger.LogInformation("=== 1C HTTP Request Details ===");
                _logger.LogInformation("Base URL: {BaseUrl}", _httpClient.BaseAddress);
                _logger.LogInformation("Endpoint parameter: {Endpoint}", endpoint);

                // Формируем полный URL
                var fullUrl = new Uri(_httpClient.BaseAddress, endpoint).ToString();
                _logger.LogInformation("Full URL to call: {FullUrl}", fullUrl);
                _logger.LogInformation("Authorization: Basic (User: {Username})", _settings.Username);
                _logger.LogInformation("===============================");

                // Выполняем запрос с retry policy
                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    _logger.LogDebug("Executing GET request to: {Endpoint}", endpoint);
                    var resp = await _httpClient.GetAsync(endpoint, cancellationToken);
                    _logger.LogDebug("Response Status: {Status}, ReasonPhrase: {Reason}",
                        resp.StatusCode, resp.ReasonPhrase);
                    return resp;
                });

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new DataSourceException("Authentication failed. Check username and password.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new DataSourceException(
                        $"1C returned error: {response.StatusCode}. Details: {error}");
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Received response from 1C: {Length} characters", json.Length);

                // Парсим ответ используя существующий класс OneCSubscriber
                var subscribers = ParseSubscribers(json);
                var result = ConvertToDataTable(subscribers);

                stopwatch.Stop();
                _logger.LogInformation(
                    "Successfully fetched {Count} records from 1C in {Duration}ms",
                    result.TotalRows,
                    stopwatch.ElapsedMilliseconds);

                return result;
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
                _logger.LogInformation("Testing connection to 1C...");

                // Пробуем получить данные с лимитом (если поддерживается)
                var testEndpoint = "/gbill/hs/api/subscribers?limit=1";

                var response = await _httpClient.GetAsync(testEndpoint, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Connection to 1C successful");
                    return true;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Connection test failed: Authentication required");
                    return false;
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

        private List<OneCSubscriber> ParseSubscribers(string json)
        {
            try
            {
                // Проверяем, что JSON не пустой
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("Received empty response from 1C");
                    return new List<OneCSubscriber>();
                }

                // Парсим массив абонентов используя существующий класс
                var subscribers = JsonConvert.DeserializeObject<List<OneCSubscriber>>(json);

                if (subscribers == null)
                {
                    _logger.LogWarning("Failed to deserialize subscribers, returning empty list");
                    return new List<OneCSubscriber>();
                }

                // Валидация данных
                var validSubscribers = subscribers
                    .Where(s => !string.IsNullOrEmpty(s.Account))
                    .ToList();

                if (validSubscribers.Count < subscribers.Count)
                {
                    _logger.LogWarning(
                        "Filtered out {Count} invalid subscribers (missing Account)",
                        subscribers.Count - validSubscribers.Count);
                }

                return validSubscribers;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse 1C response as JSON");
                throw new DataSourceException("Invalid JSON response from 1C", ex);
            }
        }

        private DataTableDTO ConvertToDataTable(List<OneCSubscriber> subscribers)
        {
            var dataTable = new DataTableDTO
            {
                Source = SourceName,
                FetchedAt = DateTime.UtcNow,
                Columns = new List<string> { "Account", "Subscriber", "Address", "Balance" }
            };

            foreach (var subscriber in subscribers)
            {
                var row = new Dictionary<string, object>
                {
                    ["Account"] = subscriber.Account ?? string.Empty,
                    ["Subscriber"] = subscriber.FIO ?? string.Empty,  // Мапим FIO -> Subscriber для БД
                    ["Address"] = subscriber.Address ?? string.Empty,
                    ["Balance"] = subscriber.Balance
                };

                dataTable.Rows.Add(row);
            }

            return dataTable;
        }
    }
}
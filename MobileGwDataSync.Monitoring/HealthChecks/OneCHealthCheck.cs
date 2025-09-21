using Microsoft.Extensions.Diagnostics.HealthChecks;
using MobileGwDataSync.Core.Models.Configuration;
using System.Diagnostics;

namespace MobileGwDataSync.Monitoring.HealthChecks
{
    public class OneCHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppSettings _settings;

        public OneCHealthCheck(IHttpClientFactory httpClientFactory, AppSettings settings)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var client = _httpClientFactory.CreateClient("OneC");
                client.Timeout = TimeSpan.FromSeconds(5);

                var response = await client.GetAsync("/subscribers?limit=1", cancellationToken);
                stopwatch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy(
                        $"1C API is accessible (response time: {stopwatch.ElapsedMilliseconds}ms)",
                        new Dictionary<string, object>
                        {
                            ["ResponseTime"] = stopwatch.ElapsedMilliseconds,
                            ["StatusCode"] = (int)response.StatusCode
                        });
                }

                return HealthCheckResult.Degraded(
                    $"1C API returned {response.StatusCode}",
                    data: new Dictionary<string, object>
                    {
                        ["ResponseTime"] = stopwatch.ElapsedMilliseconds,
                        ["StatusCode"] = (int)response.StatusCode
                    });
            }
            catch (TaskCanceledException)
            {
                return HealthCheckResult.Unhealthy(
                    "1C API timeout",
                    data: new Dictionary<string, object>
                    {
                        ["ResponseTime"] = stopwatch.ElapsedMilliseconds
                    });
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    $"1C API error: {ex.Message}",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        ["ResponseTime"] = stopwatch.ElapsedMilliseconds
                    });
            }
        }
    }
}

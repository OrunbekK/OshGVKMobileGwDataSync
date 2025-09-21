using MobileGwDataSync.Core.Interfaces;

namespace MobileGwDataSync.API.Middleware
{
    /// <summary>
    /// TODO: Сбор метрик по всем HTTP запросам для Prometheus
    /// - Счетчик запросов по endpoints
    /// - Гистограмма времени ответа
    /// - Счетчик ошибок по типам
    /// - Gauge активных запросов
    /// </summary>
    public class MetricsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMetricsService? _metricsService;

        public MetricsMiddleware(RequestDelegate next, IMetricsService? metricsService = null)
        {
            _next = next;
            _metricsService = metricsService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // TODO: Реализовать сбор метрик
            // - http_requests_total{method, endpoint, status}
            // - http_request_duration_seconds{method, endpoint}
            // - http_requests_in_progress

            await _next(context);
        }
    }
}
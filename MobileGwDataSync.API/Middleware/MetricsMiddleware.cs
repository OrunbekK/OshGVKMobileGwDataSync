using MobileGwDataSync.Core.Interfaces;
using System.Diagnostics;

namespace MobileGwDataSync.API.Middleware
{
    public class MetricsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<MetricsMiddleware> _logger;

        public MetricsMiddleware(
            RequestDelegate next,
            IMetricsService metricsService,
            ILogger<MetricsMiddleware> logger)
        {
            _next = next;
            _metricsService = metricsService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Пропускаем метрики для endpoints prometheus
            if (context.Request.Path.StartsWithSegments("/metrics"))
            {
                await _next(context);
                return;
            }

            var path = GetNormalizedPath(context.Request.Path);
            var method = context.Request.Method;

            _metricsService.IncrementHttpRequestsInProgress();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _next(context);

                stopwatch.Stop();
                var statusCode = context.Response.StatusCode;

                _metricsService.RecordHttpRequest(method, path, statusCode, stopwatch.Elapsed);

                if (statusCode >= 500)
                {
                    _logger.LogWarning("HTTP {Method} {Path} returned {StatusCode} in {Duration}ms",
                        method, path, statusCode, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _metricsService.RecordHttpRequest(method, path, 500, stopwatch.Elapsed);

                _logger.LogError(ex, "Unhandled exception in HTTP {Method} {Path}", method, path);
                throw;
            }
            finally
            {
                _metricsService.DecrementHttpRequestsInProgress();
            }
        }

        private string GetNormalizedPath(PathString path)
        {
            var pathValue = path.Value ?? "/";

            // Нормализуем пути с параметрами
            var segments = pathValue.Split('/', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < segments.Length; i++)
            {
                if (Guid.TryParse(segments[i], out _) || int.TryParse(segments[i], out _))
                {
                    segments[i] = "{id}";
                }
            }

            return "/" + string.Join("/", segments);
        }
    }
}
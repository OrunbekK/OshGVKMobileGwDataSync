namespace MobileGwDataSync.API.Middleware
{
    /// <summary>
    /// TODO: Детальное логирование всех HTTP запросов и ответов
    /// - Логирование метода, пути, query parameters
    /// - Измерение времени выполнения запросов
    /// - Логирование статус кодов ответов
    /// - Опционально: логирование тела запроса/ответа для debugging
    /// </summary>
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // TODO: Реализовать логирование
            // var stopwatch = Stopwatch.StartNew();
            // Log request details
            // await _next(context);
            // Log response details and duration

            await _next(context);
        }
    }
}
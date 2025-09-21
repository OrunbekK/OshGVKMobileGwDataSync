using System.Net;
using System.Text.Json;

namespace MobileGwDataSync.API.Middleware
{
    /// <summary>
    /// TODO: Глобальная обработка ошибок для всех API endpoints
    /// - Перехват всех необработанных исключений
    /// - Форматирование ошибок в единый формат JSON
    /// - Логирование ошибок с контекстом запроса
    /// - Возврат правильных HTTP status codes
    /// </summary>
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // TODO: Реализовать обработку различных типов исключений
            // - DataSourceException -> 503 Service Unavailable
            // - ValidationException -> 400 Bad Request
            // - UnauthorizedException -> 401 Unauthorized
            // - NotFoundException -> 404 Not Found
            // - General Exception -> 500 Internal Server Error

            _logger.LogError(exception, "An unhandled exception occurred");

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "An error occurred while processing your request",
                message = exception.Message, // В production скрыть детали
                timestamp = DateTime.UtcNow
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
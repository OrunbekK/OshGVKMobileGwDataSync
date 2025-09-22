using MobileGwDataSync.API.Services;

namespace MobileGwDataSync.API.Middleware
{
    public class DashboardAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<DashboardAuthMiddleware> _logger;

        public DashboardAuthMiddleware(
            RequestDelegate next,
            ILogger<DashboardAuthMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";

            // Проверяем доступ к dashboard файлам
            if (path.Contains("/dashboard") &&
                (path.EndsWith(".html") || path.EndsWith("/") || !path.Contains(".")))
            {
                // Получаем сервис из контекста запроса
                var tokenService = context.RequestServices.GetRequiredService<IJwtTokenService>();

                // Проверяем токен из cookie или header
                string? token = null;

                // Сначала проверяем Authorization header
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    token = authHeader.Substring(7);
                }

                // Если нет в header, проверяем cookie
                if (string.IsNullOrEmpty(token))
                {
                    token = context.Request.Cookies["access_token"];
                }

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Unauthorized access to dashboard from {IP}",
                        context.Connection.RemoteIpAddress);

                    // Редирект на логин
                    context.Response.Redirect("/login.html");
                    return;
                }

                // Валидируем токен
                var principal = tokenService.ValidateToken(token);
                if (principal == null)
                {
                    _logger.LogWarning("Invalid token for dashboard access from {IP}",
                        context.Connection.RemoteIpAddress);

                    context.Response.Redirect("/login.html");
                    return;
                }

                // Устанавливаем пользователя в контекст
                context.User = principal;

                _logger.LogInformation("Dashboard accessed by {User}",
                    principal.Identity?.Name);
            }

            await _next(context);
        }
    }
}
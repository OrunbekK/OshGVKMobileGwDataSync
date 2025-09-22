namespace MobileGwDataSync.API.Security
{
    public class APIIPRestrictionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<APIIPRestrictionMiddleware> _logger;
        private readonly HashSet<string> _allowedIPs;
        private readonly bool _restrictionEnabled;

        public APIIPRestrictionMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            ILogger<APIIPRestrictionMiddleware> logger)
        {
            _next = next;
            _logger = logger;

            // Читаем конфигурацию для общего API доступа
            var apiAccessConfig = configuration.GetSection("Security:ApiAccess");
            _restrictionEnabled = apiAccessConfig.GetValue<bool>("Enabled");

            _allowedIPs = new HashSet<string>();

            // Всегда добавляем localhost
            _allowedIPs.Add("::1");
            _allowedIPs.Add("127.0.0.1");
            _allowedIPs.Add("::ffff:127.0.0.1"); // IPv4-mapped IPv6

            // Добавляем IP из конфигурации
            var ips = apiAccessConfig.GetSection("AllowedIPs").Get<string[]>() ?? Array.Empty<string>();
            foreach (var ip in ips)
            {
                _allowedIPs.Add(ip);
            }

            _logger.LogInformation("IP Restriction enabled: {Enabled}, Allowed IPs: {Count}",
                _restrictionEnabled, _allowedIPs.Count);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Пропускаем проверку для специальных endpoints
            if (!_restrictionEnabled ||
                context.Request.Path.StartsWithSegments("/health") ||
                context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/metrics"))
            {
                await _next(context);
                return;
            }

            // Проверяем только API endpoints
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                var remoteIP = context.Connection.RemoteIpAddress;

                if (remoteIP == null)
                {
                    _logger.LogWarning("Cannot determine client IP address");
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Access denied",
                        message = "Cannot determine client IP",
                        timestamp = DateTime.UtcNow
                    });
                    return;
                }

                var clientIP = remoteIP.IsIPv4MappedToIPv6
                    ? remoteIP.MapToIPv4().ToString()
                    : remoteIP.ToString();

                if (!_allowedIPs.Contains(clientIP))
                {
                    _logger.LogWarning("Blocked API access from unauthorized IP: {IP} to {Path}",
                        clientIP, context.Request.Path);

                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Access denied",
                        message = $"Your IP ({clientIP}) is not authorized to access this API",
                        timestamp = DateTime.UtcNow
                    });
                    return;
                }
            }

            await _next(context);
        }
    }
}
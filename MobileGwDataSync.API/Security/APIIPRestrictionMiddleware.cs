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

            var ipConfig = configuration.GetSection("Security:ApiAccess");
            _restrictionEnabled = ipConfig.GetValue<bool>("Enabled");

            var ips = ipConfig.GetSection("AllowedIPs").Get<string[]>() ?? Array.Empty<string>();
            _allowedIPs = new HashSet<string>(ips);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Пропускаем проверку для admin endpoints и health
            if (!_restrictionEnabled ||
                context.Request.Path.StartsWithSegments("/api/admin") ||
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
                var clientIP = remoteIP?.IsIPv4MappedToIPv6 == true
                    ? remoteIP.MapToIPv4().ToString()
                    : remoteIP?.ToString();

                if (clientIP != null && !_allowedIPs.Contains(clientIP))
                {
                    _logger.LogWarning("Blocked API access from unauthorized IP: {IP} to {Path}",
                        clientIP, context.Request.Path);

                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Access denied",
                        message = "Your IP is not authorized to access this API",
                        timestamp = DateTime.UtcNow
                    });
                    return;
                }
            }

            await _next(context);
        }
    }
}
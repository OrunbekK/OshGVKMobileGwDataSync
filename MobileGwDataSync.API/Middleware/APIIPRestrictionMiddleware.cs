namespace MobileGwDataSync.API.Security
{
    public class ApiIPRestrictionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiIPRestrictionMiddleware> _logger;
        private readonly HashSet<string> _allowedIPs;
        private readonly bool _restrictionEnabled;

        public ApiIPRestrictionMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            ILogger<ApiIPRestrictionMiddleware> logger)
        {
            _next = next;
            _logger = logger;

            var ipConfig = configuration.GetSection("Security:ApiAccess");
            _restrictionEnabled = ipConfig.GetValue<bool>("Enabled");

            var ips = ipConfig.GetSection("AllowedIPs").Get<string[]>() ?? Array.Empty<string>();
            _allowedIPs = new HashSet<string>(ips);
            _allowedIPs.Add("::1");
            _allowedIPs.Add("127.0.0.1");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_restrictionEnabled ||
                !context.Request.Path.StartsWithSegments("/api") ||
                context.Request.Path.StartsWithSegments("/api/admin") ||
                context.Request.Path.StartsWithSegments("/health"))
            {
                await _next(context);
                return;
            }

            var clientIP = context.Connection.RemoteIpAddress?.IsIPv4MappedToIPv6 == true
                ? context.Connection.RemoteIpAddress.MapToIPv4().ToString()
                : context.Connection.RemoteIpAddress?.ToString();

            if (clientIP == null || !_allowedIPs.Contains(clientIP))
            {
                _logger.LogWarning("Blocked API access from unauthorized IP: {IP}", clientIP);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = "Access denied" });
                return;
            }

            await _next(context);
        }
    }
}
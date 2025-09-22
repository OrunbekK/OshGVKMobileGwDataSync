using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;

namespace MobileGwDataSync.API.Security
{
    public class IPWhitelistAttribute : ActionFilterAttribute
    {
        private readonly ILogger<IPWhitelistAttribute> _logger;
        private readonly List<string> _whitelistedIPs;

        public IPWhitelistAttribute()
        {
            _logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<IPWhitelistAttribute>();
            _whitelistedIPs = new List<string>();
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var configuration = context.HttpContext.RequestServices.GetService<IConfiguration>();
            var allowedIPs = configuration?.GetSection("Security:ApiKeyManagement:AllowedIPs")
                .Get<string[]>() ?? Array.Empty<string>();

            // Добавляем localhost по умолчанию
            var defaultAllowedIPs = new[] { "127.0.0.1", "::1", "localhost" };
            _whitelistedIPs.AddRange(allowedIPs.Union(defaultAllowedIPs));

            var remoteIP = context.HttpContext.Connection.RemoteIpAddress;

            if (remoteIP == null)
            {
                context.Result = new StatusCodeResult((int)HttpStatusCode.Forbidden);
                return;
            }

            var clientIP = remoteIP.IsIPv4MappedToIPv6
                ? remoteIP.MapToIPv4().ToString()
                : remoteIP.ToString();

            if (!_whitelistedIPs.Contains(clientIP))
            {
                _logger.LogWarning("Blocked access to API Key Management from unauthorized IP: {IP}", clientIP);
                context.Result = new JsonResult(new { error = "Access denied from this IP address" })
                {
                    StatusCode = (int)HttpStatusCode.Forbidden
                };
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
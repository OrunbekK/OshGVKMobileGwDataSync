using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MobileGwDataSync.Data.Context;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace MobileGwDataSync.API.Security
{
    public class APIKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public const string DefaultScheme = "APIKey";
        public string HeaderName { get; set; } = "X-API-Key";
    }

    public class APIKeyAuthenticationHandler : AuthenticationHandler<APIKeyAuthenticationOptions>
    {
        private readonly ServiceDbContext _context;
        private readonly ILogger<APIKeyAuthenticationHandler> _logger;

        public APIKeyAuthenticationHandler(
            IOptionsMonitor<APIKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ServiceDbContext context) : base(options, logger, encoder)  // Убрали ISystemClock
        {
            _context = context;
            _logger = logger.CreateLogger<APIKeyAuthenticationHandler>();
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Пропускаем Swagger и health endpoints
            var path = Request.Path.Value?.ToLower() ?? "";
            if (path.StartsWith("/swagger") ||
                path.StartsWith("/health") ||
                path.StartsWith("/metrics"))
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "Anonymous"),
                    new Claim("APIKeyId", "0")
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }

            // Получаем API ключ из заголовка
            if (!Request.Headers.TryGetValue(Options.HeaderName, out var APIKeyHeaderValues))
            {
                _logger.LogWarning("Missing API Key header from {IP}",
                    Context.Connection.RemoteIpAddress);
                return AuthenticateResult.Fail("Missing API Key");
            }

            var providedAPIKey = APIKeyHeaderValues.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(providedAPIKey))
            {
                return AuthenticateResult.Fail("Invalid API Key");
            }

            // Хешируем предоставленный ключ
            var keyHash = ComputeHash(providedAPIKey);

            // Ищем ключ в базе
            var APIKey = await _context.APIKeys
                .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive);

            if (APIKey == null)
            {
                _logger.LogWarning("Invalid API Key attempt from {IP}",
                    Context.Connection.RemoteIpAddress);
                return AuthenticateResult.Fail("Invalid API Key");
            }

            // Проверяем срок действия
            if (APIKey.ExpiresAt.HasValue && APIKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired API Key used: {Name}", APIKey.Name);
                return AuthenticateResult.Fail("API Key expired");
            }

            // Проверяем IP адрес
            if (!string.IsNullOrEmpty(APIKey.AllowedIPs))
            {
                var clientIP = Context.Connection.RemoteIpAddress?.ToString();
                var allowedIPs = APIKey.AllowedIPs.Split(',', StringSplitOptions.TrimEntries);

                if (!string.IsNullOrEmpty(clientIP) && !allowedIPs.Contains(clientIP))
                {
                    _logger.LogWarning("API Key {Name} used from unauthorized IP: {IP}",
                        APIKey.Name, clientIP);
                    return AuthenticateResult.Fail("Unauthorized IP address");
                }
            }

            // Обновляем время последнего использования
            APIKey.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Создаем claims
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, APIKey.Name),
                new Claim("APIKeyId", APIKey.Id.ToString())
            };

            // Добавляем permissions как claims
            if (!string.IsNullOrEmpty(APIKey.Permissions))
            {
                try
                {
                    var permissions = System.Text.Json.JsonSerializer.Deserialize<string[]>(APIKey.Permissions);
                    if (permissions != null)
                    {
                        foreach (var permission in permissions)
                        {
                            authClaims.Add(new Claim("Permission", permission));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse permissions for API Key {Name}", APIKey.Name);
                }
            }

            var authIdentity = new ClaimsIdentity(authClaims, Scheme.Name);
            var authPrincipal = new ClaimsPrincipal(authIdentity);
            var authTicket = new AuthenticationTicket(authPrincipal, Scheme.Name);

            _logger.LogInformation("API Key {Name} authenticated successfully from {IP}",
                APIKey.Name, Context.Connection.RemoteIpAddress);

            return AuthenticateResult.Success(authTicket);
        }

        private static string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }
    }
}
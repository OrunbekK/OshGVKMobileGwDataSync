using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MobileGwDataSync.Data.Context;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MobileGwDataSync.API.Services
{
    public interface IJwtTokenService
    {
        string GenerateAccessToken(string username, string role, List<string> permissions);
        string GenerateRefreshToken();
        ClaimsPrincipal? ValidateToken(string token);
        Task<bool> ValidateRefreshTokenAsync(string username, string refreshToken);
        Task StoreRefreshTokenAsync(string username, string refreshToken);
        Task RevokeRefreshTokenAsync(string username);
    }

    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly ServiceDbContext _context;
        private readonly ILogger<JwtTokenService> _logger;

        public JwtTokenService(
            IConfiguration configuration,
            ServiceDbContext context,
            ILogger<JwtTokenService> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        public string GenerateAccessToken(string username, string role, List<string> permissions)
        {
            var secretKey = _configuration["JWT:SecretKey"] ??
                throw new InvalidOperationException("JWT:SecretKey not configured");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Добавляем permissions
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var expiryMinutes = _configuration.GetValue<int>("JWT:ExpiryMinutes", 120);
            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:Issuer"] ?? "MobileGwSync",
                audience: _configuration["JWT:Audience"] ?? "Dashboard",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var secretKey = _configuration["JWT:SecretKey"];
                if (string.IsNullOrEmpty(secretKey))
                {
                    _logger.LogWarning("JWT:SecretKey is not configured");
                    return null;
                }

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["JWT:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["JWT:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return null;
            }
        }

        public async Task<bool> ValidateRefreshTokenAsync(string username, string refreshToken)
        {
            var user = await _context.DashboardUsers
                .FirstOrDefaultAsync(u => u.Username == username && u.RefreshToken == refreshToken);

            return user != null && user.RefreshTokenExpiryTime > DateTime.UtcNow;
        }

        public async Task StoreRefreshTokenAsync(string username, string refreshToken)
        {
            var user = await _context.DashboardUsers
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user != null)
            {
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                await _context.SaveChangesAsync();
            }
        }

        public async Task RevokeRefreshTokenAsync(string username)
        {
            var user = await _context.DashboardUsers
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                await _context.SaveChangesAsync();
            }
        }
    }
}
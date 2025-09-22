using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.API.Models.Auth;
using MobileGwDataSync.API.Services;
using MobileGwDataSync.Data.Context;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MobileGwDataSync.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IJwtTokenService _tokenService;
        private readonly ServiceDbContext _context;
        private readonly ILogger<AuthController> _logger;
        private readonly IConfiguration _configuration;

        public AuthController(
            IJwtTokenService tokenService,
            ServiceDbContext context,
            ILogger<AuthController> logger,
            IConfiguration configuration)
        {
            _tokenService = tokenService;
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Вход в Dashboard
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            // Проверяем пользователя
            var user = await _context.DashboardUsers
                .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
                return Unauthorized(new { message = "Invalid username or password" });
            }

            // Генерируем токены
            var permissions = string.IsNullOrEmpty(user.Permissions)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(user.Permissions) ?? new List<string>();

            var accessToken = _tokenService.GenerateAccessToken(user.Username, user.Role, permissions);
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Сохраняем refresh token
            await _tokenService.StoreRefreshTokenAsync(user.Username, refreshToken);

            // Обновляем последний вход
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {Username} logged in successfully", user.Username);

            // Устанавливаем HttpOnly cookie для refresh token
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            return Ok(new LoginResponse
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("JWT:ExpiryMinutes", 120)),
                User = new UserInfo
                {
                    Username = user.Username,
                    Role = user.Role,
                    Permissions = permissions
                }
            });
        }

        /// <summary>
        /// Обновление токена
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var principal = _tokenService.ValidateToken(request.Token);
            if (principal == null)
            {
                return Unauthorized(new { message = "Invalid token" });
            }

            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized(new { message = "Invalid token claims" });
            }

            // Проверяем refresh token
            if (!await _tokenService.ValidateRefreshTokenAsync(username, request.RefreshToken))
            {
                return Unauthorized(new { message = "Invalid refresh token" });
            }

            var user = await _context.DashboardUsers
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { message = "User not found or inactive" });
            }

            // Генерируем новые токены
            var permissions = string.IsNullOrEmpty(user.Permissions)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(user.Permissions) ?? new List<string>();

            var newAccessToken = _tokenService.GenerateAccessToken(user.Username, user.Role, permissions);
            var newRefreshToken = _tokenService.GenerateRefreshToken();

            await _tokenService.StoreRefreshTokenAsync(user.Username, newRefreshToken);

            return Ok(new LoginResponse
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("JWT:ExpiryMinutes", 120)),
                User = new UserInfo
                {
                    Username = user.Username,
                    Role = user.Role,
                    Permissions = permissions
                }
            });
        }

        /// <summary>
        /// Выход
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                await _tokenService.RevokeRefreshTokenAsync(username);
                _logger.LogInformation("User {Username} logged out", username);
            }

            // Удаляем cookie
            Response.Cookies.Delete("refreshToken");

            return Ok(new { message = "Logged out successfully" });
        }

        /// <summary>
        /// Проверка токена
        /// </summary>
        [HttpGet("verify")]
        [Authorize]
        public IActionResult Verify()
        {
            return Ok(new
            {
                authenticated = true,
                user = User.Identity?.Name,
                role = User.FindFirst(ClaimTypes.Role)?.Value,
                permissions = User.FindAll("permission").Select(c => c.Value)
            });
        }

        private bool VerifyPassword(string password, string passwordHash)
        {
            // Простая проверка для примера. В production используйте BCrypt или Argon2
            using var sha256 = SHA256.Create();
            var hash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
            return hash == passwordHash;
        }
    }
}
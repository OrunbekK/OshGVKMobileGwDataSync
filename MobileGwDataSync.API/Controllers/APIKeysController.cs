using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc;
using MobileGwDataSync.API.Commands;
using MobileGwDataSync.API.Security;

namespace MobileGwDataSync.API.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize]
    [IPWhitelist]
    public class APIKeysController : ControllerBase
    {
        private readonly APIKeyManager _APIKeyManager;
        private readonly ILogger<APIKeysController> _logger;

        public APIKeysController(APIKeyManager APIKeyManager, ILogger<APIKeysController> logger)
        {
            _APIKeyManager = APIKeyManager;
            _logger = logger;
        }

        /// <summary>
        /// Генерирует новый API ключ
        /// </summary>
        /// <remarks>
        /// ВНИМАНИЕ: Для первоначальной генерации используйте SQL скрипт или этот endpoint.
        /// После получения первого ключа, этот endpoint требует аутентификации.
        /// </remarks>
        [HttpPost("generate")]
        [IPWhitelist]
        [Authorize] // Закомментируйте для первой генерации, потом раскомментируйте
        public async Task<IActionResult> GenerateAPIKey([FromBody] GenerateAPIKeyRequest request)
        {
            try
            {
                // Дополнительная проверка на admin права
                if (!User.Claims.Any(c => c.Type == "Permission" && c.Value == "admin"))
                {
                    return Forbid("Admin permission required");
                }

                var APIKey = await _APIKeyManager.GenerateAPIKeyAsync(
                    request.Name,
                    request.Description,
                    request.ExpiresAt,
                    request.AllowedIPs,
                    request.Permissions);

                _logger.LogWarning("New API key generated for: {Name}", request.Name);

                return Ok(new
                {
                    success = true,
                    APIKey = APIKey,
                    warning = "СОХРАНИТЕ ЭТОТ КЛЮЧ! Он не может быть восстановлен."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate API key");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Возвращает список всех API ключей (без хешей)
        /// </summary>
        [HttpGet("list")]
        [Authorize]
        public async Task<IActionResult> ListAPIKeys()
        {
            var keys = await _APIKeyManager.ListAPIKeysAsync();
            return Ok(keys);
        }

        /// <summary>
        /// Отзывает API ключ
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> RevokeAPIKey(int id)
        {
            var result = await _APIKeyManager.RevokeAPIKeyAsync(id);
            if (result)
            {
                _logger.LogWarning("API key revoked. ID: {Id}", id);
                return Ok(new { success = true, message = "API key revoked successfully" });
            }

            return NotFound(new { success = false, message = "API key not found" });
        }

        /// <summary>
        /// Проверяет текущий API ключ
        /// </summary>
        [HttpGet("validate")]
        [Authorize]
        public IActionResult ValidateKey()
        {
            return Ok(new
            {
                valid = true,
                user = User.Identity?.Name,
                claims = User.Claims.Select(c => new { c.Type, c.Value })
            });
        }
    }

    public class GenerateAPIKeyRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? AllowedIPs { get; set; }
        public string[]? Permissions { get; set; }
    }
}
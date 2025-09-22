using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Entities;
using System.Security.Cryptography;
using System.Text;

namespace MobileGwDataSync.API.Commands
{
    public class APIKeyManager
    {
        private readonly ServiceDbContext _context;
        private readonly ILogger<APIKeyManager> _logger;

        public APIKeyManager(ServiceDbContext context, ILogger<APIKeyManager> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<string> GenerateAPIKeyAsync(
            string name,
            string? description = null,
            DateTime? expiresAt = null,
            string? allowedIPs = null,
            string[]? permissions = null)
        {
            // Генерируем случайный ключ
            var key = GenerateRandomKey();
            var keyHash = ComputeHash(key);

            var APIKey = new APIKeyEntity
            {
                Name = name,
                KeyHash = keyHash,
                Description = description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                AllowedIPs = allowedIPs,
                Permissions = permissions != null ?
                    System.Text.Json.JsonSerializer.Serialize(permissions) : null
            };

            _context.APIKeys.Add(APIKey);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated new API key for {Name}", name);

            return key; // Возвращаем сам ключ (только один раз!)
        }

        public async Task<List<APIKeyInfo>> ListAPIKeysAsync()
        {
            return await _context.APIKeys
                .Select(k => new APIKeyInfo
                {
                    Id = k.Id,
                    Name = k.Name,
                    Description = k.Description,
                    IsActive = k.IsActive,
                    CreatedAt = k.CreatedAt,
                    LastUsedAt = k.LastUsedAt,
                    ExpiresAt = k.ExpiresAt,
                    AllowedIPs = k.AllowedIPs
                })
                .ToListAsync();
        }

        public async Task<bool> RevokeAPIKeyAsync(int id)
        {
            var key = await _context.APIKeys.FindAsync(id);
            if (key == null) return false;

            key.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Revoked API key {Name} (ID: {Id})", key.Name, id);
            return true;
        }

        private static string GenerateRandomKey()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            var key = new char[32];

            for (int i = 0; i < key.Length; i++)
            {
                key[i] = chars[random.Next(chars.Length)];
            }

            return new string(key);
        }

        private static string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }
    }

    public class APIKeyInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? AllowedIPs { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;

namespace MobileGwDataSync.Data.Entities
{
    public class APIKeyEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string KeyHash { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastUsedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        [MaxLength(200)]
        public string? AllowedIPs { get; set; } // Comma-separated IP list

        [MaxLength(500)]
        public string? Permissions { get; set; } // JSON array of permissions
    }
}
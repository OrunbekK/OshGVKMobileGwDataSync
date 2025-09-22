using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Data.Entities;
using System.ComponentModel.DataAnnotations;

namespace MobileGwDataSync.Data.Entities
{
    public class DashboardUserEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Viewer"; // Admin, Operator, Viewer

        [MaxLength(500)]
        public string? Permissions { get; set; } // JSON array

        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string? RefreshToken { get; set; }

        public DateTime? RefreshTokenExpiryTime { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }
    }
}

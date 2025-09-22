// MobileGwDataSync.Data/Entities/DashboardUserEntity.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobileGwDataSync.Data.Entities
{
    [Table("dashboard_users")]
    public class DashboardUserEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("role")]
        public string Role { get; set; } = "Viewer"; //Admin, Operator, Viewer

        [MaxLength(500)]
        [Column("permissions")]
        public string? Permissions { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        [Column("refresh_token")]
        public string? RefreshToken { get; set; }

        [Column("refresh_token_expiry_time")]
        public DateTime? RefreshTokenExpiryTime { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }

        [MaxLength(100)]
        [Column("email")]
        public string? Email { get; set; }
    }
}
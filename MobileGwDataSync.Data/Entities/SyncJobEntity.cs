using System.ComponentModel.DataAnnotations;

namespace MobileGwDataSync.Data.Entities
{
    public class SyncJobEntity
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CronExpression { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastRunAt { get; set; }

        public DateTime? NextRunAt { get; set; }

        public string? Configuration { get; set; } // JSON serialized configuration

        // Navigation properties
        public virtual ICollection<SyncRunEntity> Runs { get; set; } = new List<SyncRunEntity>();
    }
}

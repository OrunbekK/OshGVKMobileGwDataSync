using System.ComponentModel.DataAnnotations;

namespace MobileGwDataSync.Data.Entities
{
    public class AlertRuleEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Condition { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Severity { get; set; } = "Information";

        [Required]
        public string Channels { get; set; } = string.Empty; // JSON array of channels

        public int ThrottleMinutes { get; set; } = 5;

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public virtual ICollection<AlertHistoryEntity> History { get; set; } = new List<AlertHistoryEntity>();
    }
}
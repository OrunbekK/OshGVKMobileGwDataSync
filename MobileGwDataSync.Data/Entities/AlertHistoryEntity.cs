using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobileGwDataSync.Data.Entities
{
    public class AlertHistoryEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RuleId { get; set; }

        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string Message { get; set; } = string.Empty;

        public string? NotificationsSent { get; set; } // JSON array of sent notifications

        public bool IsAcknowledged { get; set; }

        public DateTime? AcknowledgedAt { get; set; }

        public string? AcknowledgedBy { get; set; }

        // Navigation property
        [ForeignKey(nameof(RuleId))]
        public virtual AlertRuleEntity Rule { get; set; } = null!;
    }
}

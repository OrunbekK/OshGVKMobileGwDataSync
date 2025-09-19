using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobileGwDataSync.Data.Entities
{
    public class SyncRunStepEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid RunId { get; set; }

        [Required]
        [MaxLength(100)]
        public string StepName { get; set; } = string.Empty;

        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        public DateTime? EndTime { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public string? Details { get; set; }

        public long? DurationMs { get; set; }

        public string? Metrics { get; set; } // JSON serialized metrics

        // Navigation property
        [ForeignKey(nameof(RunId))]
        public virtual SyncRunEntity Run { get; set; } = null!;
    }
}

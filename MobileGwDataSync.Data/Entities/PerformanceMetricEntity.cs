using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobileGwDataSync.Data.Entities
{
    public class PerformanceMetricEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid RunId { get; set; }

        [Required]
        [MaxLength(100)]
        public string MetricName { get; set; } = string.Empty;

        public decimal MetricValue { get; set; }

        [MaxLength(50)]
        public string? Unit { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey(nameof(RunId))]
        public virtual SyncRunEntity Run { get; set; } = null!;
    }
}

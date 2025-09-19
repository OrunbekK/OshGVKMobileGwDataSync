using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobileGwDataSync.Data.Entities
{
    public class SyncRunEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string JobId { get; set; } = string.Empty;

        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        public DateTime? EndTime { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public int RecordsProcessed { get; set; }

        public int RecordsFetched { get; set; }

        public string? ErrorMessage { get; set; }

        public string? Metadata { get; set; } // JSON serialized metadata

        // Navigation properties
        [ForeignKey(nameof(JobId))]
        public virtual SyncJobEntity Job { get; set; } = null!;

        public virtual ICollection<SyncRunStepEntity> Steps { get; set; } = new List<SyncRunStepEntity>();
        public virtual ICollection<PerformanceMetricEntity> Metrics { get; set; } = new List<PerformanceMetricEntity>();
    }
}

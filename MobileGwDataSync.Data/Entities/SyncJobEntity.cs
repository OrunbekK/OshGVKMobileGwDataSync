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
        [MaxLength(50)]
        public string JobType { get; set; } = "Subscribers";

        [Required]
        [MaxLength(100)]
        public string CronExpression { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        // Зависимости и блокировки
        [MaxLength(100)]
        public string? DependsOnJobId { get; set; }

        public bool IsExclusive { get; set; }

        public int Priority { get; set; } = 0;

        // Конфигурация endpoint'ов
        [MaxLength(500)]
        public string OneCEndpoint { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? TargetTable { get; set; }

        [MaxLength(200)]
        public string? TargetProcedure { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastRunAt { get; set; }

        public DateTime? NextRunAt { get; set; }

        public string? Configuration { get; set; } // JSON serialized configuration

        // Navigation properties
        public virtual ICollection<SyncRunEntity> Runs { get; set; } = new List<SyncRunEntity>();
    }
}

using System.ComponentModel.DataAnnotations;

namespace MobileGwDataSync.Data.Entities
{
    public class JobLockEntity
    {
        [Key]
        [MaxLength(100)]
        public string JobId { get; set; } = string.Empty;

        public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        [MaxLength(100)]
        public string LockedBy { get; set; } = string.Empty; // Instance ID или Machine name

        public bool IsActive { get; set; } = true;
    }
}

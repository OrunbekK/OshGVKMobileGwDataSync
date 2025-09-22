namespace MobileGwDataSync.Notifications.Models
{
    public class NotificationResult
    {
        public bool Success { get; set; }
        public string? Reason { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
namespace MobileGwDataSync.Notifications.Models
{
    public class NotificationMessage
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "Information"; // Critical, Warning, Information
        public Dictionary<string, string>? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
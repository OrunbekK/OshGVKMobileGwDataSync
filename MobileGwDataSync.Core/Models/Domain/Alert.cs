namespace MobileGwDataSync.Core.Models.Domain
{
    public class Alert
    {
        public string RuleName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "Information"; // Critical, Warning, Information
        public List<string> Channels { get; set; } = new();
        public int ThrottleMinutes { get; set; } = 5;
        public Dictionary<string, string>? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
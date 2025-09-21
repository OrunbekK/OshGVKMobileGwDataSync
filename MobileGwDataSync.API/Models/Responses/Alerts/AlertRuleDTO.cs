namespace MobileGwDataSync.API.Models.Responses.Alerts
{
    public class AlertRuleDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public List<string> Channels { get; set; } = new();
        public int ThrottleMinutes { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

namespace MobileGwDataSync.API.Models.Responses.Alerts
{
    public class AlertHistoryDTO
    {
        public int Id { get; set; }
        public int RuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public DateTime TriggeredAt { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string>? NotificationsSent { get; set; }
        public bool IsAcknowledged { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public string? AcknowledgedBy { get; set; }
    }
}

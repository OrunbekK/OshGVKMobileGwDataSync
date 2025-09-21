namespace MobileGwDataSync.API.Models.Requests
{
    public class UpdateAlertRuleRequest
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Condition { get; set; }
        public string? Severity { get; set; }
        public List<string>? Channels { get; set; }
        public int? ThrottleMinutes { get; set; }
        public bool? IsEnabled { get; set; }
    }
}

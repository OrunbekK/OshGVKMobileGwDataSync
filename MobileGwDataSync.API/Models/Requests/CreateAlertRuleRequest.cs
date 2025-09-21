namespace MobileGwDataSync.API.Models.Requests
{
    public class CreateAlertRuleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Severity { get; set; } = "Information";
        public List<string> Channels { get; set; } = new();
        public int ThrottleMinutes { get; set; } = 5;
        public bool IsEnabled { get; set; } = true;
    }
}

namespace MobileGwDataSync.API.Models.Responses.Health
{
    public class HealthCheckDTO
    {
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime Uptime { get; set; }
        public Dictionary<string, ComponentHealthDTO> Checks { get; set; }
    }
}

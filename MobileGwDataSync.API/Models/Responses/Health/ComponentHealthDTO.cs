namespace MobileGwDataSync.API.Models.Responses.Health
{
    public class ComponentHealthDTO
    {
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long ResponseTime { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}

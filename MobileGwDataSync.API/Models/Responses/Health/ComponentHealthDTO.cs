namespace MobileGwDataSync.API.Models.Responses.Health
{
    public class ComponentHealthDTO
    {
        public string Status { get; set; }
        public string Description { get; set; }
        public long ResponseTime { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}

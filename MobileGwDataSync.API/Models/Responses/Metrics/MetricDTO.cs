namespace MobileGwDataSync.API.Models.Responses.Metrics
{
    public class MetricDTO
    {
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string? Unit { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}

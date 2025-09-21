namespace MobileGwDataSync.API.Models.Responses.Metrics
{
    public class MetricSummaryDTO
    {
        public decimal Average { get; set; }
        public decimal Min { get; set; }
        public decimal Max { get; set; }
        public int Count { get; set; }
    }
}

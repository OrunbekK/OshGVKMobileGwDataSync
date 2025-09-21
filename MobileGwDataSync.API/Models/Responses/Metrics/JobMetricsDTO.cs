namespace MobileGwDataSync.API.Models.Responses.Metrics
{
    public class JobMetricsDTO
    {
        public string JobId { get; set; }
        public object Period { get; set; }
        public int TotalRuns { get; set; }
        public double SuccessRate { get; set; }
        public double AverageRecords { get; set; }
        public int TotalRecords { get; set; }
        public double AverageDuration { get; set; }
        public double Throughput { get; set; }
        public double ErrorRate { get; set; }
        public List<TimelinePointDTO> Timeline { get; set; }
    }
}

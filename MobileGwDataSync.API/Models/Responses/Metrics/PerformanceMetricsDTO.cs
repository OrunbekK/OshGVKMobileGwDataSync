using MobileGwDataSync.API.Models.Responses.Sync;

namespace MobileGwDataSync.API.Models.Responses.Metrics
{
    public class PerformanceMetricsDTO
    {
        public object? Period { get; set; }
        public SyncMetricsDTO? SyncMetrics { get; set; }
        public Dictionary<string, MetricSummaryDTO>? SystemMetrics { get; set; }
    }
}

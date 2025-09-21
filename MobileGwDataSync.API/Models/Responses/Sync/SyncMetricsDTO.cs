namespace MobileGwDataSync.API.Models.Responses.Sync
{
    public class SyncMetricsDTO
    {
        public int TotalRuns { get; set; }
        public int SuccessfulRuns { get; set; }
        public int FailedRuns { get; set; }
        public double AverageRecordsPerRun { get; set; }
        public int TotalRecordsProcessed { get; set; }
        public double AverageDurationSeconds { get; set; }
    }
}

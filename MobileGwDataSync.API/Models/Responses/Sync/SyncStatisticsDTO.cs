using MobileGwDataSync.API.Models.Responses.Jobs;

namespace MobileGwDataSync.API.Models.Responses.Sync
{
    public class SyncStatisticsDTO
    {
        public int TotalRuns { get; set; }
        public int SuccessfulRuns { get; set; }
        public int FailedRuns { get; set; }
        public int CancelledRuns { get; set; }
        public int TotalRecordsProcessed { get; set; }
        public double AverageDuration { get; set; }
        public double SuccessRate { get; set; }
        public List<JobStatisticDTO>? JobStatistics { get; set; }
    }
}

namespace MobileGwDataSync.API.Models.Responses.Jobs
{
    public class JobStatisticDTO
    {
        public string JobId { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;
        public int TotalRuns { get; set; }
        public int SuccessfulRuns { get; set; }
        public int FailedRuns { get; set; }
        public int TotalRecords { get; set; }
        public double AverageDuration { get; set; }
    }
}

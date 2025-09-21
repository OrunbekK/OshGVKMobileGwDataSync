namespace MobileGwDataSync.API.Models.Responses.Jobs
{
    public class JobStatisticDTO
    {
        public string JobId { get; set; }
        public string JobName { get; set; }
        public int TotalRuns { get; set; }
        public int SuccessfulRuns { get; set; }
        public int FailedRuns { get; set; }
        public int TotalRecords { get; set; }
        public double AverageDuration { get; set; }
    }
}

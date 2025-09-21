namespace MobileGwDataSync.API.Models.Responses.Metrics
{
    public class TimelinePointDTO
    {
        public DateTime Date { get; set; }
        public int Runs { get; set; }
        public int SuccessfulRuns { get; set; }
        public int Records { get; set; }
        public double AverageDuration { get; set; }
    }
}

namespace MobileGwDataSync.API.Models.Responses.Jobs
{
    public class SyncRunSummaryDTO
    {
        public Guid Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
        public int RecordsProcessed { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

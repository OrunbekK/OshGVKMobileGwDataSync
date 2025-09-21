namespace MobileGwDataSync.API.Models.Responses.Sync
{
    public class SyncRunDTO
    {
        public Guid Id { get; set; }
        public string JobId { get; set; }
        public string JobName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
        public int RecordsProcessed { get; set; }
        public int RecordsFetched { get; set; }
        public string? ErrorMessage { get; set; }
        public double? Duration { get; set; }
    }
}

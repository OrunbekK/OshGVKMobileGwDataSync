namespace MobileGwDataSync.Core.Models.Domain
{
    public class SyncRun
    {
        public Guid Id { get; set; }
        public string JobId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public SyncStatus Status { get; set; }
        public int RecordsProcessed { get; set; }
        public int RecordsFetched { get; set; }
        public string? ErrorMessage { get; set; }
        public List<SyncRunStep> Steps { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}

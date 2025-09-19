namespace MobileGwDataSync.Core.Models.DTO
{
    public class SyncResultDTO
    {
        public bool Success { get; set; }
        public int RecordsProcessed { get; set; }
        public int RecordsFailed { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> Errors { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
    }
}

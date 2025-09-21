namespace MobileGwDataSync.API.Models.Responses.Sync
{
    public class SyncTriggerResponseDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int RecordsProcessed { get; set; }
        public int RecordsFailed { get; set; }
        public double Duration { get; set; }
        public List<string> Errors { get; set; }
        public Dictionary<string, object> Metrics { get; set; }
    }
}

namespace MobileGwDataSync.API.Models.Responses.Sync
{
    public class ActiveSyncDTO
    {
        public Guid RunId { get; set; }
        public string? JobId { get; set; }
        public string? JobName { get; set; }
        public DateTime StartTime { get; set; }
        public double RunningFor { get; set; }
        public int RecordsProcessed { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
    }
}

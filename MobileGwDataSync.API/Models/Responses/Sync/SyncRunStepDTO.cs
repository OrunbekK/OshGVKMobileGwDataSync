namespace MobileGwDataSync.API.Models.Responses.Sync
{
    public class SyncRunStepDTO
    {
        public Guid Id { get; set; }
        public string StepName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
        public string? Details { get; set; }
        public long? DurationMs { get; set; }
    }
}

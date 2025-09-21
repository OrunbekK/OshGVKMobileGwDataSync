namespace MobileGwDataSync.API.Models.Responses.Sync
{
    public class SyncRunStepDTO
    {
        public Guid Id { get; set; }
        public string StepName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Details { get; set; }
        public long? DurationMs { get; set; }
    }
}

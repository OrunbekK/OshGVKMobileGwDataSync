namespace MobileGwDataSync.Core.Models.Domain
{
    public class SyncRunStep
    {
        public Guid Id { get; set; }
        public Guid RunId { get; set; }
        public string StepName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public SyncStatus Status { get; set; }
        public string? Details { get; set; }
        public long? DurationMs { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
    }
}

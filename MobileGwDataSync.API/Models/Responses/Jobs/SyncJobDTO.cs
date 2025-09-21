namespace MobileGwDataSync.API.Models.Responses.Jobs
{
    public class SyncJobDTO
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string JobType { get; set; }
        public string CronExpression { get; set; }
        public bool IsEnabled { get; set; }
        public string? DependsOnJobId { get; set; }
        public bool IsExclusive { get; set; }
        public int Priority { get; set; }
        public string OneCEndpoint { get; set; }
        public string? TargetTable { get; set; }
        public string? TargetProcedure { get; set; }
        public DateTime? LastRunAt { get; set; }
        public DateTime? NextRunAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsRunning { get; set; }
    }
}

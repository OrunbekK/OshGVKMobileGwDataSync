namespace MobileGwDataSync.API.Models.Requests
{
    public class CreateJobRequest
    {
        public string Name { get; set; } = string.Empty;
        public string JobType { get; set; } = "Custom";
        public string CronExpression { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public string? DependsOnJobId { get; set; }
        public bool? IsExclusive { get; set; }
        public int? Priority { get; set; }
        public string OneCEndpoint { get; set; } = string.Empty;
        public string? TargetTable { get; set; }
        public string? TargetProcedure { get; set; }
        public Dictionary<string, string>? Configuration { get; set; }
    }
}

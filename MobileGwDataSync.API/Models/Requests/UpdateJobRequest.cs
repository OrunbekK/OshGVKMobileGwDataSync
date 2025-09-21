namespace MobileGwDataSync.API.Models.Requests
{
    public class UpdateJobRequest
    {
        public string? Name { get; set; }
        public string? JobType { get; set; }
        public string? CronExpression { get; set; }
        public bool? IsEnabled { get; set; }
        public string? DependsOnJobId { get; set; }
        public bool? IsExclusive { get; set; }
        public int? Priority { get; set; }
        public string? OneCEndpoint { get; set; }
        public string? TargetTable { get; set; }
        public string? TargetProcedure { get; set; }
        public Dictionary<string, string>? Configuration { get; set; }
    }
}

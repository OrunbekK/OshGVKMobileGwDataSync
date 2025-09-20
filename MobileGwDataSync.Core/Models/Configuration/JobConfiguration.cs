namespace MobileGwDataSync.Core.Models.Configuration
{
    public class JobConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string JobType { get; set; } = "Custom";
        public string CronExpression { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public string? DependsOnJobId { get; set; }
        public bool IsExclusive { get; set; }
        public int Priority { get; set; } = 0;
        public EndpointConfig Endpoint { get; set; } = new();
        public TargetConfig Target { get; set; } = new();
        public Dictionary<string, string> Parameters { get; set; } = new();
    }

    public class EndpointConfig
    {
        public string Path { get; set; } = string.Empty;           // /api/customers
        public string Method { get; set; } = "GET";                // GET/POST
        public Dictionary<string, string> Headers { get; set; } = new();
        public string? Body { get; set; }                          // Для POST запросов
    }

    public class TargetConfig
    {
        public string StagingTable { get; set; } = string.Empty;   // staging_customers
        public string Procedure { get; set; } = string.Empty;      // sp_ProcessCustomers
        public int BatchSize { get; set; } = 10000;
        public bool UseTVP { get; set; } = true;
    }
}

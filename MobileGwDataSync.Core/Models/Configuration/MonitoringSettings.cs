namespace MobileGwDataSync.Core.Models.Configuration
{
    public class MonitoringSettings
    {
        public PrometheusSettings Prometheus { get; set; } = new();
        public HealthCheckSettings HealthChecks { get; set; } = new();
    }
}

// ===== src/MobileGwDataSync.Core/Models/Configuration/AppSettings.cs =====
namespace MobileGwDataSync.Core.Models.Configuration
{
    public class AppSettings
    {
        public ConnectionStrings ConnectionStrings { get; set; } = new();
        public SyncSettings SyncSettings { get; set; } = new();
        public OneCSettings OneC { get; set; } = new();
        public MonitoringSettings Monitoring { get; set; } = new();
        public AlertSettings Alerts { get; set; } = new();
        public NotificationSettings Notifications { get; set; } = new();
    }

    public class ConnectionStrings
    {
        public string SqlServer { get; set; } = string.Empty;
        public string SQLite { get; set; } = "Data Source=sync_service.db";
    }

    public class RetryPolicySettings
    {
        public int MaxAttempts { get; set; } = 3;
        public int DelaySeconds { get; set; } = 30;
        public int MaxDelaySeconds { get; set; } = 300;
    }

    public class PrometheusSettings
    {
        public bool Enabled { get; set; } = true;
        public int Port { get; set; } = 9090;
        public string Path { get; set; } = "/metrics";
    }

    public class HealthCheckSettings
    {
        public bool Enabled { get; set; } = true;
        public string Path { get; set; } = "/health";
    }

    public class AlertSettings
    {
        public List<AlertRule> Rules { get; set; } = new();
    }

    public class AlertRule
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public List<string> Channels { get; set; } = new();
        public int ThrottleMinutes { get; set; } = 5;
    }

    public enum AlertSeverity
    {
        Information,
        Warning,
        Critical
    }
}

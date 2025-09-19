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

    public class SyncSettings
    {
        public int BatchSize { get; set; } = 20000;
        public int MaxParallelBatches { get; set; } = 3;
        public int TimeoutMinutes { get; set; } = 10;
        public RetryPolicySettings RetryPolicy { get; set; } = new();
    }

    public class RetryPolicySettings
    {
        public int MaxAttempts { get; set; } = 3;
        public int DelaySeconds { get; set; } = 30;
        public int MaxDelaySeconds { get; set; } = 300;
    }

    public class OneCSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int Timeout { get; set; } = 300;
    }

    public class MonitoringSettings
    {
        public PrometheusSettings Prometheus { get; set; } = new();
        public HealthCheckSettings HealthChecks { get; set; } = new();
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

    public class NotificationSettings
    {
        public EmailSettings Email { get; set; } = new();
        public WebhookSettings Webhooks { get; set; } = new();
    }

    public class EmailSettings
    {
        public bool Enabled { get; set; }
        public string SmtpServer { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string From { get; set; } = string.Empty;
        public List<string> Recipients { get; set; } = new();
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class WebhookSettings
    {
        public SlackSettings Slack { get; set; } = new();
        public TeamsSettings Teams { get; set; } = new();
    }

    public class SlackSettings
    {
        public bool Enabled { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
    }

    public class TeamsSettings
    {
        public bool Enabled { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}

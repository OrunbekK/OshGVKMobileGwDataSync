namespace MobileGwDataSync.Core.Models.Configuration
{
    public class NotificationSettings
    {
        public EmailSettings Email { get; set; } = new();
        public WebhookSettings Webhooks { get; set; } = new();
    }
}

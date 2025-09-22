using MobileGwDataSync.Notifications.Models;

namespace MobileGwDataSync.Notifications.Channels.Base
{
    public interface INotificationChannel
    {
        Task<NotificationResult> SendAsync(NotificationMessage message);
    }
}
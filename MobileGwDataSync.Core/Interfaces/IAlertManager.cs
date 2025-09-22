using MobileGwDataSync.Core.Models.Domain;

namespace MobileGwDataSync.Core.Interfaces
{
    public interface IAlertManager
    {
        Task SendAlertAsync(Alert alert);
        Task CheckAndSendAlertsAsync();
        bool IsThrottled(string alertKey);
    }
}
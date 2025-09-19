namespace MobileGwDataSync.Core.Models.Domain
{
    public enum SyncStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Cancelled,
        PartiallyCompleted
    }
}

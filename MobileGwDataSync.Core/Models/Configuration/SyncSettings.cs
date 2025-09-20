namespace MobileGwDataSync.Core.Models.Configuration
{
    public class SyncSettings
    {
        public int BatchSize { get; set; } = 20000;
        public int MaxParallelBatches { get; set; } = 3;
        public int TimeoutMinutes { get; set; } = 10;
        public RetryPolicySettings RetryPolicy { get; set; } = new();
    }
}

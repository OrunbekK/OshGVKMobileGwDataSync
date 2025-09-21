using Microsoft.Extensions.DependencyInjection;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Integration.OneC.Strategies;

namespace MobileGwDataSync.Integration.OneC
{
    public interface ISyncStrategyFactory
    {
        ISyncStrategy GetStrategy(string jobType);
    }

    public class SyncStrategyFactory : ISyncStrategyFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public SyncStrategyFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ISyncStrategy GetStrategy(string jobType)
        {
            return jobType.ToLower() switch
            {
                "subscribers" => _serviceProvider.GetRequiredService<SubscribersSyncStrategy>(),
                "controllers" => _serviceProvider.GetRequiredService<ControllersSyncStrategy>(),
                _ => throw new NotSupportedException($"Strategy for job type '{jobType}' not found")
            };
        }
    }
}

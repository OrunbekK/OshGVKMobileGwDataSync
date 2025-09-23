using StackExchange.Redis;
using System.Text.Json;

namespace MobileGwDataSync.Host.Services
{
    public interface ICommandQueue
    {
        Task PublishJobTriggerAsync(string jobId, string triggeredBy);
        Task<JobCommand?> WaitForCommandAsync(CancellationToken cancellationToken);
    }

    public class RedisCommandQueue : ICommandQueue
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisCommandQueue> _logger;

        public RedisCommandQueue(IConnectionMultiplexer redis, ILogger<RedisCommandQueue> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task PublishJobTriggerAsync(string jobId, string triggeredBy)
        {
            var db = _redis.GetDatabase();
            var command = new JobCommand
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Command = "TriggerNow",
                TriggeredBy = triggeredBy,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(command);
            await db.ListRightPushAsync("job:commands", json);

            // Публикуем событие для подписчиков
            var subscriber = _redis.GetSubscriber();
            await subscriber.PublishAsync(RedisChannel.Literal("job:trigger"), json);

            _logger.LogInformation("Published job trigger command for {JobId}", jobId);
        }

        public async Task<JobCommand?> WaitForCommandAsync(CancellationToken cancellationToken)
        {
            var db = _redis.GetDatabase();

            while (!cancellationToken.IsCancellationRequested)
            {
                var value = await db.ListLeftPopAsync("job:commands");
                if (!value.IsNullOrEmpty && value.HasValue)
                {
                    var json = value.ToString();
                    if (!string.IsNullOrEmpty(json))
                    {
                        return JsonSerializer.Deserialize<JobCommand>(json);
                    }
                }

                await Task.Delay(1000, cancellationToken);
            }

            return null;
        }
    }

    public class JobCommand
    {
        public Guid Id { get; set; }
        public string JobId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string TriggeredBy { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}

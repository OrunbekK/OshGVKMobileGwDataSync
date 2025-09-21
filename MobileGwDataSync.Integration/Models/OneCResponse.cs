using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MobileGwDataSync.Integration.Models
{
    public class OneCResponseWrapper
    {
        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("subscribers")]
        public List<OneCSubscriber>? Subscribers { get; set; }

        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }

        [JsonProperty("statistics")]
        public OneCStatistics? Statistics { get; set; }
    }

    public class OneCStatistics
    {
        [JsonProperty("individual")]
        public int Individual { get; set; }

        [JsonProperty("legal")]
        public int Legal { get; set; }

        [JsonProperty("totalDebt")]
        public decimal TotalDebt { get; set; }

        [JsonProperty("totalAdvance")]
        public decimal TotalAdvance { get; set; }
    }

    // Обновите OneCSubscriber, добавив новые поля:
    public class OneCSubscriber
    {
        [JsonProperty("Account")]
        public string Account { get; set; } = string.Empty;

        [JsonProperty("FIO")]
        public string FIO { get; set; } = string.Empty;

        [JsonProperty("Address")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty("Balance")]
        public decimal Balance { get; set; }

        [JsonProperty("Type")]
        public int Type { get; set; }

        [JsonProperty("State")]
        public string State { get; set; } = string.Empty;
    }

    /// <summary>
    /// Модель для TVP (используется с существующим OneCSubscriber)
    /// </summary>
    public class SubscriberTVP
    {
        public string Account { get; set; } = string.Empty;
        public string Subscriber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    /// <summary>
    /// Результат синхронизации
    /// </summary>
    public class SyncResult
    {
        public int ProcessedCount { get; set; }
        public int InsertedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int DeletedCount { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

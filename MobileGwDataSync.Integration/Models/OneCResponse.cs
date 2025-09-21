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

    public class OneCSubscriber
    {
        [JsonProperty("account")]
        public string Account { get; set; } = string.Empty;

        [JsonProperty("fio")]
        public string FIO { get; set; } = string.Empty;

        [JsonProperty("address")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty("balance")]
        public decimal Balance { get; set; }

        [JsonProperty("type")]
        public byte Type { get; set; }

        [JsonProperty("state")]
        public string State { get; set; } = string.Empty;

        [JsonProperty("controllerId")]
        public string ControllerId { get; set; } = string.Empty;

        [JsonProperty("routeId")]
        public string RouteId { get; set; } = string.Empty;
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

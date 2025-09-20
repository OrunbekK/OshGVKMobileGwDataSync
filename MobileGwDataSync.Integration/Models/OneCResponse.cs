using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MobileGwDataSync.Integration.Models
{
    /// <summary>
    /// Базовый ответ от 1С
    /// </summary>
    public class OneCResponse<T>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("data")]
        public T? Data { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }

        [JsonProperty("count")]
        public int? Count { get; set; }

        [JsonProperty("timestamp")]
        public DateTime? Timestamp { get; set; }
    }

    /// <summary>
    /// Модель абонента из 1С (реальная структура)
    /// </summary>
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
    }
}

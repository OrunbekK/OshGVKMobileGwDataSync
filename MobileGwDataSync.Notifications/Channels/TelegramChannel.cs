using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Notifications.Channels.Base;
using MobileGwDataSync.Notifications.Models;
using Newtonsoft.Json;
using System.Text;

namespace MobileGwDataSync.Notifications.Channels
{
    public class TelegramChannel : INotificationChannel
    {
        private readonly HttpClient _httpClient;
        private readonly TelegramSettings _settings;
        private readonly ILogger<TelegramChannel> _logger;
        private readonly string _apiUrl;

        public TelegramChannel(
            IHttpClientFactory httpClientFactory,
            NotificationSettings settings,
            ILogger<TelegramChannel> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _settings = settings.Telegram;
            _logger = logger;
            _apiUrl = $"https://api.telegram.org/bot{_settings.BotToken}";
        }

        public async Task<NotificationResult> SendAsync(NotificationMessage message)
        {
            if (!_settings.Enabled || string.IsNullOrEmpty(_settings.BotToken))
            {
                return new NotificationResult
                {
                    Success = false,
                    Reason = "Telegram notifications disabled or not configured"
                };
            }

            try
            {
                var emoji = message.Severity.ToLower() switch
                {
                    "critical" => "🔴",
                    "warning" => "⚠️",
                    "information" => "ℹ️",
                    _ => "📢"
                };

                var text = FormatMessage(emoji, message);

                var payload = new
                {
                    chat_id = _settings.ChatId,
                    text = text,
                    parse_mode = "HTML",
                    disable_notification = message.Severity.ToLower() == "information"
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiUrl}/sendMessage", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Telegram notification sent successfully");
                    return new NotificationResult { Success = true };
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send Telegram notification: {Error}", error);
                    return new NotificationResult
                    {
                        Success = false,
                        Reason = $"Telegram API error: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Telegram notification");
                return new NotificationResult
                {
                    Success = false,
                    Reason = ex.Message
                };
            }
        }

        private string FormatMessage(string emoji, NotificationMessage message)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"{emoji} <b>{EscapeHtml(message.Title)}</b>");
            sb.AppendLine();
            sb.AppendLine($"<b>Severity:</b> {message.Severity}");
            sb.AppendLine($"<b>Message:</b> {EscapeHtml(message.Message)}");

            if (message.Details != null && message.Details.Any())
            {
                sb.AppendLine();
                sb.AppendLine("<b>Details:</b>");
                foreach (var detail in message.Details)
                {
                    sb.AppendLine($"• {EscapeHtml(detail.Key)}: {EscapeHtml(detail.Value)}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"<i>Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</i>");

            return sb.ToString();
        }

        private string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
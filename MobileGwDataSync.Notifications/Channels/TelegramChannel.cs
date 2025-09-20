using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Models.Configuration;
using Newtonsoft.Json;
using System.Text;

namespace MobileGwDataSync.Notifications.Channels
{
    public class TelegramChannel
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
            _settings = settings.Telegram;  // Изменено с Webhooks.Telegram
            _logger = logger;
            _apiUrl = $"https://api.telegram.org/bot{_settings.BotToken}";
        }

        public async Task<bool> SendMessageAsync(string title, string message, string severity)
        {
            if (!_settings.Enabled || string.IsNullOrEmpty(_settings.BotToken))
                return false;

            try
            {
                // Форматируем сообщение с emoji по severity
                var emoji = severity.ToLower() switch
                {
                    "critical" => "c!",
                    "warning" => "w!",
                    "information" => "i!",
                    _ => "!"
                };

                var text = $"{emoji} *{EscapeMarkdown(title)}*\n\n{EscapeMarkdown(message)}\n\n_Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}_";

                var payload = new
                {
                    chat_id = _settings.ChatId,
                    text = text,
                    parse_mode = "Markdown",
                    disable_notification = severity.ToLower() == "information"
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiUrl}/sendMessage", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Telegram notification sent successfully");
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send Telegram notification: {Error}", error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Telegram notification");
                return false;
            }
        }

        private string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Экранируем специальные символы Markdown
            var specialChars = new[] { "_", "*", "[", "]", "(", ")", "~", "`", ">", "#", "+", "-", "=", "|", "{", "}", ".", "!" };
            foreach (var ch in specialChars)
            {
                text = text.Replace(ch, $"\\{ch}");
            }
            return text;
        }
    }
}

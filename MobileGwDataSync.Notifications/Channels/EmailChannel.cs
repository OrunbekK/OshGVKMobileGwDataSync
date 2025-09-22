using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Notifications.Channels.Base;
using MobileGwDataSync.Notifications.Models;
using System.Net.Mail;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace MobileGwDataSync.Notifications.Channels
{
    public class EmailChannel : INotificationChannel
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailChannel> _logger;

        public EmailChannel(
            NotificationSettings settings,
            ILogger<EmailChannel> logger)
        {
            _settings = settings.Email;
            _logger = logger;
        }

        public async Task<NotificationResult> SendAsync(NotificationMessage message)
        {
            if (!_settings.Enabled)
            {
                return new NotificationResult
                {
                    Success = false,
                    Reason = "Email notifications disabled"
                };
            }

            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(_settings.From));

                foreach (var recipient in _settings.Recipients)
                {
                    email.To.Add(MailboxAddress.Parse(recipient));
                }

                email.Subject = FormatSubject(message);
                email.Body = new TextPart(TextFormat.Html)
                {
                    Text = FormatBody(message)
                };

                // Устанавливаем приоритет
                if (message.Severity == "Critical")
                {
                    email.Priority = MessagePriority.Urgent;
                }

                using var client = new SmtpClient();

                // Подключаемся к серверу
                await client.ConnectAsync(
                    _settings.SmtpServer,
                    _settings.Port,
                    _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);

                // Аутентификация
                await client.AuthenticateAsync(_settings.Username, _settings.Password);

                // Отправка
                await client.SendAsync(email);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to {Count} recipients",
                    _settings.Recipients.Count);

                return new NotificationResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email notification");
                return new NotificationResult
                {
                    Success = false,
                    Reason = ex.Message
                };
            }
        }

        private string FormatSubject(NotificationMessage message)
        {
            // Код остается тем же
            var emoji = message.Severity switch
            {
                "Critical" => "🔴",
                "Warning" => "🟡",
                "Information" => "🔵",
                _ => "📧"
            };

            return $"{emoji} [MobileGW Sync] [{message.Severity}] {message.Title}";
        }

        private string FormatBody(NotificationMessage message)
        {
            var severityColor = message.Severity switch
            {
                "Critical" => "#dc3545",
                "Warning" => "#ffc107",
                "Information" => "#17a2b8",
                _ => "#6c757d"
            };

            var detailsHtml = "";
            if (message.Details != null && message.Details.Any())
            {
                detailsHtml = "<h3>Details:</h3><table style='width: 100%; border-collapse: collapse;'>";
                foreach (var detail in message.Details)
                {
                    detailsHtml += $@"
                        <tr>
                            <td style='padding: 8px; border: 1px solid #ddd; background: #f8f9fa;'><strong>{detail.Key}:</strong></td>
                            <td style='padding: 8px; border: 1px solid #ddd;'>{detail.Value}</td>
                        </tr>";
                }
                detailsHtml += "</table>";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
            border-radius: 10px 10px 0 0;
        }}
        .severity-badge {{
            display: inline-block;
            background: {severityColor};
            color: white;
            padding: 5px 15px;
            border-radius: 20px;
            font-weight: bold;
            font-size: 14px;
        }}
        .content {{ 
            background: white;
            padding: 20px;
            border: 1px solid #e0e0e0;
            border-radius: 0 0 10px 10px;
        }}
        .message-box {{
            background: #f8f9fa;
            padding: 15px;
            border-left: 4px solid {severityColor};
            margin: 20px 0;
        }}
        .metadata {{ 
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #e0e0e0;
            font-size: 12px;
            color: #666;
        }}
        table {{ width: 100%; margin-top: 15px; }}
        td {{ padding: 8px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2 style='margin: 0;'>MobileGW Data Sync Service</h2>
            <div style='margin-top: 10px;'>
                <span class='severity-badge'>{message.Severity.ToUpper()}</span>
            </div>
        </div>
        <div class='content'>
            <h3>{message.Title}</h3>
            <div class='message-box'>
                <p style='margin: 0;'>{message.Message}</p>
            </div>
            {detailsHtml}
            <div class='metadata'>
                <table>
                    <tr>
                        <td><strong>Time:</strong></td>
                        <td>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</td>
                    </tr>
                    <tr>
                        <td><strong>Source:</strong></td>
                        <td>MobileGW Data Sync Service</td>
                    </tr>
                    <tr>
                        <td><strong>Environment:</strong></td>
                        <td>{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}</td>
                    </tr>
                </table>
            </div>
        </div>
    </div>
</body>
</html>";
        }
    }
}
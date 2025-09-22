using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Domain;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Entities;
using MobileGwDataSync.Notifications.Channels;
using MobileGwDataSync.Notifications.Channels.Base;
using MobileGwDataSync.Notifications.Models;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace MobileGwDataSync.Notifications.Services
{
    public class AlertManager : IAlertManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AlertManager> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _throttleCache = new();

        public AlertManager(
            IServiceProvider serviceProvider,
            ILogger<AlertManager> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task SendAlertAsync(Alert alert)
        {
            // Проверка throttling
            var throttleKey = $"{alert.RuleName}_{alert.Severity}";
            if (IsThrottled(throttleKey, alert.ThrottleMinutes))
            {
                _logger.LogDebug("Alert throttled: {Rule}", alert.RuleName);
                return;
            }

            // Создаем сообщение
            var message = new NotificationMessage
            {
                Title = alert.Title,
                Message = alert.Message,
                Severity = alert.Severity,
                Details = alert.Details,
                Timestamp = alert.Timestamp
            };

            // Отправляем по всем каналам
            var tasks = new List<Task<NotificationResult>>();

            foreach (var channelName in alert.Channels)
            {
                var channel = GetChannel(channelName);
                if (channel != null)
                {
                    tasks.Add(channel.SendAsync(message));
                    _logger.LogInformation("Sending alert via {Channel}: {Title}",
                        channelName, alert.Title);
                }
                else
                {
                    _logger.LogWarning("Channel {Channel} not found", channelName);
                }
            }

            var results = await Task.WhenAll(tasks);

            // Обновляем throttle cache только если хотя бы одно уведомление отправлено
            if (results.Any(r => r.Success))
            {
                _throttleCache[throttleKey] = DateTime.UtcNow;
                _logger.LogInformation("Alert sent successfully: {Title}", alert.Title);
            }
            else
            {
                _logger.LogError("Failed to send alert: {Title}", alert.Title);
            }

            // Сохраняем в историю
            await SaveAlertHistory(alert, results);
        }

        public async Task CheckAndSendAlertsAsync()
        {
            using var scope = _serviceProvider.CreateScope();

            // Проверка доступности 1C
            var dataSource = scope.ServiceProvider.GetService<IDataSource>();
            if (dataSource != null)
            {
                try
                {
                    var isHealthy = await dataSource.TestConnectionAsync();
                    if (!isHealthy)
                    {
                        await SendAlertAsync(new Alert
                        {
                            RuleName = "1C_Unavailable",
                            Title = "1C Service Unavailable",
                            Message = "Cannot connect to 1C API endpoint",
                            Severity = "Critical",
                            Channels = new List<string> { "telegram", "email" },
                            ThrottleMinutes = 5,
                            Details = new Dictionary<string, string>
                            {
                                ["Service"] = "1C HTTP API",
                                ["CheckTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking 1C health");
                }
            }

            // Проверка доступности SQL Server
            var dataTarget = scope.ServiceProvider.GetService<IDataTarget>();
            if (dataTarget != null)
            {
                try
                {
                    var isHealthy = await dataTarget.PrepareTargetAsync();
                    await dataTarget.FinalizeTargetAsync(true);

                    if (!isHealthy)
                    {
                        await SendAlertAsync(new Alert
                        {
                            RuleName = "SqlServer_Unavailable",
                            Title = "SQL Server Unavailable",
                            Message = "Cannot connect to SQL Server database",
                            Severity = "Critical",
                            Channels = new List<string> { "telegram", "email" },
                            ThrottleMinutes = 5,
                            Details = new Dictionary<string, string>
                            {
                                ["Service"] = "SQL Server",
                                ["Database"] = "OshGVKMA",
                                ["CheckTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking SQL Server health");
                }
            }
        }

        public bool IsThrottled(string alertKey)
        {
            return IsThrottled(alertKey, 5); // Default 5 minutes
        }

        private bool IsThrottled(string alertKey, int throttleMinutes)
        {
            if (_throttleCache.TryGetValue(alertKey, out var lastSent))
            {
                return DateTime.UtcNow - lastSent < TimeSpan.FromMinutes(throttleMinutes);
            }
            return false;
        }

        private INotificationChannel? GetChannel(string channelName)
        {
            using var scope = _serviceProvider.CreateScope();

            return channelName.ToLower() switch
            {
                "email" => scope.ServiceProvider.GetService<EmailChannel>(),
                "telegram" => scope.ServiceProvider.GetService<TelegramChannel>(),
                _ => null
            };
        }

        private async Task SaveAlertHistory(Alert alert, NotificationResult[] results)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetService<ServiceDbContext>();

                if (context == null) return;

                // Найдем или создадим правило
                var rule = await context.AlertRules
                    .FirstOrDefaultAsync(r => r.Name == alert.RuleName);

                if (rule == null)
                {
                    rule = new AlertRuleEntity
                    {
                        Name = alert.RuleName,
                        Type = "Manual",
                        Condition = "Manual trigger",
                        Severity = alert.Severity,
                        Channels = JsonConvert.SerializeObject(alert.Channels),
                        ThrottleMinutes = alert.ThrottleMinutes,
                        IsEnabled = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.AlertRules.Add(rule);
                    await context.SaveChangesAsync();
                }

                // Добавим историю
                var history = new AlertHistoryEntity
                {
                    RuleId = rule.Id,
                    TriggeredAt = alert.Timestamp,
                    Message = alert.Message,
                    NotificationsSent = JsonConvert.SerializeObject(
                        results.Select((r, i) => new
                        {
                            Channel = alert.Channels.ElementAtOrDefault(i),
                            Success = r.Success,
                            Reason = r.Reason
                        })),
                    IsAcknowledged = false
                };

                context.AlertHistory.Add(history);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save alert history");
            }
        }
    }
}
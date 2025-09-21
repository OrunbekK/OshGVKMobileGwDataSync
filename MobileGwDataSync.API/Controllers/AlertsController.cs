using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Entities;
using Newtonsoft.Json;

namespace MobileGwDataSync.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class AlertsController : ControllerBase
    {
        private readonly ServiceDbContext _context;
        private readonly ILogger<AlertsController> _logger;

        public AlertsController(
            ServiceDbContext context,
            ILogger<AlertsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Получить список правил оповещений
        /// </summary>
        [HttpGet("rules")]
        public async Task<ActionResult<IEnumerable<AlertRuleDto>>> GetAlertRules()
        {
            var rules = await _context.AlertRules
                .Select(r => new AlertRuleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Type = r.Type,
                    Condition = r.Condition,
                    Severity = r.Severity,
                    Channels = JsonConvert.DeserializeObject<List<string>>(r.Channels) ?? new List<string>(),
                    ThrottleMinutes = r.ThrottleMinutes,
                    IsEnabled = r.IsEnabled,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Ok(rules);
        }

        /// <summary>
        /// Получить правило по ID
        /// </summary>
        [HttpGet("rules/{id}")]
        public async Task<ActionResult<AlertRuleDto>> GetAlertRule(int id)
        {
            var rule = await _context.AlertRules.FindAsync(id);

            if (rule == null)
            {
                return NotFound(new { message = $"Alert rule with ID {id} not found" });
            }

            var dto = new AlertRuleDto
            {
                Id = rule.Id,
                Name = rule.Name,
                Type = rule.Type,
                Condition = rule.Condition,
                Severity = rule.Severity,
                Channels = JsonConvert.DeserializeObject<List<string>>(rule.Channels) ?? new List<string>(),
                ThrottleMinutes = rule.ThrottleMinutes,
                IsEnabled = rule.IsEnabled,
                CreatedAt = rule.CreatedAt,
                UpdatedAt = rule.UpdatedAt
            };

            return Ok(dto);
        }

        /// <summary>
        /// Создать новое правило оповещения
        /// </summary>
        [HttpPost("rules")]
        public async Task<ActionResult<AlertRuleDto>> CreateAlertRule([FromBody] CreateAlertRuleRequest request)
        {
            // Проверка уникальности имени
            if (await _context.AlertRules.AnyAsync(r => r.Name == request.Name))
            {
                return Conflict(new { message = $"Alert rule with name '{request.Name}' already exists" });
            }

            var rule = new AlertRuleEntity
            {
                Name = request.Name,
                Type = request.Type,
                Condition = request.Condition,
                Severity = request.Severity,
                Channels = JsonConvert.SerializeObject(request.Channels),
                ThrottleMinutes = request.ThrottleMinutes,
                IsEnabled = request.IsEnabled,
                CreatedAt = DateTime.UtcNow
            };

            _context.AlertRules.Add(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created alert rule: {RuleId} - {RuleName}", rule.Id, rule.Name);

            return CreatedAtAction(nameof(GetAlertRule), new { id = rule.Id }, MapToDto(rule));
        }

        /// <summary>
        /// Обновить правило оповещения
        /// </summary>
        [HttpPut("rules/{id}")]
        public async Task<ActionResult<AlertRuleDto>> UpdateAlertRule(int id, [FromBody] UpdateAlertRuleRequest request)
        {
            var rule = await _context.AlertRules.FindAsync(id);

            if (rule == null)
            {
                return NotFound(new { message = $"Alert rule with ID {id} not found" });
            }

            // Проверка уникальности имени
            if (!string.IsNullOrEmpty(request.Name) && request.Name != rule.Name)
            {
                if (await _context.AlertRules.AnyAsync(r => r.Name == request.Name))
                {
                    return Conflict(new { message = $"Alert rule with name '{request.Name}' already exists" });
                }
            }

            // Обновляем поля
            if (!string.IsNullOrEmpty(request.Name))
                rule.Name = request.Name;
            if (!string.IsNullOrEmpty(request.Type))
                rule.Type = request.Type;
            if (!string.IsNullOrEmpty(request.Condition))
                rule.Condition = request.Condition;
            if (!string.IsNullOrEmpty(request.Severity))
                rule.Severity = request.Severity;
            if (request.Channels != null && request.Channels.Any())
                rule.Channels = JsonConvert.SerializeObject(request.Channels);
            if (request.ThrottleMinutes.HasValue)
                rule.ThrottleMinutes = request.ThrottleMinutes.Value;
            if (request.IsEnabled.HasValue)
                rule.IsEnabled = request.IsEnabled.Value;

            rule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated alert rule: {RuleId}", rule.Id);

            return Ok(MapToDto(rule));
        }

        /// <summary>
        /// Удалить правило оповещения
        /// </summary>
        [HttpDelete("rules/{id}")]
        public async Task<IActionResult> DeleteAlertRule(int id)
        {
            var rule = await _context.AlertRules.FindAsync(id);

            if (rule == null)
            {
                return NotFound(new { message = $"Alert rule with ID {id} not found" });
            }

            _context.AlertRules.Remove(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted alert rule: {RuleId} - {RuleName}", rule.Id, rule.Name);

            return NoContent();
        }

        /// <summary>
        /// Включить/выключить правило
        /// </summary>
        [HttpPatch("rules/{id}/toggle")]
        public async Task<ActionResult<AlertRuleDto>> ToggleAlertRule(int id)
        {
            var rule = await _context.AlertRules.FindAsync(id);

            if (rule == null)
            {
                return NotFound(new { message = $"Alert rule with ID {id} not found" });
            }

            rule.IsEnabled = !rule.IsEnabled;
            rule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Toggled alert rule: {RuleId} - Enabled: {IsEnabled}",
                rule.Id, rule.IsEnabled);

            return Ok(MapToDto(rule));
        }

        /// <summary>
        /// Получить историю срабатываний оповещений
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<IEnumerable<AlertHistoryDto>>> GetAlertHistory(
            [FromQuery] int? ruleId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] bool? acknowledged = null,
            [FromQuery] int limit = 100)
        {
            var query = _context.AlertHistory.AsQueryable();

            if (ruleId.HasValue)
                query = query.Where(h => h.RuleId == ruleId.Value);

            if (from.HasValue)
                query = query.Where(h => h.TriggeredAt >= from.Value);

            if (to.HasValue)
                query = query.Where(h => h.TriggeredAt <= to.Value);

            if (acknowledged.HasValue)
                query = query.Where(h => h.IsAcknowledged == acknowledged.Value);

            var history = await query
                .OrderByDescending(h => h.TriggeredAt)
                .Take(limit)
                .Include(h => h.Rule)
                .Select(h => new AlertHistoryDto
                {
                    Id = h.Id,
                    RuleId = h.RuleId,
                    RuleName = h.Rule.Name,
                    Severity = h.Rule.Severity,
                    TriggeredAt = h.TriggeredAt,
                    Message = h.Message,
                    NotificationsSent = string.IsNullOrEmpty(h.NotificationsSent)
                        ? new List<string>()
                        : JsonConvert.DeserializeObject<List<string>>(h.NotificationsSent),
                    IsAcknowledged = h.IsAcknowledged,
                    AcknowledgedAt = h.AcknowledgedAt,
                    AcknowledgedBy = h.AcknowledgedBy
                })
                .ToListAsync();

            return Ok(history);
        }

        /// <summary>
        /// Подтвердить получение оповещения
        /// </summary>
        [HttpPost("history/{id}/acknowledge")]
        public async Task<IActionResult> AcknowledgeAlert(int id, [FromBody] AcknowledgeRequest request)
        {
            var alert = await _context.AlertHistory.FindAsync(id);

            if (alert == null)
            {
                return NotFound(new { message = $"Alert history with ID {id} not found" });
            }

            if (alert.IsAcknowledged)
            {
                return BadRequest(new { message = "Alert already acknowledged" });
            }

            alert.IsAcknowledged = true;
            alert.AcknowledgedAt = DateTime.UtcNow;
            alert.AcknowledgedBy = request.AcknowledgedBy ?? "API User";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Alert acknowledged: {AlertId} by {User}",
                id, alert.AcknowledgedBy);

            return Ok(new { message = "Alert acknowledged successfully" });
        }

        /// <summary>
        /// Тестовое срабатывание правила
        /// </summary>
        [HttpPost("rules/{id}/test")]
        public async Task<IActionResult> TestAlertRule(int id)
        {
            var rule = await _context.AlertRules.FindAsync(id);

            if (rule == null)
            {
                return NotFound(new { message = $"Alert rule with ID {id} not found" });
            }

            // Создаем тестовую запись в истории
            var testAlert = new AlertHistoryEntity
            {
                RuleId = rule.Id,
                TriggeredAt = DateTime.UtcNow,
                Message = $"Test alert for rule '{rule.Name}'",
                NotificationsSent = JsonConvert.SerializeObject(new[] { "test" }),
                IsAcknowledged = false
            };

            _context.AlertHistory.Add(testAlert);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Test alert triggered for rule: {RuleId} - {RuleName}",
                rule.Id, rule.Name);

            // В реальности здесь должна быть отправка уведомлений
            return Ok(new
            {
                message = "Test alert triggered successfully",
                alertId = testAlert.Id,
                channels = JsonConvert.DeserializeObject<List<string>>(rule.Channels)
            });
        }

        private AlertRuleDto MapToDto(AlertRuleEntity entity)
        {
            return new AlertRuleDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Type = entity.Type,
                Condition = entity.Condition,
                Severity = entity.Severity,
                Channels = JsonConvert.DeserializeObject<List<string>>(entity.Channels) ?? new List<string>(),
                ThrottleMinutes = entity.ThrottleMinutes,
                IsEnabled = entity.IsEnabled,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }

    // DTOs
    public class AlertRuleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public List<string> Channels { get; set; } = new();
        public int ThrottleMinutes { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AlertHistoryDto
    {
        public int Id { get; set; }
        public int RuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public DateTime TriggeredAt { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string>? NotificationsSent { get; set; }
        public bool IsAcknowledged { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public string? AcknowledgedBy { get; set; }
    }

    public class CreateAlertRuleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Severity { get; set; } = "Information";
        public List<string> Channels { get; set; } = new();
        public int ThrottleMinutes { get; set; } = 5;
        public bool IsEnabled { get; set; } = true;
    }

    public class UpdateAlertRuleRequest
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Condition { get; set; }
        public string? Severity { get; set; }
        public List<string>? Channels { get; set; }
        public int? ThrottleMinutes { get; set; }
        public bool? IsEnabled { get; set; }
    }

    public class AcknowledgeRequest
    {
        public string? AcknowledgedBy { get; set; }
    }
}
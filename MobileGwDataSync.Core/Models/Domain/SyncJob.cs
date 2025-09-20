namespace MobileGwDataSync.Core.Models.Domain
{
    public class SyncJob
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public SyncJobType JobType { get; set; } = SyncJobType.Subscribers;
        public string CronExpression { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;

        // Зависимости и блокировки
        public string? DependsOnJobId { get; set; }  // ID задачи, от которой зависит
        public bool IsExclusive { get; set; }        // Блокирует другие задачи при выполнении
        public int Priority { get; set; } = 0;       // Приоритет (больше = выше)

        // Конфигурация специфичная для типа задачи
        public string OneCEndpoint { get; set; } = string.Empty;     // Endpoint в 1С
        public string TargetProcedure { get; set; } = string.Empty;  // Хранимая процедура в SQL
        public string? TargetTable { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastRunAt { get; set; }
        public DateTime? NextRunAt { get; set; }
        
        public Dictionary<string, string> Configuration { get; set; } = new();
    }
}

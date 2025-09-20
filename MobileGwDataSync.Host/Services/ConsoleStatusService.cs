using Quartz;
using System.Diagnostics;

namespace MobileGwDataSync.Host.Services
{
    public class ConsoleStatusService : BackgroundService
    {
        private readonly IHostEnvironment _environment;
        private readonly ILogger<ConsoleStatusService> _logger;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly Stopwatch _uptime = Stopwatch.StartNew();
        private DateTime _nextSyncTime;
        private int _syncCounter = 0;

        public ConsoleStatusService(
            IHostEnvironment environment,
            ILogger<ConsoleStatusService> logger,
            ISchedulerFactory schedulerFactory)
        {
            _environment = environment;
            _logger = logger;
            _schedulerFactory = schedulerFactory;
            _nextSyncTime = DateTime.Now.AddMinutes(5); // По умолчанию
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Показываем статус только в Development режиме или консольном запуске
            if (!_environment.IsDevelopment() && !Environment.UserInteractive)
                return;

            // Выводим заголовок
            Console.Clear();
            WriteHeader();

            while (!stoppingToken.IsCancellationRequested)
            {
                UpdateStatus();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         MobileGW Data Sync Service - RUNNING          ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private async void UpdateStatus()
        {
            // Получаем реальное время из Quartz
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                var triggerKey = new TriggerKey("subscribers-sync-trigger");
                var trigger = await scheduler.GetTrigger(triggerKey);
                if (trigger != null)
                {
                    var nextFire = trigger.GetNextFireTimeUtc();
                    if (nextFire.HasValue)
                    {
                        _nextSyncTime = nextFire.Value.LocalDateTime;
                    }
                }
            }
            catch { /* Игнорируем ошибки */ }
            // Сохраняем позицию курсора
            var currentLine = 4;
            Console.SetCursorPosition(0, currentLine);

            // Очищаем строки статуса
            for (int i = 0; i < 8; i++)
            {
                Console.WriteLine(new string(' ', Console.WindowWidth - 1));
            }
            Console.SetCursorPosition(0, currentLine);

            // Выводим статус
            WriteStatusLine("Status", "Active", ConsoleColor.Green);
            WriteStatusLine("Uptime", FormatUptime(), ConsoleColor.White);
            WriteStatusLine("Next Sync", $"{_nextSyncTime:yyyy-MM-dd HH:mm:ss}", ConsoleColor.Yellow);
            WriteStatusLine("Time Until", FormatTimeUntil(), ConsoleColor.White);
            WriteStatusLine("Syncs Run", _syncCounter.ToString(), ConsoleColor.Cyan);

            // Анимация активности
            Console.WriteLine();
            Console.Write("  Activity: ");
            WriteSpinner();

            // Инструкции
            Console.SetCursorPosition(0, 14);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Press Ctrl+C to stop the service...");
            Console.ResetColor();
        }

        private void WriteStatusLine(string label, string value, ConsoleColor valueColor)
        {
            Console.Write($"  {label,-12}: ");
            Console.ForegroundColor = valueColor;
            Console.WriteLine(value);
            Console.ResetColor();
        }

        private string FormatUptime()
        {
            var uptime = _uptime.Elapsed;
            return $"{(int)uptime.TotalDays}d {uptime.Hours:00}h {uptime.Minutes:00}m {uptime.Seconds:00}s";
        }

        private string FormatTimeUntil()
        {
            var timeUntil = _nextSyncTime - DateTime.Now;
            if (timeUntil.TotalSeconds < 0)
            {
                _nextSyncTime = _nextSyncTime.AddHours(1);
                _syncCounter++;
                timeUntil = _nextSyncTime - DateTime.Now;
            }
            return $"{(int)timeUntil.TotalMinutes}m {timeUntil.Seconds:00}s";
        }

        private int _spinnerIndex = 0;
        private readonly string[] _spinnerChars = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

        private void WriteSpinner()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(_spinnerChars[_spinnerIndex]);
            _spinnerIndex = (_spinnerIndex + 1) % _spinnerChars.Length;
            Console.ResetColor();
        }

        public static void IncrementSyncCounter()
        {
            // Метод для вызова из других сервисов при завершении синхронизации
        }
    }
}

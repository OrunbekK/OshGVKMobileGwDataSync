using Quartz;
using Quartz.Impl.Matchers;
using System.Diagnostics;

namespace MobileGwDataSync.Host.Services
{
    public class ConsoleStatusService : BackgroundService
    {
        private readonly IHostEnvironment _environment;
        private readonly ILogger<ConsoleStatusService> _logger;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly Stopwatch _uptime = Stopwatch.StartNew();

        private Dictionary<string, JobStatusInfo> _jobStatuses = new();
        private readonly object _statusLock = new object();

        public ConsoleStatusService(
            IHostEnvironment environment,
            ILogger<ConsoleStatusService> logger,
            ISchedulerFactory schedulerFactory)
        {
            _environment = environment;
            _logger = logger;
            _schedulerFactory = schedulerFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Показываем статус только в Development режиме или консольном запуске
            if (!_environment.IsDevelopment() && !Environment.UserInteractive)
                return;

            // Ждем инициализации Quartz
            await Task.Delay(2000, stoppingToken);

            // Выводим заголовок
            Console.Clear();
            WriteHeader();

            while (!stoppingToken.IsCancellationRequested)
            {
                await UpdateJobStatuses();
                RenderStatus();
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        private async Task UpdateJobStatuses()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                if (scheduler == null || !scheduler.IsStarted) return;

                var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());
                var executingJobs = await scheduler.GetCurrentlyExecutingJobs();

                var newStatuses = new Dictionary<string, JobStatusInfo>();

                foreach (var jobKey in jobKeys)
                {
                    var jobDetail = await scheduler.GetJobDetail(jobKey);
                    var triggers = await scheduler.GetTriggersOfJob(jobKey);
                    var trigger = triggers.FirstOrDefault();

                    var isRunning = executingJobs.Any(j => j.JobDetail.Key.Equals(jobKey));

                    var status = new JobStatusInfo
                    {
                        JobId = jobKey.Name,
                        JobName = jobDetail?.JobDataMap.GetString("JobName") ?? jobKey.Name,
                        IsRunning = isRunning,
                        NextFireTime = trigger?.GetNextFireTimeUtc()?.LocalDateTime,
                        PreviousFireTime = trigger?.GetPreviousFireTimeUtc()?.LocalDateTime
                    };

                    // Сохраняем счетчик выполнений если он был
                    if (_jobStatuses.TryGetValue(jobKey.Name, out var oldStatus))
                    {
                        status.ExecutionCount = oldStatus.ExecutionCount;

                        // Увеличиваем счетчик если задача завершилась
                        if (oldStatus.IsRunning && !status.IsRunning)
                        {
                            status.ExecutionCount++;
                            status.LastExecutionTime = DateTime.Now;
                        }
                        else
                        {
                            status.LastExecutionTime = oldStatus.LastExecutionTime;
                        }
                    }

                    newStatuses[jobKey.Name] = status;
                }

                lock (_statusLock)
                {
                    _jobStatuses = newStatuses;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update job statuses");
            }
        }

        private void RenderStatus()
        {
            lock (_statusLock)
            {
                Console.SetCursorPosition(0, 5);

                // Общая информация
                WriteGeneralInfo();

                // Информация по задачам
                Console.WriteLine();
                WriteJobsTable();

                // Инструкции
                Console.SetCursorPosition(0, Math.Min(20, Console.WindowHeight - 2));
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Press Ctrl+C to stop the service...");
                Console.ResetColor();
            }
        }

        private void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              MobileGW Data Sync Service - RUNNING                     ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private void WriteGeneralInfo()
        {
            // Очищаем область
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine(new string(' ', Console.WindowWidth - 1));
            }
            Console.SetCursorPosition(0, 5);

            var totalJobs = _jobStatuses.Count;
            var runningJobs = _jobStatuses.Count(j => j.Value.IsRunning);
            var totalExecutions = _jobStatuses.Sum(j => j.Value.ExecutionCount);

            Console.Write("  Status: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"Active");
            Console.ResetColor();

            Console.Write($"  |  Uptime: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(FormatUptime());
            Console.ResetColor();

            Console.Write($"  |  Jobs: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{totalJobs}");
            Console.ResetColor();

            Console.Write($"  |  Running: ");
            Console.ForegroundColor = runningJobs > 0 ? ConsoleColor.Yellow : ConsoleColor.Gray;
            Console.Write($"{runningJobs}");
            Console.ResetColor();

            Console.Write($"  |  Total Runs: ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"{totalExecutions}");
            Console.ResetColor();
        }

        private void WriteJobsTable()
        {
            // Заголовок таблицы
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("  ┌─────────────────────────────┬─────────┬──────────┬──────────────────────┬──────────────────────┐");
            Console.WriteLine("  │ Job Name                    │ Status  │ Runs     │ Last Run             │ Next Run             │");
            Console.WriteLine("  ├─────────────────────────────┼─────────┼──────────┼──────────────────────┼──────────────────────┤");
            Console.ResetColor();

            if (_jobStatuses.Count == 0)
            {
                Console.WriteLine("  │ No jobs configured          │         │          │                      │                      │");
            }
            else
            {
                foreach (var job in _jobStatuses.Values.OrderBy(j => j.JobName))
                {
                    WriteJobRow(job);
                }
            }

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("  └─────────────────────────────┴─────────┴──────────┴──────────────────────┴──────────────────────┘");
            Console.ResetColor();
        }

        private void WriteJobRow(JobStatusInfo job)
        {
            Console.Write("  │ ");

            // Job Name
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{TruncateString(job.JobName, 27),-27}");
            Console.ResetColor();
            Console.Write(" │ ");

            // Status
            if (job.IsRunning)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{"RUNNING",-7}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"{"Idle",-7}");
            }
            Console.ResetColor();
            Console.Write(" │ ");

            // Execution count
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{job.ExecutionCount,8}");
            Console.ResetColor();
            Console.Write(" │ ");

            // Last run
            Console.ForegroundColor = ConsoleColor.Gray;
            if (job.LastExecutionTime.HasValue)
            {
                var timeSince = DateTime.Now - job.LastExecutionTime.Value;
                if (timeSince.TotalMinutes < 1)
                {
                    Console.Write($"{timeSince.Seconds}s ago".PadRight(20));
                }
                else if (timeSince.TotalHours < 1)
                {
                    Console.Write($"{(int)timeSince.TotalMinutes}m ago".PadRight(20));
                }
                else
                {
                    Console.Write($"{job.LastExecutionTime.Value:HH:mm:ss}".PadRight(20));
                }
            }
            else
            {
                Console.Write($"{"Never",-20}");
            }
            Console.ResetColor();
            Console.Write(" │ ");

            // Next run
            if (job.NextFireTime.HasValue)
            {
                var timeUntil = job.NextFireTime.Value - DateTime.Now;
                if (timeUntil.TotalSeconds < 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"{"Overdue",-20}");
                }
                else if (timeUntil.TotalMinutes < 1)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"in {timeUntil.Seconds}s".PadRight(20));
                }
                else if (timeUntil.TotalMinutes < 60)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"in {(int)timeUntil.TotalMinutes}m {timeUntil.Seconds}s".PadRight(20));
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"{job.NextFireTime.Value:HH:mm:ss}".PadRight(20));
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"{"Not scheduled",-20}");
            }
            Console.ResetColor();
            Console.WriteLine(" │");
        }

        private string TruncateString(string str, int maxLength)
        {
            if (str.Length <= maxLength)
                return str;
            return str.Substring(0, maxLength - 3) + "...";
        }

        private string FormatUptime()
        {
            var uptime = _uptime.Elapsed;
            if (uptime.TotalDays >= 1)
                return $"{(int)uptime.TotalDays}d {uptime.Hours:00}h {uptime.Minutes:00}m";
            else if (uptime.TotalHours >= 1)
                return $"{uptime.Hours:00}h {uptime.Minutes:00}m {uptime.Seconds:00}s";
            else
                return $"{uptime.Minutes:00}m {uptime.Seconds:00}s";
        }

        private class JobStatusInfo
        {
            public string JobId { get; set; } = string.Empty;
            public string JobName { get; set; } = string.Empty;
            public bool IsRunning { get; set; }
            public DateTime? NextFireTime { get; set; }
            public DateTime? PreviousFireTime { get; set; }
            public DateTime? LastExecutionTime { get; set; }
            public int ExecutionCount { get; set; }
        }
    }
}
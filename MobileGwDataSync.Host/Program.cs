using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Core.Services;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Repositories;
using MobileGwDataSync.Data.Services;
using MobileGwDataSync.Data.SqlServer;
using MobileGwDataSync.Host.Services;
using MobileGwDataSync.Host.Services.HostedServices;
using MobileGwDataSync.Integration.OneC;
using MobileGwDataSync.Integration.OneC.Strategies;
using MobileGwDataSync.Monitoring.Metrics;
using Quartz;
using Serilog;
using Serilog.Events;

namespace MobileGwDataSync.Host
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // Configure Serilog early
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Starting MobileGwDataSync Service");

                var host = CreateHostBuilder(args).Build();

                // Run database migrations
                try
                {
                    await InitializeDatabaseAsync(host);
                }
                catch (Exception dbEx)
                {
                    Log.Error(dbEx, "Failed to initialize database, but service will continue");
                    // Продолжаем работу даже при ошибке БД
                }

                if (args.Contains("--sync-now") || args.Contains("-s"))
                {
                    Log.Information("Manual sync requested via command line");
                    await RunManualSync(host);
                    return 0;
                }

                await host.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "MobileGwDataSync";
                })
                .ConfigureAppConfiguration((context, config) =>
                {
                    config
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                            optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .AddCommandLine(args);
                })
                .UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                )
                .ConfigureServices((hostContext, services) =>
                {
                    ConfigureServices(services, hostContext.Configuration);
                });

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Configuration
            services.Configure<AppSettings>(configuration);
            var appSettings = configuration.Get<AppSettings>() ?? new AppSettings();
            services.AddSingleton(appSettings);

            // Entity Framework - SQLite for service database
            services.AddDbContext<ServiceDbContext>(options =>
                options.UseSqlite(appSettings.ConnectionStrings.SQLite));

            // Entity Framework - SQL Server for business database
            services.AddDbContext<BusinessDbContext>(options =>
                options.UseSqlServer(appSettings.ConnectionStrings.SqlServer));

            // Repositories
            services.AddScoped<ISyncRunRepository, SyncRunRepository>();
            services.AddScoped<ISyncJobRepository, SyncJobRepository>();

            // Core services - UniversalOneCConnector with strategies
            services.AddScoped<IDataSource, UniversalOneCConnector>();
            services.AddScoped<ISyncStrategyFactory, SyncStrategyFactory>();
            services.AddScoped<SubscribersSyncStrategy>();
            services.AddScoped<ControllersSyncStrategy>();
            services.AddScoped<IDataTarget, SqlServerDataTarget>();
            services.AddScoped<ISyncService, SyncOrchestrator>();

            // Metrics service (даже если пустая реализация)
            services.AddSingleton<IMetricsService, NullMetricsService>();

            // HTTP client for 1C
            services.AddHttpClient("OneC", client =>
            {
                var baseUrl = appSettings.OneC.BaseUrl;

                // Убеждаемся что заканчивается на /
                if (!baseUrl.EndsWith("/"))
                    baseUrl += "/";

                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(appSettings.OneC.Timeout);

                // Basic Auth
                var authValue = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes($"{appSettings.OneC.Username}:{appSettings.OneC.Password}"));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

                // Добавляем заголовки
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "MobileGwDataSync/1.0");

                Log.Information($"Configured OneC HttpClient with BaseAddress: {client.BaseAddress}");
            });

            // Quartz.NET configuration
            services.AddQuartz(q =>
            {
                // UseMicrosoftDependencyInjectionJobFactory больше не нужен - это поведение по умолчанию

                // Устанавливаем имя планировщика
                q.SchedulerId = "AUTO";
                q.SchedulerName = "MobileGwDataSync";

                // Настройки пула потоков
                q.UseDefaultThreadPool(tp =>
                {
                    tp.MaxConcurrency = 10;
                });

                // Настройки хранилища задач (в памяти)
                q.UseInMemoryStore();
            });

            // Добавляем Quartz hosted service
            services.AddQuartzHostedService(options =>
            {
                // Ждем завершения задач при остановке
                options.WaitForJobsToComplete = true;

                // Ждем полного запуска приложения
                options.AwaitApplicationStarted = true;
            });

            // Dynamic job scheduler - загружает задачи из БД
            services.AddHostedService<DynamicJobSchedulerService>();

            // Console status service - только в интерактивном режиме
            /*if (Environment.UserInteractive)
            {
                services.AddHostedService<ConsoleStatusService>();
            }*/

            // Health checks
            services.AddHealthChecks()
                .AddDbContextCheck<ServiceDbContext>("sqlite",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
                .AddDbContextCheck<BusinessDbContext>("sqlserver",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy); // Изменено с Critical

            // TODO: Notification services (когда будут готовы)
            // services.AddScoped<INotificationService, NotificationService>();
            // services.AddScoped<TelegramChannel>();
            // services.AddScoped<EmailChannel>();

            // TODO: Monitoring services (когда будут готовы)
            // services.AddSingleton<IMetricsService, MetricsService>();
            // services.AddHostedService<MetricsExporterService>();
        }

        private static async Task InitializeDatabaseAsync(IHost host)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<ServiceDbContext>();

                /*
                // Пробуем применить миграции
                try
                {
                    await context.Database.EnsureDeletedAsync();
                    await context.Database.EnsureCreatedAsync();
                    //await context.Database.MigrateAsync();
                    Log.Information("Database migration completed successfully");
                }
                catch (Exception migrationEx)
                {
                    Log.Warning("Migration failed: {Message}", migrationEx.Message);
                    // Продолжаем работу - возможно БД уже создана
                }
                */

                // Проверяем подключение к существующей БД
                if (await context.Database.CanConnectAsync())
                    Log.Information("Connected to existing database");
                else
                {
                    // Создаём только если БД не существует
                    await context.Database.EnsureCreatedAsync();
                    Log.Information("Database created");
                }

                // Проверяем и создаём дефолтную задачу
                try
                {
                    var existingJob = await context.SyncJobs
                        .FirstOrDefaultAsync(j => j.Id == "subscribers-sync");

                    if (existingJob == null)
                    {
                        var newJob = new Data.Entities.SyncJobEntity
                        {
                            Id = "subscribers-sync",
                            Name = "Синхронизация абонентов",
                            JobType = "Subscribers",
                            CronExpression = "0 */5 * * * ?",
                            IsEnabled = true,
                            IsExclusive = true,
                            Priority = 10,
                            OneCEndpoint = "subscribers",
                            TargetTable = "TblRefsSubscribers",
                            TargetProcedure = "USP_MA_MergeSubscribers",
                            CreatedAt = DateTime.UtcNow
                        };

                        context.SyncJobs.Add(newJob);
                        await context.SaveChangesAsync();
                        Log.Information("Default sync job 'subscribers-sync' created");
                    }
                }
                catch (Exception jobEx)
                {
                    Log.Warning("Failed to check/create default job: {Message}", jobEx.Message);
                }

                // Выводим все задания (продолжаем даже при ошибках)
                try
                {
                    Log.Information("========================================");
                    Log.Information("CONFIGURED SYNC JOBS:");
                    Log.Information("========================================");

                    var jobs = await context.SyncJobs.ToListAsync();

                    foreach (var job in jobs)
                    {
                        Log.Information("Job: {Id}", job.Id);
                        Log.Information("  Name: {Name}", job.Name);
                        Log.Information("  Type: {Type}", job.JobType);
                        Log.Information("  Cron: {Cron}", job.CronExpression);
                        Log.Information("  Enabled: {Enabled}", job.IsEnabled);
                        Log.Information("  Target: {Target}", job.TargetTable);
                        Log.Information("  Endpoint: {Endpoint}", job.OneCEndpoint);

                        try
                        {
                            var expression = new Quartz.CronExpression(job.CronExpression);
                            var nextRun = expression.GetNextValidTimeAfter(DateTimeOffset.Now);
                            if (nextRun.HasValue)
                            {
                                Log.Information("  Next Run: {NextRun}", nextRun.Value.LocalDateTime);
                                Log.Information("  Time Until: {TimeUntil}", (nextRun.Value - DateTimeOffset.Now).ToString(@"hh\:mm\:ss"));
                            }
                        }
                        catch (Exception cronEx)
                        {
                            Log.Error("  ERROR: Invalid Cron expression - {Error}", cronEx.Message);
                        }
                    }

                    Log.Information("========================================");
                    Log.Information("Total jobs configured: {Count}", jobs.Count);
                    Log.Information("========================================");
                }
                catch (Exception listEx)
                {
                    Log.Warning("Failed to list jobs: {Message}", listEx.Message);
                }

                // Логируем статус Quartz
                await LogQuartzStatus(host);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database initialization had errors, but continuing");
                // НЕ ВЫБРАСЫВАЕМ исключение - продолжаем работу
                // throw; // УБИРАЕМ это!
            }
        }

        private static async Task LogQuartzStatus(IHost host)
        {
            try
            {
                using var scope = host.Services.CreateScope();
                var schedulerFactory = scope.ServiceProvider.GetService<ISchedulerFactory>();

                if (schedulerFactory != null)
                {
                    var scheduler = await schedulerFactory.GetScheduler();

                    Log.Information("========================================");
                    Log.Information("QUARTZ SCHEDULER STATUS:");
                    Log.Information("========================================");
                    Log.Information("  Scheduler Name: {Name}", scheduler.SchedulerName);
                    Log.Information("  Scheduler Started: {Started}", scheduler.IsStarted);
                    Log.Information("  Scheduler Shutdown: {Shutdown}", scheduler.IsShutdown);
                    Log.Information("  In Standby Mode: {Standby}", scheduler.InStandbyMode);

                    // Получаем все задания
                    var jobKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.AnyGroup());
                    Log.Information("  Registered Jobs: {Count}", jobKeys.Count);

                    foreach (var jobKey in jobKeys)
                    {
                        var triggers = await scheduler.GetTriggersOfJob(jobKey);
                        var jobDetail = await scheduler.GetJobDetail(jobKey);

                        Log.Information("  Job: {JobKey}", jobKey);
                        Log.Information("    Class: {Class}", jobDetail?.JobType.Name);
                        Log.Information("    Triggers: {Count}", triggers.Count);

                        foreach (var trigger in triggers)
                        {
                            Log.Information("    Trigger: {TriggerKey}", trigger.Key);
                            Log.Information("      State: {State}", await scheduler.GetTriggerState(trigger.Key));
                            Log.Information("      Next Fire: {NextFire}", trigger.GetNextFireTimeUtc()?.LocalDateTime);
                            Log.Information("      Previous Fire: {PrevFire}", trigger.GetPreviousFireTimeUtc()?.LocalDateTime);

                            if (trigger is Quartz.ICronTrigger cronTrigger)
                            {
                                Log.Information("      Cron Expression: {Cron}", cronTrigger.CronExpressionString);
                            }
                        }
                    }
                    Log.Information("========================================");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get Quartz status");
            }
        }

        private static async Task RunManualSync(IHost host)
        {
            using var scope = host.Services.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

            Log.Information("Starting manual sync...");

            try
            {
                var result = await syncService.ExecuteSyncAsync("subscribers-sync");

                if (result.Success)
                {
                    Log.Information("Manual sync completed successfully. Records: {Count}",
                        result.RecordsProcessed);
                }
                else
                {
                    Log.Warning("Manual sync failed: {Errors}",
                        string.Join(", ", result.Errors));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Manual sync failed with exception");
            }
        }
    }
}
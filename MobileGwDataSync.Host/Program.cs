using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Core.Services;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Repositories;
using MobileGwDataSync.Data.SqlServer;
using MobileGwDataSync.Host.Jobs;
using MobileGwDataSync.Host.Services;
using MobileGwDataSync.Integration.OneC;
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
                await InitializeDatabaseAsync(host);

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
                    .WriteTo.File(
                        Path.Combine("logs", "log-.txt"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        fileSizeLimitBytes: 10_485_760, // 10MB
                        rollOnFileSizeLimit: true,
                        shared: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(1)))
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

            // Repositories
            services.AddScoped<ISyncRunRepository, SyncRunRepository>();
            // TODO: Register job repository when implemented
            // services.AddScoped<ISyncJobRepository, SyncJobRepository>();

            // Core services
            services.AddScoped<ISyncService, SyncOrchestrator>();
            services.AddScoped<IDataSource, OneCHttpConnector>();
            services.AddScoped<IDataTarget, SqlServerDataTarget>();

            // HTTP client for 1C
            services.AddHttpClient("OneC", client =>
            {
                client.BaseAddress = new Uri(appSettings.OneC.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(appSettings.OneC.Timeout);
            });

            // TODO: Register monitoring services
            // services.AddSingleton<IMetricsService, MetricsService>();

            // TODO: Register notification services
            // services.AddScoped<INotificationService, NotificationService>();

            // Configure Quartz.NET
            services.AddQuartz(q =>
            {
                // Создаём идентификатор для задачи
                var jobKey = new JobKey("subscribers-sync-job");

                // Регистрируем задачу
                q.AddJob<DataSyncJob>(opts => opts
                    .WithIdentity(jobKey)
                    .UsingJobData("JobId", "subscribers-sync"));

                // Создаём триггер с расписанием (каждый час)
                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("subscribers-sync-trigger")
                    .WithCronSchedule("0 0 * * * ?") // Каждый час
                    .WithDescription("Hourly sync of subscribers"));
            });

            // Добавляем Quartz hosted service
            services.AddQuartzHostedService(q =>
            {
                q.WaitForJobsToComplete = true;
                q.AwaitApplicationStarted = true;
            });

            // TODO: Add alternative hosted services
            // services.AddHostedService<SyncHostedService>();
            // services.AddHostedService<MetricsExporterService>();

            // Health checks
            services.AddHealthChecks()
                .AddDbContextCheck<ServiceDbContext>("sqlite");

            // TODO: Add SQL Server health check
            // .AddSqlServer(appSettings.ConnectionStrings.SqlServer, name: "sqlserver");

            // Визуализация для консольного режима
            if (Environment.UserInteractive)
            {
                services.AddHostedService<ConsoleStatusService>();
            }
        }

        private static async Task InitializeDatabaseAsync(IHost host)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<ServiceDbContext>();
                await context.Database.MigrateAsync();
                Log.Information("Database migration completed successfully");

                // Проверяем, есть ли задачи в БД, если нет - создаём дефолтную
                var hasJobs = await context.SyncJobs.AnyAsync();
                if (!hasJobs)
                {
                    context.SyncJobs.Add(new Data.Entities.SyncJobEntity
                    {
                        Id = "subscribers-sync",
                        Name = "Синхронизация абонентов",
                        JobType = "Subscribers",
                        CronExpression = "0 0 * * * ?",
                        IsEnabled = true,
                        IsExclusive = true,
                        Priority = 10,
                        OneCEndpoint = "/subscribers",
                        TargetTable = "TblRefsSubscribers",
                        TargetProcedure = "USP_MA_MergeSubscribers",
                        CreatedAt = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();
                    Log.Information("Default sync job created");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while initializing the database");
                throw;
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
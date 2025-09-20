using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Data.Context;
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
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "MobileGwDataSync";
                })
                .ConfigureAppConfiguration((context, config) =>
                {
                    config
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
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

            // TODO: Register repositories
            // services.AddScoped<ISyncJobRepository, SyncJobRepository>();
            // services.AddScoped<ISyncRunRepository, SyncRunRepository>();

            // TODO: Register core services
            // services.AddScoped<ISyncService, SyncOrchestrator>();
            // services.AddScoped<IDataSource, OneCHttpConnector>();
            // services.AddScoped<IDataTarget, SqlServerDataTarget>();

            // TODO: Register monitoring services
            // services.AddSingleton<IMetricsService, MetricsService>();

            // TODO: Register notification services
            // services.AddScoped<INotificationService, NotificationService>();

            // TODO: Add Quartz.NET for scheduling
            // services.AddQuartz(q =>
            // {
            //     q.UseMicrosoftDependencyInjectionJobFactory();
            // });
            // services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

            // TODO: Add hosted services
            // services.AddHostedService<SyncHostedService>();
            // services.AddHostedService<MetricsExporterService>();

            // Health checks
            services.AddHealthChecks()
                .AddDbContextCheck<ServiceDbContext>("sqlite");
            // TODO: Add SQL Server health check
            // .AddSqlServer(appSettings.ConnectionStrings.SqlServer, name: "sqlserver");

            // HTTP client for 1C
            services.AddHttpClient("OneC", client =>
            {
                client.BaseAddress = new Uri(appSettings.OneC.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(appSettings.OneC.Timeout);
            });
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
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while migrating the database");
                throw;
            }
        }
    }
}
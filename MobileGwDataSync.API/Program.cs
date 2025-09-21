using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Core.Services;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Repositories;
using MobileGwDataSync.Data.SqlServer;
using MobileGwDataSync.Integration.OneC;
using MobileGwDataSync.Monitoring.Metrics;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using System.Text.Json.Serialization;

namespace MobileGwDataSync.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Конфигурация
            var appSettings = builder.Configuration.Get<AppSettings>() ?? new AppSettings();
            builder.Services.AddSingleton(appSettings);

            // Add services to the container.
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });

            // Swagger/OpenAPI configuration
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "MobileGW Data Sync API",
                    Version = "v1",
                    Description = "API для управления синхронизацией данных между 1С и SQL Server",
                    Contact = new OpenApiContact
                    {
                        Name = "Support Team",
                        Email = "support@company.com"
                    }
                });

                // Добавляем поддержку XML комментариев
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }

                // Добавляем поддержку авторизации (если нужно)
                options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Description = "API key needed to access the endpoints. X-Api-Key: {key}",
                    In = ParameterLocation.Header,
                    Name = "X-Api-Key",
                    Type = SecuritySchemeType.ApiKey
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "ApiKey"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // Database contexts
            builder.Services.AddDbContext<ServiceDbContext>(options =>
                options.UseSqlite(appSettings.ConnectionStrings.SQLite));

            builder.Services.AddDbContext<BusinessDbContext>(options =>
                options.UseSqlServer(appSettings.ConnectionStrings.SqlServer));

            // HTTP Clients
            builder.Services.AddHttpClient("OneC", client =>
            {
                client.BaseAddress = new Uri(appSettings.OneC.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(appSettings.OneC.Timeout);

                // Basic Auth
                var authValue = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes($"{appSettings.OneC.Username}:{appSettings.OneC.Password}"));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
            });

            // Регистрация сервисов
            builder.Services.AddScoped<IDataSource, OneCHttpConnector>();
            builder.Services.AddScoped<IDataTarget, SqlServerDataTarget>();
            builder.Services.AddScoped<ISyncRunRepository, SyncRunRepository>();
            builder.Services.AddScoped<ISyncService, SyncOrchestrator>();
            builder.Services.AddSingleton<IMetricsService, MetricsService>();

            // Quartz configuration
            builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
            builder.Services.AddSingleton<IJobFactory, JobFactory>();

            builder.Services.AddSingleton(provider =>
            {
                var schedulerFactory = provider.GetRequiredService<ISchedulerFactory>();
                var scheduler = schedulerFactory.GetScheduler().Result;
                scheduler.JobFactory = provider.GetRequiredService<IJobFactory>();
                return scheduler;
            });

            builder.Services.AddHostedService<QuartzHostedService>();

            // Health checks
            builder.Services.AddHealthChecks()
                .AddDbContextCheck<ServiceDbContext>("sqlite")
                .AddDbContextCheck<BusinessDbContext>("sqlserver");

            // CORS (настройте под ваши требования)
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            var app = builder.Build();

            // Apply migrations on startup
            using (var scope = app.Services.Create
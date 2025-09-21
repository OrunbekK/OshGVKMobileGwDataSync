using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MobileGwDataSync.API.Controllers;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Jobs;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Core.Services;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Repositories;
using MobileGwDataSync.Data.SqlServer;
using MobileGwDataSync.Integration.OneC;
using MobileGwDataSync.Monitoring.Metrics;
using Quartz;
using Quartz.Impl;
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
                var baseUrl = appSettings.OneC.BaseUrl;
                if (!baseUrl.EndsWith("/"))
                    baseUrl += "/";

                client.BaseAddress = new Uri(baseUrl);
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

            // MetricsService опционально (может быть null)
            if (builder.Configuration.GetValue<bool>("Monitoring:Prometheus:Enabled", false))
            {
                builder.Services.AddSingleton<IMetricsService, MetricsService>();
            }

            // Quartz configuration
            builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();

            // Регистрация Job классов для DI
            builder.Services.AddTransient<DataSyncJob>();

            // Настройка Quartz
            builder.Services.AddQuartz(q =>
            {
                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();
                q.UseDefaultThreadPool(tp =>
                {
                    tp.MaxConcurrency = 10;
                });
            });

            // Добавляем Quartz hosted service
            builder.Services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });

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

                // Более строгая политика для production
                options.AddPolicy("Production", policy =>
                {
                    policy.WithOrigins("http://localhost:3000", "https://yourdomain.com")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            var app = builder.Build();

            // Apply migrations on startup
            using (var scope = app.Services.CreateScope())
            {
                var serviceDbContext = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();
                try
                {
                    serviceDbContext.Database.Migrate();
                    Console.WriteLine("SQLite database migrations applied successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error applying migrations: {ex.Message}");
                }

                // Проверяем подключение к SQL Server
                var businessDbContext = scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
                try
                {
                    if (businessDbContext.Database.CanConnect())
                    {
                        Console.WriteLine("SQL Server connection successful.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SQL Server connection failed: {ex.Message}");
                }
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "MobileGW Data Sync API v1");
                    options.RoutePrefix = string.Empty; // Swagger UI на корневом URL
                    options.DefaultModelsExpandDepth(2);
                    options.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
                    options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
                    options.EnableDeepLinking();
                    options.DisplayOperationId();
                });
            }

            // Security headers
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Xss-Protection"] = "1; mode=block";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                await next();
            });

            // Request logging middleware
            app.Use(async (context, next) =>
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Request {Method} {Path} from {IP}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Connection.RemoteIpAddress);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                await next();
                stopwatch.Stop();

                logger.LogInformation("Response {StatusCode} for {Method} {Path} in {ElapsedMs}ms",
                    context.Response.StatusCode,
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds);
            });

            // Global error handling
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";

                    var exceptionHandlerFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                    if (exceptionHandlerFeature != null)
                    {
                        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogError(exceptionHandlerFeature.Error, "Unhandled exception occurred");

                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "An error occurred while processing your request",
                            message = app.Environment.IsDevelopment()
                                ? exceptionHandlerFeature.Error.Message
                                : "Internal server error"
                        });
                    }
                });
            });

            app.UseHttpsRedirection();

            // CORS
            if (app.Environment.IsDevelopment())
            {
                app.UseCors("AllowAll");
            }
            else
            {
                app.UseCors("Production");
            }

            app.UseAuthorization();

            app.MapControllers();

            // Health check endpoints
            app.MapHealthChecks("/health");
            app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });
            app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = _ => false
            });

            // Prometheus metrics endpoint (если используется)
            app.MapGet("/metrics", async context =>
            {
                var metricsController = context.RequestServices.GetRequiredService<MetricsController>();
                // Здесь можно вызвать метод GetPrometheusMetrics
                await context.Response.WriteAsync("# Metrics endpoint");
            });

            // Запускаем динамическую загрузку задач из БД в Quartz
            Task.Run(async () =>
            {
                await Task.Delay(5000); // Ждем инициализации
                using var scope = app.Services.CreateScope();
                var scheduler = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
                var context = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                try
                {
                    var jobs = await context.SyncJobs.Where(j => j.IsEnabled).ToListAsync();
                    var sched = await scheduler.GetScheduler();

                    foreach (var jobEntity in jobs)
                    {
                        var job = JobBuilder.Create<DataSyncJob>()
                            .WithIdentity(jobEntity.Id)
                            .UsingJobData("JobId", jobEntity.Id)
                            .Build();

                        var trigger = TriggerBuilder.Create()
                            .WithIdentity($"{jobEntity.Id}-trigger")
                            .WithCronSchedule(jobEntity.CronExpression)
                            .StartNow()
                            .Build();

                        await sched.ScheduleJob(job, trigger);
                        logger.LogInformation("Scheduled job {JobId} with cron {Cron}",
                            jobEntity.Id, jobEntity.CronExpression);
                    }

                    await sched.Start();
                    logger.LogInformation("Quartz scheduler started with {Count} jobs", jobs.Count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize Quartz jobs");
                }
            });

            // Информация о запуске
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("MobileGW Data Sync API started");
            logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
            logger.LogInformation("URLs: {Urls}", string.Join(", ", app.Urls));

            app.Run();
        }
    }
}
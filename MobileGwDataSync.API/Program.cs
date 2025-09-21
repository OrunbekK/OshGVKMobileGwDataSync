using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MobileGwDataSync.API.Controllers;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Jobs;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Core.Services;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Repositories;
using MobileGwDataSync.Data.Services;
using MobileGwDataSync.Data.SqlServer;
using MobileGwDataSync.Integration.OneC;
using MobileGwDataSync.Monitoring.Metrics;
using Prometheus;
using Quartz;
using Quartz.Impl;
using System.IO.Compression;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace MobileGwDataSync.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configuration
            var appSettings = builder.Configuration.Get<AppSettings>() ?? new AppSettings();
            builder.Services.AddSingleton(appSettings);

            // Add services to the container
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });

            // API Versioning
            builder.Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new HeaderApiVersionReader("X-Api-Version"),
                    new QueryStringApiVersionReader("api-version")
                );
            })
            .AddMvc() // Important for MVC versioning
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            // Configure Swagger
            ConfigureSwagger(builder);

            // Rate Limiting
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Global limiter
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    httpContext => RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User?.Identity?.Name ??
                                     httpContext.Connection.RemoteIpAddress?.ToString() ??
                                     "anonymous",
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 100,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                // Special limiter for heavy operations
                options.AddPolicy("HeavyOperation", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User?.Identity?.Name ??
                                     httpContext.Connection.RemoteIpAddress?.ToString() ??
                                     "anonymous",
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 10,
                            QueueLimit = 2,
                            Window = TimeSpan.FromMinutes(1)
                        }));
            });

            // Response Caching
            builder.Services.AddResponseCaching();

            // Response Compression
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                    new[] { "application/json", "text/json", "text/plain", "application/xml" });
            });

            builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            builder.Services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.SmallestSize;
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

            // Register services
            builder.Services.AddScoped<IDataSource, OneCHttpConnector>();
            builder.Services.AddScoped<IDataTarget, SqlServerDataTarget>();
            builder.Services.AddScoped<ISyncRunRepository, SyncRunRepository>();
            builder.Services.AddScoped<ISyncJobRepository, SyncJobRepository>();
            builder.Services.AddScoped<ISyncService, SyncOrchestrator>();

            // MetricsService registration based on configuration
            if (builder.Configuration.GetValue<bool>("Monitoring:Prometheus:Enabled", false))
                builder.Services.AddSingleton<IMetricsService, MetricsService>();
            else
                builder.Services.AddSingleton<IMetricsService, NullMetricsService>();

            // Quartz configuration
            builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
            builder.Services.AddTransient<DataSyncJob>();

            builder.Services.AddQuartz(q =>
            {
                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();
                q.UseDefaultThreadPool(tp =>
                {
                    tp.MaxConcurrency = 10;
                });
            });

            builder.Services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });

            // Health checks
            builder.Services.AddHealthChecks()
                .AddDbContextCheck<ServiceDbContext>("sqlite", tags: new[] { "db", "sqlite" })
                .AddDbContextCheck<BusinessDbContext>("sqlserver", tags: new[] { "db", "sql" })
                .AddCheck("memory", () =>
                {
                    var allocated = GC.GetTotalMemory(forceFullCollection: false);
                    var data = new Dictionary<string, object>
                    {
                        ["AllocatedBytes"] = allocated,
                        ["AllocatedMB"] = allocated / (1024 * 1024),
                        ["Gen0Collections"] = GC.CollectionCount(0),
                        ["Gen1Collections"] = GC.CollectionCount(1),
                        ["Gen2Collections"] = GC.CollectionCount(2)
                    };

                    var status = allocated < 500_000_000
                        ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
                        : allocated < 1_000_000_000
                            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded
                            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy;

                    return new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult(
                        status,
                        $"Memory usage: {allocated / (1024 * 1024)} MB",
                        data: data);
                }, tags: new[] { "system" });

            // CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });

                options.AddPolicy("Production", policy =>
                {
                    //policy.WithOrigins("http://localhost:3000", "https://yourdomain.com")
                    //      .AllowAnyMethod()
                    //      .AllowAnyHeader()
                    //      .AllowCredentials();
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

            // Middleware Pipeline Configuration
            app.UseResponseCompression();
            app.UseResponseCaching();
            app.UseRateLimiter();

            // Configure Swagger with versioning
            ConfigureSwaggerUI(app);

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
                                : "Internal server error",
                            traceId = context.TraceIdentifier
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
            app.UseMetricServer();
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

            // Custom metrics endpoint (alternative to UseMetricServer)
            app.MapGet("/metrics", async context =>
            {
                await context.Response.WriteAsync("# Custom metrics endpoint\n");
                // Here you can add custom metrics output
            });

            // Initialize Quartz jobs from database
            Task.Run(async () =>
            {
                await Task.Delay(5000); // Wait for initialization
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

            // Startup information
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("MobileGW Data Sync API started");
            logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
            logger.LogInformation("URLs: {Urls}", string.Join(", ", app.Urls));

            app.Run();
        }

        private static void ConfigureSwagger(WebApplicationBuilder builder)
        {
            builder.Services.AddEndpointsApiExplorer();

            // Configure SwaggerGen with versioning support
            builder.Services.AddSwaggerGen(options =>
            {
                // Get API version description provider from services
                var provider = builder.Services.BuildServiceProvider()
                    .GetService<IApiVersionDescriptionProvider>();

                // Add a swagger document for each discovered API version
                if (provider != null)
                {
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerDoc(description.GroupName, CreateApiInfo(description));
                    }
                }
                else
                {
                    // Fallback if versioning provider is not available
                    options.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "MobileGW Data Sync API",
                        Version = "v1",
                        Description = "API for managing data synchronization between 1C and SQL Server",
                        Contact = new OpenApiContact
                        {
                            Name = "SoftKO",
                            Email = "softko@gmail.com"
                        }
                    });
                }

                // Add XML comments if available
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }

                // Add security definition
                options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Description = "API key needed to access the endpoints. X-Api-Key: {key}",
                    In = ParameterLocation.Header,
                    Name = "X-Api-Key",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "ApiKey"
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

                // Custom operation filter to handle versioning in URLs
                options.OperationFilter<SwaggerDefaultValues>();
            });
        }

        private static void ConfigureSwaggerUI(WebApplication app)
        {
            // Enable Swagger for all environments (you can change this)
            app.UseSwagger();

            app.UseSwaggerUI(options =>
            {
                // Build swagger endpoints for each API version
                var provider = app.Services.GetService<IApiVersionDescriptionProvider>();

                if (provider != null)
                {
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerEndpoint(
                            $"/swagger/{description.GroupName}/swagger.json",
                            $"MobileGW Data Sync API {description.GroupName.ToUpperInvariant()}");
                    }
                }
                else
                {
                    // Fallback endpoint
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "MobileGW Data Sync API v1");
                }

                options.RoutePrefix = "swagger";
                options.DefaultModelsExpandDepth(2);
                options.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
                options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
                options.EnableDeepLinking();
                options.DisplayOperationId();
                options.ShowExtensions();
                options.EnableFilter();
                options.EnableTryItOutByDefault();
            });
        }

        private static OpenApiInfo CreateApiInfo(Asp.Versioning.ApiExplorer.ApiVersionDescription description)
        {
            var info = new OpenApiInfo
            {
                Title = "MobileGW Data Sync API",
                Version = description.ApiVersion.ToString(),
                Description = "API for managing data synchronization between 1C and SQL Server",
                Contact = new OpenApiContact
                {
                    Name = "SoftKO",
                    Email = "softko@gmail.com"
                },
                License = new OpenApiLicense
                {
                    Name = "MIT",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            };

            if (description.IsDeprecated)
            {
                info.Description += " **This API version has been deprecated.**";
            }

            return info;
        }
    }

    /// <summary>
    /// Configures the Swagger generation options to support API versioning
    /// </summary>
    public class SwaggerDefaultValues : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
    {
        public void Apply(OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
        {
            var apiDescription = context.ApiDescription;

            operation.Deprecated |= apiDescription.IsDeprecated();

            // Handle responses
            foreach (var responseType in context.ApiDescription.SupportedResponseTypes)
            {
                var responseKey = responseType.IsDefaultResponse ? "default" : responseType.StatusCode.ToString();
                if (operation.Responses.TryGetValue(responseKey, out var response))
                {
                    var contentTypesToRemove = response.Content.Keys
                        .Where(contentType => responseType.ApiResponseFormats.All(x => x.MediaType != contentType))
                        .ToList();

                    foreach (var contentType in contentTypesToRemove)
                    {
                        response.Content.Remove(contentType);
                    }
                }
            }

            if (operation.Parameters == null)
            {
                return;
            }

            // Handle parameters
            foreach (var parameter in operation.Parameters)
            {
                var description = apiDescription.ParameterDescriptions.FirstOrDefault(p => p.Name == parameter.Name);

                if (description != null)
                {
                    parameter.Description ??= description.ModelMetadata?.Description;

                    if (parameter.Schema.Default == null &&
                        description.DefaultValue != null &&
                        description.DefaultValue is not DBNull &&
                        description.ModelMetadata is { } modelMetadata)
                    {
                        // Create OpenApiString for default values instead of using OpenApiAnyFactory
                        var defaultValue = description.DefaultValue?.ToString();
                        if (!string.IsNullOrEmpty(defaultValue))
                        {
                            parameter.Schema.Default = new Microsoft.OpenApi.Any.OpenApiString(defaultValue);
                        }
                    }

                    parameter.Required |= description.IsRequired;
                }
            }
        }
    }
}
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Core.Services;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Repositories;
using MobileGwDataSync.Data.Services;
using MobileGwDataSync.Data.SqlServer;
using MobileGwDataSync.Integration.OneC;
using MobileGwDataSync.Monitoring.Metrics;
using Prometheus;
using Serilog;
using Serilog.Events;
using System.IO.Compression;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace MobileGwDataSync.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure Serilog early
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(
                    new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                        .AddEnvironmentVariables()
                        .Build())
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Starting MobileGW Data Sync API");

                var builder = WebApplication.CreateBuilder(args);

                // Configure Serilog for the application
                builder.Host.UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithEnvironmentName()
                    .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName));

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
                .AddMvc()
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

                // MetricsService registration
                if (builder.Configuration.GetValue<bool>("Monitoring:Prometheus:Enabled", false))
                    builder.Services.AddSingleton<IMetricsService, MetricsService>();
                else
                    builder.Services.AddSingleton<IMetricsService, NullMetricsService>();

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
                        policy.WithOrigins("http://localhost:3000", "https://yourdomain.com")
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    });
                });

                var app = builder.Build();

                // Apply migrations on startup
                await ApplyMigrationsAsync(app);

                // Configure middleware pipeline
                ConfigureMiddleware(app);

                // Configure endpoints
                ConfigureEndpoints(app);

                // Log startup information
                Log.Information("MobileGW Data Sync API started successfully");
                Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
                Log.Information("URLs: {Urls}", string.Join(", ", app.Urls));

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "API terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.Information("Shutting down API");
                Log.CloseAndFlush();
            }
        }

        private static async Task ApplyMigrationsAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();

            var serviceDbContext = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();
            try
            {
                await serviceDbContext.Database.EnsureCreatedAsync();
                //await serviceDbContext.Database.MigrateAsync();
                Log.Information("SQLite database migrations applied successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error applying SQLite migrations");
            }

            var businessDbContext = scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
            try
            {
                if (await businessDbContext.Database.CanConnectAsync())
                {
                    Log.Information("SQL Server connection successful");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SQL Server connection failed");
            }
        }

        private static void ConfigureMiddleware(WebApplication app)
        {
            // Response compression and caching
            app.UseResponseCompression();
            app.UseResponseCaching();
            app.UseRateLimiter();

            // Serilog request logging
            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

                options.GetLevel = (httpContext, elapsed, ex) =>
                {
                    if (httpContext.Request.Path.StartsWithSegments("/health") ||
                        httpContext.Request.Path.StartsWithSegments("/metrics"))
                        return LogEventLevel.Verbose;
                    if (ex != null)
                        return LogEventLevel.Error;
                    if (httpContext.Response.StatusCode > 499)
                        return LogEventLevel.Error;
                    if (httpContext.Response.StatusCode > 399)
                        return LogEventLevel.Warning;
                    return LogEventLevel.Information;
                };

                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].FirstOrDefault());
                    diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString());
                    diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
                };
            });

            // Configure Swagger
            ConfigureSwaggerUI(app);

            // Security headers
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Xss-Protection"] = "1; mode=block";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                await next();
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
                        Log.Error(exceptionHandlerFeature.Error, "Unhandled exception occurred");

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
        }

        private static void ConfigureEndpoints(WebApplication app)
        {
            // Prometheus metrics
            app.UseMetricServer();

            // API Controllers
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
        }

        private static void ConfigureSwagger(WebApplicationBuilder builder)
        {
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen(options =>
            {
                var provider = builder.Services.BuildServiceProvider()
                    .GetService<IApiVersionDescriptionProvider>();

                if (provider != null)
                {
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerDoc(description.GroupName, CreateApiInfo(description));
                    }
                }
                else
                {
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

                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }

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

                options.OperationFilter<SwaggerDefaultValues>();
            });
        }

        private static void ConfigureSwaggerUI(WebApplication app)
        {
            app.UseSwagger();

            app.UseSwaggerUI(options =>
            {
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

    public class SwaggerDefaultValues : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
    {
        public void Apply(OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
        {
            var apiDescription = context.ApiDescription;

            operation.Deprecated |= apiDescription.IsDeprecated();

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
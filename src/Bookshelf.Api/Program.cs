using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Bookshelf.Application;
using Bookshelf.Application.Services;
using Bookshelf.Api.Api;
using Bookshelf.Api.Api.Auth;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Api.Api.HealthChecks;
using Bookshelf.Api.Api.Middleware;
using Bookshelf.Infrastructure;
using Bookshelf.Infrastructure.Integrations.FantLab;
using Bookshelf.Infrastructure.Integrations.Jackett;
using Bookshelf.Infrastructure.Integrations.QBittorrent;
using Bookshelf.Shared.Contracts.Api;
using Bookshelf.Shared.Contracts.System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApi();
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API is running."), tags: ["live"])
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);
builder.Services.AddBookshelfApplication();
builder.Services.AddBookshelfInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DownloadJobSyncWorker>();
builder.Services
    .AddAuthentication("Bearer")
    .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>("Bearer", _ => { });
builder.Services.AddAuthorization();
builder.Services.Configure<PayloadSizeGuardOptions>(options =>
{
    var maxBodyBytes = builder.Configuration.GetValue<long?>("API_MAX_BODY_BYTES")
        ?? builder.Configuration.GetValue<long?>("RequestLimits:MaxBodyBytes")
        ?? 262_144;
    options.MaxBodyBytes = Math.Max(1_024, maxBodyBytes);
});
ConfigureRateLimiting(builder.Services, builder.Configuration);
ConfigureOpenTelemetry(builder.Services, builder.Configuration, builder.Environment);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<PayloadSizeGuardMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

var liveChecks = new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
};

var readyChecks = new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
};

app.MapHealthChecks("/health", liveChecks);
app.MapHealthChecks("/health/live", liveChecks);
app.MapHealthChecks("/health/ready", readyChecks);
app.MapV1Endpoints();

app.MapGet("/api/v1/system/ping", () =>
{
    return Results.Ok(new PingResponse("bookshelf-api", DateTimeOffset.UtcNow));
});

app.Run();

static void ConfigureRateLimiting(IServiceCollection services, IConfiguration configuration)
{
    var permitLimit = configuration.GetValue<int?>("API_RATE_LIMIT_PERMIT_LIMIT")
        ?? configuration.GetValue<int?>("RateLimiting:PermitLimit")
        ?? 120;
    var windowSeconds = configuration.GetValue<int?>("API_RATE_LIMIT_WINDOW_SECONDS")
        ?? configuration.GetValue<int?>("RateLimiting:WindowSeconds")
        ?? 60;
    var queueLimit = configuration.GetValue<int?>("API_RATE_LIMIT_QUEUE_LIMIT")
        ?? configuration.GetValue<int?>("RateLimiting:QueueLimit")
        ?? 0;

    services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/api/v1/system/ping", StringComparison.OrdinalIgnoreCase))
            {
                return RateLimitPartition.GetNoLimiter("health");
            }

            return CreatePartition(context);
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            var httpContext = context.HttpContext;
            httpContext.Response.ContentType = "application/json; charset=utf-8";

            var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationValue)
                ? correlationValue?.ToString() ?? httpContext.TraceIdentifier
                : httpContext.TraceIdentifier;

            var payload = new ErrorResponse(
                Code: ApiErrorCodes.RateLimitExceeded,
                Message: "Request rate limit exceeded.",
                Details: null,
                CorrelationId: correlationId);

            await JsonSerializer.SerializeAsync(httpContext.Response.Body, payload, cancellationToken: cancellationToken);
        };

        options.AddPolicy("api-v1", context =>
        {
            return CreatePartition(context);
        });

        RateLimitPartition<string> CreatePartition(HttpContext context)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var partitionKey = !string.IsNullOrWhiteSpace(userId) ? $"user:{userId}" : "anonymous";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, permitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = Math.Max(0, queueLimit),
                });
        }
    });
}

static void ConfigureOpenTelemetry(
    IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    var serviceName = configuration["OTEL_SERVICE_NAME"] ?? "bookshelf-api";
    var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
    var hasOtlpEndpoint = Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpEndpointUri);

    services
        .AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
            .AddAttributes(
            [
                new KeyValuePair<string, object>("deployment.environment", environment.EnvironmentName),
            ]))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddHttpClientInstrumentation();

            if (hasOtlpEndpoint)
            {
                tracing.AddOtlpExporter(options => options.Endpoint = otlpEndpointUri!);
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(
                    RequestLoggingMiddleware.MeterName,
                    DownloadJobService.MeterName,
                    FantLabMetadataProvider.MeterName,
                    JackettCandidateProvider.MeterName,
                    QBittorrentDownloadClient.MeterName);

            if (hasOtlpEndpoint)
            {
                metrics.AddOtlpExporter(options => options.Endpoint = otlpEndpointUri!);
            }
        });
}

public partial class Program;

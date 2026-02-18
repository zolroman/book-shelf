using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Bookshelf.Api.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
    public const string MeterName = "Bookshelf.Api.Http";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>("api_requests_total");
    private static readonly Counter<long> ErrorCounter = Meter.CreateCounter<long>("api_request_errors_total");
    private static readonly Histogram<double> RequestDurationHistogram = Meter.CreateHistogram<double>("api_request_duration_ms");

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var method = context.Request.Method;
        var route = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
            ? value?.ToString() ?? context.TraceIdentifier
            : context.TraceIdentifier;

        _logger.LogInformation(
            "HTTP request started. Method={Method} Route={Route} CorrelationId={CorrelationId}",
            method,
            route,
            correlationId);

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        finally
        {
            var statusCode = context.Response.StatusCode;
            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            var tags = new TagList
            {
                { "http.method", method },
                { "http.route", route },
                { "http.status_code", statusCode },
            };

            RequestCounter.Add(1, tags);
            RequestDurationHistogram.Record(elapsedMs, tags);

            if (statusCode >= StatusCodes.Status400BadRequest)
            {
                ErrorCounter.Add(1, tags);
            }

            _logger.LogInformation(
                "HTTP request completed. Method={Method} Route={Route} StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                method,
                route,
                statusCode,
                elapsedMs,
                correlationId);
        }
    }
}

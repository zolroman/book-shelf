using Bookshelf.Shared.Diagnostics;

namespace Bookshelf.Api.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incomingValue)
            && !string.IsNullOrWhiteSpace(incomingValue.ToString())
            ? incomingValue.ToString()
            : Guid.NewGuid().ToString("D");

        var previous = CorrelationContext.Current;
        CorrelationContext.Current = correlationId;

        context.TraceIdentifier = correlationId;
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        try
        {
            await next(context);
        }
        finally
        {
            CorrelationContext.Current = previous;
        }
    }
}

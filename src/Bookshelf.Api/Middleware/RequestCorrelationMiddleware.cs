using Microsoft.Extensions.Primitives;

namespace Bookshelf.Api.Middleware;

public sealed class RequestCorrelationMiddleware(
    RequestDelegate next,
    ILogger<RequestCorrelationMiddleware> logger)
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public async Task Invoke(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationHeader] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId
               }))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(CorrelationHeader, out StringValues requestCorrelationId)
            && !StringValues.IsNullOrEmpty(requestCorrelationId))
        {
            var value = requestCorrelationId.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return Guid.NewGuid().ToString("n");
    }
}

using System.Net;
using System.Text.Json;

namespace Bookshelf.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ApiExceptionMiddleware> _logger = logger;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "Request validation failed.");
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Invalid operation.");
            await WriteErrorAsync(context, HttpStatusCode.Conflict, exception.Message);
        }
    }

    private static Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}

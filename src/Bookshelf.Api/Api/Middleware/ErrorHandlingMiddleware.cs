using System.Net;
using System.Text.Json;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Api.Api.Middleware;

public sealed class ErrorHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BadHttpRequestException exception)
        {
            await WriteErrorResponseAsync(
                context,
                HttpStatusCode.BadRequest,
                ApiErrorCodes.InvalidArgument,
                exception.Message);
        }
        catch (ApiException exception)
        {
            await WriteErrorResponseAsync(context, exception.StatusCode, exception.Code, exception.Message, exception.Details);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception.");
            await WriteErrorResponseAsync(
                context,
                HttpStatusCode.InternalServerError,
                ApiErrorCodes.InternalError,
                "Unhandled server error.");
        }
    }

    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string code,
        string message,
        object? details = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationValue)
            ? correlationValue?.ToString() ?? context.TraceIdentifier
            : context.TraceIdentifier;

        var payload = new ErrorResponse(
            Code: code,
            Message: message,
            Details: details,
            CorrelationId: correlationId);

        await JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions);
    }
}

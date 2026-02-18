using System.Net;
using Bookshelf.Api.Api.Errors;
using Microsoft.Extensions.Options;

namespace Bookshelf.Api.Api.Middleware;

public sealed class PayloadSizeGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly long _maxBodyBytes;

    public PayloadSizeGuardMiddleware(
        RequestDelegate next,
        IOptions<PayloadSizeGuardOptions> options)
    {
        _next = next;
        _maxBodyBytes = Math.Max(1_024, options.Value.MaxBodyBytes);
    }

    public async Task Invoke(HttpContext context)
    {
        if (IsBodyMethod(context.Request.Method)
            && context.Request.ContentLength.HasValue
            && context.Request.ContentLength.Value > _maxBodyBytes)
        {
            throw new ApiException(
                ApiErrorCodes.PayloadTooLarge,
                $"Request body exceeded maximum allowed size of {_maxBodyBytes} bytes.",
                HttpStatusCode.RequestEntityTooLarge);
        }

        if (IsBodyMethod(context.Request.Method) && !context.Request.ContentLength.HasValue)
        {
            context.Request.EnableBuffering();

            const int chunkSize = 8_192;
            var buffer = new byte[chunkSize];
            long totalRead = 0;

            while (true)
            {
                var read = await context.Request.Body.ReadAsync(
                    buffer.AsMemory(0, chunkSize),
                    context.RequestAborted);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
                if (totalRead > _maxBodyBytes)
                {
                    context.Request.Body.Position = 0;
                    throw new ApiException(
                        ApiErrorCodes.PayloadTooLarge,
                        $"Request body exceeded maximum allowed size of {_maxBodyBytes} bytes.",
                        HttpStatusCode.RequestEntityTooLarge);
                }
            }

            context.Request.Body.Position = 0;
        }

        await _next(context);
    }

    private static bool IsBodyMethod(string method)
    {
        return HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);
    }
}

public sealed class PayloadSizeGuardOptions
{
    public long MaxBodyBytes { get; set; } = 262_144;
}

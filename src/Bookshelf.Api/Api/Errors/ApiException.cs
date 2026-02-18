using System.Net;

namespace Bookshelf.Api.Api.Errors;

public sealed class ApiException : Exception
{
    public ApiException(
        string code,
        string message,
        HttpStatusCode statusCode,
        object? details = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }

    public string Code { get; }

    public HttpStatusCode StatusCode { get; }

    public object? Details { get; }
}

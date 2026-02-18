namespace Bookshelf.Shared.Client;

public sealed class ApiClientException : Exception
{
    public ApiClientException(
        int statusCode,
        string code,
        string message,
        object? details = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }

    public int StatusCode { get; }

    public string Code { get; }

    public object? Details { get; }
}

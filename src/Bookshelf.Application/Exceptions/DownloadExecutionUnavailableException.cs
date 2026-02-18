namespace Bookshelf.Application.Exceptions;

public sealed class DownloadExecutionUnavailableException : Exception
{
    public DownloadExecutionUnavailableException(string providerCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderCode = providerCode;
    }

    public string ProviderCode { get; }
}

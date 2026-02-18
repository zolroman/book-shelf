namespace Bookshelf.Application.Exceptions;

public sealed class DownloadExecutionFailedException : Exception
{
    public DownloadExecutionFailedException(string providerCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderCode = providerCode;
    }

    public string ProviderCode { get; }
}

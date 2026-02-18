namespace Bookshelf.Application.Exceptions;

public sealed class DownloadCandidateProviderUnavailableException : Exception
{
    public DownloadCandidateProviderUnavailableException(string providerCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderCode = providerCode;
    }

    public string ProviderCode { get; }
}

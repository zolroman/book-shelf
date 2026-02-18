namespace Bookshelf.Application.Exceptions;

public sealed class MetadataProviderUnavailableException : Exception
{
    public MetadataProviderUnavailableException(string providerCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderCode = providerCode;
    }

    public string ProviderCode { get; }
}

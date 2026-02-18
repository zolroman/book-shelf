namespace Bookshelf.Application.Exceptions;

public sealed class BookNotFoundException : Exception
{
    public BookNotFoundException(string providerCode, string providerBookKey)
        : base($"Book '{providerCode}:{providerBookKey}' was not found.")
    {
        ProviderCode = providerCode;
        ProviderBookKey = providerBookKey;
    }

    public string ProviderCode { get; }

    public string ProviderBookKey { get; }
}

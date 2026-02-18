namespace Bookshelf.Application.Abstractions.Providers;

public interface IMetadataProvider
{
    string ProviderCode { get; }

    Task<MetadataSearchResult> SearchAsync(
        MetadataSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<MetadataBookDetails?> GetDetailsAsync(
        string providerBookKey,
        CancellationToken cancellationToken = default);
}

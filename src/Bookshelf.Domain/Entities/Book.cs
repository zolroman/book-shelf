using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Entities;

public sealed class Book
{
    private Book()
    {
    }

    public Book(string providerCode, string providerBookKey, string title)
    {
        ProviderCode = NormalizeRequired(providerCode);
        ProviderBookKey = NormalizeRequired(providerBookKey);
        Title = NormalizeRequired(title);
        CatalogState = CatalogState.Archive;
        var nowUtc = DateTimeOffset.UtcNow;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public long Id { get; private set; }

    public string ProviderCode { get; private set; } = string.Empty;

    public string ProviderBookKey { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string? OriginalTitle { get; private set; }

    public string? Description { get; private set; }

    public int? PublishYear { get; private set; }

    public string? LanguageCode { get; private set; }

    public string? CoverUrl { get; private set; }

    public CatalogState CatalogState { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public ICollection<BookAuthor> BookAuthors { get; } = new List<BookAuthor>();

    public ICollection<SeriesBook> SeriesBooks { get; } = new List<SeriesBook>();

    public ICollection<BookMediaAsset> MediaAssets { get; } = new List<BookMediaAsset>();

    public void UpdateMetadata(
        string title,
        string? originalTitle,
        string? description,
        int? publishYear,
        string? languageCode,
        string? coverUrl)
    {
        Title = NormalizeRequired(title);
        OriginalTitle = NormalizeOptional(originalTitle);
        Description = NormalizeOptional(description);
        PublishYear = publishYear;
        LanguageCode = NormalizeOptional(languageCode);
        CoverUrl = NormalizeOptional(coverUrl);
        Touch();
    }

    public BookMediaAsset UpsertMediaAsset(MediaType mediaType, string? sourceUrl, string sourceProvider)
    {
        var existing = MediaAssets.SingleOrDefault(x => x.MediaType == mediaType);
        if (existing is null)
        {
            var created = new BookMediaAsset(Id, mediaType, sourceUrl, sourceProvider);
            MediaAssets.Add(created);
            RecomputeCatalogState();
            Touch();
            return created;
        }

        existing.UpdateSource(sourceUrl, sourceProvider);
        RecomputeCatalogState();
        Touch();
        return existing;
    }

    public void RecomputeCatalogState()
    {
        CatalogState = MediaAssets.Any(x => x.Status == MediaAssetStatus.Available)
            ? CatalogState.Library
            : CatalogState.Archive;
    }

    public void Touch(DateTimeOffset? nowUtc = null)
    {
        UpdatedAtUtc = nowUtc ?? DateTimeOffset.UtcNow;
    }

    private static string NormalizeRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", nameof(value));
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

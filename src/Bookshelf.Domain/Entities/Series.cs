namespace Bookshelf.Domain.Entities;

public sealed class Series
{
    private Series()
    {
    }

    public Series(string providerCode, string providerSeriesKey, string title)
    {
        ProviderCode = NormalizeRequired(providerCode);
        ProviderSeriesKey = NormalizeRequired(providerSeriesKey);
        Title = NormalizeRequired(title);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public long Id { get; private set; }

    public string ProviderCode { get; private set; } = string.Empty;

    public string ProviderSeriesKey { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public ICollection<SeriesBook> SeriesBooks { get; } = new List<SeriesBook>();

    private static string NormalizeRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", nameof(value));
        }

        return value.Trim();
    }
}

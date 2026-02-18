using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Entities;

public sealed class BookMediaAsset
{
    private BookMediaAsset()
    {
    }

    public BookMediaAsset(long bookId, MediaType mediaType, string? sourceUrl, string sourceProvider)
    {
        if (string.IsNullOrWhiteSpace(sourceProvider))
        {
            throw new ArgumentException("Source provider is required.", nameof(sourceProvider));
        }

        BookId = bookId;
        MediaType = mediaType;
        SourceUrl = NormalizeOptional(sourceUrl);
        SourceProvider = sourceProvider.Trim();
        Status = MediaAssetStatus.Available;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public long Id { get; private set; }

    public long BookId { get; private set; }

    public Book? Book { get; private set; }

    public MediaType MediaType { get; private set; }

    public string? SourceUrl { get; private set; }

    public string SourceProvider { get; private set; } = "jackett";

    public string? StoragePath { get; private set; }

    public long? FileSizeBytes { get; private set; }

    public string? Checksum { get; private set; }

    public MediaAssetStatus Status { get; private set; }

    public DateTimeOffset? DownloadedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void UpdateSource(string? sourceUrl, string sourceProvider)
    {
        if (string.IsNullOrWhiteSpace(sourceProvider))
        {
            throw new ArgumentException("Source provider is required.", nameof(sourceProvider));
        }

        SourceUrl = NormalizeOptional(sourceUrl) ?? SourceUrl;
        SourceProvider = sourceProvider.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkAvailable(string storagePath, long? fileSizeBytes, string? checksum, DateTimeOffset completedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException("Storage path is required.", nameof(storagePath));
        }

        StoragePath = storagePath.Trim();
        FileSizeBytes = fileSizeBytes;
        Checksum = NormalizeOptional(checksum);
        Status = MediaAssetStatus.Available;
        DownloadedAtUtc = completedAtUtc;
        DeletedAtUtc = null;
        UpdatedAtUtc = completedAtUtc;
    }

    public void MarkDeleted(MediaAssetStatus deletedStatus, DateTimeOffset deletedAtUtc)
    {
        if (deletedStatus is not MediaAssetStatus.Deleted and not MediaAssetStatus.Missing)
        {
            throw new ArgumentException("Deleted status must be deleted or missing.", nameof(deletedStatus));
        }

        Status = deletedStatus;
        DeletedAtUtc = deletedAtUtc;
        UpdatedAtUtc = deletedAtUtc;
        StoragePath = null;
        FileSizeBytes = null;
        Checksum = null;
        DownloadedAtUtc = null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

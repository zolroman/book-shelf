namespace Bookshelf.Domain.Entities;

public class LocalAsset
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int BookFormatId { get; set; }

    public string LocalPath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime DownloadedAtUtc { get; set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;

    public void Restore()
    {
        DeletedAtUtc = null;
    }

    public void MarkDeleted(DateTime utcNow)
    {
        DeletedAtUtc = utcNow;
    }
}

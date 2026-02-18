namespace Bookshelf.Domain.Entities;

public sealed class User
{
    private User()
    {
    }

    public User(string login, string? displayName = null, string? externalSubject = null)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            throw new ArgumentException("Login is required.", nameof(login));
        }

        Login = login.Trim();
        DisplayName = NormalizeOptional(displayName);
        ExternalSubject = NormalizeOptional(externalSubject);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public long Id { get; private set; }

    public string? ExternalSubject { get; private set; }

    public string Login { get; private set; } = string.Empty;

    public string? DisplayName { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public ICollection<Shelf> Shelves { get; } = new List<Shelf>();

    public ICollection<ProgressSnapshot> ProgressSnapshots { get; } = new List<ProgressSnapshot>();

    public ICollection<HistoryEvent> HistoryEvents { get; } = new List<HistoryEvent>();

    public ICollection<DownloadJob> DownloadJobs { get; } = new List<DownloadJob>();

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

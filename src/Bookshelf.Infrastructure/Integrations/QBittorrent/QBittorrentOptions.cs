namespace Bookshelf.Infrastructure.Integrations.QBittorrent;

public sealed class QBittorrentOptions
{
    public string BaseUrl { get; set; } = "http://192.168.40.25:8070";

    public string AuthMode { get; set; } = "none";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public int TimeoutSeconds { get; set; } = 15;

    public int MaxRetries { get; set; } = 2;

    public int RetryDelayMs { get; set; } = 300;

    public int NotFoundGraceSeconds { get; set; } = 60;
}

namespace Bookshelf.Infrastructure.Options;

public sealed class QbittorrentOptions
{
    public bool Enabled { get; set; }

    public bool UseMockFallback { get; set; } = true;

    public string BaseUrl { get; set; } = "http://localhost:8080";

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 8;

    public int MockAutoCompleteSeconds { get; set; } = 4;
}

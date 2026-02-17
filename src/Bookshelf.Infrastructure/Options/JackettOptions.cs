namespace Bookshelf.Infrastructure.Options;

public sealed class JackettOptions
{
    public bool Enabled { get; set; }

    public bool UseMockFallback { get; set; } = true;

    public string BaseUrl { get; set; } = "http://localhost:9117";

    public string ApiKey { get; set; } = string.Empty;

    public string Indexer { get; set; } = "all";

    public int TimeoutSeconds { get; set; } = 8;

    public int MaxItems { get; set; } = 20;

    public int MaxRetries { get; set; } = 2;

    public int RetryDelayMilliseconds { get; set; } = 300;
}

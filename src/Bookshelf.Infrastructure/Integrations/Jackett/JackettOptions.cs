namespace Bookshelf.Infrastructure.Integrations.Jackett;

public sealed class JackettOptions
{
    public string BaseUrl { get; set; } = "http://192.168.40.25:9117";

    public string ApiKey { get; set; } = string.Empty;

    public string Indexer { get; set; } = "all";

    public int TimeoutSeconds { get; set; } = 15;

    public int MaxRetries { get; set; } = 2;

    public int RetryDelayMs { get; set; } = 300;

    public int MaxItems { get; set; } = 50;
}

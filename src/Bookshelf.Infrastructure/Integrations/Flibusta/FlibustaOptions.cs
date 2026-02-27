namespace Bookshelf.Infrastructure.Integrations.Flibusta;

public sealed class FlibustaOptions
{
    public string BaseUrl { get; set; } = "http://flibusta.site";

    public int TimeoutSeconds { get; set; } = 15;

    public int MaxRetries { get; set; } = 2;

    public int RetryDelayMs { get; set; } = 300;

    public int MaxItems { get; set; } = 20;

    public int MaxPages { get; set; } = 5;
}

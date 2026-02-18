namespace Bookshelf.Infrastructure.Integrations.FantLab;

public sealed class FantLabOptions
{
    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://api.fantlab.ru";

    public string SearchPath { get; set; } = "/search";

    public string BookDetailsPath { get; set; } = "/work/{bookKey}";

    public int TimeoutSeconds { get; set; } = 10;

    public int MaxRetries { get; set; } = 2;

    public int RetryDelayMs { get; set; } = 300;

    public bool CacheEnabled { get; set; } = true;

    public int SearchCacheMinutes { get; set; } = 10;

    public int DetailsCacheHours { get; set; } = 24;

    public int CircuitBreakerFailureThreshold { get; set; } = 3;

    public int CircuitBreakerOpenSeconds { get; set; } = 60;
}

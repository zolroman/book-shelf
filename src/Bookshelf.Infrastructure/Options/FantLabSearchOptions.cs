namespace Bookshelf.Infrastructure.Options;

public sealed class FantLabSearchOptions
{
    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://api.fantlab.ru";

    public string SearchPath { get; set; } = "/search";

    public string QueryParameter { get; set; } = "q";

    public int TimeoutSeconds { get; set; } = 8;

    public int MaxRetries { get; set; } = 2;

    public int RetryDelayMilliseconds { get; set; } = 300;

    public int CacheTtlMinutes { get; set; } = 5;

    public int CircuitBreakerFailureThreshold { get; set; } = 3;

    public int CircuitBreakerOpenSeconds { get; set; } = 60;
}

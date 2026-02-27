namespace Bookshelf.Api.Api;

public sealed class DirectDownloadOptions
{
    public string StorageRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "downloads");

    public int PollIntervalSeconds { get; set; } = 15;

    public int MaxConcurrentJobs { get; set; } = 1;
}

using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Infrastructure.Integrations.FantLab;
using Bookshelf.Infrastructure.Integrations.Jackett;
using Bookshelf.Infrastructure.Persistence;
using Bookshelf.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBookshelfInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Bookshelf")
            ?? configuration["ConnectionStrings:Bookshelf"]
            ?? "Host=localhost;Port=5432;Database=bookshelf;Username=bookshelf;Password=bookshelf";

        services.AddDbContext<BookshelfDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddMemoryCache();
        services.Configure<FantLabOptions>(options =>
        {
            options.Enabled = GetBool(configuration, "FANTLAB_ENABLED", "FantLab:Enabled", true);
            options.BaseUrl = GetString(configuration, "FANTLAB_BASE_URL", "FantLab:BaseUrl", "https://api.fantlab.ru");
            options.SearchPath = GetString(configuration, "FANTLAB_SEARCH_PATH", "FantLab:SearchPath", "/search");
            options.BookDetailsPath = GetString(configuration, "FANTLAB_BOOK_DETAILS_PATH", "FantLab:BookDetailsPath", "/work/{bookKey}");
            options.TimeoutSeconds = GetInt(configuration, "FANTLAB_TIMEOUT_SECONDS", "FantLab:TimeoutSeconds", 10);
            options.MaxRetries = GetInt(configuration, "FANTLAB_MAX_RETRIES", "FantLab:MaxRetries", 2);
            options.RetryDelayMs = GetInt(configuration, "FANTLAB_RETRY_DELAY_MS", "FantLab:RetryDelayMs", 300);
            options.CacheEnabled = GetBool(configuration, "FANTLAB_CACHE_ENABLED", "FantLab:CacheEnabled", true);
            options.SearchCacheMinutes = GetInt(configuration, "FANTLAB_SEARCH_CACHE_MINUTES", "FantLab:SearchCacheMinutes", 10);
            options.DetailsCacheHours = GetInt(configuration, "FANTLAB_DETAILS_CACHE_HOURS", "FantLab:DetailsCacheHours", 24);
            options.CircuitBreakerFailureThreshold = GetInt(configuration, "FANTLAB_CIRCUIT_BREAKER_FAILURES", "FantLab:CircuitBreakerFailureThreshold", 3);
            options.CircuitBreakerOpenSeconds = GetInt(configuration, "FANTLAB_CIRCUIT_BREAKER_OPEN_SECONDS", "FantLab:CircuitBreakerOpenSeconds", 60);
        });
        services.Configure<JackettOptions>(options =>
        {
            options.Enabled = GetBool(configuration, "JACKETT_ENABLED", "Jackett:Enabled", true);
            options.BaseUrl = GetString(configuration, "JACKETT_BASE_URL", "Jackett:BaseUrl", "http://192.168.40.25:9117");
            options.ApiKey = GetString(configuration, "JACKETT_API_KEY", "Jackett:ApiKey", string.Empty);
            options.Indexer = GetString(configuration, "JACKETT_INDEXER", "Jackett:Indexer", "all");
            options.TimeoutSeconds = GetInt(configuration, "JACKETT_TIMEOUT_SECONDS", "Jackett:TimeoutSeconds", 15);
            options.MaxRetries = GetInt(configuration, "JACKETT_MAX_RETRIES", "Jackett:MaxRetries", 2);
            options.RetryDelayMs = GetInt(configuration, "JACKETT_RETRY_DELAY_MS", "Jackett:RetryDelayMs", 300);
            options.MaxItems = GetInt(configuration, "JACKETT_MAX_ITEMS", "Jackett:MaxItems", 50);
        });

        services.AddHttpClient<FantLabMetadataProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<FantLabOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });
        services.AddHttpClient<JackettCandidateProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<JackettOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });
        services.AddScoped<IMetadataProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<FantLabMetadataProvider>());
        services.AddScoped<IDownloadCandidateProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<JackettCandidateProvider>());

        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IShelfRepository, ShelfRepository>();
        services.AddScoped<IDownloadJobRepository, DownloadJobRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }

    private static string GetString(IConfiguration configuration, string envKey, string sectionKey, string defaultValue)
    {
        return configuration[envKey]
            ?? configuration[sectionKey]
            ?? defaultValue;
    }

    private static int GetInt(IConfiguration configuration, string envKey, string sectionKey, int defaultValue)
    {
        var raw = configuration[envKey] ?? configuration[sectionKey];
        return int.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    private static bool GetBool(IConfiguration configuration, string envKey, string sectionKey, bool defaultValue)
    {
        var raw = configuration[envKey] ?? configuration[sectionKey];
        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }
}

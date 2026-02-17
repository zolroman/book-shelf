using Bookshelf.Domain.Abstractions;
using Bookshelf.Infrastructure.Options;
using Bookshelf.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBookshelfInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IBookshelfRepository, InMemoryBookshelfRepository>();
        services.AddMemoryCache();
        services.AddHttpClient(nameof(FantLabBookSearchProvider));
        services.AddHttpClient(nameof(JackettTorrentSearchClient));
        services.AddHttpClient(nameof(QbittorrentDownloadClient));
        services.Configure<FantLabSearchOptions>(configuration.GetSection("Search:FantLab"));
        services.Configure<JackettOptions>(configuration.GetSection("Downloads:Jackett"));
        services.Configure<QbittorrentOptions>(configuration.GetSection("Downloads:Qbittorrent"));
        services.AddSingleton<IBookSearchProvider, FantLabBookSearchProvider>();
        services.AddSingleton<ITorrentSearchClient, JackettTorrentSearchClient>();
        services.AddSingleton<IQbittorrentDownloadClient, QbittorrentDownloadClient>();
        services.AddSingleton<IDownloadService, DownloadPipelineService>();
        return services;
    }
}

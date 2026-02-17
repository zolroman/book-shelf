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
        services.AddSingleton<IDownloadService, InMemoryDownloadService>();
        services.AddMemoryCache();
        services.AddHttpClient(nameof(FantLabBookSearchProvider));
        services.Configure<FantLabSearchOptions>(configuration.GetSection("Search:FantLab"));
        services.AddSingleton<IBookSearchProvider, FantLabBookSearchProvider>();
        return services;
    }
}

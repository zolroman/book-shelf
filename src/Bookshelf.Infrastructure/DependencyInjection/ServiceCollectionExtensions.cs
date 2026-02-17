using Bookshelf.Domain.Abstractions;
using Bookshelf.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBookshelfInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IBookshelfRepository, InMemoryBookshelfRepository>();
        services.AddSingleton<IBookSearchProvider, InMemoryBookSearchProvider>();
        services.AddSingleton<IDownloadService, InMemoryDownloadService>();
        return services;
    }
}

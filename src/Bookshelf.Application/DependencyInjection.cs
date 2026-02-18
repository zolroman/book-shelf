using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBookshelfApplication(this IServiceCollection services)
    {
        services.AddScoped<IBookSearchService, BookSearchService>();
        services.AddScoped<ICandidateDiscoveryService, CandidateDiscoveryService>();
        services.AddScoped<IAddAndDownloadService, AddAndDownloadService>();
        return services;
    }
}

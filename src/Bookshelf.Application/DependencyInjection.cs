using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBookshelfApplication(this IServiceCollection services)
    {
        return services;
    }
}

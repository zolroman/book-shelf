using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Infrastructure.Persistence;
using Bookshelf.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

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

        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IShelfRepository, ShelfRepository>();
        services.AddScoped<IDownloadJobRepository, DownloadJobRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }
}

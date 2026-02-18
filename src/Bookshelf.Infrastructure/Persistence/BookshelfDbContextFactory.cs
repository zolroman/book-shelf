using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bookshelf.Infrastructure.Persistence;

public sealed class BookshelfDbContextFactory : IDesignTimeDbContextFactory<BookshelfDbContext>
{
    public BookshelfDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BookshelfDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("BOOKSHELF_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=bookshelf;Username=bookshelf;Password=bookshelf";

        optionsBuilder.UseNpgsql(connectionString);

        return new BookshelfDbContext(optionsBuilder.Options);
    }
}

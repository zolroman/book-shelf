using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bookshelf.Infrastructure.Persistence;

public sealed class BookshelfDbContextFactory : IDesignTimeDbContextFactory<BookshelfDbContext>
{
    public BookshelfDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BookshelfDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("BOOKSHELF_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("BOOKSHELF_DB_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is not configured. Set BOOKSHELF_CONNECTION_STRING for design-time operations.");
        }

        optionsBuilder.UseNpgsql(connectionString);

        return new BookshelfDbContext(optionsBuilder.Options);
    }
}

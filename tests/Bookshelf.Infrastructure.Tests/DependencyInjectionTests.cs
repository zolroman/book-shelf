using Bookshelf.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddBookshelfInfrastructure_ReturnsServiceCollection()
    {
        IServiceCollection services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Bookshelf"] = "Host=localhost;Port=5432;Database=bookshelf_test;Username=bookshelf;Password=bookshelf",
            })
            .Build();

        var result = services.AddBookshelfInfrastructure(configuration);

        Assert.Same(services, result);
    }
}

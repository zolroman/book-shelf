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
        var configuration = new ConfigurationBuilder().Build();

        var result = services.AddBookshelfInfrastructure(configuration);

        Assert.Same(services, result);
    }
}

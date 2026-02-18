using Bookshelf.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddBookshelfApplication_ReturnsServiceCollection()
    {
        IServiceCollection services = new ServiceCollection();

        var result = services.AddBookshelfApplication();

        Assert.Same(services, result);
    }
}

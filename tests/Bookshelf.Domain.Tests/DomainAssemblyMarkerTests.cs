using Bookshelf.Domain;

namespace Bookshelf.Domain.Tests;

public class DomainAssemblyMarkerTests
{
    [Fact]
    public void MarkerType_IsResolvable()
    {
        Assert.NotNull(typeof(DomainAssemblyMarker));
    }
}

using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Tests;

public class BookInvariantTests
{
    [Fact]
    public void RecomputeCatalogState_NoMedia_IsArchive()
    {
        var book = new Book("fantlab", "123", "Test Book");

        book.RecomputeCatalogState();

        Assert.Equal(CatalogState.Archive, book.CatalogState);
    }

    [Fact]
    public void RecomputeCatalogState_AvailableMedia_IsLibrary()
    {
        var book = new Book("fantlab", "123", "Test Book");
        book.UpsertMediaAsset(MediaType.Audio, "https://example/source", "jackett");

        book.RecomputeCatalogState();

        Assert.Equal(CatalogState.Library, book.CatalogState);
    }

    [Fact]
    public void MediaDeletion_RetainsSourceUrl_AndMovesToArchiveWhenLastMediaRemoved()
    {
        var book = new Book("fantlab", "123", "Test Book");
        var asset = book.UpsertMediaAsset(MediaType.Audio, "https://example/source", "jackett");

        asset.MarkDeleted(MediaAssetStatus.Deleted, DateTimeOffset.UtcNow);
        book.RecomputeCatalogState();

        Assert.Equal("https://example/source", asset.SourceUrl);
        Assert.Equal(CatalogState.Archive, book.CatalogState);
    }
}

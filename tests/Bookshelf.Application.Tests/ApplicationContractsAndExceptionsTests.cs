using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Tests;

public class ApplicationContractsAndExceptionsTests
{
    [Fact]
    public void MetadataModels_PreserveConstructorValues()
    {
        var request = new MetadataSearchRequest("dune", "herbert", 2, 30);
        var series = new MetadataSeriesInfo("77", "Dune Saga", 1);
        var item = new MetadataSearchItem("123", "Dune", ["Frank Herbert"], series);
        var result = new MetadataSearchResult(1, [item]);
        var details = new MetadataBookDetails(
            ProviderBookKey: "123",
            Title: "Dune",
            OriginalTitle: "Dune",
            Description: "Sci-fi classic",
            PublishYear: 1965,
            CoverUrl: "https://images.example/dune.jpg",
            Authors: ["Frank Herbert"],
            Series: series);

        Assert.Equal("dune", request.Title);
        Assert.Equal("herbert", request.Author);
        Assert.Equal(2, request.Page);
        Assert.Equal(30, request.PageSize);
        Assert.Equal("77", item.Series!.ProviderSeriesKey);
        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("Dune", details.Title);
        Assert.Equal("Dune Saga", details.Series!.Title);
    }

    [Fact]
    public void ShelfAddBookResult_HoldsStatusAndShelf()
    {
        var shelf = new ShelfDto(
            Id: 1,
            UserId: 10,
            Name: "Sci-Fi",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            BookIds: [42]);
        var result = new ShelfAddBookResult(ShelfAddBookResultStatus.Success, shelf);

        Assert.Equal(ShelfAddBookResultStatus.Success, result.Status);
        Assert.NotNull(result.Shelf);
        Assert.Equal("Sci-Fi", result.Shelf!.Name);
    }

    [Fact]
    public void ExceptionTypes_ExposeContextProperties()
    {
        var bookNotFound = new BookNotFoundException("fantlab", "123");
        var bookIdNotFound = new BookIdNotFoundException(42);
        var candidateNotFound = new DownloadCandidateNotFoundException("jackett:abc");
        var providerUnavailable = new DownloadCandidateProviderUnavailableException("jackett", "down");
        var enqueueUnavailable = new DownloadExecutionUnavailableException("qbittorrent", "down");
        var enqueueFailed = new DownloadExecutionFailedException("qbittorrent", "enqueue failed");
        var jobNotFound = new DownloadJobNotFoundException(7);
        var cancelNotAllowed = new DownloadJobCancelNotAllowedException(9, "completed");
        var metadataUnavailable = new MetadataProviderUnavailableException("fantlab", "down");

        Assert.Equal("fantlab", bookNotFound.ProviderCode);
        Assert.Equal("123", bookNotFound.ProviderBookKey);
        Assert.Equal(42, bookIdNotFound.BookId);
        Assert.Equal("jackett:abc", candidateNotFound.CandidateId);
        Assert.Equal("jackett", providerUnavailable.ProviderCode);
        Assert.Equal("qbittorrent", enqueueUnavailable.ProviderCode);
        Assert.Equal("qbittorrent", enqueueFailed.ProviderCode);
        Assert.Equal(7, jobNotFound.JobId);
        Assert.Equal(9, cancelNotAllowed.JobId);
        Assert.Equal("completed", cancelNotAllowed.Status);
        Assert.Equal("fantlab", metadataUnavailable.ProviderCode);
        Assert.Contains("fantlab:123", bookNotFound.Message);
    }
}

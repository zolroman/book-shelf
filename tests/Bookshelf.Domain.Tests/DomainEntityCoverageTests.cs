using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Tests;

public class DomainEntityCoverageTests
{
    [Fact]
    public void Author_ValidatesAndNormalizesName()
    {
        Assert.Throws<ArgumentException>(() => new Author("  "));

        var author = new Author("  Frank Herbert  ");

        Assert.Equal("Frank Herbert", author.Name);
        Assert.Empty(author.BookAuthors);
    }

    [Fact]
    public void BookAuthor_AssignsIds()
    {
        var relation = new BookAuthor(10, 20);

        Assert.Equal(10, relation.BookId);
        Assert.Equal(20, relation.AuthorId);
    }

    [Fact]
    public void HistoryEvent_NormalizesPositionRef()
    {
        var eventWithPosition = new HistoryEvent(
            userId: 1,
            bookId: 2,
            mediaType: MediaType.Text,
            eventType: HistoryEventType.Progress,
            positionRef: "  chapter:1/page:5  ",
            eventAtUtc: DateTimeOffset.UtcNow);

        var eventWithoutPosition = new HistoryEvent(
            userId: 1,
            bookId: 2,
            mediaType: MediaType.Audio,
            eventType: HistoryEventType.Started,
            positionRef: "   ",
            eventAtUtc: DateTimeOffset.UtcNow);

        Assert.Equal("chapter:1/page:5", eventWithPosition.PositionRef);
        Assert.Null(eventWithoutPosition.PositionRef);
    }

    [Fact]
    public void ProgressSnapshot_ValidatesAndUpdates()
    {
        Assert.Throws<ArgumentException>(() => new ProgressSnapshot(1, 2, MediaType.Text, " ", 10m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressSnapshot(1, 2, MediaType.Text, "p1", 101m));

        var snapshot = new ProgressSnapshot(1, 2, MediaType.Text, " p1 ", 10m);
        var updatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        snapshot.Update(" p2 ", 55m, updatedAtUtc);

        Assert.Equal("p2", snapshot.PositionRef);
        Assert.Equal(55m, snapshot.ProgressPercent);
        Assert.Equal(updatedAtUtc, snapshot.UpdatedAtUtc);

        Assert.Throws<ArgumentException>(() => snapshot.Update(" ", 55m, updatedAtUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.Update("p3", -1m, updatedAtUtc));
    }

    [Fact]
    public void Series_ValidatesAndNormalizesFields()
    {
        Assert.Throws<ArgumentException>(() => new Series(" ", "s1", "Series"));
        Assert.Throws<ArgumentException>(() => new Series("fantlab", " ", "Series"));
        Assert.Throws<ArgumentException>(() => new Series("fantlab", "s1", " "));

        var series = new Series(" fantlab ", " s1 ", " Dune Saga ");

        Assert.Equal("fantlab", series.ProviderCode);
        Assert.Equal("s1", series.ProviderSeriesKey);
        Assert.Equal("Dune Saga", series.Title);
    }

    [Fact]
    public void SeriesBook_ValidatesSeriesOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeriesBook(1, 2, 0));

        var relation = new SeriesBook(1, 2, 3);

        Assert.Equal(1, relation.SeriesId);
        Assert.Equal(2, relation.BookId);
        Assert.Equal(3, relation.SeriesOrder);
    }

    [Fact]
    public void Shelf_AddBook_IsIdempotent()
    {
        Assert.Throws<ArgumentException>(() => new Shelf(1, " "));

        var shelf = new Shelf(10, "  Favorites ");
        shelf.AddBook(42);
        shelf.AddBook(42);

        Assert.Equal("Favorites", shelf.Name);
        Assert.True(shelf.ContainsBook(42));
        Assert.Single(shelf.ShelfBooks);
    }

    [Fact]
    public void ShelfBook_AssignsValues()
    {
        var relation = new ShelfBook(11, 22);

        Assert.Equal(11, relation.ShelfId);
        Assert.Equal(22, relation.BookId);
        Assert.NotEqual(default, relation.AddedAtUtc);
    }

    [Fact]
    public void User_ValidatesAndNormalizesFields()
    {
        Assert.Throws<ArgumentException>(() => new User(" "));

        var user = new User("  roman  ", "  Roman  ", "  ext-1  ");
        var userWithoutOptional = new User("reader", " ", " ");

        Assert.Equal("roman", user.Login);
        Assert.Equal("Roman", user.DisplayName);
        Assert.Equal("ext-1", user.ExternalSubject);
        Assert.Null(userWithoutOptional.DisplayName);
        Assert.Null(userWithoutOptional.ExternalSubject);
    }

    [Fact]
    public void Book_UpdateMetadata_NormalizesOptionalFields()
    {
        var book = new Book("fantlab", "123", " Dune ");
        book.UpdateMetadata(
            title: " Dune Messiah ",
            originalTitle: "  Dune Messiah Original ",
            description: "  Sequel ",
            publishYear: 1969,
            languageCode: " en ",
            coverUrl: " https://example/cover.jpg ");

        Assert.Equal("Dune Messiah", book.Title);
        Assert.Equal("Dune Messiah Original", book.OriginalTitle);
        Assert.Equal("Sequel", book.Description);
        Assert.Equal(1969, book.PublishYear);
        Assert.Equal("en", book.LanguageCode);
        Assert.Equal("https://example/cover.jpg", book.CoverUrl);
    }

    [Fact]
    public void BookMediaAsset_SourceAndStatusTransitions_AreApplied()
    {
        Assert.Throws<ArgumentException>(() => new BookMediaAsset(1, MediaType.Text, "https://source", " "));

        var asset = new BookMediaAsset(1, MediaType.Text, " https://source/item ", " jackett ");
        Assert.Equal(MediaAssetStatus.Available, asset.Status);
        Assert.Equal("https://source/item", asset.SourceUrl);
        Assert.Equal("jackett", asset.SourceProvider);

        asset.UpdateSource(" ", " tracker ");
        Assert.Equal("https://source/item", asset.SourceUrl);
        Assert.Equal("tracker", asset.SourceProvider);

        var completedAtUtc = DateTimeOffset.UtcNow;
        asset.MarkAvailable(" D:\\media\\book.epub ", 1024, " abc ", completedAtUtc);
        Assert.Equal("D:\\media\\book.epub", asset.StoragePath);
        Assert.Equal(1024, asset.FileSizeBytes);
        Assert.Equal("abc", asset.Checksum);
        Assert.Equal(MediaAssetStatus.Available, asset.Status);
        Assert.Equal(completedAtUtc, asset.DownloadedAtUtc);

        var deletedAtUtc = completedAtUtc.AddMinutes(5);
        asset.MarkDeleted(MediaAssetStatus.Missing, deletedAtUtc);
        Assert.Equal(MediaAssetStatus.Missing, asset.Status);
        Assert.Null(asset.StoragePath);
        Assert.Null(asset.FileSizeBytes);
        Assert.Null(asset.Checksum);
        Assert.Null(asset.DownloadedAtUtc);
        Assert.Equal(deletedAtUtc, asset.DeletedAtUtc);

        Assert.Throws<ArgumentException>(() => asset.MarkDeleted(MediaAssetStatus.Available, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void DownloadJob_StateHelpersAndTransitions_WorkAsExpected()
    {
        Assert.Throws<ArgumentException>(() => new DownloadJob(1, 2, MediaType.Audio, " ", "magnet:?xt=urn:btih:1"));

        var job = new DownloadJob(1, 2, MediaType.Audio, " https://source ", " magnet:?xt=urn:btih:1 ");
        Assert.Equal("https://source", job.Source);
        Assert.Equal("magnet:?xt=urn:btih:1", job.TorrentMagnet);

        var externalAtUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        job.SetExternalJobId("  hash-1  ", externalAtUtc);
        Assert.Equal("hash-1", job.ExternalJobId);
        Assert.Equal(externalAtUtc, job.UpdatedAtUtc);

        var firstNotFoundAtUtc = externalAtUtc.AddSeconds(10);
        job.SetNotFoundObserved(firstNotFoundAtUtc);
        job.SetNotFoundObserved(firstNotFoundAtUtc.AddSeconds(5));
        Assert.Equal(firstNotFoundAtUtc, job.FirstNotFoundAtUtc);

        var clearedAtUtc = firstNotFoundAtUtc.AddSeconds(30);
        job.ClearNotFoundObserved(clearedAtUtc);
        Assert.Null(job.FirstNotFoundAtUtc);
        Assert.Equal(clearedAtUtc, job.UpdatedAtUtc);

        var failedAtUtc = clearedAtUtc.AddSeconds(1);
        job.TransitionTo(DownloadJobStatus.Failed, failedAtUtc, " provider_error ");
        Assert.Equal(DownloadJobStatus.Failed, job.Status);
        Assert.Equal("provider_error", job.FailureReason);
        Assert.Null(job.CompletedAtUtc);

        // Same-state failed update keeps state but refreshes failure reason.
        var failedAgainAtUtc = failedAtUtc.AddSeconds(1);
        job.TransitionTo(DownloadJobStatus.Failed, failedAgainAtUtc, " transient ");
        Assert.Equal("transient", job.FailureReason);
        Assert.Equal(failedAgainAtUtc, job.UpdatedAtUtc);
    }

    [Fact]
    public void DownloadJob_CanTransition_RejectsTerminalSources()
    {
        Assert.True(DownloadJob.CanTransition(DownloadJobStatus.Queued, DownloadJobStatus.Downloading));
        Assert.True(DownloadJob.CanTransition(DownloadJobStatus.Downloading, DownloadJobStatus.Completed));
        Assert.False(DownloadJob.CanTransition(DownloadJobStatus.Completed, DownloadJobStatus.Failed));
        Assert.False(DownloadJob.CanTransition(DownloadJobStatus.Canceled, DownloadJobStatus.Downloading));
    }
}

using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Bookshelf.Shared.Contracts.Assets;
using Bookshelf.Shared.Contracts.Books;
using Bookshelf.Shared.Contracts.Downloads;
using Bookshelf.Shared.Contracts.History;
using Bookshelf.Shared.Contracts.Library;
using Bookshelf.Shared.Contracts.Progress;

namespace Bookshelf.Api.Mappers;

public static class DtoMapper
{
    public static AuthorDto ToDto(this Author author) => new(author.Id, author.Name);

    public static BookFormatDto ToDto(this BookFormat format) => new(
        format.Id,
        format.FormatType.ToString().ToLowerInvariant(),
        format.Language,
        format.DurationSeconds,
        format.FileSizeBytes);

    public static BookSummaryDto ToSummaryDto(
        this Book book,
        IReadOnlyList<Author> authors,
        IReadOnlyList<BookFormat> formats) => new(
            book.Id,
            book.Title,
            book.OriginalTitle,
            book.PublishYear,
            book.CommunityRating,
            book.CoverUrl,
            authors.Select(ToDto).ToList(),
            formats.Any(x => x.FormatType == BookFormatType.Text),
            formats.Any(x => x.FormatType == BookFormatType.Audio));

    public static BookDetailsDto ToDetailsDto(
        this Book book,
        IReadOnlyList<Author> authors,
        IReadOnlyList<BookFormat> formats) => new(
            book.Id,
            book.Title,
            book.OriginalTitle,
            book.PublishYear,
            book.CommunityRating,
            book.CoverUrl,
            book.Description,
            authors.Select(ToDto).ToList(),
            formats.Select(ToDto).ToList());

    public static LibraryItemDto ToDto(this LibraryItem item) => new(
        item.Id,
        item.UserId,
        item.BookId,
        item.Status.ToString().ToLowerInvariant(),
        item.UserRating,
        item.AddedAtUtc);

    public static ProgressSnapshotDto ToDto(this ProgressSnapshot snapshot) => new(
        snapshot.Id,
        snapshot.UserId,
        snapshot.BookId,
        snapshot.FormatType.ToString().ToLowerInvariant(),
        snapshot.PositionRef,
        snapshot.ProgressPercent,
        snapshot.UpdatedAtUtc);

    public static HistoryEventDto ToDto(this HistoryEvent historyEvent) => new(
        historyEvent.Id,
        historyEvent.UserId,
        historyEvent.BookId,
        historyEvent.FormatType.ToString().ToLowerInvariant(),
        historyEvent.EventType.ToString().ToLowerInvariant(),
        historyEvent.PositionRef,
        historyEvent.EventAtUtc);

    public static DownloadJobDto ToDto(this DownloadJob job) => new(
        job.Id,
        job.UserId,
        job.BookFormatId,
        job.Status.ToString().ToLowerInvariant(),
        job.Source,
        job.ExternalJobId,
        job.CreatedAtUtc,
        job.CompletedAtUtc);

    public static LocalAssetDto ToDto(this LocalAsset asset) => new(
        asset.Id,
        asset.UserId,
        asset.BookFormatId,
        asset.LocalPath,
        asset.FileSizeBytes,
        asset.DownloadedAtUtc,
        asset.DeletedAtUtc);
}

namespace Bookshelf.Shared.Contracts.Progress;

public sealed record ProgressSnapshotDto(
    int Id,
    int UserId,
    int BookId,
    string FormatType,
    string PositionRef,
    float ProgressPercent,
    DateTime UpdatedAtUtc);

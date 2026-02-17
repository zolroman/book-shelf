namespace Bookshelf.Shared.Contracts.Progress;

public sealed record UpsertProgressRequest(
    int UserId,
    int BookId,
    string FormatType,
    string PositionRef,
    float ProgressPercent);

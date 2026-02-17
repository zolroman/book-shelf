namespace Bookshelf.App.Models;

public sealed record SyncOperationRecord(
    long Id,
    string OperationType,
    string PayloadJson,
    string? DedupKey,
    int Attempts,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? LastError);

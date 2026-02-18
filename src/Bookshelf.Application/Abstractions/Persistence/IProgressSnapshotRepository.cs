using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Abstractions.Persistence;

public interface IProgressSnapshotRepository
{
    Task<ProgressSnapshot?> GetAsync(
        long userId,
        long bookId,
        MediaType mediaType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgressSnapshot>> ListAsync(
        long userId,
        long? bookId,
        MediaType? mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        long userId,
        long? bookId,
        MediaType? mediaType,
        CancellationToken cancellationToken = default);

    Task AddAsync(ProgressSnapshot snapshot, CancellationToken cancellationToken = default);

    void Update(ProgressSnapshot snapshot);
}

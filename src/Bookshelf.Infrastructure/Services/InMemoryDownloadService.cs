using Bookshelf.Domain.Abstractions;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Infrastructure.Services;

public sealed class InMemoryDownloadService(IClock clock) : IDownloadService
{
    private readonly IClock _clock = clock;
    private readonly object _syncRoot = new();
    private readonly List<DownloadJob> _jobs = [];
    private int _nextJobId = 1;

    public Task<IReadOnlyList<DownloadJob>> GetJobsAsync(int userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var jobs = _jobs.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAtUtc).ToList();
            return Task.FromResult<IReadOnlyList<DownloadJob>>(jobs);
        }
    }

    public Task<DownloadJob?> GetJobAsync(int jobId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_jobs.SingleOrDefault(x => x.Id == jobId));
        }
    }

    public Task<DownloadJob> StartAsync(int userId, int bookFormatId, string source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var existingActive = _jobs
                .FirstOrDefault(x => x.UserId == userId
                    && x.BookFormatId == bookFormatId
                    && x.Status is DownloadJobStatus.Queued or DownloadJobStatus.Downloading);
            if (existingActive is not null)
            {
                return Task.FromResult(existingActive);
            }

            var entity = new DownloadJob
            {
                Id = _nextJobId++,
                UserId = userId,
                BookFormatId = bookFormatId,
                Source = source,
                ExternalJobId = $"qb-{Guid.NewGuid():N}",
                CreatedAtUtc = _clock.UtcNow
            };
            entity.TransitionTo(DownloadJobStatus.Downloading, _clock.UtcNow);
            _jobs.Add(entity);
            return Task.FromResult(entity);
        }
    }

    public Task<DownloadJob?> CancelAsync(int jobId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var entity = _jobs.SingleOrDefault(x => x.Id == jobId);
            if (entity is null)
            {
                return Task.FromResult<DownloadJob?>(null);
            }

            if (entity.Status is DownloadJobStatus.Queued or DownloadJobStatus.Downloading)
            {
                entity.TransitionTo(DownloadJobStatus.Canceled, _clock.UtcNow);
            }

            return Task.FromResult<DownloadJob?>(entity);
        }
    }
}

using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Bookshelf.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Bookshelf.Infrastructure.Services;

public sealed class DownloadPipelineService(
    IBookshelfRepository repository,
    ITorrentSearchClient torrentSearchClient,
    IQbittorrentDownloadClient qbittorrentDownloadClient,
    ILogger<DownloadPipelineService> logger) : IDownloadService
{
    public async Task<IReadOnlyList<TorrentCandidate>> SearchCandidatesAsync(
        string query,
        int maxItems,
        CancellationToken cancellationToken)
    {
        return await torrentSearchClient.SearchAsync(query, maxItems, cancellationToken);
    }

    public async Task<IReadOnlyList<DownloadJob>> GetJobsAsync(int userId, CancellationToken cancellationToken)
    {
        var jobs = await repository.GetDownloadJobsAsync(userId, cancellationToken);
        foreach (var job in jobs.Where(IsActive))
        {
            await SyncJobStateAsync(job, cancellationToken);
        }

        return await repository.GetDownloadJobsAsync(userId, cancellationToken);
    }

    public async Task<DownloadJob?> GetJobAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await repository.GetDownloadJobAsync(jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        await SyncJobStateAsync(job, cancellationToken);
        return await repository.GetDownloadJobAsync(jobId, cancellationToken);
    }

    public async Task<DownloadJob> StartAsync(int userId, int bookFormatId, string source, CancellationToken cancellationToken)
    {
        var existing = await repository.GetActiveDownloadJobAsync(userId, bookFormatId, cancellationToken);
        if (existing is not null)
        {
            await SyncJobStateAsync(existing, cancellationToken);
            return await repository.GetDownloadJobAsync(existing.Id, cancellationToken) ?? existing;
        }

        var format = await repository.GetBookFormatAsync(bookFormatId, cancellationToken)
                     ?? throw new ArgumentException($"Book format {bookFormatId} not found.");
        var book = await repository.GetBookAsync(format.BookId, cancellationToken)
                   ?? throw new ArgumentException($"Book {format.BookId} not found for format {bookFormatId}.");

        var resolvedUri = await ResolveDownloadUriAsync(source, book.Title, cancellationToken);

        var job = await repository.CreateDownloadJobAsync(userId, bookFormatId, source, cancellationToken);
        var externalId = await qbittorrentDownloadClient.EnqueueAsync(resolvedUri, cancellationToken);

        job = await repository.UpdateDownloadJobExternalIdAsync(job.Id, externalId, cancellationToken);
        job = await repository.UpdateDownloadJobStatusAsync(job.Id, DownloadJobStatus.Downloading, cancellationToken);

        logger.LogInformation(
            "Download job started. JobId={JobId}, UserId={UserId}, BookFormatId={BookFormatId}, ExternalJobId={ExternalJobId}",
            job.Id,
            userId,
            bookFormatId,
            externalId);

        return job;
    }

    public async Task<DownloadJob?> CancelAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await repository.GetDownloadJobAsync(jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        if (!IsActive(job))
        {
            return job;
        }

        if (!string.IsNullOrWhiteSpace(job.ExternalJobId))
        {
            await qbittorrentDownloadClient.CancelAsync(job.ExternalJobId, cancellationToken);
        }

        return await repository.UpdateDownloadJobStatusAsync(job.Id, DownloadJobStatus.Canceled, cancellationToken);
    }

    private async Task SyncJobStateAsync(DownloadJob job, CancellationToken cancellationToken)
    {
        if (!IsActive(job) || string.IsNullOrWhiteSpace(job.ExternalJobId))
        {
            return;
        }

        var externalStatus = await qbittorrentDownloadClient.GetStatusAsync(job.ExternalJobId, cancellationToken);
        var targetStatus = externalStatus switch
        {
            ExternalDownloadStatus.Queued => DownloadJobStatus.Queued,
            ExternalDownloadStatus.Downloading => DownloadJobStatus.Downloading,
            ExternalDownloadStatus.Completed => DownloadJobStatus.Completed,
            ExternalDownloadStatus.Canceled => DownloadJobStatus.Canceled,
            ExternalDownloadStatus.Failed => DownloadJobStatus.Failed,
            _ => (DownloadJobStatus?)null
        };

        if (!targetStatus.HasValue || targetStatus.Value == job.Status)
        {
            return;
        }

        var updatedJob = await repository.UpdateDownloadJobStatusAsync(job.Id, targetStatus.Value, cancellationToken);
        if (targetStatus == DownloadJobStatus.Completed)
        {
            var localPath = $"/downloads/{updatedJob.ExternalJobId}";
            await repository.AddOrUpdateLocalAssetAsync(
                updatedJob.UserId,
                updatedJob.BookFormatId,
                localPath,
                fileSizeBytes: 0,
                cancellationToken);
        }
    }

    private async Task<string> ResolveDownloadUriAsync(string source, string fallbackQuery, CancellationToken cancellationToken)
    {
        if (MagnetUriHelper.IsDownloadUri(source))
        {
            return source.Trim();
        }

        var query = string.IsNullOrWhiteSpace(source) ? fallbackQuery : source.Trim();
        var candidates = await torrentSearchClient.SearchAsync(query, maxItems: 10, cancellationToken);
        var bestCandidate = candidates
            .OrderByDescending(x => x.Seeders)
            .ThenByDescending(x => x.SizeBytes ?? 0)
            .FirstOrDefault();

        if (bestCandidate is null)
        {
            throw new InvalidOperationException("No torrent candidates were found.");
        }

        return bestCandidate.DownloadUri;
    }

    private static bool IsActive(DownloadJob job)
    {
        return job.Status is DownloadJobStatus.Queued or DownloadJobStatus.Downloading;
    }
}

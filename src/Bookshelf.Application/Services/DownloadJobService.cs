using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Services;

public sealed class DownloadJobService : IDownloadJobService
{
    private readonly IDownloadJobRepository _downloadJobRepository;
    private readonly IBookRepository _bookRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDownloadExecutionClient _downloadExecutionClient;

    public DownloadJobService(
        IDownloadJobRepository downloadJobRepository,
        IBookRepository bookRepository,
        IUnitOfWork unitOfWork,
        IDownloadExecutionClient downloadExecutionClient)
    {
        _downloadJobRepository = downloadJobRepository;
        _bookRepository = bookRepository;
        _unitOfWork = unitOfWork;
        _downloadExecutionClient = downloadExecutionClient;
    }

    public async Task<DownloadJobsResponse> ListAsync(
        long userId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var parsedStatus = ParseStatus(status);

        await SyncActiveAsync(cancellationToken);

        var total = await _downloadJobRepository.CountByUserAsync(userId, parsedStatus, cancellationToken);
        var jobs = await _downloadJobRepository.ListByUserAsync(
            userId,
            parsedStatus,
            safePage,
            safePageSize,
            cancellationToken);

        return new DownloadJobsResponse(
            Page: safePage,
            PageSize: safePageSize,
            Total: total,
            Items: jobs.Select(Map).ToArray());
    }

    public async Task<DownloadJobDto?> GetAsync(
        long jobId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var job = await _downloadJobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null || job.UserId != userId)
        {
            return null;
        }

        if (IsActive(job.Status))
        {
            await SyncSingleAsync(job, cancellationToken);
            job = await _downloadJobRepository.GetByIdAsync(jobId, cancellationToken);
            if (job is null || job.UserId != userId)
            {
                return null;
            }
        }

        return Map(job);
    }

    public async Task<DownloadJobDto> CancelAsync(
        long jobId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var job = await _downloadJobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null || job.UserId != userId)
        {
            throw new DownloadJobNotFoundException(jobId);
        }

        if (job.Status is not (DownloadJobStatus.Queued or DownloadJobStatus.Downloading))
        {
            throw new DownloadJobCancelNotAllowedException(jobId, job.Status.ToString().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(job.ExternalJobId))
        {
            await _downloadExecutionClient.CancelAsync(
                job.ExternalJobId,
                deleteFiles: false,
                cancellationToken);
        }

        var nowUtc = DateTimeOffset.UtcNow;
        job.TransitionTo(DownloadJobStatus.Canceled, nowUtc);
        _downloadJobRepository.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(job);
    }

    public async Task SyncActiveAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _downloadJobRepository.ListActiveAsync(limit: 100, cancellationToken);
        foreach (var job in jobs)
        {
            await SyncSingleAsync(job, cancellationToken);
        }
    }

    private async Task SyncSingleAsync(DownloadJob job, CancellationToken cancellationToken)
    {
        if (!IsActive(job.Status))
        {
            return;
        }

        DownloadStatusResult status;
        if (string.IsNullOrWhiteSpace(job.ExternalJobId))
        {
            status = new DownloadStatusResult(ExternalDownloadState.NotFound, null, null);
        }
        else
        {
            status = await _downloadExecutionClient.GetStatusAsync(job.ExternalJobId, cancellationToken);
        }

        var nowUtc = DateTimeOffset.UtcNow;
        switch (status.State)
        {
            case ExternalDownloadState.NotFound:
                await ApplyNotFoundSyncAsync(job, nowUtc, cancellationToken);
                return;
            case ExternalDownloadState.Queued:
                await ClearNotFoundIfNeededAsync(job, nowUtc, cancellationToken);
                return;
            case ExternalDownloadState.Downloading:
                await ApplyDownloadingSyncAsync(job, nowUtc, cancellationToken);
                return;
            case ExternalDownloadState.Completed:
                await ApplyCompletedSyncAsync(job, status, nowUtc, cancellationToken);
                return;
            case ExternalDownloadState.Failed:
                await ApplyFailedSyncAsync(job, nowUtc, cancellationToken);
                return;
            default:
                return;
        }
    }

    private async Task ApplyNotFoundSyncAsync(
        DownloadJob job,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!job.FirstNotFoundAtUtc.HasValue)
        {
            job.SetNotFoundObserved(observedAtUtc);
            _downloadJobRepository.Update(job);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var elapsed = observedAtUtc - job.FirstNotFoundAtUtc.Value;
        if (elapsed >= _downloadExecutionClient.NotFoundGracePeriod)
        {
            job.TransitionTo(DownloadJobStatus.Failed, observedAtUtc, "missing_external_job");
            _downloadJobRepository.Update(job);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        job.SetNotFoundObserved(observedAtUtc);
        _downloadJobRepository.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyDownloadingSyncAsync(
        DownloadJob job,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var changed = false;
        if (job.FirstNotFoundAtUtc.HasValue)
        {
            job.ClearNotFoundObserved(updatedAtUtc);
            changed = true;
        }

        if (job.Status == DownloadJobStatus.Queued)
        {
            job.TransitionTo(DownloadJobStatus.Downloading, updatedAtUtc);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        _downloadJobRepository.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyCompletedSyncAsync(
        DownloadJob job,
        DownloadStatusResult status,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        if (job.FirstNotFoundAtUtc.HasValue)
        {
            job.ClearNotFoundObserved(updatedAtUtc);
        }

        if (job.Status == DownloadJobStatus.Queued)
        {
            job.TransitionTo(DownloadJobStatus.Downloading, updatedAtUtc);
            _downloadJobRepository.Update(job);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        if (job.Status != DownloadJobStatus.Downloading)
        {
            return;
        }

        var book = await _bookRepository.GetByIdAsync(job.BookId, cancellationToken);
        if (book is null)
        {
            job.TransitionTo(DownloadJobStatus.Failed, updatedAtUtc, "book_not_found");
            _downloadJobRepository.Update(job);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var asset = book.UpsertMediaAsset(job.MediaType, job.Source, "jackett");
        var storagePath = !string.IsNullOrWhiteSpace(status.StoragePath)
            ? status.StoragePath
            : $"downloads/{job.ExternalJobId ?? job.Id.ToString()}";

        asset.MarkAvailable(
            storagePath!,
            status.SizeBytes,
            checksum: null,
            completedAtUtc: updatedAtUtc);

        book.RecomputeCatalogState();
        _bookRepository.Update(book);

        job.TransitionTo(DownloadJobStatus.Completed, updatedAtUtc);
        _downloadJobRepository.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyFailedSyncAsync(
        DownloadJob job,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        if (job.FirstNotFoundAtUtc.HasValue)
        {
            job.ClearNotFoundObserved(updatedAtUtc);
        }

        job.TransitionTo(DownloadJobStatus.Failed, updatedAtUtc, "provider_error");
        _downloadJobRepository.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearNotFoundIfNeededAsync(
        DownloadJob job,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!job.FirstNotFoundAtUtc.HasValue)
        {
            return;
        }

        job.ClearNotFoundObserved(updatedAtUtc);
        _downloadJobRepository.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static bool IsActive(DownloadJobStatus status)
    {
        return status is DownloadJobStatus.Queued or DownloadJobStatus.Downloading;
    }

    private static DownloadJobDto Map(DownloadJob job)
    {
        return new DownloadJobDto(
            Id: job.Id,
            UserId: job.UserId,
            BookId: job.BookId,
            MediaType: job.MediaType.ToString().ToLowerInvariant(),
            Status: job.Status.ToString().ToLowerInvariant(),
            ExternalJobId: job.ExternalJobId,
            FailureReason: job.FailureReason,
            CreatedAtUtc: job.CreatedAtUtc,
            UpdatedAtUtc: job.UpdatedAtUtc,
            CompletedAtUtc: job.CompletedAtUtc);
    }

    private static DownloadJobStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "queued" => DownloadJobStatus.Queued,
            "downloading" => DownloadJobStatus.Downloading,
            "completed" => DownloadJobStatus.Completed,
            "failed" => DownloadJobStatus.Failed,
            "canceled" => DownloadJobStatus.Canceled,
            _ => throw new ArgumentException("Unsupported download job status filter.", nameof(status)),
        };
    }
}

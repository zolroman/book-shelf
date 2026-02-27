using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Bookshelf.Api.Api;

public sealed class FlibustaTextDownloadWorker : BackgroundService
{
    private const string ExecutionProviderCode = "local-http-epub";
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<DirectDownloadOptions> _options;
    private readonly ILogger<FlibustaTextDownloadWorker> _logger;

    public FlibustaTextDownloadWorker(
        IServiceScopeFactory serviceScopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<DirectDownloadOptions> options,
        ILogger<FlibustaTextDownloadWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.Value.PollIntervalSeconds));
        var timer = new PeriodicTimer(pollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Unexpected error while running Flibusta EPUB download worker.");
                }
            }
        }
        finally
        {
            timer.Dispose();
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var downloadJobRepository = scope.ServiceProvider.GetRequiredService<IDownloadJobRepository>();

        var limit = Math.Max(1, _options.Value.MaxConcurrentJobs);
        var jobs = await downloadJobRepository.ListActiveAsync(limit: 100, cancellationToken);
        var candidates = jobs
            .Where(IsLocalEpubJob)
            .Take(limit)
            .ToArray();

        foreach (var job in candidates)
        {
            await ProcessJobAsync(job.Id, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(long jobId, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var downloadJobRepository = scope.ServiceProvider.GetRequiredService<IDownloadJobRepository>();
        var bookRepository = scope.ServiceProvider.GetRequiredService<IBookRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var job = await downloadJobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null || !IsLocalEpubJob(job))
        {
            return;
        }

        if (job.Status == DownloadJobStatus.Queued)
        {
            job.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow);
            downloadJobRepository.Update(job);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (job.Status != DownloadJobStatus.Downloading)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(job.DownloadUri))
        {
            await MarkFailedAsync(downloadJobRepository, unitOfWork, job, "provider_error", cancellationToken);
            return;
        }

        var storageRoot = ResolveStorageRoot();
        var fileStem = SanitizeFileName(job.ExternalJobId ?? $"job-{job.Id}");
        var finalPath = Path.Combine(storageRoot, $"{fileStem}.epub");
        var tempPath = $"{finalPath}.part";
        Directory.CreateDirectory(storageRoot);
        DeleteFileIfExists(tempPath);

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(job.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await MarkFailedAsync(downloadJobRepository, unitOfWork, job, "provider_error", cancellationToken);
                return;
            }

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            var refreshedJob = await downloadJobRepository.GetByIdAsync(job.Id, cancellationToken);
            if (refreshedJob is null)
            {
                DeleteFileIfExists(tempPath);
                return;
            }

            if (refreshedJob.Status == DownloadJobStatus.Canceled)
            {
                DeleteFileIfExists(tempPath);
                DeleteFileIfExists(finalPath);
                return;
            }

            if (refreshedJob.Status != DownloadJobStatus.Downloading)
            {
                DeleteFileIfExists(tempPath);
                return;
            }

            File.Move(tempPath, finalPath, overwrite: true);
            var fileInfo = new FileInfo(finalPath);

            var book = await bookRepository.GetByIdAsync(refreshedJob.BookId, cancellationToken);
            if (book is null)
            {
                DeleteFileIfExists(finalPath);
                await MarkFailedAsync(downloadJobRepository, unitOfWork, refreshedJob, "book_not_found", cancellationToken);
                return;
            }

            var asset = book.UpsertMediaAsset(refreshedJob.MediaType, refreshedJob.Source, refreshedJob.SourceProvider);
            asset.MarkAvailable(finalPath, fileInfo.Length, checksum: null, completedAtUtc: DateTimeOffset.UtcNow);
            book.RecomputeCatalogState();
            bookRepository.Update(book);

            refreshedJob.TransitionTo(DownloadJobStatus.Completed, DateTimeOffset.UtcNow);
            downloadJobRepository.Update(refreshedJob);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            DeleteFileIfExists(tempPath);
            throw;
        }
        catch (IOException exception)
        {
            _logger.LogWarning(exception, "Flibusta EPUB storage failure. JobId={JobId}", job.Id);
            DeleteFileIfExists(tempPath);
            await MarkFailedAsync(downloadJobRepository, unitOfWork, job, "storage_write_failed", cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(exception, "Flibusta EPUB storage access failure. JobId={JobId}", job.Id);
            DeleteFileIfExists(tempPath);
            await MarkFailedAsync(downloadJobRepository, unitOfWork, job, "storage_write_failed", cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Flibusta EPUB download failed. JobId={JobId}", job.Id);
            DeleteFileIfExists(tempPath);
            await MarkFailedAsync(downloadJobRepository, unitOfWork, job, "provider_error", cancellationToken);
        }
    }

    private async Task MarkFailedAsync(
        IDownloadJobRepository downloadJobRepository,
        IUnitOfWork unitOfWork,
        DownloadJob job,
        string reason,
        CancellationToken cancellationToken)
    {
        var refreshedJob = await downloadJobRepository.GetByIdAsync(job.Id, cancellationToken);
        if (refreshedJob is null || refreshedJob.Status is DownloadJobStatus.Completed or DownloadJobStatus.Canceled)
        {
            return;
        }

        if (refreshedJob.Status == DownloadJobStatus.Queued)
        {
            refreshedJob.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow);
        }

        refreshedJob.TransitionTo(DownloadJobStatus.Failed, DateTimeOffset.UtcNow, reason);
        downloadJobRepository.Update(refreshedJob);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private string ResolveStorageRoot()
    {
        var configured = _options.Value.StorageRoot;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Path.Combine(AppContext.BaseDirectory, "downloads");
        }

        return Path.IsPathRooted(configured)
            ? configured.Trim()
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured.Trim()));
    }

    private static bool IsLocalEpubJob(DownloadJob job)
    {
        return job.ExecutionProvider.Equals(ExecutionProviderCode, StringComparison.OrdinalIgnoreCase)
            && job.MediaType == MediaType.Text
            && job.Status is DownloadJobStatus.Queued or DownloadJobStatus.Downloading;
    }

    private static void DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var normalized = new string(value.Trim().Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "download" : normalized;
    }
}

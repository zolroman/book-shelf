using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;

namespace Bookshelf.Api.Api;

public sealed class DownloadJobSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<DownloadJobSyncWorker> _logger;

    public DownloadJobSyncWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<DownloadJobSyncWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var downloadJobService = scope.ServiceProvider.GetRequiredService<IDownloadJobService>();
                var downloadJobRepository = scope.ServiceProvider.GetRequiredService<IDownloadJobRepository>();
                try
                {
                    var beforeSync = await downloadJobRepository.ListActiveAsync(limit: 500, stoppingToken);
                    var snapshots = beforeSync
                        .Select(job => new JobStatusSnapshot(job.Id, job.Status.ToString().ToLowerInvariant(), job.FailureReason))
                        .ToArray();

                    await downloadJobService.SyncActiveAsync(stoppingToken);

                    foreach (var snapshot in snapshots)
                    {
                        var updated = await downloadJobRepository.GetByIdAsync(snapshot.JobId, stoppingToken);
                        if (updated is null)
                        {
                            continue;
                        }

                        var updatedStatus = updated.Status.ToString().ToLowerInvariant();
                        if (updatedStatus == snapshot.Status
                            && string.Equals(updated.FailureReason, snapshot.FailureReason, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        _logger.LogInformation(
                            "Download job state transition. JobId={JobId} From={FromStatus} To={ToStatus} FailureReason={FailureReason}",
                            updated.Id,
                            snapshot.Status,
                            updatedStatus,
                            updated.FailureReason);
                    }
                }
                catch (DownloadExecutionUnavailableException exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Download sync skipped due to temporary qBittorrent unavailability.");
                }
                catch (DownloadExecutionFailedException exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Download sync failed due to qBittorrent operation error.");
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Unexpected error while running download sync worker.");
                }
            }
        }
        finally
        {
            timer.Dispose();
        }
    }

    private sealed record JobStatusSnapshot(long JobId, string Status, string? FailureReason);
}

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
                try
                {
                    await downloadJobService.SyncActiveAsync(stoppingToken);
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
}

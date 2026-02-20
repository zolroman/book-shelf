using System.Net;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;

namespace Bookshelf.Api.Api.Endpoints.DownloadJobs;

public static class ListDownloadJobsEndpoint
{
    public static RouteGroupBuilder MapListDownloadJobsEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapGet("download-jobs", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        long? userId,
        string? status,
        int? page,
        int? pageSize,
        IDownloadJobService downloadJobService,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = EndpointGuards.EnsureUserId(userId);
        var safePage = !page.HasValue || page.Value < 1 ? 1 : page.Value;
        var safePageSize = !pageSize.HasValue || pageSize.Value is < 1 or > 100 ? 20 : pageSize.Value;

        try
        {
            var response = await downloadJobService.ListAsync(
                normalizedUserId,
                status,
                safePage,
                safePageSize,
                cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "status filter is invalid.",
                HttpStatusCode.BadRequest);
        }
        catch (DownloadExecutionUnavailableException)
        {
            throw new ApiException(
                ApiErrorCodes.QBittorrentStatusFailed,
                "Failed to synchronize download job status from qBittorrent.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadExecutionFailedException)
        {
            throw new ApiException(
                ApiErrorCodes.QBittorrentStatusFailed,
                "Failed to synchronize download job status from qBittorrent.",
                HttpStatusCode.BadGateway);
        }
    }
}

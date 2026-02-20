using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;

namespace Bookshelf.Api.Api.Endpoints.DownloadJobs;

public static class GetDownloadJobEndpoint
{
    public static RouteGroupBuilder MapGetDownloadJobEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapGet("download-jobs/{jobId:long}", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        long jobId,
        ClaimsPrincipal user,
        IDownloadJobService downloadJobService,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;

        try
        {
            var job = await downloadJobService.GetAsync(jobId, userId, cancellationToken);
            if (job is null)
            {
                throw new ApiException(
                    ApiErrorCodes.DownloadNotFound,
                    "Download job was not found.",
                    HttpStatusCode.NotFound);
            }

            return Results.Ok(job);
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

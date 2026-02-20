using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;

namespace Bookshelf.Api.Api.Endpoints.DownloadJobs;

public static class CancelDownloadJobEndpoint
{
    public static RouteGroupBuilder MapCancelDownloadJobEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapPost("download-jobs/{jobId:long}/cancel", Handle);
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
            var job = await downloadJobService.CancelAsync(jobId, userId, cancellationToken);
            return Results.Ok(job);
        }
        catch (DownloadJobNotFoundException)
        {
            throw new ApiException(
                ApiErrorCodes.DownloadNotFound,
                "Download job was not found.",
                HttpStatusCode.NotFound);
        }
        catch (DownloadJobCancelNotAllowedException)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "Cancel is allowed only for queued or downloading jobs.",
                HttpStatusCode.BadRequest);
        }
        catch (DownloadExecutionUnavailableException)
        {
            throw new ApiException(
                ApiErrorCodes.DownloadCancelFailed,
                "qBittorrent cancel operation failed.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadExecutionFailedException)
        {
            throw new ApiException(
                ApiErrorCodes.DownloadCancelFailed,
                "qBittorrent cancel operation failed.",
                HttpStatusCode.BadGateway);
        }
    }
}

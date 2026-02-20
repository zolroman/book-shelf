using System.Net;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Shared.Contracts.Api;

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
        CancelDownloadJobRequest request,
        IDownloadJobService downloadJobService,
        CancellationToken cancellationToken)
    {
        EndpointGuards.EnsureUserId(request.UserId);

        try
        {
            var job = await downloadJobService.CancelAsync(jobId, request.UserId, cancellationToken);
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

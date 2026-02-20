using System.Net;
using System.Security.Claims;
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
        string? status,
        int? page,
        int? pageSize,
        ClaimsPrincipal user,
        IDownloadJobService downloadJobService,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;
        var pagination = EndpointGuards.NormalizePaging(page, pageSize);

        try
        {
            var response = await downloadJobService.ListAsync(
                userId,
                status,
                pagination.Page,
                pagination.PageSize,
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

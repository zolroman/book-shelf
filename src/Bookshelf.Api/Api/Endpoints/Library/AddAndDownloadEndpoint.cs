using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Api.Api.Endpoints.Library;

public static class AddAndDownloadEndpoint
{
    private sealed record RequestBody(
        string ProviderCode,
        string ProviderBookKey,
        string MediaType,
        string CandidateId);

    public static RouteGroupBuilder MapAddAndDownloadEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapPost("library/add-and-download", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        RequestBody request,
        ClaimsPrincipal user,
        IAddAndDownloadService addAndDownloadService,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;
        EndpointGuards.EnsureRequired(request.ProviderCode, nameof(request.ProviderCode));
        EndpointGuards.EnsureRequired(request.ProviderBookKey, nameof(request.ProviderBookKey));
        _ = EndpointGuards.EnsureMediaType(request.MediaType);

        if (string.IsNullOrWhiteSpace(request.CandidateId))
        {
            throw new ApiException(
                ApiErrorCodes.CandidateRequired,
                "candidateId is required.",
                HttpStatusCode.BadRequest);
        }

        try
        {
            var serviceRequest = new AddAndDownloadRequest(
                UserId: userId,
                ProviderCode: request.ProviderCode,
                ProviderBookKey: request.ProviderBookKey,
                MediaType: request.MediaType,
                CandidateId: request.CandidateId);

            var response = await addAndDownloadService.ExecuteAsync(serviceRequest, cancellationToken);
            return Results.Ok(response);
        }
        catch (BookNotFoundException)
        {
            throw new ApiException(
                ApiErrorCodes.BookNotFound,
                "Book was not found in metadata provider.",
                HttpStatusCode.NotFound);
        }
        catch (DownloadCandidateNotFoundException)
        {
            throw new ApiException(
                ApiErrorCodes.CandidateNotFound,
                "Selected candidate was not found.",
                HttpStatusCode.NotFound);
        }
        catch (MetadataProviderUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.FantlabUnavailable,
                $"Metadata provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadCandidateProviderUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.JackettUnavailable,
                $"Candidate provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadExecutionUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.QBittorrentUnavailable,
                $"Download provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadExecutionFailedException exception)
        {
            throw new ApiException(
                ApiErrorCodes.QBittorrentEnqueueFailed,
                $"Download provider '{exception.ProviderCode}' failed to enqueue torrent.",
                HttpStatusCode.BadGateway);
        }
    }
}

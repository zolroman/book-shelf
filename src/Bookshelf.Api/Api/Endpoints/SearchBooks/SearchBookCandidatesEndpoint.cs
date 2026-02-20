using System.Net;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;

namespace Bookshelf.Api.Api.Endpoints.SearchBooks;

public static class SearchBookCandidatesEndpoint
{
    private const string FantLabProviderCode = "fantlab";

    public static RouteGroupBuilder MapSearchBookCandidatesEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapGet("search/books/{providerCode}/{providerBookKey}/candidates", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        string providerCode,
        string providerBookKey,
        string? mediaType,
        int? page,
        int? pageSize,
        ICandidateDiscoveryService candidateDiscoveryService,
        CancellationToken cancellationToken)
    {
        EndpointGuards.EnsureRequired(providerCode, nameof(providerCode));
        EndpointGuards.EnsureRequired(providerBookKey, nameof(providerBookKey));
        if (!providerCode.Equals(FantLabProviderCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "Unsupported providerCode for v1. Expected 'fantlab'.",
                HttpStatusCode.BadRequest);
        }

        var normalizedMediaType = EndpointGuards.EnsureMediaType(mediaType);
        var pagination = EndpointGuards.NormalizePaging(page, pageSize);

        try
        {
            var response = await candidateDiscoveryService.FindAsync(
                providerCode,
                providerBookKey,
                normalizedMediaType,
                pagination.Page,
                pagination.PageSize,
                cancellationToken);

            return Results.Ok(response);
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
    }
}

using System.Net;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;

namespace Bookshelf.Api.Api.Endpoints.SearchBooks;

public static class SearchBookDetailsEndpoint
{
    private const string FantLabProviderCode = "fantlab";

    public static RouteGroupBuilder MapSearchBookDetailsEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapGet("search/books/{providerCode}/{providerBookKey}", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        string providerCode,
        string providerBookKey,
        IBookSearchService searchService,
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

        try
        {
            var response = await searchService.GetDetailsAsync(
                providerCode,
                providerBookKey,
                cancellationToken);

            if (response is null)
            {
                throw new ApiException(
                    ApiErrorCodes.BookNotFound,
                    "Book was not found in metadata provider.",
                    HttpStatusCode.NotFound);
            }

            return Results.Ok(response);
        }
        catch (MetadataProviderUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.FantlabUnavailable,
                $"Metadata provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
    }
}

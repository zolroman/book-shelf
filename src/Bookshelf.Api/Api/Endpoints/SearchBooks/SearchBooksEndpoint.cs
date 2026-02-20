using System.Net;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;

namespace Bookshelf.Api.Api.Endpoints.SearchBooks;

public static class SearchBooksEndpoint
{
    public static RouteGroupBuilder MapSearchBooksEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapGet("search/books", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        string? title,
        string? author,
        int? page,
        IBookSearchService searchService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(author))
        {
            throw new ApiException(
                ApiErrorCodes.QueryRequired,
                "At least one of title or author is required.",
                HttpStatusCode.BadRequest);
        }

        var safePage = EndpointGuards.NormalizePage(page);

        try
        {
            var response = await searchService.SearchAsync(
                title,
                author,
                safePage,
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
    }
}

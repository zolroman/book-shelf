using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Api.Api.Endpoints.Shelves;

public static class AddBookToShelfEndpoint
{
    private sealed record RequestBody(long BookId);

    public static RouteGroupBuilder MapAddBookToShelfEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapPost("shelves/{shelfId:long}/books", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        long shelfId,
        RequestBody request,
        ClaimsPrincipal user,
        IShelfService shelfService,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;
        if (request.BookId <= 0)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "bookId must be greater than zero.",
                HttpStatusCode.BadRequest);
        }

        var result = await shelfService.AddBookAsync(
            shelfId,
            userId,
            request.BookId,
            cancellationToken);

        if (result.Status == ShelfAddBookResultStatus.NotFound)
        {
            throw new ApiException(
                ApiErrorCodes.ShelfNotFound,
                "Shelf was not found.",
                HttpStatusCode.NotFound);
        }

        if (result.Status == ShelfAddBookResultStatus.AlreadyExists)
        {
            throw new ApiException(
                ApiErrorCodes.ShelfBookExists,
                "Book already exists on shelf.",
                HttpStatusCode.Conflict);
        }

        return Results.Ok(new AddBookToShelfResponse(result.Shelf!));
    }
}

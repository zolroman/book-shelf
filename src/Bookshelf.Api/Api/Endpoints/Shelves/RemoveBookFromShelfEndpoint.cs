using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;

namespace Bookshelf.Api.Api.Endpoints.Shelves;

public static class RemoveBookFromShelfEndpoint
{
    public static RouteGroupBuilder MapRemoveBookFromShelfEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapDelete("shelves/{shelfId:long}/books/{bookId:long}", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        long shelfId,
        long bookId,
        ClaimsPrincipal user,
        IShelfService shelfService,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;
        if (bookId <= 0)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "bookId must be greater than zero.",
                HttpStatusCode.BadRequest);
        }

        var removed = await shelfService.RemoveBookAsync(
            shelfId,
            userId,
            bookId,
            cancellationToken);
        if (!removed)
        {
            throw new ApiException(
                ApiErrorCodes.ShelfNotFound,
                "Shelf was not found.",
                HttpStatusCode.NotFound);
        }

        return Results.Ok();
    }
}

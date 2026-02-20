using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Api.Api.Endpoints.Shelves;

public static class CreateShelfEndpoint
{
    private sealed record RequestBody(string Name);

    public static RouteGroupBuilder MapCreateShelfEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapPost("shelves", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        RequestBody request,
        ClaimsPrincipal user,
        IShelfService shelfService,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;
        EndpointGuards.EnsureRequired(request.Name, nameof(request.Name));

        var shelf = await shelfService.CreateAsync(userId, request.Name.Trim(), cancellationToken);
        if (shelf is null)
        {
            throw new ApiException(
                ApiErrorCodes.ShelfNameConflict,
                "Shelf name is already used by this user.",
                HttpStatusCode.Conflict);
        }

        return Results.Created($"/api/v1/shelves/{shelf.Id}", new CreateShelfResponse(shelf));
    }
}

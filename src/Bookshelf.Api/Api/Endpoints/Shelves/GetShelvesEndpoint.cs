using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Application.Abstractions.Services;

namespace Bookshelf.Api.Api.Endpoints.Shelves;

public static class GetShelvesEndpoint
{
    public static RouteGroupBuilder MapGetShelvesEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapGet("shelves", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        ClaimsPrincipal user,
        IShelfService shelfService,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;
        var response = await shelfService.ListAsync(userId, cancellationToken);
        return Results.Ok(response);
    }
}

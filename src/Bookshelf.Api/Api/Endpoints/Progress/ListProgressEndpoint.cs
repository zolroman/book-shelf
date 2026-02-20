using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;

namespace Bookshelf.Api.Api.Endpoints.Progress;

public static class ListProgressEndpoint
{
    public static RouteGroupBuilder MapListProgressEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapGet("progress", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        long? bookId,
        string? mediaType,
        int? page,
        int? pageSize,
        ClaimsPrincipal user,
        IProgressHistoryService progressHistoryService,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;
        var pagination = EndpointGuards.NormalizePaging(page, pageSize);

        try
        {
            var response = await progressHistoryService.ListProgressAsync(
                userId,
                bookId,
                mediaType,
                pagination.Page,
                pagination.PageSize,
                cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "Invalid progress filter argument.",
                HttpStatusCode.BadRequest);
        }
    }
}

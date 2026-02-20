using System.Net;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;

namespace Bookshelf.Api.Api.Endpoints.History;

public static class ListHistoryEventsEndpoint
{
    public static RouteGroupBuilder MapListHistoryEventsEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapGet("history/events", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        long? bookId,
        string? mediaType,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        IProgressHistoryService progressHistoryService,
        CancellationToken cancellationToken)
    {
        var userId = EndpointGuards.EnsureUserIdFromClaims(httpContext.User);
        var safePage = !page.HasValue || page.Value < 1 ? 1 : page.Value;
        var safePageSize = !pageSize.HasValue || pageSize.Value is < 1 or > 100 ? 20 : pageSize.Value;

        try
        {
            var response = await progressHistoryService.ListHistoryAsync(
                userId,
                bookId,
                mediaType,
                safePage,
                safePageSize,
                cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "Invalid history filter argument.",
                HttpStatusCode.BadRequest);
        }
    }
}

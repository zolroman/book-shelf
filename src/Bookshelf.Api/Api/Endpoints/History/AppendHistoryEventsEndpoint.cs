using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Api.Api.Endpoints.History;

public static class AppendHistoryEventsEndpoint
{
    public static RouteGroupBuilder MapAppendHistoryEventsEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapPost("history/events", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        AppendHistoryEventsRequest request,
        ClaimsPrincipal user,
        IProgressHistoryService progressHistoryService,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;
        if (request.Items.Count == 0)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "At least one history item is required.",
                HttpStatusCode.BadRequest);
        }

        try
        {
            var response = await progressHistoryService.AppendHistoryAsync(
                userId,
                request,
                cancellationToken);
            return Results.Ok(response);
        }
        catch (BookIdNotFoundException)
        {
            throw new ApiException(
                ApiErrorCodes.BookNotFound,
                "Book was not found.",
                HttpStatusCode.NotFound);
        }
        catch (ArgumentException)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "Invalid history payload.",
                HttpStatusCode.BadRequest);
        }
    }
}

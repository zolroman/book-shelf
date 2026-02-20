using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Api.Api.Endpoints.Progress;

public static class UpsertProgressEndpoint
{
    public static RouteGroupBuilder MapUpsertProgressEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapPut("progress", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        UpsertProgressRequest request,
        ClaimsPrincipal user,
        IProgressHistoryService progressHistoryService,
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

        _ = EndpointGuards.EnsureMediaType(request.MediaType);
        EndpointGuards.EnsureRequired(request.PositionRef, nameof(request.PositionRef));

        try
        {
            var response = await progressHistoryService.UpsertProgressAsync(
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
                "Invalid progress payload.",
                HttpStatusCode.BadRequest);
        }
    }
}

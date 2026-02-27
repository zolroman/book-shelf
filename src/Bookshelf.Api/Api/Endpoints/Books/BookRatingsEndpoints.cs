using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Api.Api.Endpoints.Books;

public static class BookRatingsEndpoints
{
    public static RouteGroupBuilder MapBookRatingsEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPut("books/{bookId:long}/rating", Upsert);
        v1.MapDelete("books/{bookId:long}/rating", Delete);
        return v1;
    }

    private static async Task<IResult> Upsert(
        long bookId,
        UpsertBookRatingRequest request,
        ClaimsPrincipal user,
        IBookRatingService bookRatingService,
        CancellationToken cancellationToken)
    {
        if (bookId <= 0 || request.Rating is < 1 or > 5)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "bookId must be greater than zero and rating must be in range 1..5.",
                HttpStatusCode.BadRequest);
        }

        try
        {
            var response = await bookRatingService.UpsertAsync(
                user.Id,
                bookId,
                request.Rating,
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
                "Invalid book rating payload.",
                HttpStatusCode.BadRequest);
        }
    }

    private static async Task<IResult> Delete(
        long bookId,
        ClaimsPrincipal user,
        IBookRatingService bookRatingService,
        CancellationToken cancellationToken)
    {
        if (bookId <= 0)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "bookId must be greater than zero.",
                HttpStatusCode.BadRequest);
        }

        try
        {
            await bookRatingService.DeleteAsync(user.Id, bookId, cancellationToken);
            return Results.NoContent();
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
                "Invalid book rating payload.",
                HttpStatusCode.BadRequest);
        }
    }
}

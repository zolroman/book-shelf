using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;

namespace Bookshelf.Api.Api.Endpoints.Library;

public static class ArchiveLibraryBookEndpoint
{
    public static RouteGroupBuilder MapArchiveLibraryBookEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapPost("library/{bookId:long}/archive", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        long bookId,
        ClaimsPrincipal user,
        ILibraryService libraryService,
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
            await libraryService.ArchiveAsync(user.Id, bookId, cancellationToken);
            return Results.NoContent();
        }
        catch (BookIdNotFoundException)
        {
            throw new ApiException(
                ApiErrorCodes.BookNotFound,
                "Book was not found.",
                HttpStatusCode.NotFound);
        }
        catch (DownloadExecutionUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.QBittorrentUnavailable,
                $"Download provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadExecutionFailedException)
        {
            throw new ApiException(
                ApiErrorCodes.DownloadCancelFailed,
                "qBittorrent cancel operation failed.",
                HttpStatusCode.BadGateway);
        }
        catch (ArgumentException)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "Invalid archive request.",
                HttpStatusCode.BadRequest);
        }
    }
}

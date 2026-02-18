using System.Net;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Api.Api;

public static class V1Endpoints
{
    private const string FantLabProviderCode = "fantlab";

    public static IEndpointRouteBuilder MapV1Endpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");
        var search = v1.MapGroup("/search/books");

        search.MapGet(string.Empty, SearchBooks);
        search.MapGet("/{providerCode}/{providerBookKey}", GetSearchBookDetails);
        search.MapGet("/{providerCode}/{providerBookKey}/candidates", GetCandidates);

        var library = v1.MapGroup("/library");
        library.MapPost("/add-and-download", AddAndDownload);

        var jobs = v1.MapGroup("/download-jobs");
        jobs.MapGet(string.Empty, ListDownloadJobs);
        jobs.MapGet("/{jobId:long}", GetDownloadJob);
        jobs.MapPost("/{jobId:long}/cancel", CancelDownloadJob);

        var shelves = v1.MapGroup("/shelves");
        shelves.MapGet(string.Empty, GetShelves);
        shelves.MapPost(string.Empty, CreateShelf);
        shelves.MapPost("/{shelfId:long}/books", AddBookToShelf);
        shelves.MapDelete("/{shelfId:long}/books/{bookId:long}", RemoveBookFromShelf);

        return app;
    }

    private static async Task<IResult> SearchBooks(
        string? title,
        string? author,
        int? page,
        int? pageSize,
        IBookSearchService searchService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(author))
        {
            throw new ApiException(
                ApiErrorCodes.QueryRequired,
                "At least one of title or author is required.",
                HttpStatusCode.BadRequest);
        }

        var safePage = !page.HasValue || page.Value < 1 ? 1 : page.Value;
        var safePageSize = !pageSize.HasValue || pageSize.Value is < 1 or > 100 ? 20 : pageSize.Value;

        try
        {
            var response = await searchService.SearchAsync(
                title,
                author,
                safePage,
                safePageSize,
                cancellationToken);

            return Results.Ok(response);
        }
        catch (MetadataProviderUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.FantlabUnavailable,
                $"Metadata provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
    }

    private static async Task<IResult> GetSearchBookDetails(
        string providerCode,
        string providerBookKey,
        IBookSearchService searchService,
        CancellationToken cancellationToken)
    {
        EnsureRequired(providerCode, nameof(providerCode));
        EnsureRequired(providerBookKey, nameof(providerBookKey));
        if (!providerCode.Equals(FantLabProviderCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "Unsupported providerCode for v1. Expected 'fantlab'.",
                HttpStatusCode.BadRequest);
        }

        try
        {
            var response = await searchService.GetDetailsAsync(
                providerCode,
                providerBookKey,
                cancellationToken);

            if (response is null)
            {
                throw new ApiException(
                    ApiErrorCodes.BookNotFound,
                    "Book was not found in metadata provider.",
                    HttpStatusCode.NotFound);
            }

            return Results.Ok(response);
        }
        catch (MetadataProviderUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.FantlabUnavailable,
                $"Metadata provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
    }

    private static async Task<IResult> GetCandidates(
        string providerCode,
        string providerBookKey,
        string? mediaType,
        int? page,
        int? pageSize,
        ICandidateDiscoveryService candidateDiscoveryService,
        CancellationToken cancellationToken)
    {
        EnsureRequired(providerCode, nameof(providerCode));
        EnsureRequired(providerBookKey, nameof(providerBookKey));
        if (!providerCode.Equals(FantLabProviderCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "Unsupported providerCode for v1. Expected 'fantlab'.",
                HttpStatusCode.BadRequest);
        }

        var normalizedMediaType = EnsureMediaType(mediaType);

        var safePage = !page.HasValue || page.Value < 1 ? 1 : page.Value;
        var safePageSize = !pageSize.HasValue || pageSize.Value is < 1 or > 100 ? 20 : pageSize.Value;

        try
        {
            var response = await candidateDiscoveryService.FindAsync(
                    providerCode,
                    providerBookKey,
                    normalizedMediaType,
                    safePage,
                    safePageSize,
                    cancellationToken);

            return Results.Ok(response);
        }
        catch (MetadataProviderUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.FantlabUnavailable,
                $"Metadata provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadCandidateProviderUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.JackettUnavailable,
                $"Candidate provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
    }

    private static async Task<IResult> AddAndDownload(
        AddAndDownloadRequest request,
        IAddAndDownloadService addAndDownloadService,
        CancellationToken cancellationToken)
    {
        EnsureUserId(request.UserId);
        EnsureRequired(request.ProviderCode, nameof(request.ProviderCode));
        EnsureRequired(request.ProviderBookKey, nameof(request.ProviderBookKey));
        _ = EnsureMediaType(request.MediaType);

        if (string.IsNullOrWhiteSpace(request.CandidateId))
        {
            throw new ApiException(
                ApiErrorCodes.CandidateRequired,
                "candidateId is required.",
                HttpStatusCode.BadRequest);
        }

        try
        {
            var response = await addAndDownloadService.ExecuteAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (BookNotFoundException)
        {
            throw new ApiException(
                ApiErrorCodes.BookNotFound,
                "Book was not found in metadata provider.",
                HttpStatusCode.NotFound);
        }
        catch (DownloadCandidateNotFoundException)
        {
            throw new ApiException(
                ApiErrorCodes.CandidateNotFound,
                "Selected candidate was not found.",
                HttpStatusCode.NotFound);
        }
        catch (MetadataProviderUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.FantlabUnavailable,
                $"Metadata provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadCandidateProviderUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.JackettUnavailable,
                $"Candidate provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadExecutionUnavailableException exception)
        {
            throw new ApiException(
                ApiErrorCodes.QBittorrentUnavailable,
                $"Download provider '{exception.ProviderCode}' is unavailable.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadExecutionFailedException exception)
        {
            throw new ApiException(
                ApiErrorCodes.QBittorrentEnqueueFailed,
                $"Download provider '{exception.ProviderCode}' failed to enqueue torrent.",
                HttpStatusCode.BadGateway);
        }
    }

    private static IResult ListDownloadJobs(
        long? userId,
        string? status,
        int? page,
        int? pageSize,
        InMemoryApiStore store)
    {
        var normalizedUserId = EnsureUserId(userId);
        var response = store.ListJobs(normalizedUserId, status, page ?? 1, pageSize ?? 20);
        return Results.Ok(response);
    }

    private static IResult GetDownloadJob(long jobId, long? userId, InMemoryApiStore store)
    {
        var normalizedUserId = EnsureUserId(userId);
        var job = store.GetJob(jobId, normalizedUserId);
        if (job is null)
        {
            throw new ApiException(
                ApiErrorCodes.DownloadNotFound,
                "Download job was not found.",
                HttpStatusCode.NotFound);
        }

        return Results.Ok(job);
    }

    private static IResult CancelDownloadJob(long jobId, CancelDownloadJobRequest request, InMemoryApiStore store)
    {
        EnsureUserId(request.UserId);
        var job = store.CancelJob(jobId, request.UserId);
        if (job is null)
        {
            throw new ApiException(
                ApiErrorCodes.DownloadNotFound,
                "Download job was not found.",
                HttpStatusCode.NotFound);
        }

        return Results.Ok(job);
    }

    private static IResult GetShelves(long? userId, InMemoryApiStore store)
    {
        var normalizedUserId = EnsureUserId(userId);
        return Results.Ok(store.ListShelves(normalizedUserId));
    }

    private static IResult CreateShelf(CreateShelfRequest request, InMemoryApiStore store)
    {
        var userId = EnsureUserId(request.UserId);
        EnsureRequired(request.Name, nameof(request.Name));

        var shelf = store.CreateShelf(userId, request.Name.Trim());
        if (shelf is null)
        {
            throw new ApiException(
                ApiErrorCodes.ShelfNameConflict,
                "Shelf name is already used by this user.",
                HttpStatusCode.Conflict);
        }

        return Results.Created($"/api/v1/shelves/{shelf.Id}", new CreateShelfResponse(shelf));
    }

    private static IResult AddBookToShelf(long shelfId, AddBookToShelfRequest request, InMemoryApiStore store)
    {
        var userId = EnsureUserId(request.UserId);
        if (request.BookId <= 0)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "bookId must be greater than zero.",
                HttpStatusCode.BadRequest);
        }

        var result = store.AddBookToShelf(shelfId, userId, request.BookId);
        if (result.IsNotFound)
        {
            throw new ApiException(
                ApiErrorCodes.ShelfNotFound,
                "Shelf was not found.",
                HttpStatusCode.NotFound);
        }

        if (result.IsAlreadyExists)
        {
            throw new ApiException(
                ApiErrorCodes.ShelfBookExists,
                "Book already exists on shelf.",
                HttpStatusCode.Conflict);
        }

        return Results.Ok(new AddBookToShelfResponse(result.Shelf!));
    }

    private static IResult RemoveBookFromShelf(long shelfId, long bookId, long? userId, InMemoryApiStore store)
    {
        var normalizedUserId = EnsureUserId(userId);
        if (bookId <= 0)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "bookId must be greater than zero.",
                HttpStatusCode.BadRequest);
        }

        var removed = store.RemoveBookFromShelf(shelfId, normalizedUserId, bookId);
        if (!removed)
        {
            throw new ApiException(
                ApiErrorCodes.ShelfNotFound,
                "Shelf was not found.",
                HttpStatusCode.NotFound);
        }

        return Results.Ok();
    }

    private static long EnsureUserId(long? userId)
    {
        if (!userId.HasValue || userId.Value <= 0)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "userId must be greater than zero.",
                HttpStatusCode.BadRequest);
        }

        return userId.Value;
    }

    private static string EnsureMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ApiException(
                ApiErrorCodes.MediaTypeRequired,
                "mediaType is required.",
                HttpStatusCode.BadRequest);
        }

        var normalized = mediaType.Trim().ToLowerInvariant();
        if (normalized is not ("text" or "audio"))
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "mediaType must be either text or audio.",
                HttpStatusCode.BadRequest);
        }

        return normalized;
    }

    private static void EnsureRequired(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                $"{argumentName} is required.",
                HttpStatusCode.BadRequest);
        }
    }
}

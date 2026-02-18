using System.Net;
using System.Security.Claims;
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
        library.MapGet(string.Empty, GetLibrary).RequireAuthorization();
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

    private static async Task<IResult> GetLibrary(
        bool? includeArchived,
        string? query,
        string? providerCode,
        string? catalogState,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        ILibraryService libraryService,
        CancellationToken cancellationToken)
    {
        var userId = EnsureUserIdFromClaims(httpContext.User);
        var safePage = !page.HasValue || page.Value < 1 ? 1 : page.Value;
        var safePageSize = !pageSize.HasValue || pageSize.Value is < 1 or > 100 ? 20 : pageSize.Value;
        var includeArchivedValue = includeArchived ?? false;

        try
        {
            var response = await libraryService.ListAsync(
                userId,
                includeArchivedValue,
                query,
                providerCode,
                catalogState,
                safePage,
                safePageSize,
                cancellationToken);

            return Results.Ok(response);
        }
        catch (ArgumentException)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "Invalid library filter argument.",
                HttpStatusCode.BadRequest);
        }
    }

    private static async Task<IResult> ListDownloadJobs(
        long? userId,
        string? status,
        int? page,
        int? pageSize,
        IDownloadJobService downloadJobService,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = EnsureUserId(userId);
        var safePage = !page.HasValue || page.Value < 1 ? 1 : page.Value;
        var safePageSize = !pageSize.HasValue || pageSize.Value is < 1 or > 100 ? 20 : pageSize.Value;

        try
        {
            var response = await downloadJobService.ListAsync(
                normalizedUserId,
                status,
                safePage,
                safePageSize,
                cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "status filter is invalid.",
                HttpStatusCode.BadRequest);
        }
        catch (DownloadExecutionUnavailableException)
        {
            throw new ApiException(
                ApiErrorCodes.QBittorrentStatusFailed,
                "Failed to synchronize download job status from qBittorrent.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadExecutionFailedException)
        {
            throw new ApiException(
                ApiErrorCodes.QBittorrentStatusFailed,
                "Failed to synchronize download job status from qBittorrent.",
                HttpStatusCode.BadGateway);
        }
    }

    private static async Task<IResult> GetDownloadJob(
        long jobId,
        long? userId,
        IDownloadJobService downloadJobService,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = EnsureUserId(userId);

        try
        {
            var job = await downloadJobService.GetAsync(jobId, normalizedUserId, cancellationToken);
            if (job is null)
            {
                throw new ApiException(
                    ApiErrorCodes.DownloadNotFound,
                    "Download job was not found.",
                    HttpStatusCode.NotFound);
            }

            return Results.Ok(job);
        }
        catch (DownloadExecutionUnavailableException)
        {
            throw new ApiException(
                ApiErrorCodes.QBittorrentStatusFailed,
                "Failed to synchronize download job status from qBittorrent.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadExecutionFailedException)
        {
            throw new ApiException(
                ApiErrorCodes.QBittorrentStatusFailed,
                "Failed to synchronize download job status from qBittorrent.",
                HttpStatusCode.BadGateway);
        }
    }

    private static async Task<IResult> CancelDownloadJob(
        long jobId,
        CancelDownloadJobRequest request,
        IDownloadJobService downloadJobService,
        CancellationToken cancellationToken)
    {
        EnsureUserId(request.UserId);

        try
        {
            var job = await downloadJobService.CancelAsync(jobId, request.UserId, cancellationToken);
            return Results.Ok(job);
        }
        catch (DownloadJobNotFoundException)
        {
            throw new ApiException(
                ApiErrorCodes.DownloadNotFound,
                "Download job was not found.",
                HttpStatusCode.NotFound);
        }
        catch (DownloadJobCancelNotAllowedException)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "Cancel is allowed only for queued or downloading jobs.",
                HttpStatusCode.BadRequest);
        }
        catch (DownloadExecutionUnavailableException)
        {
            throw new ApiException(
                ApiErrorCodes.DownloadCancelFailed,
                "qBittorrent cancel operation failed.",
                HttpStatusCode.BadGateway);
        }
        catch (DownloadExecutionFailedException)
        {
            throw new ApiException(
                ApiErrorCodes.DownloadCancelFailed,
                "qBittorrent cancel operation failed.",
                HttpStatusCode.BadGateway);
        }
    }

    private static async Task<IResult> GetShelves(
        long? userId,
        IShelfService shelfService,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = EnsureUserId(userId);
        var response = await shelfService.ListAsync(normalizedUserId, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateShelf(
        CreateShelfRequest request,
        IShelfService shelfService,
        CancellationToken cancellationToken)
    {
        var userId = EnsureUserId(request.UserId);
        EnsureRequired(request.Name, nameof(request.Name));

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

    private static async Task<IResult> AddBookToShelf(
        long shelfId,
        AddBookToShelfRequest request,
        IShelfService shelfService,
        CancellationToken cancellationToken)
    {
        var userId = EnsureUserId(request.UserId);
        if (request.BookId <= 0)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "bookId must be greater than zero.",
                HttpStatusCode.BadRequest);
        }

        var result = await shelfService.AddBookAsync(
            shelfId,
            userId,
            request.BookId,
            cancellationToken);

        if (result.Status == ShelfAddBookResultStatus.NotFound)
        {
            throw new ApiException(
                ApiErrorCodes.ShelfNotFound,
                "Shelf was not found.",
                HttpStatusCode.NotFound);
        }

        if (result.Status == ShelfAddBookResultStatus.AlreadyExists)
        {
            throw new ApiException(
                ApiErrorCodes.ShelfBookExists,
                "Book already exists on shelf.",
                HttpStatusCode.Conflict);
        }

        return Results.Ok(new AddBookToShelfResponse(result.Shelf!));
    }

    private static async Task<IResult> RemoveBookFromShelf(
        long shelfId,
        long bookId,
        long? userId,
        IShelfService shelfService,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = EnsureUserId(userId);
        if (bookId <= 0)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "bookId must be greater than zero.",
                HttpStatusCode.BadRequest);
        }

        var removed = await shelfService.RemoveBookAsync(
            shelfId,
            normalizedUserId,
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

    private static long EnsureUserIdFromClaims(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("userId")
            ?? user.FindFirstValue("sub");

        if (long.TryParse(raw, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        throw new ApiException(
            ApiErrorCodes.InvalidArgument,
            "Authenticated user id claim is missing or invalid.",
            HttpStatusCode.BadRequest);
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

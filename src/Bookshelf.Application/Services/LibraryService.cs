using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Domain.Enums;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Services;

public sealed class LibraryService : ILibraryService
{
    private readonly IBookRepository _bookRepository;
    private readonly IDownloadJobRepository _downloadJobRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDownloadExecutionClient _downloadExecutionClient;

    public LibraryService(
        IBookRepository bookRepository,
        IDownloadJobRepository downloadJobRepository,
        IUnitOfWork unitOfWork,
        IDownloadExecutionClient downloadExecutionClient)
    {
        _bookRepository = bookRepository;
        _downloadJobRepository = downloadJobRepository;
        _unitOfWork = unitOfWork;
        _downloadExecutionClient = downloadExecutionClient;
    }

    public async Task<LibraryResponse> ListAsync(
        long userId,
        bool includeArchived,
        string? query,
        string? providerCode,
        string? catalogState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        _ = userId;
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var normalizedQuery = NormalizeOptional(query);
        var normalizedProviderCode = NormalizeOptional(providerCode);
        var stateFilter = ParseCatalogState(catalogState);

        var total = await _bookRepository.CountLibraryAsync(
            includeArchived,
            normalizedQuery,
            normalizedProviderCode,
            stateFilter,
            cancellationToken);

        var books = await _bookRepository.ListLibraryAsync(
            includeArchived,
            normalizedQuery,
            normalizedProviderCode,
            stateFilter,
            safePage,
            safePageSize,
            cancellationToken);

        return new LibraryResponse(
            Page: safePage,
            PageSize: safePageSize,
            Total: total,
            IncludeArchived: includeArchived,
            Items: books.Select(x => new LibraryBookDto(
                Id: x.Id,
                ProviderCode: x.ProviderCode,
                ProviderBookKey: x.ProviderBookKey,
                Title: x.Title,
                OriginalTitle: x.OriginalTitle,
                Description: x.Description,
                PublishYear: x.PublishYear,
                LanguageCode: x.LanguageCode,
                CoverUrl: x.CoverUrl,
                HasTextMedia: x.MediaAssets.Any(asset => asset.MediaType == MediaType.Text && asset.Status == MediaAssetStatus.Available),
                HasAudioMedia: x.MediaAssets.Any(asset => asset.MediaType == MediaType.Audio && asset.Status == MediaAssetStatus.Available),
                CatalogState: x.CatalogState.ToString().ToLowerInvariant(),
                CreatedAtUtc: x.CreatedAtUtc,
                UpdatedAtUtc: x.UpdatedAtUtc))
            .ToArray());
    }

    public async Task ArchiveAsync(
        long userId,
        long bookId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("userId must be greater than zero.", nameof(userId));
        }

        if (bookId <= 0)
        {
            throw new ArgumentException("bookId must be greater than zero.", nameof(bookId));
        }

        var book = await _bookRepository.GetByIdAsync(bookId, cancellationToken);
        if (book is null)
        {
            throw new BookIdNotFoundException(bookId);
        }

        var jobs = await _downloadJobRepository.ListByUserAndBookAsync(userId, bookId, cancellationToken);

        var externalJobIds = jobs
            .Select(x => x.ExternalJobId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var externalJobId in externalJobIds)
        {
            var matchingJob = jobs.FirstOrDefault(x =>
                string.Equals(x.ExternalJobId, externalJobId, StringComparison.OrdinalIgnoreCase));
            if (matchingJob is null ||
                !matchingJob.ExecutionProvider.Equals("qbittorrent", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await _downloadExecutionClient.CancelAsync(
                externalJobId,
                deleteFiles: true,
                cancellationToken);
        }

        var nowUtc = DateTimeOffset.UtcNow;
        foreach (var mediaAsset in book.MediaAssets)
        {
            DeleteStorageBestEffort(mediaAsset.StoragePath);
            mediaAsset.MarkDeleted(MediaAssetStatus.Deleted, nowUtc);
        }

        book.RecomputeCatalogState();
        book.Touch(nowUtc);
        _bookRepository.Update(book);

        foreach (var job in jobs.Where(x => x.Status is DownloadJobStatus.Queued or DownloadJobStatus.Downloading))
        {
            job.TransitionTo(DownloadJobStatus.Canceled, nowUtc);
            _downloadJobRepository.Update(job);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static CatalogState? ParseCatalogState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "archive" => CatalogState.Archive,
            "library" => CatalogState.Library,
            _ => throw new ArgumentException("Invalid catalogState filter.", nameof(value)),
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static void DeleteStorageBestEffort(string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return;
        }

        var normalizedPath = storagePath.Trim();
        try
        {
            if (File.Exists(normalizedPath))
            {
                File.Delete(normalizedPath);
                return;
            }

            if (Directory.Exists(normalizedPath))
            {
                Directory.Delete(normalizedPath, recursive: true);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }
}

using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Services;

public sealed class AddAndDownloadService : IAddAndDownloadService
{
    private const string JackettSourceProvider = "jackett";

    private readonly IBookSearchService _bookSearchService;
    private readonly ICandidateDiscoveryService _candidateDiscoveryService;
    private readonly IBookRepository _bookRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDownloadJobRepository _downloadJobRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDownloadExecutionClient _downloadExecutionClient;

    public AddAndDownloadService(
        IBookSearchService bookSearchService,
        ICandidateDiscoveryService candidateDiscoveryService,
        IBookRepository bookRepository,
        IUserRepository userRepository,
        IDownloadJobRepository downloadJobRepository,
        IUnitOfWork unitOfWork,
        IDownloadExecutionClient downloadExecutionClient)
    {
        _bookSearchService = bookSearchService;
        _candidateDiscoveryService = candidateDiscoveryService;
        _bookRepository = bookRepository;
        _userRepository = userRepository;
        _downloadJobRepository = downloadJobRepository;
        _unitOfWork = unitOfWork;
        _downloadExecutionClient = downloadExecutionClient;
    }

    public async Task<AddAndDownloadResponse> ExecuteAsync(
        AddAndDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerCode = request.ProviderCode.Trim();
        var providerBookKey = request.ProviderBookKey.Trim();
        var candidateId = request.CandidateId.Trim();
        var mediaTypeText = request.MediaType.Trim().ToLowerInvariant();
        var mediaType = ParseMediaType(mediaTypeText);

        var details = await _bookSearchService.GetDetailsAsync(
            providerCode,
            providerBookKey,
            cancellationToken);
        if (details is null)
        {
            throw new BookNotFoundException(providerCode, providerBookKey);
        }

        var candidate = await _candidateDiscoveryService.ResolveAsync(
            providerCode,
            providerBookKey,
            mediaTypeText,
            candidateId,
            cancellationToken);
        if (candidate is null)
        {
            throw new DownloadCandidateNotFoundException(candidateId);
        }

        var book = await _bookRepository.GetByProviderKeyAsync(
            providerCode,
            providerBookKey,
            cancellationToken);
        if (book is not null)
        {
            var existingActive = await _downloadJobRepository.GetActiveAsync(
                request.UserId,
                book.Id,
                mediaType,
                cancellationToken);
            if (existingActive is not null)
            {
                return BuildResponse(book, existingActive);
            }
        }
        else
        {
            book = new Book(providerCode, providerBookKey, details.Title);
            await _bookRepository.AddAsync(book, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await UpsertBookMetadataAsync(book, details, cancellationToken);
        EnsureMediaSlot(book, mediaType, candidate.SourceUrl);
        book.RecomputeCatalogState();
        _bookRepository.Update(book);

        await _userRepository.EnsureExistsAsync(request.UserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var activeAfterMetadata = await _downloadJobRepository.GetActiveAsync(
            request.UserId,
            book.Id,
            mediaType,
            cancellationToken);
        if (activeAfterMetadata is not null)
        {
            return BuildResponse(book, activeAfterMetadata);
        }

        var job = new DownloadJob(
            request.UserId,
            book.Id,
            mediaType,
            candidate.SourceUrl,
            candidate.DownloadUri);

        await _downloadJobRepository.AddAsync(job, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            var activeByRace = await _downloadJobRepository.GetActiveAsync(
                request.UserId,
                book.Id,
                mediaType,
                cancellationToken);
            if (activeByRace is not null)
            {
                return BuildResponse(book, activeByRace);
            }

            throw;
        }

        try
        {
            var enqueue = await _downloadExecutionClient.EnqueueAsync(
                candidate.DownloadUri,
                cancellationToken);

            var nowUtc = DateTimeOffset.UtcNow;
            job.SetExternalJobId(enqueue.ExternalJobId, nowUtc);
            job.TransitionTo(DownloadJobStatus.Downloading, nowUtc);
            _downloadJobRepository.Update(job);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DownloadExecutionUnavailableException)
        {
            await MarkJobFailedAsync(job, "enqueue_unavailable", cancellationToken);
            throw;
        }
        catch (DownloadExecutionFailedException)
        {
            await MarkJobFailedAsync(job, "enqueue_failed", cancellationToken);
            throw;
        }

        return BuildResponse(book, job);
    }

    private async Task UpsertBookMetadataAsync(
        Book book,
        SearchBookDetailsResponse details,
        CancellationToken cancellationToken)
    {
        book.UpdateMetadata(
            details.Title,
            details.OriginalTitle,
            details.Description,
            details.PublishYear,
            languageCode: null,
            details.CoverUrl);

        var desiredAuthorNames = details.Authors
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var relation in book.BookAuthors.ToList())
        {
            var authorName = relation.Author?.Name ?? string.Empty;
            if (!desiredAuthorNames.Contains(authorName, StringComparer.OrdinalIgnoreCase))
            {
                book.BookAuthors.Remove(relation);
            }
        }

        foreach (var authorName in desiredAuthorNames)
        {
            if (book.BookAuthors.Any(x =>
                    string.Equals(x.Author?.Name, authorName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var author = await _bookRepository.GetAuthorByNameAsync(authorName, cancellationToken);
            if (author is null)
            {
                author = new Author(authorName);
                await _bookRepository.AddAuthorAsync(author, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            book.BookAuthors.Add(new BookAuthor(book.Id, author.Id));
        }

        if (details.Series is null)
        {
            foreach (var relation in book.SeriesBooks.ToList())
            {
                book.SeriesBooks.Remove(relation);
            }

            _bookRepository.Update(book);
            return;
        }

        var series = await _bookRepository.GetSeriesByProviderKeyAsync(
            details.ProviderCode,
            details.Series.ProviderSeriesKey,
            cancellationToken);
        if (series is null)
        {
            series = new Series(
                details.ProviderCode,
                details.Series.ProviderSeriesKey,
                details.Series.Title);
            await _bookRepository.AddSeriesAsync(series, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var hasExactRelation = book.SeriesBooks.Any(
            x => x.SeriesId == series.Id && x.SeriesOrder == details.Series.Order);

        foreach (var relation in book.SeriesBooks.ToList())
        {
            if (relation.SeriesId == series.Id && relation.SeriesOrder == details.Series.Order)
            {
                continue;
            }

            book.SeriesBooks.Remove(relation);
        }

        if (!hasExactRelation)
        {
            book.SeriesBooks.Add(new SeriesBook(series.Id, book.Id, details.Series.Order));
        }

        _bookRepository.Update(book);
    }

    private static void EnsureMediaSlot(Book book, MediaType mediaType, string sourceUrl)
    {
        var existing = book.MediaAssets.SingleOrDefault(x => x.MediaType == mediaType);
        var asset = book.UpsertMediaAsset(mediaType, sourceUrl, JackettSourceProvider);
        if (existing is null)
        {
            asset.MarkDeleted(MediaAssetStatus.Missing, DateTimeOffset.UtcNow);
        }
    }

    private async Task MarkJobFailedAsync(
        DownloadJob job,
        string reason,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        job.TransitionTo(DownloadJobStatus.Failed, nowUtc, reason);
        _downloadJobRepository.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static AddAndDownloadResponse BuildResponse(Book book, DownloadJob job)
    {
        return new AddAndDownloadResponse(
            BookId: book.Id,
            BookState: book.CatalogState.ToString().ToLowerInvariant(),
            DownloadJob: new DownloadJobSummaryDto(
                Id: job.Id,
                Status: job.Status.ToString().ToLowerInvariant(),
                ExternalJobId: job.ExternalJobId,
                CreatedAtUtc: job.CreatedAtUtc));
    }

    private static MediaType ParseMediaType(string mediaType)
    {
        return mediaType switch
        {
            "text" => MediaType.Text,
            "audio" => MediaType.Audio,
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType), "Unsupported media type."),
        };
    }
}

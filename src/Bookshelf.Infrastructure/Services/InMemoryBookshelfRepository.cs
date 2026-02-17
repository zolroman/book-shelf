using Bookshelf.Domain;
using Bookshelf.Domain.Abstractions;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Bookshelf.Infrastructure.Models;

namespace Bookshelf.Infrastructure.Services;

public sealed class InMemoryBookshelfRepository : IBookshelfRepository
{
    private readonly IClock _clock;
    private readonly object _syncRoot = new();

    private readonly List<User> _users = [];
    private readonly List<Book> _books = [];
    private readonly List<Author> _authors = [];
    private readonly List<BookFormat> _bookFormats = [];
    private readonly List<LibraryItem> _libraryItems = [];
    private readonly List<ProgressSnapshot> _progressSnapshots = [];
    private readonly List<HistoryEvent> _historyEvents = [];
    private readonly List<DownloadJob> _downloadJobs = [];
    private readonly List<LocalAsset> _localAssets = [];

    private int _nextBookId;
    private int _nextAuthorId;
    private int _nextBookFormatId;
    private int _nextLibraryItemId;
    private int _nextProgressSnapshotId;
    private int _nextHistoryEventId;
    private int _nextDownloadJobId;
    private int _nextLocalAssetId;

    public InMemoryBookshelfRepository(IClock clock)
    {
        _clock = clock;
        Seed();
    }

    public Task<User?> GetUserAsync(int userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_users.SingleOrDefault(x => x.Id == userId));
        }
    }

    public Task<IReadOnlyList<Book>> GetBooksAsync(string? query, string? author, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            IEnumerable<Book> books = _books;

            if (!string.IsNullOrWhiteSpace(query))
            {
                var normalizedQuery = query.Trim();
                books = books.Where(book =>
                    book.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                    book.OriginalTitle.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(author))
            {
                var normalizedAuthor = author.Trim();
                var matchedBookIds = _authors
                    .Where(item => item.Name.Contains(normalizedAuthor, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(item => item.Books.Select(book => book.Id))
                    .Distinct()
                    .ToHashSet();
                books = books.Where(book => matchedBookIds.Contains(book.Id));
            }

            return Task.FromResult<IReadOnlyList<Book>>(books.OrderBy(x => x.Title).ToList());
        }
    }

    public Task<Book?> GetBookAsync(int bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_books.SingleOrDefault(x => x.Id == bookId));
        }
    }

    public Task<Book> UpsertImportedBookAsync(ImportedBookSeed seed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            if (string.IsNullOrWhiteSpace(seed.Title))
            {
                throw new ArgumentException("Book title is required for import.", nameof(seed));
            }

            var normalizedTitle = seed.Title.Trim();
            var normalizedOriginalTitle = string.IsNullOrWhiteSpace(seed.OriginalTitle)
                ? normalizedTitle
                : seed.OriginalTitle.Trim();

            var existing = _books.FirstOrDefault(book =>
                book.Title.Equals(normalizedTitle, StringComparison.OrdinalIgnoreCase) ||
                book.OriginalTitle.Equals(normalizedOriginalTitle, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                existing = new Book
                {
                    Id = _nextBookId++,
                    Title = normalizedTitle,
                    OriginalTitle = normalizedOriginalTitle,
                    PublishYear = seed.PublishYear,
                    CommunityRating = seed.CommunityRating,
                    Description = seed.Description ?? string.Empty,
                    CoverUrl = seed.CoverUrl ?? string.Empty,
                    CreatedAtUtc = _clock.UtcNow
                };
                _books.Add(existing);
            }
            else
            {
                existing.Title = PreferValue(seed.Title, existing.Title);
                existing.OriginalTitle = PreferValue(seed.OriginalTitle, existing.OriginalTitle);
                existing.Description = PreferValue(seed.Description, existing.Description);
                existing.CoverUrl = PreferValue(seed.CoverUrl, existing.CoverUrl);
                existing.PublishYear ??= seed.PublishYear;
                existing.CommunityRating ??= seed.CommunityRating;
            }

            foreach (var authorName in seed.Authors
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Select(name => name.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var author = _authors.FirstOrDefault(entity =>
                    entity.Name.Equals(authorName, StringComparison.OrdinalIgnoreCase));
                if (author is null)
                {
                    author = new Author
                    {
                        Id = _nextAuthorId++,
                        Name = authorName
                    };
                    _authors.Add(author);
                }

                if (existing.Authors.All(a => a.Id != author.Id))
                {
                    existing.Authors.Add(author);
                }

                if (author.Books.All(book => book.Id != existing.Id))
                {
                    author.Books.Add(existing);
                }
            }

            var ensureText = seed.HasText || (!seed.HasText && !seed.HasAudio);
            var ensureAudio = seed.HasAudio;

            if (ensureText && _bookFormats.All(x => x.BookId != existing.Id || x.FormatType != BookFormatType.Text))
            {
                _bookFormats.Add(new BookFormat
                {
                    Id = _nextBookFormatId++,
                    BookId = existing.Id,
                    FormatType = BookFormatType.Text,
                    Language = "en",
                    FileSizeBytes = 0,
                    Checksum = $"imported-{existing.Id}-text"
                });
            }

            if (ensureAudio && _bookFormats.All(x => x.BookId != existing.Id || x.FormatType != BookFormatType.Audio))
            {
                _bookFormats.Add(new BookFormat
                {
                    Id = _nextBookFormatId++,
                    BookId = existing.Id,
                    FormatType = BookFormatType.Audio,
                    Language = "en",
                    DurationSeconds = 0,
                    FileSizeBytes = 0,
                    Checksum = $"imported-{existing.Id}-audio"
                });
            }

            return Task.FromResult(existing);
        }
    }

    public Task<IReadOnlyList<Author>> GetAuthorsForBookAsync(int bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<Author>>(_books.SingleOrDefault(b => b.Id == bookId)?.Authors
                .OrderBy(a => a.Name).ToList() ?? []);
        }
    }

    public Task<IReadOnlyList<BookFormat>> GetFormatsForBookAsync(int bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var formats = _bookFormats.Where(x => x.BookId == bookId).OrderBy(x => x.FormatType).ToList();
            return Task.FromResult<IReadOnlyList<BookFormat>>(formats);
        }
    }

    public Task<BookFormat?> GetBookFormatAsync(int bookFormatId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_bookFormats.SingleOrDefault(x => x.Id == bookFormatId));
        }
    }

    public Task<IReadOnlyList<LibraryItem>> GetLibraryItemsAsync(int userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var items = _libraryItems
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.AddedAtUtc)
                .ToList();
            return Task.FromResult<IReadOnlyList<LibraryItem>>(items);
        }
    }

    public Task<LibraryItem?> GetLibraryItemAsync(int userId, int bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_libraryItems.SingleOrDefault(x => x.UserId == userId && x.BookId == bookId));
        }
    }

    public Task<LibraryItem> AddLibraryItemAsync(int userId, int bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            _ = _users.Single(x => x.Id == userId);
            _ = _books.Single(x => x.Id == bookId);

            var existing = _libraryItems.SingleOrDefault(x => x.UserId == userId && x.BookId == bookId);
            if (existing is not null)
            {
                return Task.FromResult(existing);
            }

            var entity = new LibraryItem
            {
                Id = _nextLibraryItemId++,
                UserId = userId,
                BookId = bookId,
                AddedAtUtc = _clock.UtcNow
            };
            _libraryItems.Add(entity);
            return Task.FromResult(entity);
        }
    }

    public Task<bool> RemoveLibraryItemAsync(int userId, int bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var existing = _libraryItems.SingleOrDefault(x => x.UserId == userId && x.BookId == bookId);
            if (existing is null)
            {
                return Task.FromResult(false);
            }

            _libraryItems.Remove(existing);
            return Task.FromResult(true);
        }
    }

    public Task<ProgressSnapshot?> GetProgressSnapshotAsync(
        int userId,
        int bookId,
        BookFormatType formatType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_progressSnapshots.SingleOrDefault(x =>
                x.UserId == userId &&
                x.BookId == bookId &&
                x.FormatType == formatType));
        }
    }

    public Task<ProgressSnapshot> UpsertProgressSnapshotAsync(
        int userId,
        int bookId,
        BookFormatType formatType,
        string positionRef,
        float progressPercent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            DomainGuards.RequirePercent(progressPercent, nameof(progressPercent));

            var entity = _progressSnapshots.SingleOrDefault(x =>
                x.UserId == userId &&
                x.BookId == bookId &&
                x.FormatType == formatType);

            if (entity is null)
            {
                entity = new ProgressSnapshot
                {
                    Id = _nextProgressSnapshotId++,
                    UserId = userId,
                    BookId = bookId,
                    FormatType = formatType
                };
                _progressSnapshots.Add(entity);
            }

            entity.Update(positionRef, progressPercent, _clock.UtcNow);
            return Task.FromResult(entity);
        }
    }

    public Task<HistoryEvent> AddHistoryEventAsync(
        int userId,
        int bookId,
        BookFormatType formatType,
        HistoryEventType eventType,
        string positionRef,
        DateTime eventAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var entity = new HistoryEvent
            {
                Id = _nextHistoryEventId++,
                UserId = userId,
                BookId = bookId,
                FormatType = formatType,
                EventType = eventType,
                PositionRef = positionRef,
                EventAtUtc = eventAtUtc
            };
            _historyEvents.Add(entity);
            return Task.FromResult(entity);
        }
    }

    public Task<IReadOnlyList<HistoryEvent>> GetHistoryEventsAsync(
        int userId,
        int? bookId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            IEnumerable<HistoryEvent> events = _historyEvents.Where(x => x.UserId == userId);
            if (bookId.HasValue)
            {
                events = events.Where(x => x.BookId == bookId.Value);
            }

            var history = events.OrderByDescending(x => x.EventAtUtc).ToList();
            return Task.FromResult<IReadOnlyList<HistoryEvent>>(history);
        }
    }

    public Task<IReadOnlyList<DownloadJob>> GetDownloadJobsAsync(int userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var jobs = _downloadJobs
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList();
            return Task.FromResult<IReadOnlyList<DownloadJob>>(jobs);
        }
    }

    public Task<DownloadJob?> GetDownloadJobAsync(int jobId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_downloadJobs.SingleOrDefault(x => x.Id == jobId));
        }
    }

    public Task<DownloadJob?> GetActiveDownloadJobAsync(
        int userId,
        int bookFormatId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var existing = _downloadJobs.SingleOrDefault(x =>
                x.UserId == userId &&
                x.BookFormatId == bookFormatId &&
                x.Status is DownloadJobStatus.Queued or DownloadJobStatus.Downloading);
            return Task.FromResult(existing);
        }
    }

    public Task<DownloadJob> CreateDownloadJobAsync(
        int userId,
        int bookFormatId,
        string source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            _ = _users.Single(x => x.Id == userId);
            _ = _bookFormats.Single(x => x.Id == bookFormatId);

            var existing = _downloadJobs.SingleOrDefault(x =>
                x.UserId == userId &&
                x.BookFormatId == bookFormatId &&
                x.Status is DownloadJobStatus.Queued or DownloadJobStatus.Downloading);
            if (existing is not null)
            {
                return Task.FromResult(existing);
            }

            var entity = new DownloadJob
            {
                Id = _nextDownloadJobId++,
                UserId = userId,
                BookFormatId = bookFormatId,
                Source = source,
                CreatedAtUtc = _clock.UtcNow
            };
            _downloadJobs.Add(entity);
            return Task.FromResult(entity);
        }
    }

    public Task<DownloadJob> UpdateDownloadJobExternalIdAsync(
        int jobId,
        string externalJobId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var entity = _downloadJobs.SingleOrDefault(x => x.Id == jobId)
                         ?? throw new ArgumentException($"Download job {jobId} not found.");
            entity.ExternalJobId = externalJobId;
            return Task.FromResult(entity);
        }
    }

    public Task<DownloadJob> UpdateDownloadJobStatusAsync(
        int jobId,
        DownloadJobStatus status,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var entity = _downloadJobs.SingleOrDefault(x => x.Id == jobId)
                         ?? throw new ArgumentException($"Download job {jobId} not found.");
            if (entity.Status == status)
            {
                return Task.FromResult(entity);
            }

            entity.TransitionTo(status, _clock.UtcNow);
            return Task.FromResult(entity);
        }
    }

    public Task<IReadOnlyList<LocalAsset>> GetLocalAssetsAsync(int userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var items = _localAssets.Where(x => x.UserId == userId).OrderByDescending(x => x.DownloadedAtUtc).ToList();
            return Task.FromResult<IReadOnlyList<LocalAsset>>(items);
        }
    }

    public Task<LocalAsset> AddOrUpdateLocalAssetAsync(
        int userId,
        int bookFormatId,
        string localPath,
        long fileSizeBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var entity = _localAssets.SingleOrDefault(x => x.UserId == userId && x.BookFormatId == bookFormatId);
            if (entity is null)
            {
                entity = new LocalAsset
                {
                    Id = _nextLocalAssetId++,
                    UserId = userId,
                    BookFormatId = bookFormatId
                };
                _localAssets.Add(entity);
            }

            entity.LocalPath = localPath;
            entity.FileSizeBytes = fileSizeBytes;
            entity.DownloadedAtUtc = _clock.UtcNow;
            entity.Restore();
            return Task.FromResult(entity);
        }
    }

    public Task<bool> MarkLocalAssetDeletedAsync(int userId, int bookFormatId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            // Retention guarantee: deleting file state must never cascade into library/progress/history data.
            var entity = _localAssets.SingleOrDefault(x => x.UserId == userId && x.BookFormatId == bookFormatId);
            if (entity is null)
            {
                return Task.FromResult(false);
            }

            entity.MarkDeleted(_clock.UtcNow);
            return Task.FromResult(true);
        }
    }

    private void Seed()
    {
        var utcNow = _clock.UtcNow;
        _users.Add(new User
        {
            Id = 1,
            Login = "demo",
            DisplayName = "Demo User",
            CreatedAtUtc = utcNow
        });

        _authors.AddRange(
        [
            new Author { Id = 1, Name = "Andy Weir" },
            new Author { Id = 2, Name = "Frank Herbert" },
            new Author { Id = 3, Name = "Arthur Conan Doyle" }
        ]);

        _books.AddRange(
        [
            new Book
            {
                Id = 1,
                Title = "The Martian",
                OriginalTitle = "The Martian",
                PublishYear = 2011,
                Description = "Sci-fi survival story about an astronaut stranded on Mars.",
                CoverUrl = "https://example.com/covers/martian.jpg",
                CommunityRating = 8.5f,
                CreatedAtUtc = utcNow
            },
            new Book
            {
                Id = 2,
                Title = "Dune",
                OriginalTitle = "Dune",
                PublishYear = 1965,
                Description = "Classic epic science fiction novel.",
                CoverUrl = "https://example.com/covers/dune.jpg",
                CommunityRating = 9.2f,
                CreatedAtUtc = utcNow
            },
            new Book
            {
                Id = 3,
                Title = "The Hound of the Baskervilles",
                OriginalTitle = "The Hound of the Baskervilles",
                PublishYear = 1902,
                Description = "Detective story featuring Sherlock Holmes.",
                CoverUrl = "https://example.com/covers/hound.jpg",
                CommunityRating = 8.1f,
                CreatedAtUtc = utcNow
            }
        ]);

        for (var i = 0; i < _authors.Count; i++)
        {
            _authors[i].Books = new List<Book> { _books[i] };
            _books[i].Authors = new List<Author> { _authors[i] };
        }
        
        _bookFormats.AddRange(
        [
            new BookFormat
            {
                Id = 1,
                BookId = 1,
                FormatType = BookFormatType.Text,
                Language = "en",
                FileSizeBytes = 1_200_000,
                Checksum = "martian-text-sha1"
            },
            new BookFormat
            {
                Id = 2,
                BookId = 1,
                FormatType = BookFormatType.Audio,
                Language = "en",
                DurationSeconds = 37_200,
                FileSizeBytes = 520_000_000,
                Checksum = "martian-audio-sha1"
            },
            new BookFormat
            {
                Id = 3,
                BookId = 2,
                FormatType = BookFormatType.Text,
                Language = "en",
                FileSizeBytes = 1_600_000,
                Checksum = "dune-text-sha1"
            },
            new BookFormat
            {
                Id = 4,
                BookId = 3,
                FormatType = BookFormatType.Audio,
                Language = "en",
                DurationSeconds = 25_000,
                FileSizeBytes = 300_000_000,
                Checksum = "hound-audio-sha1"
            }
        ]);

        _nextBookId = _books.Max(x => x.Id) + 1;
        _nextAuthorId = _authors.Max(x => x.Id) + 1;
        _nextBookFormatId = _bookFormats.Max(x => x.Id) + 1;
        _nextLibraryItemId = 1;
        _nextProgressSnapshotId = 1;
        _nextHistoryEventId = 1;
        _nextDownloadJobId = 1;
        _nextLocalAssetId = 1;
    }

    private static string PreferValue(string? candidate, string existing)
    {
        return string.IsNullOrWhiteSpace(candidate) ? existing : candidate.Trim();
    }
}

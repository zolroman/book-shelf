using Bookshelf.Domain;
using Bookshelf.Domain.Abstractions;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

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
    private readonly List<LocalAsset> _localAssets = [];

    private int _nextLibraryItemId;
    private int _nextProgressSnapshotId;
    private int _nextHistoryEventId;
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

        _nextLibraryItemId = 1;
        _nextProgressSnapshotId = 1;
        _nextHistoryEventId = 1;
        _nextLocalAssetId = 1;
    }
}
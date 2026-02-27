using System.Reflection;
using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Exceptions;
using Bookshelf.Application.Services;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Tests;

public class BookRatingServiceTests
{
    [Fact]
    public async Task UpsertAsync_CreatesNewRating_WhenMissing()
    {
        var fixture = CreateFixture(bookExists: true);

        var response = await fixture.Service.UpsertAsync(userId: 7, bookId: 42, rating: 4);

        Assert.Equal(7, response.UserId);
        Assert.Equal(42, response.BookId);
        Assert.Equal(4, response.Rating);
        Assert.Single(fixture.BookRatingRepository.Ratings);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCalls);
        Assert.Contains(7, fixture.UserRepository.EnsuredUserIds);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingRating()
    {
        var fixture = CreateFixture(bookExists: true);
        await fixture.BookRatingRepository.AddAsync(new BookRating(7, 42, 5));

        var response = await fixture.Service.UpsertAsync(userId: 7, bookId: 42, rating: 2);

        Assert.Equal(2, response.Rating);
        Assert.Single(fixture.BookRatingRepository.Ratings);
        Assert.Equal(2, fixture.BookRatingRepository.Ratings[(7, 42)].Rating);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRating_WhenExists()
    {
        var fixture = CreateFixture(bookExists: true);
        await fixture.BookRatingRepository.AddAsync(new BookRating(7, 42, 3));

        await fixture.Service.DeleteAsync(userId: 7, bookId: 42);

        Assert.Empty(fixture.BookRatingRepository.Ratings);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task UpsertAsync_InvalidRating_Throws()
    {
        var fixture = CreateFixture(bookExists: true);

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await fixture.Service.UpsertAsync(userId: 7, bookId: 42, rating: 7));
    }

    [Fact]
    public async Task DeleteAsync_MissingBook_ThrowsBookIdNotFound()
    {
        var fixture = CreateFixture(bookExists: false);

        await Assert.ThrowsAsync<BookIdNotFoundException>(
            async () => await fixture.Service.DeleteAsync(userId: 7, bookId: 42));
    }

    private static TestFixture CreateFixture(bool bookExists)
    {
        var bookRepository = new FakeBookRepository();
        if (bookExists)
        {
            var book = new Book("fantlab", "42", "Dune");
            SetProperty(book, nameof(Book.Id), 42L);
            bookRepository.Books.Add(book);
        }

        var fixture = new TestFixture(
            new FakeBookRatingRepository(),
            bookRepository,
            new FakeUserRepository(),
            new FakeUnitOfWork());
        return fixture;
    }

    private static void SetProperty<T>(T entity, string propertyName, object? value)
    {
        var property = typeof(T).GetProperty(
            propertyName,
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);
        if (property is null)
        {
            throw new InvalidOperationException($"Property {propertyName} was not found.");
        }

        property.SetValue(entity, value);
    }

    private sealed class TestFixture
    {
        public TestFixture(
            FakeBookRatingRepository bookRatingRepository,
            FakeBookRepository bookRepository,
            FakeUserRepository userRepository,
            FakeUnitOfWork unitOfWork)
        {
            BookRatingRepository = bookRatingRepository;
            BookRepository = bookRepository;
            UserRepository = userRepository;
            UnitOfWork = unitOfWork;
            Service = new BookRatingService(
                bookRatingRepository,
                bookRepository,
                userRepository,
                unitOfWork);
        }

        public BookRatingService Service { get; }

        public FakeBookRatingRepository BookRatingRepository { get; }

        public FakeBookRepository BookRepository { get; }

        public FakeUserRepository UserRepository { get; }

        public FakeUnitOfWork UnitOfWork { get; }
    }

    private sealed class FakeBookRatingRepository : IBookRatingRepository
    {
        public Dictionary<(long UserId, long BookId), BookRating> Ratings { get; } = [];

        public Task<BookRating?> GetAsync(
            long userId,
            long bookId,
            CancellationToken cancellationToken = default)
        {
            Ratings.TryGetValue((userId, bookId), out var rating);
            return Task.FromResult(rating);
        }

        public Task AddAsync(BookRating rating, CancellationToken cancellationToken = default)
        {
            Ratings[(rating.UserId, rating.BookId)] = rating;
            return Task.CompletedTask;
        }

        public void Update(BookRating rating)
        {
            Ratings[(rating.UserId, rating.BookId)] = rating;
        }

        public void Remove(BookRating rating)
        {
            Ratings.Remove((rating.UserId, rating.BookId));
        }
    }

    private sealed class FakeBookRepository : IBookRepository
    {
        public List<Book> Books { get; } = [];

        public Task<Book?> GetByIdAsync(long bookId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Books.SingleOrDefault(x => x.Id == bookId));
        }

        public Task<Book?> GetByProviderKeyAsync(
            string providerCode,
            string providerBookKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Books.SingleOrDefault(x =>
                x.ProviderCode.Equals(providerCode, StringComparison.OrdinalIgnoreCase) &&
                x.ProviderBookKey.Equals(providerBookKey, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<Author?> GetAuthorByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAuthorAsync(Author author, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Series?> GetSeriesByProviderKeyAsync(
            string providerCode,
            string providerSeriesKey,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddSeriesAsync(Series series, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Book>> ListLibraryAsync(
            bool includeArchived,
            string? query,
            string? providerCode,
            CatalogState? catalogState,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountLibraryAsync(
            bool includeArchived,
            string? query,
            string? providerCode,
            CatalogState? catalogState,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Book book, CancellationToken cancellationToken = default)
        {
            Books.Add(book);
            return Task.CompletedTask;
        }

        public void Update(Book book)
        {
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public HashSet<long> EnsuredUserIds { get; } = [];

        public Task EnsureExistsAsync(long userId, CancellationToken cancellationToken = default)
        {
            EnsuredUserIds.Add(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }
}

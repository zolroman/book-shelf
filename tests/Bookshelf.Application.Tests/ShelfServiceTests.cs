using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Services;
using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Tests;

public class ShelfServiceTests
{
    [Fact]
    public async Task ListAsync_MapsShelves()
    {
        var shelfRepository = new FakeShelfRepository();
        var shelf = new Shelf(7, "Sci-Fi");
        SetProperty(shelf, nameof(Shelf.Id), 100L);
        shelf.AddBook(20);
        shelf.AddBook(10);
        shelfRepository.Seed(shelf);

        var service = new ShelfService(shelfRepository, new FakeUserRepository(), new FakeUnitOfWork());

        var response = await service.ListAsync(7);

        var item = Assert.Single(response.Items);
        Assert.Equal(100L, item.Id);
        Assert.Equal("Sci-Fi", item.Name);
        Assert.Equal(new long[] { 10, 20 }, item.BookIds);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsNull()
    {
        var shelfRepository = new FakeShelfRepository();
        shelfRepository.Seed(new Shelf(9, "Favorites"));
        var userRepository = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ShelfService(shelfRepository, userRepository, unitOfWork);

        var result = await service.CreateAsync(9, " favorites ");

        Assert.Null(result);
        Assert.Empty(userRepository.EnsuredUserIds);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_NewShelf_PersistsAndEnsuresUser()
    {
        var shelfRepository = new FakeShelfRepository();
        var userRepository = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ShelfService(shelfRepository, userRepository, unitOfWork);

        var created = await service.CreateAsync(11, "  To Read ");

        Assert.NotNull(created);
        Assert.Equal("To Read", created!.Name);
        Assert.Contains(11L, userRepository.EnsuredUserIds);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Single(shelfRepository.Shelves);
    }

    [Fact]
    public async Task AddBookAsync_ReturnsNotFound_WhenShelfMissingOrOwnedByAnotherUser()
    {
        var shelfRepository = new FakeShelfRepository();
        var service = new ShelfService(shelfRepository, new FakeUserRepository(), new FakeUnitOfWork());

        var missing = await service.AddBookAsync(10, 1, 42);
        Assert.Equal(ShelfAddBookResultStatus.NotFound, missing.Status);

        var shelf = new Shelf(2, "Sci-Fi");
        SetProperty(shelf, nameof(Shelf.Id), 10L);
        shelfRepository.Seed(shelf);
        var wrongUser = await service.AddBookAsync(10, 1, 42);
        Assert.Equal(ShelfAddBookResultStatus.NotFound, wrongUser.Status);
    }

    [Fact]
    public async Task AddBookAsync_ReturnsAlreadyExists_WhenBookAlreadyOnShelf()
    {
        var shelfRepository = new FakeShelfRepository();
        var shelf = new Shelf(3, "Sci-Fi");
        SetProperty(shelf, nameof(Shelf.Id), 15L);
        shelf.AddBook(42);
        shelfRepository.Seed(shelf);
        var service = new ShelfService(shelfRepository, new FakeUserRepository(), new FakeUnitOfWork());

        var result = await service.AddBookAsync(15, 3, 42);

        Assert.Equal(ShelfAddBookResultStatus.AlreadyExists, result.Status);
    }

    [Fact]
    public async Task AddBookAsync_Success_AddsBookAndPersists()
    {
        var shelfRepository = new FakeShelfRepository();
        var shelf = new Shelf(4, "Sci-Fi");
        SetProperty(shelf, nameof(Shelf.Id), 20L);
        shelfRepository.Seed(shelf);
        var unitOfWork = new FakeUnitOfWork();
        var service = new ShelfService(shelfRepository, new FakeUserRepository(), unitOfWork);

        var result = await service.AddBookAsync(20, 4, 99);

        Assert.Equal(ShelfAddBookResultStatus.Success, result.Status);
        Assert.NotNull(result.Shelf);
        Assert.Equal(new long[] { 99 }, result.Shelf!.BookIds);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task RemoveBookAsync_HandlesMissingAndExistingRelations()
    {
        var shelfRepository = new FakeShelfRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ShelfService(shelfRepository, new FakeUserRepository(), unitOfWork);

        var notFound = await service.RemoveBookAsync(999, 4, 42);
        Assert.False(notFound);

        var shelf = new Shelf(4, "Sci-Fi");
        SetProperty(shelf, nameof(Shelf.Id), 30L);
        shelf.AddBook(42);
        shelfRepository.Seed(shelf);

        var removed = await service.RemoveBookAsync(30, 4, 42);
        Assert.True(removed);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.False(shelf.ContainsBook(42));

        var missingRelation = await service.RemoveBookAsync(30, 4, 999);
        Assert.True(missingRelation);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    private sealed class FakeShelfRepository : IShelfRepository
    {
        private long _nextId = 1000;

        public List<Shelf> Shelves { get; } = [];

        public void Seed(Shelf shelf)
        {
            if (shelf.Id == 0)
            {
                SetProperty(shelf, nameof(Shelf.Id), _nextId++);
            }

            Shelves.Add(shelf);
        }

        public Task<Shelf?> GetByIdAsync(long shelfId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Shelves.SingleOrDefault(x => x.Id == shelfId));
        }

        public Task<IReadOnlyList<Shelf>> ListByUserAsync(long userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shelf>>(Shelves.Where(x => x.UserId == userId).ToArray());
        }

        public Task<bool> ExistsByNameAsync(long userId, string shelfName, CancellationToken cancellationToken = default)
        {
            var exists = Shelves.Any(x =>
                x.UserId == userId &&
                x.Name.Equals(shelfName, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(exists);
        }

        public Task AddAsync(Shelf shelf, CancellationToken cancellationToken = default)
        {
            if (shelf.Id == 0)
            {
                SetProperty(shelf, nameof(Shelf.Id), _nextId++);
            }

            Shelves.Add(shelf);
            return Task.CompletedTask;
        }

        public void Remove(Shelf shelf)
        {
            Shelves.Remove(shelf);
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

    private static void SetProperty<T>(T entity, string propertyName, object? value)
    {
        var property = typeof(T).GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        if (property is null)
        {
            throw new InvalidOperationException($"Property {propertyName} was not found.");
        }

        property.SetValue(entity, value);
    }
}

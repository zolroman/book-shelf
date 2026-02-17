using Bookshelf.Domain.Abstractions;
using Bookshelf.Domain.Enums;
using Bookshelf.Infrastructure.Services;

namespace Bookshelf.Infrastructure.Tests;

public class InMemoryBookshelfRepositoryTests
{
    [Fact]
    public async Task AddLibraryItem_Returns_Existing_Record_For_Duplicate_Request()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());

        var first = await repository.AddLibraryItemAsync(1, 1, CancellationToken.None);
        var second = await repository.AddLibraryItemAsync(1, 1, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task Deleting_Local_Asset_Does_Not_Remove_History()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());

        await repository.AddHistoryEventAsync(1, 1, BookFormatType.Text, HistoryEventType.Completed, "100%", DateTime.UtcNow, CancellationToken.None);
        await repository.AddOrUpdateLocalAssetAsync(1, 1, "local/file.epub", 42, CancellationToken.None);
        await repository.MarkLocalAssetDeletedAsync(1, 1, CancellationToken.None);

        var history = await repository.GetHistoryEventsAsync(1, 1, CancellationToken.None);

        Assert.Single(history);
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 2, 17, 10, 0, 0, DateTimeKind.Utc);
    }
}

using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Tests;

public class DomainRulesTests
{
    [Fact]
    public void LibraryItem_SetRating_Throws_When_OutOfRange()
    {
        var libraryItem = new LibraryItem();

        Assert.Throws<ArgumentOutOfRangeException>(() => libraryItem.SetRating(11));
        Assert.Throws<ArgumentOutOfRangeException>(() => libraryItem.SetRating(-1));
    }

    [Fact]
    public void DownloadJob_Rejects_Invalid_Status_Transition()
    {
        var job = new DownloadJob();

        Assert.Throws<InvalidOperationException>(() => job.TransitionTo(DownloadJobStatus.Completed, DateTime.UtcNow));
    }

    [Fact]
    public void ProgressSnapshot_Update_Throws_When_Percent_OutOfRange()
    {
        var snapshot = new ProgressSnapshot();

        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.Update("ch1", 101, DateTime.UtcNow));
    }
}

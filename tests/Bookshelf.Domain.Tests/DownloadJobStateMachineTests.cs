using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Tests;

public class DownloadJobStateMachineTests
{
    [Fact]
    public void Queued_To_Downloading_IsAllowed()
    {
        var job = new DownloadJob(1, 1, MediaType.Audio, "jackett", "magnet:?xt=urn:btih:123");

        job.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow);

        Assert.Equal(DownloadJobStatus.Downloading, job.Status);
    }

    [Fact]
    public void Queued_To_Completed_IsRejected()
    {
        var job = new DownloadJob(1, 1, MediaType.Audio, "jackett", "magnet:?xt=urn:btih:123");

        Assert.Throws<InvalidOperationException>(
            () => job.TransitionTo(DownloadJobStatus.Completed, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void TerminalState_CannotBeReopened()
    {
        var job = new DownloadJob(1, 1, MediaType.Audio, "jackett", "magnet:?xt=urn:btih:123");
        job.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow);
        job.TransitionTo(DownloadJobStatus.Completed, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(
            () => job.TransitionTo(DownloadJobStatus.Downloading, DateTimeOffset.UtcNow));
    }
}

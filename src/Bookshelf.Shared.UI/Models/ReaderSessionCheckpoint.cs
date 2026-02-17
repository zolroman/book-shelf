namespace Bookshelf.Shared.UI.Models;

public sealed class ReaderSessionCheckpoint
{
    public int UserId { get; set; }

    public int BookId { get; set; }

    public string FormatType { get; set; } = "text";

    public string PositionRef { get; set; } = string.Empty;

    public float ProgressPercent { get; set; }

    public int CurrentChapter { get; set; } = 1;

    public int CurrentPage { get; set; } = 1;

    public int AudioPositionSeconds { get; set; }

    public int AudioDurationSeconds { get; set; }

    public float AudioSpeed { get; set; } = 1f;

    public bool IsPlaying { get; set; }

    public bool StartedEventSent { get; set; }

    public bool CompletedEventSent { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

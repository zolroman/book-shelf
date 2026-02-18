using System.Threading;

namespace Bookshelf.Shared.Diagnostics;

public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();

    public static string? Current
    {
        get => CurrentCorrelationId.Value;
        set => CurrentCorrelationId.Value = value;
    }
}

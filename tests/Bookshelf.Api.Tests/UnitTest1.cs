using Bookshelf.Api.Parsing;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Api.Tests;

public class RequestParserTests
{
    [Theory]
    [InlineData("text", BookFormatType.Text)]
    [InlineData("audio", BookFormatType.Audio)]
    public void TryParseBookFormatType_Parses_Valid_Values(string input, BookFormatType expected)
    {
        var success = RequestParser.TryParseBookFormatType(input, out var parsed);

        Assert.True(success);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("started", HistoryEventType.Started)]
    [InlineData("progress", HistoryEventType.Progress)]
    [InlineData("completed", HistoryEventType.Completed)]
    public void TryParseHistoryEventType_Parses_Valid_Values(string input, HistoryEventType expected)
    {
        var success = RequestParser.TryParseHistoryEventType(input, out var parsed);

        Assert.True(success);
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void TryParseHistoryEventType_Returns_False_For_Unknown_Value()
    {
        var success = RequestParser.TryParseHistoryEventType("broken", out _);

        Assert.False(success);
    }
}

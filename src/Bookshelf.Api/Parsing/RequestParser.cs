using Bookshelf.Domain.Enums;

namespace Bookshelf.Api.Parsing;

public static class RequestParser
{
    public static bool TryParseBookFormatType(string? value, out BookFormatType formatType)
    {
        formatType = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value, ignoreCase: true, out formatType);
    }

    public static bool TryParseHistoryEventType(string? value, out HistoryEventType eventType)
    {
        eventType = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value, ignoreCase: true, out eventType);
    }
}

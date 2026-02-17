using System.Text.Json;
using System.Text.Json.Serialization;
using Bookshelf.Infrastructure.Models;

namespace Bookshelf.Infrastructure.Services;

internal static class FantLabResponseParser
{
    public static IReadOnlyList<ImportedBookSeed> Parse(string json)
    {
        var items = JsonSerializer.Deserialize<List<FantLabWork>>(json, SerializerOptions) ?? [];
        var results = new List<ImportedBookSeed>(items.Count);

        foreach (var item in items)
        {
            var title = item.RusName?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = item.Name?.Trim();
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var originalTitle = item.Name?.Trim() ?? title;
            var authors = SplitAuthors(item.AllAutorRusName) ?? SplitAuthors(item.AllAutorName) ?? [];

            results.Add(new ImportedBookSeed(
                title,
                originalTitle,
                item.Year,
                ParseMidmark(item.Midmark),
                string.Empty,
                string.Empty,
                authors,
                true,
                false));
        }

        return results;
    }

    private static IReadOnlyList<string>? SplitAuthors(string? authors)
    {
        if (string.IsNullOrWhiteSpace(authors))
        {
            return null;
        }

        return authors
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static float? ParseMidmark(JsonElement? midmark)
    {
        if (midmark is null)
        {
            return null;
        }

        var element = midmark.Value;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetSingle(out var value) ? value : null,
            JsonValueKind.String => float.TryParse(element.GetString(), out var value) ? value : null,
            JsonValueKind.Array => ReadFirstNumber(element),
            _ => null
        };
    }

    private static float? ReadFirstNumber(JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetSingle(out var value))
            {
                return value;
            }

            if (item.ValueKind == JsonValueKind.String && float.TryParse(item.GetString(), out value))
            {
                return value;
            }
        }

        return null;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record FantLabWork(
        [property: JsonPropertyName("rusname")] string? RusName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("year")] int? Year,
        [property: JsonPropertyName("midmark")] JsonElement? Midmark,
        [property: JsonPropertyName("all_autor_rusname")] string? AllAutorRusName,
        [property: JsonPropertyName("all_autor_name")] string? AllAutorName);
}

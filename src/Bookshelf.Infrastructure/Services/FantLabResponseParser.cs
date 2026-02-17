using System.Text.Json;
using Bookshelf.Infrastructure.Models;

namespace Bookshelf.Infrastructure.Services;

internal static class FantLabResponseParser
{
    private static readonly string[] ItemsContainerKeys = ["items", "results", "works", "books", "data"];

    public static IReadOnlyList<ImportedBookSeed> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var items = ResolveItems(root);

        var results = new List<ImportedBookSeed>();
        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var title = GetString(item, "title", "name", "work_name", "ru_name");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var originalTitle = GetString(item, "original_title", "originalName", "name_orig", "en_name") ?? title;
            var year = GetNullableInt(item, "year", "publish_year");
            var rating = GetNullableFloat(item, "rating", "avg_mark", "mark");
            var coverUrl = GetString(item, "cover", "cover_url", "img") ?? string.Empty;
            var description = GetString(item, "description", "annotation", "summary") ?? string.Empty;
            var authors = ExtractAuthors(item);
            var (hasText, hasAudio) = ExtractFormats(item);

            results.Add(new ImportedBookSeed(
                title.Trim(),
                originalTitle.Trim(),
                year,
                rating,
                coverUrl,
                description,
                authors,
                hasText,
                hasAudio));
        }

        return results;
    }

    private static IReadOnlyList<JsonElement> ResolveItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().ToList();
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        foreach (var key in ItemsContainerKeys)
        {
            if (root.TryGetProperty(key, out var nestedArray) && nestedArray.ValueKind == JsonValueKind.Array)
            {
                return nestedArray.EnumerateArray().ToList();
            }
        }

        if (GetString(root, "title", "name", "work_name", "ru_name") is not null)
        {
            return [root];
        }

        return [];
    }

    private static (bool HasText, bool HasAudio) ExtractFormats(JsonElement item)
    {
        var hasText = GetNullableBool(item, "has_text", "text_available");
        var hasAudio = GetNullableBool(item, "has_audio", "audio_available");

        if (item.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
        {
            foreach (var format in formats.EnumerateArray())
            {
                if (format.ValueKind == JsonValueKind.String)
                {
                    var value = format.GetString();
                    if (string.Equals(value, "text", StringComparison.OrdinalIgnoreCase))
                    {
                        hasText = true;
                    }
                    else if (string.Equals(value, "audio", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAudio = true;
                    }
                }
            }
        }

        return (hasText ?? true, hasAudio ?? false);
    }

    private static IReadOnlyList<string> ExtractAuthors(JsonElement item)
    {
        var authors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (item.TryGetProperty("author", out var singleAuthor))
        {
            var name = singleAuthor.ValueKind switch
            {
                JsonValueKind.String => singleAuthor.GetString(),
                JsonValueKind.Object => GetString(singleAuthor, "name", "author_name"),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(name))
            {
                authors.Add(name.Trim());
            }
        }

        if (item.TryGetProperty("authors", out var authorsElement) && authorsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var author in authorsElement.EnumerateArray())
            {
                string? name = author.ValueKind switch
                {
                    JsonValueKind.String => author.GetString(),
                    JsonValueKind.Object => GetString(author, "name", "author_name"),
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(name))
                {
                    authors.Add(name.Trim());
                }
            }
        }

        return authors.ToList();
    }

    private static string? GetString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var property) || property.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            if (property.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return property.ToString();
            }
        }

        return null;
    }

    private static int? GetNullableInt(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var property) || property.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out value))
            {
                return value;
            }
        }

        return null;
    }

    private static float? GetNullableFloat(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var property) || property.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetSingle(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String && float.TryParse(property.GetString(), out value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool? GetNullableBool(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var property) || property.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out var value))
            {
                return value;
            }
        }

        return null;
    }
}

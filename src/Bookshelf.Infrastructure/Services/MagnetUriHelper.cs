using System.Security.Cryptography;
using System.Text;

namespace Bookshelf.Infrastructure.Services;

internal static class MagnetUriHelper
{
    public static bool IsDownloadUri(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public static string CreateMockMagnet(string query)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(query.ToLowerInvariant()));
        var hex = Convert.ToHexString(hash);
        return $"magnet:?xt=urn:btih:{hex}&dn={Uri.EscapeDataString(query)}";
    }

    public static string? TryExtractInfoHash(string? downloadUri)
    {
        if (string.IsNullOrWhiteSpace(downloadUri) ||
            !downloadUri.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = downloadUri["magnet:?".Length..];
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!part.StartsWith("xt=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = Uri.UnescapeDataString(part[3..]);
            const string prefix = "urn:btih:";
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hash = value[prefix.Length..].Trim();
            return string.IsNullOrWhiteSpace(hash) ? null : hash.ToLowerInvariant();
        }

        return null;
    }
}

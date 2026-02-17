using System.Net.Http.Headers;
using System.Xml.Linq;
using Bookshelf.Infrastructure.Models;
using Bookshelf.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Services;

public sealed class JackettTorrentSearchClient(
    IHttpClientFactory httpClientFactory,
    IOptions<JackettOptions> options,
    ILogger<JackettTorrentSearchClient> logger) : ITorrentSearchClient
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IOptions<JackettOptions> _options = options;
    private readonly ILogger<JackettTorrentSearchClient> _logger = logger;

    public async Task<IReadOnlyList<TorrentCandidate>> SearchAsync(
        string query,
        int maxItems,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = query.Trim();
        var settings = _options.Value;
        var effectiveMax = Math.Max(1, Math.Min(maxItems, Math.Max(1, settings.MaxItems)));

        if (!settings.Enabled)
        {
            return settings.UseMockFallback ? BuildMockCandidates(normalizedQuery, effectiveMax) : [];
        }

        try
        {
            var candidates = await SearchViaJackettAsync(normalizedQuery, effectiveMax, settings, cancellationToken);
            if (candidates.Count > 0)
            {
                return candidates;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Jackett search failed for query '{Query}'.", normalizedQuery);
        }

        return settings.UseMockFallback ? BuildMockCandidates(normalizedQuery, effectiveMax) : [];
    }

    private async Task<IReadOnlyList<TorrentCandidate>> SearchViaJackettAsync(
        string query,
        int maxItems,
        JackettOptions settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("Jackett ApiKey is required when Jackett integration is enabled.");
        }

        var client = _httpClientFactory.CreateClient(nameof(JackettTorrentSearchClient));
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));

        var baseUrl = settings.BaseUrl.TrimEnd('/');
        var indexer = string.IsNullOrWhiteSpace(settings.Indexer) ? "all" : settings.Indexer.Trim();
        var requestUri =
            $"{baseUrl}/api/v2.0/indexers/{Uri.EscapeDataString(indexer)}/results/torznab/api?apikey={Uri.EscapeDataString(settings.ApiKey)}&t=search&q={Uri.EscapeDataString(query)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseTorznabResponse(xml, maxItems);
    }

    private static IReadOnlyList<TorrentCandidate> ParseTorznabResponse(string xml, int maxItems)
    {
        var document = XDocument.Parse(xml);
        XNamespace torznab = "http://torznab.com/schemas/2015/feed";

        var candidates = new List<TorrentCandidate>();
        var items = document.Descendants("item");
        foreach (var item in items)
        {
            var title = item.Element("title")?.Value?.Trim();
            var link = item.Element("link")?.Value?.Trim();
            var source = item.Element("guid")?.Value?.Trim() ?? "jackett";

            var seeders = item.Elements(torznab + "attr")
                .Where(attr => string.Equals(attr.Attribute("name")?.Value, "seeders", StringComparison.OrdinalIgnoreCase))
                .Select(attr => int.TryParse(attr.Attribute("value")?.Value, out var value) ? value : 0)
                .DefaultIfEmpty(0)
                .First();

            var size = item.Element("size")?.Value;
            long? sizeBytes = long.TryParse(size, out var parsedSize) ? parsedSize : null;

            var magnet = item.Elements(torznab + "attr")
                .Where(attr => string.Equals(attr.Attribute("name")?.Value, "magneturl", StringComparison.OrdinalIgnoreCase))
                .Select(attr => attr.Attribute("value")?.Value)
                .FirstOrDefault();

            var downloadUri = !string.IsNullOrWhiteSpace(magnet) ? magnet : link;
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(downloadUri))
            {
                continue;
            }

            candidates.Add(new TorrentCandidate(title, downloadUri, source, seeders, sizeBytes));
            if (candidates.Count >= maxItems)
            {
                break;
            }
        }

        return candidates;
    }

    private static IReadOnlyList<TorrentCandidate> BuildMockCandidates(string query, int maxItems)
    {
        return Enumerable.Range(1, maxItems)
            .Select(index => new TorrentCandidate(
                $"{query} - mock torrent {index}",
                MagnetUriHelper.CreateMockMagnet($"{query}-{index}"),
                "mock-jackett",
                Seeders: 100 - index,
                SizeBytes: 750_000_000 + (index * 25_000_000L)))
            .ToList();
    }
}

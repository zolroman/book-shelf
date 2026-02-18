using System.Diagnostics;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Integrations.Jackett;

public sealed class JackettCandidateProvider : IDownloadCandidateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JackettCandidateProvider> _logger;
    private readonly JackettOptions _options;

    public JackettCandidateProvider(
        HttpClient httpClient,
        IOptions<JackettOptions> options,
        ILogger<JackettCandidateProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public string ProviderCode => "jackett";

    public async Task<IReadOnlyList<DownloadCandidateRaw>> SearchAsync(
        string query,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new DownloadCandidateProviderUnavailableException(ProviderCode, "Jackett integration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new DownloadCandidateProviderUnavailableException(ProviderCode, "Jackett API key is not configured.");
        }

        var uri = BuildUri(query);
        var payload = await SendWithRetryAsync(uri, cancellationToken);
        var parsed = ParsePayload(payload);
        var limit = Math.Min(Math.Max(1, maxItems), Math.Max(1, _options.MaxItems));
        return parsed.Take(limit).ToArray();
    }

    private async Task<string> SendWithRetryAsync(Uri uri, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        var redactedUri = RedactUri(uri);
        var attempts = Math.Max(0, _options.MaxRetries) + 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            try
            {
                using var response = await _httpClient.GetAsync(uri, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    if (IsTransientStatusCode(response.StatusCode))
                    {
                        throw new HttpRequestException(
                            $"Jackett transient status code {(int)response.StatusCode}.");
                    }

                    throw new DownloadCandidateProviderUnavailableException(
                        ProviderCode,
                        $"Jackett returned non-success status code {(int)response.StatusCode}.");
                }

                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    throw new InvalidOperationException("Jackett returned an empty payload.");
                }

                _logger.LogInformation(
                    "Jackett search completed. Attempt={Attempt} Url={Url} DurationMs={DurationMs}",
                    attempt,
                    redactedUri,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

                return payload;
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                lastException = exception;
                _logger.LogWarning(
                    exception,
                    "Jackett transient failure. Attempt={Attempt}/{Attempts} Url={Url}",
                    attempt,
                    attempts,
                    redactedUri);

                if (attempt >= attempts)
                {
                    break;
                }

                var delay = TimeSpan.FromMilliseconds(
                    (_options.RetryDelayMs * Math.Pow(2, attempt - 1)) + Random.Shared.Next(0, 120));

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception exception)
            {
                lastException = exception;
                _logger.LogError(exception, "Jackett non-transient failure. Url={Url}", redactedUri);
                break;
            }
        }

        throw new DownloadCandidateProviderUnavailableException(
            ProviderCode,
            "Jackett is unavailable after retry attempts.",
            lastException);
    }

    private IReadOnlyList<DownloadCandidateRaw> ParsePayload(string xmlPayload)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xmlPayload);
        }
        catch (Exception exception)
        {
            throw new DownloadCandidateProviderUnavailableException(ProviderCode, "Jackett response is not valid XML.", exception);
        }

        var candidates = new List<DownloadCandidateRaw>();
        var items = document.Descendants().Where(x => x.Name.LocalName.Equals("item", StringComparison.OrdinalIgnoreCase));
        foreach (var item in items)
        {
            var title = item.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("title", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
            var link = item.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("link", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
            var guid = item.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("guid", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
            var pubDateRaw = item.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("pubDate", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
            var sizeRaw = item.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("size", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

            var attributes = item.Descendants().Where(x => x.Name.LocalName.Equals("attr", StringComparison.OrdinalIgnoreCase)).ToArray();
            var magnetUrl = GetAttributeValue(attributes, "magneturl");
            var detailsUrl = GetAttributeValue(attributes, "details");
            var seedersRaw = GetAttributeValue(attributes, "seeders");
            sizeRaw ??= GetAttributeValue(attributes, "size");

            var downloadUri = !string.IsNullOrWhiteSpace(magnetUrl) ? magnetUrl : link;
            var sourceUrl = !string.IsNullOrWhiteSpace(detailsUrl) ? detailsUrl : guid;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(downloadUri))
            {
                continue;
            }

            var seeders = int.TryParse(seedersRaw, out var parsedSeeders) ? parsedSeeders : (int?)null;
            var sizeBytes = long.TryParse(sizeRaw, out var parsedSize) ? parsedSize : (long?)null;
            var publishedAt = DateTimeOffset.TryParse(pubDateRaw, out var parsedDate) ? parsedDate : (DateTimeOffset?)null;

            candidates.Add(new DownloadCandidateRaw(
                Title: title,
                DownloadUri: downloadUri.Trim(),
                SourceUrl: string.IsNullOrWhiteSpace(sourceUrl) ? downloadUri.Trim() : sourceUrl.Trim(),
                Seeders: seeders,
                SizeBytes: sizeBytes,
                PublishedAtUtc: publishedAt?.ToUniversalTime()));
        }

        return candidates;
    }

    private Uri BuildUri(string query)
    {
        var normalizedQuery = string.Join(' ', query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var encodedQuery = Uri.EscapeDataString(normalizedQuery);
        var encodedApiKey = Uri.EscapeDataString(_options.ApiKey);
        var encodedIndexer = Uri.EscapeDataString(_options.Indexer);

        var relative =
            $"/api/v2.0/indexers/{encodedIndexer}/results/torznab/api?apikey={encodedApiKey}&t=search&q={encodedQuery}";
        return new Uri(relative, UriKind.Relative);
    }

    private static bool IsTransient(Exception exception)
    {
        return exception is HttpRequestException or TaskCanceledException or TimeoutException;
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;
    }

    private string RedactUri(Uri uri)
    {
        var raw = uri.ToString();
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return raw;
        }

        var encodedApiKey = Uri.EscapeDataString(_options.ApiKey);
        return raw
            .Replace(_options.ApiKey, "***", StringComparison.Ordinal)
            .Replace(encodedApiKey, "***", StringComparison.Ordinal);
    }

    private static string? GetAttributeValue(IEnumerable<XElement> attributes, string targetName)
    {
        foreach (var attribute in attributes)
        {
            var name = attribute.Attribute("name")?.Value;
            if (!string.Equals(name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return attribute.Attribute("value")?.Value;
        }

        return null;
    }
}

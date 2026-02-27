using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Bookshelf.Application.Abstractions.Providers;
using Bookshelf.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Integrations.Flibusta;

public sealed class FlibustaCandidateProvider : IDownloadCandidateProvider
{
    public const string MeterName = "Bookshelf.Integrations.Flibusta";

    private const string OpenAccessRel = "http://opds-spec.org/acquisition/open-access";
    private const string EpubMimeType = "application/epub+zip";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>("flibusta_requests_total");
    private static readonly Counter<long> FailureCounter = Meter.CreateCounter<long>("flibusta_failures_total");
    private static readonly Histogram<double> LatencyHistogram = Meter.CreateHistogram<double>("flibusta_request_duration_ms");
    private static readonly Regex SizeRegex = new(@"Размер:\s*(?<value>\d+)\s*(?<unit>Kb|Mb|Gb)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BookIdRegex = new(@"/b/(?<id>\d+)/epub/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly ILogger<FlibustaCandidateProvider> _logger;
    private readonly FlibustaOptions _options;

    public FlibustaCandidateProvider(
        HttpClient httpClient,
        IOptions<FlibustaOptions> options,
        ILogger<FlibustaCandidateProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public string ProviderCode => "flibusta";

    public string ProviderName => "Flibusta";

    public IReadOnlyCollection<string> SupportedMediaTypes => ["text"];

    public int Priority => 100;

    public async Task<IReadOnlyList<DownloadCandidateRaw>> SearchAsync(
        DownloadCandidateSearchRequest request,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeText(request.Query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<DownloadCandidateRaw>();
        }

        var limit = Math.Min(Math.Max(1, maxItems), Math.Max(1, _options.MaxItems));
        var maxPages = Math.Max(1, _options.MaxPages);
        var results = new List<DownloadCandidateRaw>(limit);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var pageNumber = 0; pageNumber < maxPages && results.Count < limit; pageNumber++)
        {
            var uri = BuildUri(normalizedQuery, pageNumber);
            var payload = await SendWithRetryAsync(uri, cancellationToken);
            var parsedPage = ParsePayload(payload, request.AuthorFilter);
            foreach (var item in parsedPage.Items)
            {
                if (!seen.Add(item.UniqueIdentifier))
                {
                    continue;
                }

                results.Add(item);
                if (results.Count >= limit)
                {
                    break;
                }
            }

            if (!parsedPage.HasMorePages)
            {
                break;
            }
        }

        return results;
    }

    private async Task<string> SendWithRetryAsync(Uri uri, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        var attempts = Math.Max(0, _options.MaxRetries) + 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            RequestCounter.Add(1, new("provider", ProviderCode), new("operation", "search"));
            try
            {
                using var response = await _httpClient.GetAsync(uri, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    if (IsTransientStatusCode(response.StatusCode))
                    {
                        throw new HttpRequestException($"Flibusta transient status code {(int)response.StatusCode}.");
                    }

                    throw new DownloadCandidateProviderUnavailableException(
                        ProviderCode,
                        $"Flibusta returned non-success status code {(int)response.StatusCode}.");
                }

                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    throw new InvalidOperationException("Flibusta returned an empty payload.");
                }

                _logger.LogInformation(
                    "Flibusta search completed. Attempt={Attempt} Url={Url} DurationMs={DurationMs}",
                    attempt,
                    uri,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                LogLatency(startedAt, success: true);
                return payload;
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                lastException = exception;
                FailureCounter.Add(1, new("provider", ProviderCode), new("operation", "search"));
                LogLatency(startedAt, success: false);
                _logger.LogWarning(
                    exception,
                    "Flibusta transient failure. Attempt={Attempt}/{Attempts} Url={Url}",
                    attempt,
                    attempts,
                    uri);

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
                FailureCounter.Add(1, new("provider", ProviderCode), new("operation", "search"));
                LogLatency(startedAt, success: false);
                _logger.LogError(exception, "Flibusta non-transient failure. Url={Url}", uri);
                break;
            }
        }

        throw new DownloadCandidateProviderUnavailableException(
            ProviderCode,
            "Flibusta is unavailable after retry attempts.",
            lastException);
    }

    private ParsedFlibustaPage ParsePayload(string xmlPayload, string? authorFilter)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xmlPayload, LoadOptions.None);
        }
        catch (Exception exception) when (exception is XmlException or ArgumentException)
        {
            throw new DownloadCandidateProviderUnavailableException(ProviderCode, "Flibusta response is not valid XML.", exception);
        }

        var atom = XNamespace.Get("http://www.w3.org/2005/Atom");
        var dc = XNamespace.Get("http://purl.org/dc/terms/");
        var os = XNamespace.Get("http://a9.com/-/spec/opensearch/1.1/");
        var results = new List<DownloadCandidateRaw>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in document.Root?.Elements(atom + "entry") ?? [])
        {
            var title = NormalizeText(entry.Element(atom + "title")?.Value);
            var authors = entry.Elements(atom + "author")
                .Select(x => NormalizeText(x.Element(atom + "name")?.Value))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToArray();

            if (!MatchesAuthorFilter(authors, authorFilter))
            {
                continue;
            }

            var epubLink = entry.Elements(atom + "link")
                .FirstOrDefault(x =>
                    string.Equals((string?)x.Attribute("rel"), OpenAccessRel, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string?)x.Attribute("type"), EpubMimeType, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(title) || epubLink is null)
            {
                continue;
            }

            var downloadUri = ToAbsoluteUri((string?)epubLink.Attribute("href"));
            if (downloadUri is null)
            {
                continue;
            }

            var alternateLink = entry.Elements(atom + "link")
                .FirstOrDefault(x =>
                    string.Equals((string?)x.Attribute("rel"), "alternate", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string?)x.Attribute("type"), "text/html", StringComparison.OrdinalIgnoreCase));
            var sourceUrl = ToAbsoluteUri((string?)alternateLink?.Attribute("href")) ?? downloadUri;
            var uniqueIdentifier = BuildUniqueIdentifier(downloadUri);
            if (!seen.Add(uniqueIdentifier))
            {
                continue;
            }

            var content = entry.Element(atom + "content")?.Value;
            results.Add(new DownloadCandidateRaw(
                Title: $"{title} [EPUB]",
                DownloadUri: downloadUri,
                SourceUrl: sourceUrl,
                Seeders: null,
                SizeBytes: ParseSize(content),
                PublishedAtUtc: ParsePublishedAt(entry.Element(dc + "issued")?.Value),
                UniqueIdentifier: uniqueIdentifier,
                SourceProviderCode: ProviderCode,
                SourceProviderName: ProviderName,
                ExecutionProviderCode: "local-http-epub"));
        }

        var startIndex = ParseInt(document.Root?.Element(os + "startIndex")?.Value);
        var itemsPerPage = ParseInt(document.Root?.Element(os + "itemsPerPage")?.Value);
        var totalResults = ParseInt(document.Root?.Element(os + "totalResults")?.Value);
        var hasMorePages = startIndex.HasValue
            && itemsPerPage.HasValue
            && itemsPerPage.Value > 0
            && totalResults.HasValue
            && (startIndex.Value + itemsPerPage.Value) < totalResults.Value;

        return new ParsedFlibustaPage(results, hasMorePages);
    }

    private Uri BuildUri(string query, int pageNumber)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        return new Uri($"/opds/opensearch?searchTerm={encodedQuery}&searchType=books&pageNumber={pageNumber}", UriKind.Relative);
    }

    private string BuildUniqueIdentifier(string downloadUri)
    {
        var match = BookIdRegex.Match(downloadUri);
        if (match.Success)
        {
            return $"{match.Groups["id"].Value}:epub";
        }

        return $"epub:{downloadUri}";
    }

    private string? ToAbsoluteUri(string? href)
    {
        var normalizedHref = NormalizeText(href);
        if (string.IsNullOrWhiteSpace(normalizedHref))
        {
            return null;
        }

        if (Uri.TryCreate(normalizedHref, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.ToString();
        }

        if (!Uri.TryCreate(_httpClient.BaseAddress, normalizedHref, out var resolved))
        {
            return null;
        }

        return resolved.ToString();
    }

    private static bool MatchesAuthorFilter(IReadOnlyList<string> authors, string? authorFilter)
    {
        var filterTokens = Tokenize(authorFilter);
        if (filterTokens.Count == 0)
        {
            return true;
        }

        foreach (var author in authors)
        {
            var authorTokens = Tokenize(author);
            if (authorTokens.Count == 0)
            {
                continue;
            }

            if (filterTokens.All(authorTokens.Contains))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> Tokenize(string? value)
    {
        var normalized = NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        return TokenRegex.Matches(normalized.ToLowerInvariant())
            .Select(x => x.Value.Trim())
            .Where(x => x.Length > 1)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(NormalizeText(value), out var parsed) ? parsed : null;
    }

    private static long? ParseSize(string? content)
    {
        var normalized = NormalizeText(content);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var match = SizeRegex.Match(normalized);
        if (!match.Success || !long.TryParse(match.Groups["value"].Value, out var value))
        {
            return null;
        }

        var multiplier = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "kb" => 1024L,
            "mb" => 1024L * 1024L,
            "gb" => 1024L * 1024L * 1024L,
            _ => 0L,
        };

        return multiplier == 0L ? null : value * multiplier;
    }

    private static DateTimeOffset? ParsePublishedAt(string? issued)
    {
        var normalized = NormalizeText(issued);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (int.TryParse(normalized, out var year) && year is > 0 and <= 9999)
        {
            return new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }

        return null;
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

    private static void LogLatency(long startTimestamp, bool success)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LatencyHistogram.Record(
            elapsedMs,
            new("provider", "flibusta"),
            new("operation", "search"),
            new("success", success.ToString().ToLowerInvariant()));
    }

    private sealed record ParsedFlibustaPage(
        IReadOnlyList<DownloadCandidateRaw> Items,
        bool HasMorePages);
}

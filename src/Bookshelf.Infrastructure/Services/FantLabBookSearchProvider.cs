using System.Net.Http.Headers;
using Bookshelf.Domain.Entities;
using Bookshelf.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookshelf.Infrastructure.Services;

public sealed class FantLabBookSearchProvider(
    IBookshelfRepository repository,
    IMemoryCache cache,
    IHttpClientFactory httpClientFactory,
    IOptions<FantLabSearchOptions> options,
    ILogger<FantLabBookSearchProvider> logger) : IBookSearchProvider
{
    private readonly object _stateLock = new();

    private int _consecutiveFailures;
    private DateTimeOffset? _circuitOpenedUntil;

    public async Task<IReadOnlyList<Book>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = query.Trim();
        var cacheKey = $"search:{normalizedQuery.ToLowerInvariant()}";
        // if (cache.TryGetValue<IReadOnlyList<Book>>(cacheKey, out var cached) && cached is not null)
        // {
        //     return cached;
        // }

        var localResults = await repository.GetBooksAsync(normalizedQuery, null, cancellationToken);
        var settings = options.Value;
        
        if (!settings.Enabled || IsCircuitOpen())
        {
            Cache(cacheKey, localResults, settings.CacheTtlMinutes);
            return localResults;
        }

        try
        {
            var importedSeeds = await FetchExternalSeedsAsync(normalizedQuery, settings, cancellationToken);
            foreach (var seed in importedSeeds)
            {
                await repository.UpsertImportedBookAsync(seed, cancellationToken);
            }

            RegisterSuccess();
            var mergedResults = await repository.GetBooksAsync(normalizedQuery, null, cancellationToken);
            Cache(cacheKey, mergedResults, settings.CacheTtlMinutes);
            return mergedResults;
        }
        catch (Exception exception)
        {
            RegisterFailure(settings);
            logger.LogWarning(exception, "External search failed. Returning local search results.");
            return localResults;
        }
    }

    private async Task<IReadOnlyList<Models.ImportedBookSeed>> FetchExternalSeedsAsync(
        string query,
        FantLabSearchOptions settings,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt <= settings.MaxRetries; attempt++)
        {
            try
            {
                return await FetchOnceAsync(query, settings, cancellationToken);
            }
            catch (Exception exception)
            {
                lastException = exception;
                if (attempt >= settings.MaxRetries)
                {
                    break;
                }

                var delay = settings.RetryDelayMilliseconds * (attempt + 1);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException("Search request failed without detailed exception.");
    }

    private async Task<IReadOnlyList<Models.ImportedBookSeed>> FetchOnceAsync(
        string query,
        FantLabSearchOptions settings,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(FantLabBookSearchProvider));
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));

        var requestUri = BuildRequestUri(settings, query);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return FantLabResponseParser.Parse(json);
    }

    private static string BuildRequestUri(FantLabSearchOptions settings, string query)
    {
        var baseUrl = settings.BaseUrl.TrimEnd('/');
        var path = settings.SearchPath.TrimStart('/');
        var separator = settings.SearchPath.Contains('?') ? "&" : "?";
        return $"{baseUrl}/{path}{separator}{settings.QueryParameter}={Uri.EscapeDataString(query)}&onlymatches=1";
    }

    private void Cache(string cacheKey, IReadOnlyList<Book> value, int ttlMinutes)
    {
        cache.Set(cacheKey, value, TimeSpan.FromMinutes(Math.Max(1, ttlMinutes)));
    }

    private bool IsCircuitOpen()
    {
        lock (_stateLock)
        {
            return _circuitOpenedUntil.HasValue && _circuitOpenedUntil.Value > DateTimeOffset.UtcNow;
        }
    }

    private void RegisterSuccess()
    {
        lock (_stateLock)
        {
            _consecutiveFailures = 0;
            _circuitOpenedUntil = null;
        }
    }

    private void RegisterFailure(FantLabSearchOptions settings)
    {
        lock (_stateLock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures < settings.CircuitBreakerFailureThreshold)
            {
                return;
            }

            _consecutiveFailures = 0;
            _circuitOpenedUntil = DateTimeOffset.UtcNow.AddSeconds(Math.Max(5, settings.CircuitBreakerOpenSeconds));
        }
    }
}

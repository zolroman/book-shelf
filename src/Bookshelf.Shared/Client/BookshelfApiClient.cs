using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bookshelf.Shared.Contracts.Api;
using Microsoft.Extensions.Logging;

namespace Bookshelf.Shared.Client;

public sealed class BookshelfApiClient : IBookshelfApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly UserSessionState _sessionState;
    private readonly ILogger<BookshelfApiClient> _logger;

    public BookshelfApiClient(
        HttpClient httpClient,
        UserSessionState sessionState,
        ILogger<BookshelfApiClient> logger)
    {
        _httpClient = httpClient;
        _sessionState = sessionState;
        _logger = logger;
    }

    public Task<SearchBooksResponse> SearchBooksAsync(
        string? title,
        string? author,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("title", title),
            ("author", author),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()));
        var request = CreateRequest(HttpMethod.Get, $"/api/v1/search/books{query}");
        return SendAsync<SearchBooksResponse>(request, cancellationToken);
    }

    public Task<SearchBookDetailsResponse> GetBookDetailsAsync(
        string providerCode,
        string providerBookKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/search/books/{Escape(providerCode)}/{Escape(providerBookKey)}");
        return SendAsync<SearchBookDetailsResponse>(request, cancellationToken);
    }

    public Task<DownloadCandidatesResponse> GetCandidatesAsync(
        string providerCode,
        string providerBookKey,
        string mediaType,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("mediaType", mediaType),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()));
        var request = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/search/books/{Escape(providerCode)}/{Escape(providerBookKey)}/candidates{query}");
        return SendAsync<DownloadCandidatesResponse>(request, cancellationToken);
    }

    public Task<AddAndDownloadResponse> AddAndDownloadAsync(
        string providerCode,
        string providerBookKey,
        string mediaType,
        string candidateId,
        CancellationToken cancellationToken = default)
    {
        var body = new AddAndDownloadRequest(
            UserId: _sessionState.UserId,
            ProviderCode: providerCode,
            ProviderBookKey: providerBookKey,
            MediaType: mediaType,
            CandidateId: candidateId);

        var request = CreateRequest(HttpMethod.Post, "/api/v1/library/add-and-download");
        request.Content = Serialize(body);
        return SendAsync<AddAndDownloadResponse>(request, cancellationToken);
    }

    public Task<DownloadJobsResponse> ListDownloadJobsAsync(
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("userId", _sessionState.UserId.ToString()),
            ("status", status),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()));
        var request = CreateRequest(HttpMethod.Get, $"/api/v1/download-jobs{query}");
        return SendAsync<DownloadJobsResponse>(request, cancellationToken);
    }

    public Task<DownloadJobDto> GetDownloadJobAsync(
        long jobId,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("userId", _sessionState.UserId.ToString()));
        var request = CreateRequest(HttpMethod.Get, $"/api/v1/download-jobs/{jobId}{query}");
        return SendAsync<DownloadJobDto>(request, cancellationToken);
    }

    public Task<DownloadJobDto> CancelDownloadJobAsync(
        long jobId,
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Post, $"/api/v1/download-jobs/{jobId}/cancel");
        request.Content = Serialize(new CancelDownloadJobRequest(_sessionState.UserId));
        return SendAsync<DownloadJobDto>(request, cancellationToken);
    }

    public Task<LibraryResponse> GetLibraryAsync(
        bool includeArchived,
        string? query,
        string? providerCode,
        string? catalogState,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var queryString = BuildQuery(
            ("includeArchived", includeArchived.ToString().ToLowerInvariant()),
            ("query", query),
            ("providerCode", providerCode),
            ("catalogState", catalogState),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()));
        var request = CreateRequest(HttpMethod.Get, $"/api/v1/library{queryString}");
        return SendAsync<LibraryResponse>(request, cancellationToken);
    }

    public Task<ShelvesResponse> GetShelvesAsync(CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("userId", _sessionState.UserId.ToString()));
        var request = CreateRequest(HttpMethod.Get, $"/api/v1/shelves{query}");
        return SendAsync<ShelvesResponse>(request, cancellationToken);
    }

    public Task<CreateShelfResponse> CreateShelfAsync(string name, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Post, "/api/v1/shelves");
        request.Content = Serialize(new CreateShelfRequest(_sessionState.UserId, name));
        return SendAsync<CreateShelfResponse>(request, cancellationToken);
    }

    public Task<AddBookToShelfResponse> AddBookToShelfAsync(
        long shelfId,
        long bookId,
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Post, $"/api/v1/shelves/{shelfId}/books");
        request.Content = Serialize(new AddBookToShelfRequest(_sessionState.UserId, bookId));
        return SendAsync<AddBookToShelfResponse>(request, cancellationToken);
    }

    public async Task RemoveBookFromShelfAsync(
        long shelfId,
        long bookId,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("userId", _sessionState.UserId.ToString()));
        var request = CreateRequest(HttpMethod.Delete, $"/api/v1/shelves/{shelfId}/books/{bookId}{query}");
        await SendWithoutPayloadAsync(request, cancellationToken);
    }

    public Task<ProgressSnapshotDto> UpsertProgressAsync(
        UpsertProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Put, "/api/v1/progress");
        httpRequest.Content = Serialize(request);
        return SendAsync<ProgressSnapshotDto>(httpRequest, cancellationToken);
    }

    public Task<ProgressSnapshotsResponse> ListProgressAsync(
        long? bookId,
        string? mediaType,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("bookId", bookId?.ToString()),
            ("mediaType", mediaType),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()));
        var request = CreateRequest(HttpMethod.Get, $"/api/v1/progress{query}");
        return SendAsync<ProgressSnapshotsResponse>(request, cancellationToken);
    }

    public Task<AppendHistoryEventsResponse> AppendHistoryEventsAsync(
        AppendHistoryEventsRequest request,
        CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "/api/v1/history/events");
        httpRequest.Content = Serialize(request);
        return SendAsync<AppendHistoryEventsResponse>(httpRequest, cancellationToken);
    }

    public Task<HistoryEventsResponse> ListHistoryEventsAsync(
        long? bookId,
        string? mediaType,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("bookId", bookId?.ToString()),
            ("mediaType", mediaType),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()));
        var request = CreateRequest(HttpMethod.Get, $"/api/v1/history/events{query}");
        return SendAsync<HistoryEventsResponse>(request, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"uid:{_sessionState.UserId}");
        return request;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, cancellationToken);
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        if (payload is null)
        {
            throw new ApiClientException(
                (int)response.StatusCode,
                "INVALID_RESPONSE",
                "API response body is empty or invalid.");
        }

        return payload;
    }

    private async Task SendWithoutPayloadAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw await BuildExceptionAsync(response, cancellationToken);
    }

    private async Task<ApiClientException> BuildExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                var error = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);
                if (error is not null)
                {
                    return new ApiClientException(statusCode, error.Code, error.Message, error.Details);
                }
            }
            catch (JsonException jsonException)
            {
                _logger.LogDebug(jsonException, "Failed to parse API error payload.");
            }
        }

        return new ApiClientException(
            statusCode,
            $"HTTP_{statusCode}",
            $"Request failed with HTTP {(int)response.StatusCode}.");
    }

    private static StringContent Serialize<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static string BuildQuery(params (string Key, string? Value)[] values)
    {
        var items = values
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Escape(x.Key)}={Escape(x.Value!)}")
            .ToArray();

        return items.Length == 0 ? string.Empty : $"?{string.Join("&", items)}";
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }
}

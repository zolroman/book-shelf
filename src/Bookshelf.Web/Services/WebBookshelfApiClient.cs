using System.Net.Http.Json;
using Bookshelf.Shared.Contracts.Assets;
using Bookshelf.Shared.Contracts.Books;
using Bookshelf.Shared.Contracts.History;
using Bookshelf.Shared.Contracts.Library;
using Bookshelf.Shared.Contracts.Progress;
using Bookshelf.Shared.Contracts.Search;
using Bookshelf.Shared.UI.Services;

namespace Bookshelf.Web.Services;

public sealed class WebBookshelfApiClient(IHttpClientFactory httpClientFactory) : IBookshelfApiClient
{
    private HttpClient HttpClient => httpClientFactory.CreateClient("BookshelfApi");

    public async Task<BookDetailsDto?> GetBookDetailsAsync(int bookId, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<BookDetailsDto>($"api/books/{bookId}", cancellationToken);
    }

    public async Task<IReadOnlyList<LibraryBookDto>> GetLibraryAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<List<LibraryBookDto>>($"api/library?userId={userId}", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<BookSummaryDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var response = await HttpClient.GetFromJsonAsync<SearchResultDto>($"api/search?query={Uri.EscapeDataString(query)}", cancellationToken);
        return response?.Items ?? [];
    }

    public async Task<bool> AddToLibraryAsync(int userId, int bookId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/library", new AddLibraryItemRequest(userId, bookId), cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<LocalAssetDto>> GetAssetsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<List<LocalAssetDto>>($"api/assets?userId={userId}", cancellationToken) ?? [];
    }

    public async Task<LocalAssetDto?> UpsertLocalAssetAsync(UpsertLocalAssetRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/assets", request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LocalAssetDto>(cancellationToken);
    }

    public async Task<bool> MarkAssetDeletedAsync(int userId, int bookFormatId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/assets?userId={userId}&bookFormatId={bookFormatId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<HistoryEventDto>> GetHistoryAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<List<HistoryEventDto>>($"api/history?userId={userId}", cancellationToken) ?? [];
    }

    public async Task<ProgressSnapshotDto?> GetProgressAsync(int userId, int bookId, string formatType, CancellationToken cancellationToken = default)
    {
        try
        {
            return await HttpClient.GetFromJsonAsync<ProgressSnapshotDto>($"api/progress?userId={userId}&bookId={bookId}&formatType={formatType}", cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<ProgressSnapshotDto?> UpsertProgressAsync(UpsertProgressRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/progress", request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ProgressSnapshotDto>(cancellationToken);
    }

    public async Task<bool> AddHistoryEventAsync(AddHistoryEventRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/history", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}

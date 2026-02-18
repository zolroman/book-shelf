using System.Net;
using System.Net.Http.Json;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Api.Api.Middleware;
using Bookshelf.Shared.Contracts.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bookshelf.Api.Tests;

public class ApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Search_WithoutQuery_ReturnsQueryRequired()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("QUERY_REQUIRED", payload!.Code);
    }

    [Fact]
    public async Task ErrorResponse_ContainsPropagatedCorrelationId()
    {
        const string correlationId = "test-correlation-id";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await client.GetAsync("/api/v1/search/books");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var headerValues));
        Assert.Contains(correlationId, headerValues!);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal(correlationId, payload!.CorrelationId);
    }

    [Fact]
    public async Task CreateShelf_DuplicateName_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var userId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var request = new CreateShelfRequest(userId, "Sci-Fi");

        var first = await client.PostAsJsonAsync("/api/v1/shelves", request);
        var second = await client.PostAsJsonAsync("/api/v1/shelves", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var payload = await second.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("SHELF_NAME_CONFLICT", payload!.Code);
    }

    [Fact]
    public async Task AddBookToShelf_DuplicateBook_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var userId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/shelves",
            new CreateShelfRequest(userId, "Favorites"));

        var created = await createResponse.Content.ReadFromJsonAsync<CreateShelfResponse>();
        Assert.NotNull(created);
        var shelfId = created!.Shelf.Id;

        var request = new AddBookToShelfRequest(userId, 42);
        var first = await client.PostAsJsonAsync($"/api/v1/shelves/{shelfId}/books", request);
        var second = await client.PostAsJsonAsync($"/api/v1/shelves/{shelfId}/books", request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var payload = await second.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("SHELF_BOOK_EXISTS", payload!.Code);
    }

    [Fact]
    public async Task Candidates_InvalidMediaType_ReturnsInvalidArgument()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books/fantlab/123/candidates?mediaType=video");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("INVALID_ARGUMENT", payload!.Code);
    }

    [Fact]
    public async Task Details_UnsupportedProvider_ReturnsInvalidArgument()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books/other/123");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("INVALID_ARGUMENT", payload!.Code);
    }

    [Fact]
    public async Task Candidates_ReturnsServicePayload()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICandidateDiscoveryService>();
                services.AddScoped<ICandidateDiscoveryService>(_ => new StubCandidateDiscoveryService());
            });
        });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books/fantlab/123/candidates?mediaType=audio&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<DownloadCandidatesResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Total);
        var item = Assert.Single(payload.Items);
        Assert.Equal("jackett:abc123", item.CandidateId);
        Assert.Equal("audio", item.MediaType);
    }

    [Fact]
    public async Task Candidates_WhenJackettUnavailable_ReturnsMappedError()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICandidateDiscoveryService>();
                services.AddScoped<ICandidateDiscoveryService>(_ => new ThrowingCandidateDiscoveryService(
                    new DownloadCandidateProviderUnavailableException("jackett", "down")));
            });
        });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books/fantlab/123/candidates?mediaType=audio");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("JACKETT_UNAVAILABLE", payload!.Code);
    }

    [Fact]
    public async Task Candidates_WhenFantLabUnavailable_ReturnsMappedError()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICandidateDiscoveryService>();
                services.AddScoped<ICandidateDiscoveryService>(_ => new ThrowingCandidateDiscoveryService(
                    new MetadataProviderUnavailableException("fantlab", "down")));
            });
        });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search/books/fantlab/123/candidates?mediaType=audio");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("FANTLAB_UNAVAILABLE", payload!.Code);
    }

    private sealed class StubCandidateDiscoveryService : ICandidateDiscoveryService
    {
        public Task<DownloadCandidatesResponse> FindAsync(
            string providerCode,
            string providerBookKey,
            string mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new DownloadCandidatesResponse(
                    providerCode,
                    providerBookKey,
                    mediaType,
                    page,
                    pageSize,
                    1,
                    new[]
                    {
                        new DownloadCandidateDto(
                            "jackett:abc123",
                            "audio",
                            "Dune Audiobook",
                            "magnet:?xt=urn:btih:abc123",
                            "https://tracker.example/item/123",
                            52,
                            734003200),
                    }));
        }
    }

    private sealed class ThrowingCandidateDiscoveryService : ICandidateDiscoveryService
    {
        private readonly Exception _exception;

        public ThrowingCandidateDiscoveryService(Exception exception)
        {
            _exception = exception;
        }

        public Task<DownloadCandidatesResponse> FindAsync(
            string providerCode,
            string providerBookKey,
            string mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }
}

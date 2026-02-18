using System.Net;
using System.Net.Http.Json;
using Bookshelf.Api.Api.Middleware;
using Bookshelf.Shared.Contracts.Api;
using Microsoft.AspNetCore.Mvc.Testing;

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
}

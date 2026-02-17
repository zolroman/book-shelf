using Bookshelf.Api.Health;
using Bookshelf.Domain.Abstractions;
using Bookshelf.Infrastructure.Options;
using Bookshelf.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Bookshelf.Api.Tests;

public class HealthChecksTests
{
    [Fact]
    public async Task ExternalIntegrationsHealthCheck_Returns_Healthy_For_Valid_Config()
    {
        var check = new ExternalIntegrationsHealthCheck(
            Options.Create(new FantLabSearchOptions
            {
                Enabled = true,
                BaseUrl = "https://api.fantlab.ru"
            }),
            Options.Create(new JackettOptions { Enabled = false }),
            Options.Create(new QbittorrentOptions { Enabled = false }));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ExternalIntegrationsHealthCheck_Returns_Degraded_When_Jackett_Key_Is_Missing()
    {
        var check = new ExternalIntegrationsHealthCheck(
            Options.Create(new FantLabSearchOptions { Enabled = false }),
            Options.Create(new JackettOptions
            {
                Enabled = true,
                BaseUrl = "http://localhost:9117",
                ApiKey = ""
            }),
            Options.Create(new QbittorrentOptions { Enabled = false }));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task RepositoryHealthCheck_Returns_Healthy_When_Repository_Is_Accessible()
    {
        var repository = new InMemoryBookshelfRepository(new FixedClock());
        var check = new RepositoryHealthCheck(repository);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 2, 17, 10, 0, 0, DateTimeKind.Utc);
    }
}

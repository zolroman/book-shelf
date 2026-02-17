using Bookshelf.Infrastructure.Options;
using Bookshelf.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Bookshelf.Api.Health;

public sealed class RepositoryHealthCheck(IBookshelfRepository repository) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await repository.GetUserAsync(1, cancellationToken);
            return user is null
                ? HealthCheckResult.Degraded("Repository is reachable but seed data is missing.")
                : HealthCheckResult.Healthy("Repository reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Repository check failed.", exception);
        }
    }
}

public sealed class ExternalIntegrationsHealthCheck(
    IOptions<FantLabSearchOptions> fantlabOptions,
    IOptions<JackettOptions> jackettOptions,
    IOptions<QbittorrentOptions> qbittorrentOptions) : IHealthCheck
{
    private readonly FantLabSearchOptions _fantlabOptions = fantlabOptions.Value;
    private readonly JackettOptions _jackettOptions = jackettOptions.Value;
    private readonly QbittorrentOptions _qbittorrentOptions = qbittorrentOptions.Value;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["fantlab_enabled"] = _fantlabOptions.Enabled,
            ["jackett_enabled"] = _jackettOptions.Enabled,
            ["qbittorrent_enabled"] = _qbittorrentOptions.Enabled
        };

        var failures = new List<string>();

        if (_fantlabOptions.Enabled && !Uri.TryCreate(_fantlabOptions.BaseUrl, UriKind.Absolute, out _))
        {
            failures.Add("FantLab base URL is invalid.");
        }

        if (_jackettOptions.Enabled)
        {
            if (!Uri.TryCreate(_jackettOptions.BaseUrl, UriKind.Absolute, out _))
            {
                failures.Add("Jackett base URL is invalid.");
            }

            if (string.IsNullOrWhiteSpace(_jackettOptions.ApiKey))
            {
                failures.Add("Jackett API key is missing.");
            }
        }

        if (_qbittorrentOptions.Enabled)
        {
            if (!Uri.TryCreate(_qbittorrentOptions.BaseUrl, UriKind.Absolute, out _))
            {
                failures.Add("qBittorrent base URL is invalid.");
            }

            if (string.IsNullOrWhiteSpace(_qbittorrentOptions.Username) || string.IsNullOrWhiteSpace(_qbittorrentOptions.Password))
            {
                failures.Add("qBittorrent credentials are missing.");
            }
        }

        if (failures.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("External integration configuration is valid.", data: data));
        }

        return Task.FromResult(HealthCheckResult.Degraded(string.Join(' ', failures), data: data));
    }
}

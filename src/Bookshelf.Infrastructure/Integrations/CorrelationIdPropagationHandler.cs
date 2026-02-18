using Bookshelf.Shared.Diagnostics;

namespace Bookshelf.Infrastructure.Integrations;

public sealed class CorrelationIdPropagationHandler : DelegatingHandler
{
    private const string HeaderName = "X-Correlation-Id";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationContext.Current;
        if (!string.IsNullOrWhiteSpace(correlationId)
            && !request.Headers.Contains(HeaderName))
        {
            request.Headers.TryAddWithoutValidation(HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

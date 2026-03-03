using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Observability.Correlation;

/// <summary>
/// DelegatingHandler that propagates the correlation ID to outgoing HTTP requests.
/// </summary>
public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly IMediatorContext _context;
    private readonly CorrelationOptions _options;

    /// <summary>
    /// Creates a new CorrelationIdDelegatingHandler.
    /// </summary>
    public CorrelationIdDelegatingHandler(IMediatorContext context, CorrelationOptions options)
    {
        _context = context;
        _options = options;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!_options.PropagateToHttpClient)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        if (!string.IsNullOrEmpty(_context.CorrelationId))
        {
            request.Headers.TryAddWithoutValidation(_options.HeaderName, _context.CorrelationId);
        }

        if (!string.IsNullOrEmpty(_context.CausationId))
        {
            request.Headers.TryAddWithoutValidation("X-Causation-ID", _context.CausationId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

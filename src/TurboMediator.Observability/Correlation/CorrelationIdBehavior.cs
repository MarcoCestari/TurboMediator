using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TurboMediator.Observability.Correlation;

/// <summary>
/// Pipeline behavior that manages correlation ID propagation across the pipeline.
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public class CorrelationIdBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IMediatorContext _context;
    private readonly CorrelationOptions _options;
    private readonly ILogger<CorrelationIdBehavior<TMessage, TResponse>>? _logger;

    /// <summary>
    /// Creates a new CorrelationIdBehavior.
    /// </summary>
    /// <param name="context">The mediator context.</param>
    /// <param name="options">The correlation options.</param>
    /// <param name="logger">Optional logger.</param>
    public CorrelationIdBehavior(
        IMediatorContext context,
        CorrelationOptions? options = null,
        ILogger<CorrelationIdBehavior<TMessage, TResponse>>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options ?? new CorrelationOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        EnsureCorrelationId();

        // Add to Activity baggage for distributed tracing
        if (_options.AddToActivityBaggage && Activity.Current != null)
        {
            Activity.Current.SetBaggage(_options.HeaderName, _context.CorrelationId);

            if (!string.IsNullOrEmpty(_context.CausationId))
            {
                Activity.Current.SetBaggage("causation-id", _context.CausationId);
            }
        }

        // Add to log scope
        if (_options.AddToLogScope && _logger != null)
        {
            using var scope = _logger.BeginScope(
                "CorrelationId: {CorrelationId}, MessageType: {MessageType}",
                _context.CorrelationId,
                typeof(TMessage).Name);

            return await next();
        }

        return await next();
    }

    private void EnsureCorrelationId()
    {
        // Try to get from external provider (e.g., HTTP context)
        var externalCorrelationId = _options.CorrelationIdProvider?.Invoke();
        if (!string.IsNullOrEmpty(externalCorrelationId))
        {
            _context.CorrelationId = externalCorrelationId;
            return;
        }

        // Generate if missing and configured to do so
        if (string.IsNullOrEmpty(_context.CorrelationId) && _options.GenerateIfMissing)
        {
            _context.CorrelationId = _options.CorrelationIdGenerator();
        }
    }
}

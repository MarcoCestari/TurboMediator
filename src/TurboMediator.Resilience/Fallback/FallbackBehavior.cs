using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.Resilience.Fallback;

/// <summary>
/// Pipeline behavior that provides fallback functionality when the primary handler fails.
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public class FallbackBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly FallbackOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<FallbackHandlerInfo>? _attributeFallbacks;

    /// <summary>
    /// Creates a new FallbackBehavior.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving fallback handlers.</param>
    /// <param name="options">The fallback options.</param>
    public FallbackBehavior(IServiceProvider serviceProvider, FallbackOptions? options = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? new FallbackOptions();
        _attributeFallbacks = GetFallbackHandlersFromAttribute();
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex) when (ShouldHandleException(ex))
        {
            return await ExecuteFallbacksAsync(message, ex, cancellationToken);
        }
    }

    private bool ShouldHandleException(Exception exception)
    {
        // Check custom predicate first
        if (_options.ShouldHandle != null)
        {
            return _options.ShouldHandle(exception);
        }

        // Check exception types list
        if (_options.ExceptionTypes.Count > 0)
        {
            return _options.ExceptionTypes.Any(t => t.IsInstanceOfType(exception));
        }

        // Default: handle all exceptions
        return true;
    }

    private async ValueTask<TResponse> ExecuteFallbacksAsync(
        TMessage message,
        Exception originalException,
        CancellationToken cancellationToken)
    {
        var fallbacks = _attributeFallbacks ?? Array.Empty<FallbackHandlerInfo>();
        var attemptNumber = 0;
        Exception? lastException = originalException;

        foreach (var fallbackInfo in fallbacks)
        {
            attemptNumber++;

            // Check if this fallback handles this exception type
            if (!ShouldFallbackHandle(fallbackInfo, originalException))
            {
                continue;
            }

            try
            {
                _options.OnFallbackInvoked?.Invoke(new FallbackInvokedInfo(
                    typeof(TMessage).Name,
                    lastException,
                    fallbackInfo.HandlerType,
                    attemptNumber));

                var handler = ResolveFallbackHandler(fallbackInfo.HandlerType);
                if (handler != null)
                {
                    return await handler.HandleFallbackAsync(message, lastException, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                // Continue to next fallback
            }
        }

        // All fallbacks failed or no fallbacks found
        if (_options.ThrowOnAllFallbacksFailure)
        {
            throw lastException;
        }

        if (_options.DefaultValueFactory != null)
        {
            return (TResponse)_options.DefaultValueFactory();
        }

        return default!;
    }

    private static bool ShouldFallbackHandle(FallbackHandlerInfo fallbackInfo, Exception exception)
    {
        if (fallbackInfo.ExceptionTypes == null || fallbackInfo.ExceptionTypes.Length == 0)
        {
            return true;
        }

        return fallbackInfo.ExceptionTypes.Any(t => t.IsInstanceOfType(exception));
    }

    private IFallbackHandler<TMessage, TResponse>? ResolveFallbackHandler(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        // Try to resolve from DI first
        var handler = _serviceProvider.GetService(handlerType);
        if (handler != null)
        {
            return handler as IFallbackHandler<TMessage, TResponse>;
        }

        // Try to create instance directly
        try
        {
            return ActivatorUtilities.CreateInstance(_serviceProvider, handlerType)
                as IFallbackHandler<TMessage, TResponse>;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<FallbackHandlerInfo>? GetFallbackHandlersFromAttribute()
    {
        var attributes = typeof(TMessage).GetCustomAttributes<FallbackAttribute>(true);
        if (!attributes.Any())
        {
            return null;
        }

        return attributes
            .OrderBy(a => a.Order)
            .Select(a => new FallbackHandlerInfo(a.FallbackHandlerType, a.OnExceptionTypes))
            .ToList();
    }

    private sealed class FallbackHandlerInfo
    {
        public Type HandlerType { get; }
        public Type[]? ExceptionTypes { get; }

        public FallbackHandlerInfo(Type handlerType, Type[]? exceptionTypes)
        {
            HandlerType = handlerType;
            ExceptionTypes = exceptionTypes;
        }
    }
}

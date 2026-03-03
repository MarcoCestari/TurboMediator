using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TurboMediator.SourceGenerator;

/// <summary>
/// TurboMediator incremental source generator.
/// Generates IMediator implementation and DI registration.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TurboMediatorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all handler classes
        var handlerProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => Parser.IsHandlerCandidate(node),
                transform: static (ctx, ct) => Parser.GetHandlerInfo(ctx, ct))
            .Where(static info => info.HasValue)
            .Select(static (info, _) => info!.Value);

        // Find all message types (for diagnostics)
        var messageProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => Parser.IsMessageCandidate(node),
                transform: static (ctx, ct) => Parser.GetMessageInfo(ctx, ct))
            .Where(static info => info.HasValue)
            .Select(static (info, _) => info!.Value);

        // Collect all handlers
        var handlersCollected = handlerProvider.Collect();

        // Collect all messages
        var messagesCollected = messageProvider.Collect();

        // Combine handlers and messages for diagnostics
        var combined = handlersCollected.Combine(messagesCollected);

        // Generate source
        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var (handlers, messages) = source;

            // Report diagnostics for missing handlers
            ReportDiagnostics(spc, handlers, messages);

            // Generate code
            if (handlers.Length > 0)
            {
                var code = Emitter.GenerateMediatorImplementation(handlers, messages, "TurboMediator.Generated");
                spc.AddSource("TurboMediator.g.cs", code);
            }
            else
            {
                // Generate empty mediator if no handlers
                var code = GenerateEmptyMediator();
                spc.AddSource("TurboMediator.g.cs", code);
            }
        });
    }

    private static void ReportDiagnostics(
        SourceProductionContext context,
        ImmutableArray<HandlerInfo> handlers,
        ImmutableArray<MessageInfo> messages)
    {
        // TURBO008: Handler must be public
        foreach (var handler in handlers)
        {
            if (!handler.IsPublic)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.HandlerMustBePublic,
                    Location.None,
                    handler.HandlerTypeFullName));
            }
        }

        // TURBO005: Abstract handler type
        foreach (var handler in handlers)
        {
            if (handler.IsAbstract)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AbstractHandlerType,
                    Location.None,
                    handler.HandlerTypeFullName));
            }
        }

        // Filter out abstract/non-public handlers for subsequent checks
        var concreteHandlers = handlers.Where(h => !h.IsAbstract).ToImmutableArray();

        // TURBO001: Missing handler (non-streaming, non-notification)
        foreach (var message in messages)
        {
            if (message.Kind == MessageKind.Notification)
                continue;
            if (message.IsStreaming)
                continue;

            var hasHandler = concreteHandlers.Any(h =>
                !h.IsStreaming && h.MessageTypeFullName == message.TypeFullName);

            if (!hasHandler)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingHandler,
                    Location.None,
                    message.TypeFullName));
            }
        }

        // TURBO002: Multiple handlers (non-streaming, non-notification)
        var handlersByMessage = concreteHandlers
            .Where(h => h.Kind != HandlerKind.Notification && !h.IsStreaming)
            .GroupBy(h => h.MessageTypeFullName)
            .Where(g => g.Count() > 1);

        foreach (var group in handlersByMessage)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MultipleHandlers,
                Location.None,
                group.Key));
        }

        // TURBO004: Response type mismatch
        foreach (var handler in concreteHandlers)
        {
            if (handler.Kind == HandlerKind.Notification)
                continue;

            var matchingMessage = messages.FirstOrDefault(m =>
                m.TypeFullName == handler.MessageTypeFullName);

            if (matchingMessage.TypeFullName != null &&
                matchingMessage.ResponseTypeFullName != null &&
                handler.ResponseTypeFullName != null &&
                matchingMessage.ResponseTypeFullName != handler.ResponseTypeFullName)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ResponseTypeMismatch,
                    Location.None,
                    handler.HandlerTypeFullName,
                    handler.ResponseTypeFullName,
                    matchingMessage.TypeFullName,
                    matchingMessage.ResponseTypeFullName));
            }
        }

        // TURBO006: Missing stream handler
        foreach (var message in messages)
        {
            if (message.Kind == MessageKind.Notification)
                continue;
            if (!message.IsStreaming)
                continue;

            var hasHandler = concreteHandlers.Any(h =>
                h.IsStreaming && h.MessageTypeFullName == message.TypeFullName);

            if (!hasHandler)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingStreamHandler,
                    Location.None,
                    message.TypeFullName));
            }
        }

        // TURBO007: Multiple stream handlers
        var streamHandlersByMessage = concreteHandlers
            .Where(h => h.Kind != HandlerKind.Notification && h.IsStreaming)
            .GroupBy(h => h.MessageTypeFullName)
            .Where(g => g.Count() > 1);

        foreach (var group in streamHandlersByMessage)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MultipleStreamHandlers,
                Location.None,
                group.Key));
        }

        // TURBO009: Duplicate notification handler
        var notificationHandlers = concreteHandlers
            .Where(h => h.Kind == HandlerKind.Notification)
            .GroupBy(h => (h.HandlerTypeFullName, h.MessageTypeFullName))
            .Where(g => g.Count() > 1);

        foreach (var group in notificationHandlers)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DuplicateNotificationHandler,
                Location.None,
                group.Key.HandlerTypeFullName,
                group.Key.MessageTypeFullName));
        }
    }

    private static string GenerateEmptyMediator()
    {
        return """
            // <auto-generated />
            #nullable enable

            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using TurboMediator;

            namespace TurboMediator.Generated
            {
                /// <summary>
                /// Generated Mediator implementation.
                /// </summary>
                public sealed class Mediator : IMediator
                {
                    private readonly IServiceProvider _serviceProvider;

                    public Mediator(IServiceProvider serviceProvider)
                    {
                        _serviceProvider = serviceProvider;
                    }

                    /// <inheritdoc />
                    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
                    {
                        throw new InvalidOperationException($"No handler registered for {request.GetType().Name}");
                    }

                    /// <inheritdoc />
                    public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
                    {
                        throw new InvalidOperationException($"No handler registered for {command.GetType().Name}");
                    }

                    /// <inheritdoc />
                    public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
                    {
                        throw new InvalidOperationException($"No handler registered for {query.GetType().Name}");
                    }

                    /// <inheritdoc />
                    public async ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
                        where TNotification : INotification
                    {
                        var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>();
                        foreach (var handler in handlers)
                        {
                            await handler.Handle(notification, cancellationToken);
                        }
                    }

                    /// <inheritdoc />
                    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
                    {
                        throw new InvalidOperationException($"No stream handler registered for {request.GetType().Name}");
                    }

                    /// <inheritdoc />
                    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default)
                    {
                        throw new InvalidOperationException($"No stream handler registered for {command.GetType().Name}");
                    }

                    /// <inheritdoc />
                    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default)
                    {
                        throw new InvalidOperationException($"No stream handler registered for {query.GetType().Name}");
                    }
                }

                /// <summary>
                /// Extension methods for registering TurboMediator with DI.
                /// </summary>
                public static class TurboMediatorServiceCollectionExtensions
                {
                    /// <summary>
                    /// Adds TurboMediator services to the service collection.
                    /// </summary>
                    public static IServiceCollection AddTurboMediator(this IServiceCollection services)
                    {
                        return AddTurboMediator(services, ServiceLifetime.Scoped);
                    }

                    /// <summary>
                    /// Adds TurboMediator services to the service collection with the specified lifetime.
                    /// </summary>
                    public static IServiceCollection AddTurboMediator(this IServiceCollection services, ServiceLifetime lifetime)
                    {
                        services.Add(new ServiceDescriptor(typeof(IMediator), typeof(Mediator), lifetime));
                        services.Add(new ServiceDescriptor(typeof(ISender), sp => sp.GetRequiredService<IMediator>(), lifetime));
                        services.Add(new ServiceDescriptor(typeof(IPublisher), sp => sp.GetRequiredService<IMediator>(), lifetime));
                        return services;
                    }
                }
            }
            """;
    }
}

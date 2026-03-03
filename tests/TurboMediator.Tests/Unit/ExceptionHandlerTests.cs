using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator;
using TurboMediator.Generated;
using TurboMediator.Tests.Handlers;
using TurboMediator.Tests.Messages;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Exception Handlers.
/// </summary>
public class ExceptionHandlerTests
{
    [Fact]
    public async Task ExceptionHandler_CanHandle_AndReturnAlternativeResponse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddExceptionHandler<TestExceptionHandler>();

        // Register a handler that throws
        services.AddSingleton<IQueryHandler<FailingQuery, string>, FailingQueryHandler>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        TestExceptionHandler.Reset();

        // Act
        var result = await mediator.Send(new FailingQuery());

        // Assert
        result.Should().Be("Handled exception!");
        TestExceptionHandler.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExceptionHandler_CanRethrow_IfNotHandled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddExceptionHandler<NonHandlingExceptionHandler>();

        // Register a handler that throws
        services.AddSingleton<IQueryHandler<FailingQuery, string>, FailingQueryHandler>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act & Assert
        var act = () => mediator.Send(new FailingQuery()).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Query handler failed!");
    }
}

// Test messages
public record FailingQuery : IQuery<string>;

public class FailingQueryHandler : IQueryHandler<FailingQuery, string>
{
    public ValueTask<string> Handle(FailingQuery query, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Query handler failed!");
    }
}

// Test exception handlers
public class TestExceptionHandler : IMessageExceptionHandler<FailingQuery, string, Exception>
{
    public static bool WasCalled { get; private set; }
    public static Exception? ReceivedException { get; private set; }

    public static void Reset()
    {
        WasCalled = false;
        ReceivedException = null;
    }

    public ValueTask<ExceptionHandlingResult<string>> Handle(FailingQuery message, Exception exception, CancellationToken cancellationToken)
    {
        WasCalled = true;
        ReceivedException = exception;

        return new ValueTask<ExceptionHandlingResult<string>>(
            ExceptionHandlingResult<string>.HandledWith("Handled exception!"));
    }
}

public class NonHandlingExceptionHandler : IMessageExceptionHandler<FailingQuery, string, Exception>
{
    public ValueTask<ExceptionHandlingResult<string>> Handle(FailingQuery message, Exception exception, CancellationToken cancellationToken)
    {
        // Return NotHandled to rethrow the exception
        return new ValueTask<ExceptionHandlingResult<string>>(
            ExceptionHandlingResult<string>.NotHandled());
    }
}

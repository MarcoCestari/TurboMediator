using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using TurboMediator.Generated;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Tests for streaming handlers (IStreamRequest, IStreamCommand, IStreamQuery).
/// </summary>
[Collection("StreamingTests")]
public class StreamingTests
{
    [Fact]
    public async Task StreamRequest_ShouldReturnItems_WhenHandlerExists()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var items = new List<int>();
        await foreach (var item in mediator.CreateStream(new GetNumbersStreamRequest(5)))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(5, items.Count);
        Assert.Equal([1, 2, 3, 4, 5], items);
    }

    [Fact]
    public async Task StreamCommand_ShouldReturnItems_WhenHandlerExists()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var items = new List<string>();
        await foreach (var item in mediator.CreateStream(new ProcessItemsStreamCommand(["A", "B", "C"])))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal(["Processed: A", "Processed: B", "Processed: C"], items);
    }

    [Fact]
    public async Task StreamQuery_ShouldReturnItems_WhenHandlerExists()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var items = new List<UserDto>();
        await foreach (var item in mediator.CreateStream(new GetAllUsersStreamQuery()))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal("Alice", items[0].Name);
        Assert.Equal("Bob", items[1].Name);
        Assert.Equal("Charlie", items[2].Name);
    }

    [Fact]
    public async Task StreamRequest_ShouldSupportCancellation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var cts = new CancellationTokenSource();

        // Act
        var items = new List<int>();
        await foreach (var item in mediator.CreateStream(new GetNumbersStreamRequest(100), cts.Token))
        {
            items.Add(item);
            if (items.Count == 3)
            {
                await cts.CancelAsync();
                break;
            }
        }

        // Assert
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task StreamRequest_WithPipelineBehavior_ShouldExecuteBehavior()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddStreamPipelineBehavior<StreamLoggingBehavior>();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        StreamLoggingBehavior.CallCount = 0;

        // Act
        var items = new List<int>();
        await foreach (var item in mediator.CreateStream(new GetNumbersStreamRequest(3)))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal(1, StreamLoggingBehavior.CallCount);
    }

    [Fact]
    public async Task StreamRequest_WithPreProcessor_ShouldExecuteBeforeHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddStreamPreProcessor<StreamValidationPreProcessor>();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        StreamValidationPreProcessor.CallCount = 0;

        // Act
        var items = new List<int>();
        await foreach (var item in mediator.CreateStream(new GetNumbersStreamRequest(3)))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal(1, StreamValidationPreProcessor.CallCount);
    }

    [Fact]
    public async Task StreamRequest_WithPostProcessor_ShouldWrapStream()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTurboMediator();
        services.AddStreamPostProcessor<StreamMetricsPostProcessor>();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        StreamMetricsPostProcessor.ItemCount = 0;

        // Act
        var items = new List<int>();
        await foreach (var item in mediator.CreateStream(new GetNumbersStreamRequest(5)))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(5, items.Count);
        Assert.Equal(5, StreamMetricsPostProcessor.ItemCount);
    }
}

// Stream Messages
public record GetNumbersStreamRequest(int Count) : IStreamRequest<int>;
public record ProcessItemsStreamCommand(string[] Items) : IStreamCommand<string>;
public record GetAllUsersStreamQuery() : IStreamQuery<UserDto>;
public record UserDto(int Id, string Name);

// Stream Handlers
public class GetNumbersStreamRequestHandler : IStreamRequestHandler<GetNumbersStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(
        GetNumbersStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken); // Simulate async work
            yield return i;
        }
    }
}

public class ProcessItemsStreamCommandHandler : IStreamCommandHandler<ProcessItemsStreamCommand, string>
{
    public async IAsyncEnumerable<string> Handle(
        ProcessItemsStreamCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var item in command.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
            yield return $"Processed: {item}";
        }
    }
}

public class GetAllUsersStreamQueryHandler : IStreamQueryHandler<GetAllUsersStreamQuery, UserDto>
{
    public async IAsyncEnumerable<UserDto> Handle(
        GetAllUsersStreamQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var users = new[] { new UserDto(1, "Alice"), new UserDto(2, "Bob"), new UserDto(3, "Charlie") };
        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
            yield return user;
        }
    }
}

// Stream Pipeline Behavior
public class StreamLoggingBehavior : IStreamPipelineBehavior<GetNumbersStreamRequest, int>
{
    public static int CallCount { get; set; }

    public IAsyncEnumerable<int> Handle(
        GetNumbersStreamRequest message,
        StreamHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        CallCount++;
        return next();
    }
}

// Stream Pre-Processor
public class StreamValidationPreProcessor : IStreamPreProcessor<GetNumbersStreamRequest>
{
    public static int CallCount { get; set; }

    public ValueTask Process(GetNumbersStreamRequest message, CancellationToken cancellationToken)
    {
        CallCount++;
        if (message.Count < 0)
        {
            throw new ArgumentException("Count must be non-negative");
        }
        return ValueTask.CompletedTask;
    }
}

// Stream Post-Processor
public class StreamMetricsPostProcessor : IStreamPostProcessor<GetNumbersStreamRequest, int>
{
    public static int ItemCount { get; set; }

    public async IAsyncEnumerable<int> Process(
        GetNumbersStreamRequest message,
        IAsyncEnumerable<int> stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            ItemCount++;
            yield return item;
        }
    }
}

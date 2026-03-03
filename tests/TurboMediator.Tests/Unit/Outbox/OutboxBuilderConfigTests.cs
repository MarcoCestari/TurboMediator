using System;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Persistence;
using TurboMediator.Persistence.Inbox;
using TurboMediator.Persistence.Outbox;
using Xunit;

namespace TurboMediator.Tests.Outbox;

/// <summary>
/// Unit tests for OutboxBuilder dead letter and inbox configuration.
/// </summary>
public class OutboxBuilderConfigTests
{
    #region Test Types

    public class TestDeadLetterHandler : IOutboxDeadLetterHandler
    {
        public ValueTask HandleAsync(OutboxMessage message, string reason, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    public class TestInboxStore : IInboxStore
    {
        public ValueTask<bool> HasBeenProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);
        public ValueTask RecordAsync(InboxMessage message, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        public ValueTask<int> CleanupAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(0);
    }

    public class TestOutboxStore : IOutboxStore
    {
        public ValueTask SaveAsync(OutboxMessage message, CancellationToken ct = default) => ValueTask.CompletedTask;
        public IAsyncEnumerable<OutboxMessage> GetPendingAsync(int batchSize, CancellationToken ct = default) => throw new NotImplementedException();
        public ValueTask MarkAsProcessingAsync(Guid messageId, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask<bool> TryClaimAsync(Guid messageId, string workerId, CancellationToken ct = default) => ValueTask.FromResult(true);
        public ValueTask MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask IncrementRetryAsync(Guid messageId, string error, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask MoveToDeadLetterAsync(Guid messageId, string reason, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask<int> CleanupAsync(TimeSpan olderThan, CancellationToken ct = default) => ValueTask.FromResult(0);
    }

    #endregion

    /// <summary>
    /// Helper to build outbox configuration and return services.
    /// Uses the OutboxBuilder directly since AddTurboMediator is source-generated.
    /// </summary>
    private static IServiceProvider BuildOutboxConfig(Action<OutboxBuilder> configure)
    {
        var services = new ServiceCollection();
        var builder = new TurboMediatorBuilder(services);
        builder.WithOutbox(configure);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void WithDeadLetterHandler_ShouldRegisterHandler()
    {
        // Act
        var sp = BuildOutboxConfig(outbox =>
        {
            outbox.UseStore<TestOutboxStore>()
                  .WithDeadLetterHandler<TestDeadLetterHandler>();
        });

        // Assert
        using var scope = sp.CreateScope();
        var handler = scope.ServiceProvider.GetService<IOutboxDeadLetterHandler>();
        handler.Should().NotBeNull();
        handler.Should().BeOfType<TestDeadLetterHandler>();
    }

    [Fact]
    public void WithInbox_ShouldRegisterInboxOptions()
    {
        // Act
        var sp = BuildOutboxConfig(outbox =>
        {
            outbox.UseStore<TestOutboxStore>()
                  .WithInbox();
        });

        // Assert
        var options = sp.GetService<InboxOptions>();
        options.Should().NotBeNull();
        options!.RetentionPeriod.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void WithInboxRetention_ShouldConfigureRetentionPeriod()
    {
        // Act
        var sp = BuildOutboxConfig(outbox =>
        {
            outbox.UseStore<TestOutboxStore>()
                  .WithInbox()
                  .WithInboxRetention(TimeSpan.FromDays(30));
        });

        // Assert
        var options = sp.GetService<InboxOptions>();
        options.Should().NotBeNull();
        options!.RetentionPeriod.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void WithInbox_CustomStore_ShouldRegisterCustomStore()
    {
        // Act
        var sp = BuildOutboxConfig(outbox =>
        {
            outbox.UseStore<TestOutboxStore>()
                  .WithInbox<TestInboxStore>();
        });

        // Assert
        using var scope = sp.CreateScope();
        var store = scope.ServiceProvider.GetService<IInboxStore>();
        store.Should().NotBeNull();
        store.Should().BeOfType<TestInboxStore>();
    }

    [Fact]
    public void WithoutDeadLetterHandler_ShouldNotRegister()
    {
        // Act
        var sp = BuildOutboxConfig(outbox =>
        {
            outbox.UseStore<TestOutboxStore>()
                  .AddProcessor();
        });

        // Assert
        using var scope = sp.CreateScope();
        var handler = scope.ServiceProvider.GetService<IOutboxDeadLetterHandler>();
        handler.Should().BeNull();
    }

    [Fact]
    public void WithoutInbox_ShouldNotRegisterInboxOptions()
    {
        // Act
        var sp = BuildOutboxConfig(outbox =>
        {
            outbox.UseStore<TestOutboxStore>()
                  .AddProcessor();
        });

        // Assert
        var options = sp.GetService<InboxOptions>();
        options.Should().BeNull();
    }

    [Fact]
    public void OutboxMessageStatus_DeadLettered_ShouldHaveValue3()
    {
        // Assert
        ((int)OutboxMessageStatus.DeadLettered).Should().Be(3);
    }

    [Fact]
    public void OutboxMessageStatus_ShouldHaveAll4Values()
    {
        // Assert
        Enum.GetValues<OutboxMessageStatus>().Should().HaveCount(4);
        Enum.GetValues<OutboxMessageStatus>().Should().Contain(new[]
        {
            OutboxMessageStatus.Pending,
            OutboxMessageStatus.Processing,
            OutboxMessageStatus.Processed,
            OutboxMessageStatus.DeadLettered
        });
    }

    [Fact]
    public void OutboxBuilder_ShouldRegisterProcessorOptions_WhenAddProcessorCalled()
    {
        // Act
        var sp = BuildOutboxConfig(outbox =>
        {
            outbox.UseStore<TestOutboxStore>()
                  .AddProcessor()
                  .WithMaxRetries(10)
                  .WithBatchSize(50)
                  .WithProcessingInterval(TimeSpan.FromSeconds(15));
        });

        // Assert
        var options = sp.GetService<OutboxProcessorOptions>();
        options.Should().NotBeNull();
        options!.MaxRetryAttempts.Should().Be(10);
        options.BatchSize.Should().Be(50);
        options.ProcessingInterval.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void OutboxBuilder_CombinedConfig_ShouldRegisterAll()
    {
        // Act
        var sp = BuildOutboxConfig(outbox =>
        {
            outbox.UseStore<TestOutboxStore>()
                  .AddProcessor()
                  .WithDeadLetterHandler<TestDeadLetterHandler>()
                  .WithInbox<TestInboxStore>()
                  .WithInboxRetention(TimeSpan.FromDays(14));
        });

        // Assert
        using var scope = sp.CreateScope();
        scope.ServiceProvider.GetService<IOutboxDeadLetterHandler>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IInboxStore>().Should().NotBeNull();
        sp.GetService<InboxOptions>()!.RetentionPeriod.Should().Be(TimeSpan.FromDays(14));
        sp.GetService<OutboxProcessorOptions>().Should().NotBeNull();
    }
}

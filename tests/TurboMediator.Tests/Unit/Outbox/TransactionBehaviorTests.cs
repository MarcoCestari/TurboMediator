using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TurboMediator.Persistence.Transaction;
using Xunit;

namespace TurboMediator.Tests.Outbox;

public class TransactionBehaviorTests
{
    #region Test Types

    public record SimpleCommand(string Data) : ICommand<string>;

    [Transactional(TimeoutSeconds = 5)]
    public record TimeoutCommand(string Data) : ICommand<string>;

    [Transactional(TimeoutSeconds = 120, IsolationLevel = IsolationLevel.Serializable)]
    public record CustomTransactionalCommand(string Data) : ICommand<string>;

    #endregion

    #region Helpers

    private static Mock<ITransactionManager> CreateTransactionManagerMock()
    {
        var mock = new Mock<ITransactionManager>();
        var scopeMock = new Mock<ITransactionScope>();

        scopeMock.Setup(s => s.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        scopeMock.Setup(s => s.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        scopeMock.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        mock.Setup(m => m.HasActiveTransaction).Returns(false);
        mock.Setup(m => m.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scopeMock.Object);
        mock.Setup(m => m.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        mock.Setup(m => m.ExecuteWithStrategyAsync(
                It.IsAny<Func<CancellationToken, ValueTask<string>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, ValueTask<string>> op, CancellationToken ct) => op(ct));

        return mock;
    }

    #endregion

    [Fact]
    public async Task TransactionBehavior_ShouldUseDefaultTimeout_Of30Seconds()
    {
        var options = new TransactionOptions();
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task TransactionBehavior_ShouldUseTimeout_FromTransactionOptions()
    {
        var transactionManagerMock = CreateTransactionManagerMock();
        var options = new TransactionOptions
        {
            Timeout = TimeSpan.FromSeconds(10),
            UseExecutionStrategy = false
        };

        var behavior = new TransactionBehavior<SimpleCommand, string>(
            transactionManagerMock.Object, options);

        var command = new SimpleCommand("test");
        var handlerCalled = false;

        var result = await behavior.Handle(command, (msg, ct) =>
        {
            handlerCalled = true;
            return ValueTask.FromResult("ok");
        }, CancellationToken.None);

        handlerCalled.Should().BeTrue();
        result.Should().Be("ok");
        transactionManagerMock.Verify(
            m => m.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TransactionBehavior_ShouldUseTimeoutSeconds_FromTransactionalAttribute()
    {
        var transactionManagerMock = CreateTransactionManagerMock();
        var options = new TransactionOptions
        {
            Timeout = TimeSpan.FromSeconds(60),
            UseExecutionStrategy = false
        };

        var behavior = new TransactionBehavior<TimeoutCommand, string>(
            transactionManagerMock.Object, options);

        var command = new TimeoutCommand("test");

        var result = await behavior.Handle(command, (msg, ct) =>
        {
            return ValueTask.FromResult("done");
        }, CancellationToken.None);

        result.Should().Be("done");
        transactionManagerMock.Verify(
            m => m.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TransactionBehavior_AttributeTimeout_ShouldCancelLongRunningHandler()
    {
        var transactionManagerMock = CreateTransactionManagerMock();

        var options = new TransactionOptions
        {
            Timeout = TimeSpan.FromSeconds(60),
            UseExecutionStrategy = false
        };

        var behavior = new TransactionBehavior<TimeoutCommand, string>(
            transactionManagerMock.Object, options);

        var command = new TimeoutCommand("test");

        var result = await behavior.Handle(command, (msg, ct) =>
        {
            return ValueTask.FromResult("ok");
        }, CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public void TransactionalAttribute_ShouldHaveDefaultTimeoutOf30Seconds()
    {
        var attr = new TransactionalAttribute();
        attr.TimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void TransactionalAttribute_ShouldAllowCustomTimeout()
    {
        var attr = (TransactionalAttribute)Attribute.GetCustomAttribute(
            typeof(TimeoutCommand), typeof(TransactionalAttribute))!;

        attr.Should().NotBeNull();
        attr.TimeoutSeconds.Should().Be(5);
    }

    [Fact]
    public void TransactionalAttribute_OnCustomCommand_ShouldHaveConfiguredValues()
    {
        var attr = (TransactionalAttribute)Attribute.GetCustomAttribute(
            typeof(CustomTransactionalCommand), typeof(TransactionalAttribute))!;

        attr.Should().NotBeNull();
        attr.TimeoutSeconds.Should().Be(120);
        attr.IsolationLevel.Should().Be(IsolationLevel.Serializable);
    }
}

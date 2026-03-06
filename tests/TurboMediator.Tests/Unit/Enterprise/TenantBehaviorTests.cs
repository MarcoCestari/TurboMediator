using System;
using System.Threading;
using System.Threading.Tasks;
using TurboMediator.Enterprise.Tenant;
using Xunit;

namespace TurboMediator.Tests.Enterprise;

public class TenantBehaviorTests
{
    #region Test Messages

    [RequiresTenant]
    public record TenantRequiredCommand : IRequest<string>;

    public record NoTenantRequiredCommand : IRequest<string>;

    public record TenantAwareCommand(string? TenantId) : IRequest<string>, ITenantAware;

    [RequiresTenant]
    public record TenantRequiredAwareCommand(string? TenantId) : IRequest<string>, ITenantAware;

    #endregion

    [Fact]
    public async Task Handle_NoTenantRequired_NoTenantContext_ExecutesHandler()
    {
        // Arrange
        var tenantContext = new SimpleTenantContext();
        var behavior = new TenantBehavior<NoTenantRequiredCommand, string>(tenantContext);
        var message = new NoTenantRequiredCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<NoTenantRequiredCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.True(handlerCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_TenantRequired_NoTenantContext_ThrowsTenantRequired()
    {
        // Arrange
        var tenantContext = new SimpleTenantContext();
        var behavior = new TenantBehavior<TenantRequiredCommand, string>(tenantContext);
        var message = new TenantRequiredCommand();

        MessageHandlerDelegate<TenantRequiredCommand, string> next = (msg, ct) => new ValueTask<string>("Success");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantRequiredException>(
            async () => await behavior.Handle(message, next, CancellationToken.None));

        Assert.Equal(typeof(TenantRequiredCommand), exception.MessageType);
    }

    [Fact]
    public async Task Handle_TenantRequired_HasTenantContext_ExecutesHandler()
    {
        // Arrange
        var tenantContext = new SimpleTenantContext { TenantId = "tenant-123", TenantName = "Test Tenant" };
        var behavior = new TenantBehavior<TenantRequiredCommand, string>(tenantContext);
        var message = new TenantRequiredCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<TenantRequiredCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.True(handlerCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_TenantAware_MatchingTenant_ExecutesHandler()
    {
        // Arrange
        var tenantContext = new SimpleTenantContext { TenantId = "tenant-123" };
        var behavior = new TenantBehavior<TenantAwareCommand, string>(tenantContext);
        var message = new TenantAwareCommand("tenant-123");
        var handlerCalled = false;

        MessageHandlerDelegate<TenantAwareCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.True(handlerCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_TenantAware_MismatchingTenant_ThrowsTenantMismatch()
    {
        // Arrange
        var tenantContext = new SimpleTenantContext { TenantId = "tenant-123" };
        var options = new TenantBehaviorOptions { ValidateTenantMatch = true };
        var behavior = new TenantBehavior<TenantAwareCommand, string>(tenantContext, options);
        var message = new TenantAwareCommand("tenant-456");

        MessageHandlerDelegate<TenantAwareCommand, string> next = (msg, ct) => new ValueTask<string>("Success");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantMismatchException>(
            async () => await behavior.Handle(message, next, CancellationToken.None));

        Assert.Equal(typeof(TenantAwareCommand), exception.MessageType);
        Assert.Equal("tenant-123", exception.ExpectedTenantId);
        Assert.Equal("tenant-456", exception.ActualTenantId);
    }

    [Fact]
    public async Task Handle_TenantAware_MismatchingTenant_ValidationDisabled_ExecutesHandler()
    {
        // Arrange
        var tenantContext = new SimpleTenantContext { TenantId = "tenant-123" };
        var options = new TenantBehaviorOptions { ValidateTenantMatch = false };
        var behavior = new TenantBehavior<TenantAwareCommand, string>(tenantContext, options);
        var message = new TenantAwareCommand("tenant-456");
        var handlerCalled = false;

        MessageHandlerDelegate<TenantAwareCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.True(handlerCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_TenantAware_NullMessageTenant_NoContext_ExecutesHandler()
    {
        // Arrange
        var tenantContext = new SimpleTenantContext();
        var behavior = new TenantBehavior<TenantAwareCommand, string>(tenantContext);
        var message = new TenantAwareCommand(null);
        var handlerCalled = false;

        MessageHandlerDelegate<TenantAwareCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.True(handlerCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public void DefaultTenantContext_HasNoTenant()
    {
        // Arrange & Act
        var context = DefaultTenantContext.Instance;

        // Assert
        Assert.Null(context.TenantId);
        Assert.Null(context.TenantName);
        Assert.False(context.HasTenant);
    }

    [Fact]
    public void SimpleTenantContext_WithTenant_HasTenant()
    {
        // Arrange & Act
        var context = new SimpleTenantContext
        {
            TenantId = "test-tenant",
            TenantName = "Test Tenant"
        };

        // Assert
        Assert.Equal("test-tenant", context.TenantId);
        Assert.Equal("Test Tenant", context.TenantName);
        Assert.True(context.HasTenant);
    }
}

using System;
using FluentAssertions;
using TurboMediator.Persistence.Outbox;
using Xunit;

namespace TurboMediator.Tests.Outbox;

public class OutboxRoutingTests
{
    #region Test Types

    public record OrderCreatedEvent(Guid OrderId);

    public record PaymentProcessedEvent(Guid PaymentId);

    [PublishTo("custom-queue")]
    public record CustomDestinationEvent(string Data);

    [PublishTo("partitioned-queue", PartitionKey = "TenantId")]
    public record PartitionedEvent(string TenantId, string Data);

    [PublishTo("orders-topic", PartitionKey = "OrderId")]
    public record OrderPlacedEvent(Guid OrderId, string Product) : INotification;

    [PublishTo("payments-queue")]
    public record PaymentCompletedRoutingEvent(Guid PaymentId) : INotification;

    public record UnroutedEvent(string Data) : INotification;

    #endregion

    [Fact]
    public void Router_ShouldUseDefaultDestination_WhenNoMappingExists()
    {
        // Arrange
        var options = new OutboxRoutingOptions
        {
            DefaultDestination = "default-queue",
            NamingConvention = OutboxNamingConvention.Default
        };
        var router = new OutboxMessageRouter(options);

        // Act
        var destination = router.GetDestination<OrderCreatedEvent>();

        // Assert
        Assert.Equal("default-queue", destination);
    }

    [Fact]
    public void Router_ShouldUseKebabCase_WhenConfigured()
    {
        // Arrange
        var options = new OutboxRoutingOptions
        {
            NamingConvention = OutboxNamingConvention.KebabCase
        };
        var router = new OutboxMessageRouter(options);

        // Act
        var destination = router.GetDestination<OrderCreatedEvent>();

        // Assert
        Assert.Equal("order-created-event", destination);
    }

    [Fact]
    public void Router_ShouldUseSnakeCase_WhenConfigured()
    {
        // Arrange
        var options = new OutboxRoutingOptions
        {
            NamingConvention = OutboxNamingConvention.SnakeCase
        };
        var router = new OutboxMessageRouter(options);

        // Act
        var destination = router.GetDestination<OrderCreatedEvent>();

        // Assert
        Assert.Equal("order_created_event", destination);
    }

    [Fact]
    public void Router_ShouldUseTypeName_WhenConfigured()
    {
        // Arrange
        var options = new OutboxRoutingOptions
        {
            NamingConvention = OutboxNamingConvention.TypeName
        };
        var router = new OutboxMessageRouter(options);

        // Act
        var destination = router.GetDestination<OrderCreatedEvent>();

        // Assert
        Assert.Equal("OrderCreatedEvent", destination);
    }

    [Fact]
    public void Router_ShouldUseExplicitMapping_WhenConfigured()
    {
        // Arrange
        var options = new OutboxRoutingOptions()
            .MapType<OrderCreatedEvent>("orders-topic")
            .MapType<PaymentProcessedEvent>("payments-topic");
        var router = new OutboxMessageRouter(options);

        // Act
        var orderDestination = router.GetDestination<OrderCreatedEvent>();
        var paymentDestination = router.GetDestination<PaymentProcessedEvent>();

        // Assert
        Assert.Equal("orders-topic", orderDestination);
        Assert.Equal("payments-topic", paymentDestination);
    }

    [Fact]
    public void Router_ShouldUsePublishToAttribute_WhenPresent()
    {
        // Arrange
        var options = new OutboxRoutingOptions
        {
            NamingConvention = OutboxNamingConvention.KebabCase
        };
        var router = new OutboxMessageRouter(options);

        // Act
        var destination = router.GetDestination<CustomDestinationEvent>();

        // Assert
        Assert.Equal("custom-queue", destination);
    }

    [Fact]
    public void Router_ShouldApplyPrefix_WhenConfigured()
    {
        // Arrange
        var options = new OutboxRoutingOptions
        {
            DestinationPrefix = "prod-",
            NamingConvention = OutboxNamingConvention.KebabCase
        };
        var router = new OutboxMessageRouter(options);

        // Act
        var destination = router.GetDestination<OrderCreatedEvent>();

        // Assert
        Assert.Equal("prod-order-created-event", destination);
    }

    [Fact]
    public void Router_ExplicitMapping_ShouldTakePrecedence_OverAttribute()
    {
        // Arrange
        var options = new OutboxRoutingOptions()
            .MapType<CustomDestinationEvent>("override-queue");
        var router = new OutboxMessageRouter(options);

        // Act
        var destination = router.GetDestination<CustomDestinationEvent>();

        // Assert
        Assert.Equal("override-queue", destination);
    }

    [Fact]
    public void Router_ShouldResolveFromMessageTypeString()
    {
        // Arrange
        var options = new OutboxRoutingOptions
        {
            NamingConvention = OutboxNamingConvention.KebabCase
        };
        var router = new OutboxMessageRouter(options);
        var messageType = typeof(OrderCreatedEvent).AssemblyQualifiedName!;

        // Act
        var destination = router.GetDestination(messageType);

        // Assert
        Assert.Equal("order-created-event", destination);
    }

    [Fact]
    public void Router_ShouldCacheResults()
    {
        // Arrange
        var options = new OutboxRoutingOptions
        {
            NamingConvention = OutboxNamingConvention.KebabCase
        };
        var router = new OutboxMessageRouter(options);

        // Act - call multiple times
        var destination1 = router.GetDestination<OrderCreatedEvent>();
        var destination2 = router.GetDestination<OrderCreatedEvent>();
        var destination3 = router.GetDestination<OrderCreatedEvent>();

        // Assert - should return same result
        Assert.Equal(destination1, destination2);
        Assert.Equal(destination2, destination3);
    }

    [Fact]
    public void PublishToAttribute_ShouldHavePartitionKey()
    {
        // Arrange
        var attr = typeof(PartitionedEvent)
            .GetCustomAttributes(typeof(PublishToAttribute), true)[0] as PublishToAttribute;

        // Assert
        Assert.NotNull(attr);
        Assert.Equal("partitioned-queue", attr.Destination);
        Assert.Equal("TenantId", attr.PartitionKey);
    }

    [Fact]
    public void OutboxRoutingOptions_ShouldHaveReasonableDefaults()
    {
        // Arrange & Act
        var options = new OutboxRoutingOptions();

        // Assert
        Assert.Equal("outbox-messages", options.DefaultDestination);
        Assert.Null(options.DestinationPrefix);
        Assert.Equal(OutboxNamingConvention.KebabCase, options.NamingConvention);
        Assert.Empty(options.TypeMappings);
    }

    // ========================================================================
    // PartitionKey Tests
    // ========================================================================

    [Fact]
    public void GetPartitionKey_ShouldReadFromPublishToAttribute()
    {
        var options = new OutboxRoutingOptions();
        var router = new OutboxMessageRouter(options);

        var partitionKey = router.GetPartitionKey(typeof(OrderPlacedEvent));

        partitionKey.Should().Be("OrderId");
    }

    [Fact]
    public void GetPartitionKey_ShouldReturnNull_WhenNoAttribute()
    {
        var options = new OutboxRoutingOptions();
        var router = new OutboxMessageRouter(options);

        var partitionKey = router.GetPartitionKey(typeof(UnroutedEvent));

        partitionKey.Should().BeNull();
    }

    [Fact]
    public void GetPartitionKey_ShouldReturnNull_WhenAttributeHasNoPartitionKey()
    {
        var options = new OutboxRoutingOptions();
        var router = new OutboxMessageRouter(options);

        var partitionKey = router.GetPartitionKey(typeof(PaymentCompletedRoutingEvent));

        partitionKey.Should().BeNull();
    }

    [Fact]
    public void GetPartitionKeyGeneric_ShouldWork_WithTypeParameter()
    {
        var options = new OutboxRoutingOptions();
        var router = new OutboxMessageRouter(options);

        var partitionKey = router.GetPartitionKey<OrderPlacedEvent>();

        partitionKey.Should().Be("OrderId");
    }

    [Fact]
    public void GetPartitionKeyGeneric_ShouldReturnNull_WhenNoAttribute()
    {
        var options = new OutboxRoutingOptions();
        var router = new OutboxMessageRouter(options);

        var partitionKey = router.GetPartitionKey<UnroutedEvent>();

        partitionKey.Should().BeNull();
    }

    [Fact]
    public void GetPartitionKey_FromMessageTypeString_ShouldResolve()
    {
        var options = new OutboxRoutingOptions();
        var router = new OutboxMessageRouter(options);
        var messageType = typeof(OrderPlacedEvent).AssemblyQualifiedName!;

        var partitionKey = router.GetPartitionKey(messageType);

        partitionKey.Should().Be("OrderId");
    }

    [Fact]
    public void GetPartitionKey_FromUnresolvableTypeString_ShouldReturnNull()
    {
        var options = new OutboxRoutingOptions();
        var router = new OutboxMessageRouter(options);

        var partitionKey = router.GetPartitionKey("NonExistent.Type, SomeAssembly");

        partitionKey.Should().BeNull();
    }
}

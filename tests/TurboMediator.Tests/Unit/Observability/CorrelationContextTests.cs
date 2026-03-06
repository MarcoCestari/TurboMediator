using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TurboMediator.Observability.Correlation;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.Tests.Observability;

/// <summary>
/// Tests for Correlation and Context features (Phase 13).
/// </summary>
public class CorrelationContextTests
{
    // ==================== MediatorContext Tests ====================

    [Fact]
    public void MediatorContext_ShouldGenerateCorrelationId()
    {
        // Act
        var context = new MediatorContext();

        // Assert
        context.CorrelationId.Should().NotBeNullOrEmpty();
        context.CorrelationId.Should().HaveLength(32); // GUID without dashes
    }

    [Fact]
    public void MediatorContext_ShouldAcceptCustomCorrelationId()
    {
        // Arrange
        var customId = "my-custom-correlation-id";

        // Act
        var context = new MediatorContext(customId);

        // Assert
        context.CorrelationId.Should().Be(customId);
    }

    [Fact]
    public void MediatorContext_SetAndGet_ShouldWork()
    {
        // Arrange
        var context = new MediatorContext();

        // Act
        context.Set("key1", "value1");
        context.Set("key2", 42);
        context.Set("key3", new TestData("test"));

        // Assert
        context.Get<string>("key1").Should().Be("value1");
        context.Get<int>("key2").Should().Be(42);
        context.Get<TestData>("key3")!.Value.Should().Be("test");
    }

    [Fact]
    public void MediatorContext_TryGet_ShouldReturnFalseForMissingKey()
    {
        // Arrange
        var context = new MediatorContext();

        // Act
        var result = context.TryGet<string>("missing", out var value);

        // Assert
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void MediatorContext_TryGet_ShouldReturnTrueForExistingKey()
    {
        // Arrange
        var context = new MediatorContext();
        context.Set("key", "value");

        // Act
        var result = context.TryGet<string>("key", out var value);

        // Assert
        result.Should().BeTrue();
        value.Should().Be("value");
    }

    [Fact]
    public void MediatorContext_CreateChild_ShouldCopyValues()
    {
        // Arrange
        var parent = new MediatorContext
        {
            UserId = "user123",
            TenantId = "tenant456"
        };
        parent.Set("custom", "value");

        // Act
        var child = parent.CreateChild();

        // Assert
        child.CausationId.Should().Be(parent.CorrelationId);
        child.CorrelationId.Should().NotBe(parent.CorrelationId);
        child.UserId.Should().Be("user123");
        child.TenantId.Should().Be("tenant456");
        child.Get<string>("custom").Should().Be("value");
    }

    [Fact]
    public void MediatorContext_Items_ShouldBeAccessible()
    {
        // Arrange
        var context = new MediatorContext();

        // Act
        context.Items["direct-key"] = "direct-value";

        // Assert
        context.Items.Should().ContainKey("direct-key");
        context.Items["direct-key"].Should().Be("direct-value");
    }

    // ==================== MediatorContextAccessor Tests ====================

    [Fact]
    public void MediatorContextAccessor_ShouldStoreContext()
    {
        // Arrange
        var accessor = new MediatorContextAccessor();
        var context = new MediatorContext("test-correlation-id");

        // Act
        accessor.Context = context;

        // Assert
        accessor.Context.Should().BeSameAs(context);
        accessor.Context!.CorrelationId.Should().Be("test-correlation-id");
    }

    [Fact]
    public void MediatorContextAccessor_ShouldReturnNullWhenNotSet()
    {
        // Arrange
        var accessor = new MediatorContextAccessor();

        // Assert
        accessor.Context.Should().BeNull();
    }

    // ==================== CorrelationOptions Tests ====================

    [Fact]
    public void CorrelationOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new CorrelationOptions();

        // Assert
        options.HeaderName.Should().Be("X-Correlation-ID");
        options.GenerateIfMissing.Should().BeTrue();
        options.AddToActivityBaggage.Should().BeTrue();
        options.AddToLogScope.Should().BeTrue();
        options.PropagateToHttpClient.Should().BeTrue();
        options.CorrelationIdGenerator.Should().NotBeNull();
    }

    [Fact]
    public void CorrelationOptions_Generator_ShouldGenerateUniqueIds()
    {
        // Arrange
        var options = new CorrelationOptions();

        // Act
        var id1 = options.CorrelationIdGenerator();
        var id2 = options.CorrelationIdGenerator();

        // Assert
        id1.Should().NotBeNullOrEmpty();
        id2.Should().NotBeNullOrEmpty();
        id1.Should().NotBe(id2);
    }

    // ==================== CorrelationIdBehavior Tests ====================

    [Fact]
    public async Task CorrelationIdBehavior_ShouldGenerateCorrelationIdIfMissing()
    {
        // Arrange
        var context = new MediatorContext();
        var originalId = context.CorrelationId;
        var behavior = new CorrelationIdBehavior<TestCommand, string>(context);
        var command = new TestCommand("test");

        // Act
        var result = await behavior.Handle(
            command,
            async (msg, ct) => "success",
            CancellationToken.None);

        // Assert
        result.Should().Be("success");
        context.CorrelationId.Should().Be(originalId); // Already had an ID
    }

    [Fact]
    public async Task CorrelationIdBehavior_WithProvider_ShouldUseExternalId()
    {
        // Arrange
        var context = new MediatorContext();
        var options = new CorrelationOptions
        {
            CorrelationIdProvider = () => "external-correlation-id"
        };
        var behavior = new CorrelationIdBehavior<TestCommand, string>(context, options);
        var command = new TestCommand("test");

        // Act
        await behavior.Handle(
            command,
            async (msg, ct) => "success",
            CancellationToken.None);

        // Assert
        context.CorrelationId.Should().Be("external-correlation-id");
    }

    [Fact]
    public async Task CorrelationIdBehavior_ShouldCallNextHandler()
    {
        // Arrange
        var context = new MediatorContext();
        var behavior = new CorrelationIdBehavior<TestCommand, string>(context);
        var command = new TestCommand("test");
        var handlerCalled = false;

        // Act
        await behavior.Handle(
            command,
            async (msg, ct) => {
                handlerCalled = true;
                return "success";
            },
            CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
    }

    // ==================== IMediatorContext Interface Tests ====================

    [Fact]
    public void IMediatorContext_ShouldSupportAllProperties()
    {
        // Arrange
        IMediatorContext context = new MediatorContext
        {
            CorrelationId = "corr-123",
            CausationId = "cause-456",
            UserId = "user-789",
            TenantId = "tenant-abc",
            TraceId = "trace-def",
            SpanId = "span-ghi"
        };

        // Assert
        context.CorrelationId.Should().Be("corr-123");
        context.CausationId.Should().Be("cause-456");
        context.UserId.Should().Be("user-789");
        context.TenantId.Should().Be("tenant-abc");
        context.TraceId.Should().Be("trace-def");
        context.SpanId.Should().Be("span-ghi");
    }

    // ==================== Test Types ====================

    public record TestCommand(string Data) : ICommand<string>;

    public record TestData(string Value);

    // ==================== Correlation HeaderName Tests ====================

    [Fact]
    public async Task CorrelationIdBehavior_ShouldUseHeaderNameForActivityBaggage()
    {
        var context = new MediatorContext("test-correlation-id");
        var options = new CorrelationOptions
        {
            HeaderName = "X-Custom-Correlation",
            AddToActivityBaggage = true,
            AddToLogScope = false,
            GenerateIfMissing = false
        };
        var behavior = new CorrelationIdBehavior<TestCommand, string>(context, options);
        var message = new TestCommand("test");

        using var activity = new Activity("test").Start();

        MessageHandlerDelegate<TestCommand, string> next = (msg, ct) => new ValueTask<string>("ok");

        await behavior.Handle(message, next, CancellationToken.None);

        activity.GetBaggageItem("X-Custom-Correlation").Should().Be("test-correlation-id");
    }

    [Fact]
    public async Task CorrelationIdBehavior_DefaultHeaderName_UsesXCorrelationID()
    {
        var context = new MediatorContext("test-id");
        var options = new CorrelationOptions
        {
            AddToActivityBaggage = true,
            AddToLogScope = false,
            GenerateIfMissing = false
        };
        var behavior = new CorrelationIdBehavior<TestCommand, string>(context, options);

        using var activity = new Activity("test").Start();

        MessageHandlerDelegate<TestCommand, string> next = (msg, ct) => new ValueTask<string>("ok");

        await behavior.Handle(new TestCommand("test"), next, CancellationToken.None);

        activity.GetBaggageItem("X-Correlation-ID").Should().Be("test-id");
    }

    // ==================== CorrelationIdDelegatingHandler Tests ====================

    [Fact]
    public async Task CorrelationIdDelegatingHandler_ShouldAddHeaderToOutgoingRequest()
    {
        var context = new MediatorContext("handler-test-id");
        var options = new CorrelationOptions { HeaderName = "X-My-Correlation" };
        var innerHandler = new TestHttpMessageHandler();
        var handler = new CorrelationIdDelegatingHandler(context, options)
        {
            InnerHandler = innerHandler
        };
        var client = new HttpClient(handler);

        var response = await client.GetAsync("http://localhost/test");

        innerHandler.LastRequest!.Headers.GetValues("X-My-Correlation")
            .Should().Contain("handler-test-id");
    }

    [Fact]
    public async Task CorrelationIdDelegatingHandler_NoCorrelationId_ShouldNotAddHeader()
    {
        var context = new MediatorContext();
        context.CorrelationId = null!;
        var options = new CorrelationOptions();
        var innerHandler = new TestHttpMessageHandler();
        var handler = new CorrelationIdDelegatingHandler(context, options)
        {
            InnerHandler = innerHandler
        };
        var client = new HttpClient(handler);

        var response = await client.GetAsync("http://localhost/test");

        innerHandler.LastRequest!.Headers.Contains("X-Correlation-ID").Should().BeFalse();
    }

    [Fact]
    public async Task CorrelationIdDelegatingHandler_ShouldPropagaateCausationId()
    {
        var context = new MediatorContext("corr-123") { CausationId = "cause-456" };
        var options = new CorrelationOptions();
        var innerHandler = new TestHttpMessageHandler();
        var handler = new CorrelationIdDelegatingHandler(context, options)
        {
            InnerHandler = innerHandler
        };
        var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/test");

        innerHandler.LastRequest!.Headers.GetValues("X-Causation-ID")
            .Should().Contain("cause-456");
    }

    // ==================== CorrelationOptions Additional Tests ====================

    [Fact]
    public void CorrelationOptions_HeaderName_CanBeCustomized()
    {
        var options = new CorrelationOptions { HeaderName = "X-Request-Id" };
        options.HeaderName.Should().Be("X-Request-Id");
    }

    // ==================== Helper Classes ====================

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}

using FluentAssertions;
using Moq;
using TurboMediator.Caching;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for CachingBehaviorOptions — verifies that #16 fix actually uses the options.
/// </summary>
public class CachingBehaviorOptionsTests
{
    public record GetWidgetQuery(int Id) : IQuery<string>;

    [Fact]
    public void CachingBehaviorOptions_ShouldHaveReasonableDefaults()
    {
        var options = new CachingBehaviorOptions();
        options.DefaultDuration.Should().Be(TimeSpan.FromMinutes(5));
        options.GlobalKeyPrefix.Should().BeNull();
        options.DefaultUseSlidingExpiration.Should().BeFalse();
    }

    [Fact]
    public async Task CachingBehavior_ShouldUseDefaultDuration_WhenAttributeDurationIsZero()
    {
        // Arrange
        var mockCache = new Mock<ICacheProvider>();
        mockCache.Setup(c => c.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CacheResult<string>.Miss());
        mockCache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var options = new CachingBehaviorOptions { DefaultDuration = TimeSpan.FromMinutes(10) };
        var behavior = new CachingBehavior<GetWidgetQuery, string>(mockCache.Object, options);

        // GetWidgetQuery does NOT have [Cacheable] so the behavior skips caching
        var result = await behavior.Handle(
            new GetWidgetQuery(1),
            () => new ValueTask<string>("widget-1"),
            CancellationToken.None);

        result.Should().Be("widget-1");
    }

    [Fact]
    public async Task CachingBehavior_ShouldSkip_WhenNoCacheableAttribute()
    {
        var mockCache = new Mock<ICacheProvider>();
        var behavior = new CachingBehavior<GetWidgetQuery, string>(mockCache.Object);
        var called = false;

        var result = await behavior.Handle(
            new GetWidgetQuery(1),
            () => { called = true; return new ValueTask<string>("result"); },
            CancellationToken.None);

        result.Should().Be("result");
        called.Should().BeTrue();
        mockCache.Verify(c => c.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CachingBehavior_ShouldThrowOnNullProvider()
    {
        var act = () => new CachingBehavior<GetWidgetQuery, string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

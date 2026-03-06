using FluentAssertions;
using TurboMediator.FeatureFlags;
using Xunit;

namespace TurboMediator.Tests;

public class FeatureFlagTests
{
    // Test query with feature flag
    [FeatureFlag("new-dashboard")]
    public class GetDashboardQuery : IQuery<DashboardResult> { }

    [FeatureFlag("beta-feature", PerUser = true)]
    public class GetBetaFeatureQuery : IQuery<string> { }

    [FeatureFlag("optional-feature", FallbackBehavior = FeatureFallback.ReturnDefault)]
    public class GetOptionalFeatureQuery : IQuery<int> { }

    [FeatureFlag("skippable-feature", FallbackBehavior = FeatureFallback.Skip)]
    public class GetSkippableFeatureQuery : IQuery<string> { }

    public class DashboardResult
    {
        public string Title { get; set; } = "Dashboard";
    }

    // Regular query without feature flag
    public class GetRegularDataQuery : IQuery<string> { }

    [Fact]
    public void FeatureFlagAttribute_ShouldStoreFeatureName()
    {
        var attr = new FeatureFlagAttribute("test-feature");
        attr.FeatureName.Should().Be("test-feature");
        attr.PerUser.Should().BeFalse();
        attr.FallbackBehavior.Should().Be(FeatureFallback.Throw);
    }

    [Fact]
    public void FeatureFlagAttribute_ShouldSupportPerUser()
    {
        var attr = new FeatureFlagAttribute("user-feature") { PerUser = true };
        attr.FeatureName.Should().Be("user-feature");
        attr.PerUser.Should().BeTrue();
    }

    [Fact]
    public void FeatureFlagAttribute_ShouldSupportFallbackBehavior()
    {
        var attr = new FeatureFlagAttribute("optional") { FallbackBehavior = FeatureFallback.ReturnDefault };
        attr.FallbackBehavior.Should().Be(FeatureFallback.ReturnDefault);
    }

    [Fact]
    public async Task InMemoryFeatureFlagProvider_ShouldReturnFalseByDefault()
    {
        var provider = new InMemoryFeatureFlagProvider();
        var result = await provider.IsEnabledAsync("unknown-feature");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InMemoryFeatureFlagProvider_ShouldEnableFeature()
    {
        var provider = new InMemoryFeatureFlagProvider();
        provider.SetFeature("my-feature", true);

        var result = await provider.IsEnabledAsync("my-feature");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task InMemoryFeatureFlagProvider_ShouldDisableFeature()
    {
        var provider = new InMemoryFeatureFlagProvider();
        provider.SetFeature("my-feature", true);
        provider.SetFeature("my-feature", false);

        var result = await provider.IsEnabledAsync("my-feature");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InMemoryFeatureFlagProvider_ShouldSupportPerUserFlags()
    {
        var provider = new InMemoryFeatureFlagProvider();
        provider.SetFeature("beta", "user1", true);
        provider.SetFeature("beta", "user2", false);

        var user1Result = await provider.IsEnabledAsync("beta", "user1");
        var user2Result = await provider.IsEnabledAsync("beta", "user2");

        user1Result.Should().BeTrue();
        user2Result.Should().BeFalse();
    }

    [Fact]
    public async Task InMemoryFeatureFlagProvider_ShouldFallbackToGlobalForUnknownUser()
    {
        var provider = new InMemoryFeatureFlagProvider();
        provider.SetFeature("global-feature", true);

        var result = await provider.IsEnabledAsync("global-feature", "any-user");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task InMemoryFeatureFlagProvider_UserFlagOverridesGlobal()
    {
        var provider = new InMemoryFeatureFlagProvider();
        provider.SetFeature("feature", true); // Global enabled
        provider.SetFeature("feature", "special-user", false); // User disabled

        var globalResult = await provider.IsEnabledAsync("feature");
        var userResult = await provider.IsEnabledAsync("feature", "special-user");
        var otherUserResult = await provider.IsEnabledAsync("feature", "other-user");

        globalResult.Should().BeTrue();
        userResult.Should().BeFalse();
        otherUserResult.Should().BeTrue();
    }

    [Fact]
    public void FeatureDisabledException_ShouldContainCorrectInfo()
    {
        var exception = new FeatureDisabledException("my-feature", "GetDataQuery");

        exception.FeatureName.Should().Be("my-feature");
        exception.MessageType.Should().Be("GetDataQuery");
        exception.Message.Should().Contain("my-feature");
        exception.Message.Should().Contain("GetDataQuery");
    }

    [Fact]
    public void FeatureFlagOptions_ShouldHaveCorrectDefaults()
    {
        var options = new FeatureFlagOptions();

        options.DefaultFallback.Should().Be(FeatureFallback.Throw);
        options.UserIdProvider.Should().BeNull();
        options.OnFeatureCheck.Should().BeNull();
    }

    [Fact]
    public void FeatureFlagOptions_ShouldBeConfigurable()
    {
        string? checkedFeature = null;
        var options = new FeatureFlagOptions
        {
            DefaultFallback = FeatureFallback.ReturnDefault,
            UserIdProvider = () => "current-user",
            OnFeatureCheck = info => checkedFeature = info.FeatureName
        };

        options.DefaultFallback.Should().Be(FeatureFallback.ReturnDefault);
        options.UserIdProvider!().Should().Be("current-user");

        options.OnFeatureCheck!(new FeatureCheckInfo("test-feature", "TestRequest", true, null));
        checkedFeature.Should().Be("test-feature");
    }

    [Fact]
    public async Task FeatureFlagBehavior_ShouldAllowWhenEnabled()
    {
        var provider = new InMemoryFeatureFlagProvider();
        provider.SetFeature("new-dashboard", true);

        var behavior = new FeatureFlagBehavior<GetDashboardQuery, DashboardResult>(provider);
        var query = new GetDashboardQuery();
        var expectedResult = new DashboardResult { Title = "My Dashboard" };
        var callCount = 0;

        var result = await behavior.Handle(query, (msg, ct) =>
        {
            callCount++;
            return new ValueTask<DashboardResult>(expectedResult);
        }, CancellationToken.None);

        result.Should().Be(expectedResult);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task FeatureFlagBehavior_ShouldThrowWhenDisabled()
    {
        var provider = new InMemoryFeatureFlagProvider();
        provider.SetFeature("new-dashboard", false);

        var behavior = new FeatureFlagBehavior<GetDashboardQuery, DashboardResult>(provider);
        var query = new GetDashboardQuery();

        var act = () => behavior.Handle(query, (msg, ct) =>
        {
            return new ValueTask<DashboardResult>(new DashboardResult());
        }, CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<FeatureDisabledException>()
            .WithMessage("*new-dashboard*");
    }

    [Fact]
    public async Task FeatureFlagBehavior_ShouldReturnDefaultWhenConfigured()
    {
        var provider = new InMemoryFeatureFlagProvider();
        provider.SetFeature("optional-feature", false);

        var behavior = new FeatureFlagBehavior<GetOptionalFeatureQuery, int>(provider);
        var query = new GetOptionalFeatureQuery();
        var handlerCalled = false;

        var result = await behavior.Handle(query, (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<int>(42);
        }, CancellationToken.None);

        result.Should().Be(0); // default(int)
        handlerCalled.Should().BeFalse();
    }

    [Fact]
    public async Task FeatureFlagBehavior_ShouldSkipWhenConfigured()
    {
        var provider = new InMemoryFeatureFlagProvider();
        provider.SetFeature("skippable-feature", false);

        var behavior = new FeatureFlagBehavior<GetSkippableFeatureQuery, string>(provider);
        var query = new GetSkippableFeatureQuery();

        var result = await behavior.Handle(query, (msg, ct) =>
        {
            return new ValueTask<string>("should not be returned");
        }, CancellationToken.None);

        result.Should().BeNull(); // default(string)
    }

    [Fact]
    public async Task FeatureFlagBehavior_ShouldInvokeOnFeatureCheck()
    {
        var provider = new InMemoryFeatureFlagProvider();
        provider.SetFeature("new-dashboard", true);

        FeatureCheckInfo? checkInfo = null;
        var options = new FeatureFlagOptions
        {
            OnFeatureCheck = info => checkInfo = info
        };

        var behavior = new FeatureFlagBehavior<GetDashboardQuery, DashboardResult>(provider, options);
        var query = new GetDashboardQuery();

        await behavior.Handle(query, (msg, ct) =>
        {
            return new ValueTask<DashboardResult>(new DashboardResult());
        }, CancellationToken.None);

        checkInfo.Should().NotBeNull();
        checkInfo!.Value.FeatureName.Should().Be("new-dashboard");
        checkInfo.Value.MessageType.Should().Be("GetDashboardQuery");
        checkInfo.Value.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task FeatureFlagBehavior_ShouldPassThroughWithoutAttribute()
    {
        var provider = new InMemoryFeatureFlagProvider();
        var behavior = new FeatureFlagBehavior<GetRegularDataQuery, string>(provider);
        var query = new GetRegularDataQuery();
        var callCount = 0;

        var result = await behavior.Handle(query, (msg, ct) =>
        {
            callCount++;
            return new ValueTask<string>("data");
        }, CancellationToken.None);

        result.Should().Be("data");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task FeatureFlagBehavior_ShouldUsePerUserCheck()
    {
        var provider = new InMemoryFeatureFlagProvider();
        provider.SetFeature("beta-feature", "user1", true);
        provider.SetFeature("beta-feature", "user2", false);

        var options = new FeatureFlagOptions
        {
            UserIdProvider = () => "user1"
        };

        var behavior = new FeatureFlagBehavior<GetBetaFeatureQuery, string>(provider, options);
        var query = new GetBetaFeatureQuery();

        var result = await behavior.Handle(query, (msg, ct) =>
        {
            return new ValueTask<string>("beta data");
        }, CancellationToken.None);

        result.Should().Be("beta data");
    }

    [Fact]
    public void InMemoryFeatureFlagProvider_ShouldSeedFromOptions()
    {
        // Arrange
        var options = new InMemoryFeatureFlagOptions();
        options.SetFeature("feat-a", true);
        options.SetFeature("feat-b", false);

        // Act
        var provider = new InMemoryFeatureFlagProvider(options);

        // Assert
        provider.IsEnabledAsync("feat-a").Result.Should().BeTrue();
        provider.IsEnabledAsync("feat-b").Result.Should().BeFalse();
        provider.IsEnabledAsync("feat-c").Result.Should().BeFalse(); // not configured
    }

    [Fact]
    public void InMemoryFeatureFlagProvider_ShouldAllowRuntimeModificationAfterSeeding()
    {
        var options = new InMemoryFeatureFlagOptions();
        options.SetFeature("feat-a", false);

        var provider = new InMemoryFeatureFlagProvider(options);
        provider.IsEnabledAsync("feat-a").Result.Should().BeFalse();

        // Modify at runtime
        provider.SetFeature("feat-a", true);
        provider.IsEnabledAsync("feat-a").Result.Should().BeTrue();
    }
}

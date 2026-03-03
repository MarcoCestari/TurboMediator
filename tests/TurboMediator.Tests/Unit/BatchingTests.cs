using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Batching;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Unit tests for Batching behavior.
/// </summary>
public class BatchingTests
{
    // --- Test types ---

    public record GetProductQuery(int ProductId) : IBatchableQuery<string>;

    public class GetProductBatchHandler : IBatchHandler<GetProductQuery, string>
    {
        public ValueTask<IDictionary<GetProductQuery, string>> HandleAsync(
            IReadOnlyList<GetProductQuery> queries,
            CancellationToken cancellationToken)
        {
            var results = new Dictionary<GetProductQuery, string>();
            foreach (var q in queries)
            {
                results[q] = $"Product-{q.ProductId}";
            }
            return new ValueTask<IDictionary<GetProductQuery, string>>(results);
        }
    }

    // --- Tests ---

    [Fact]
    public async Task BatchingBehavior_ShouldFallBackToNext_WhenNoBatchHandler()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        var options = new BatchingOptions { ThrowIfNoBatchHandler = false };
        var behavior = new BatchingBehavior<GetProductQuery, string>(sp, options);

        var result = await behavior.Handle(
            new GetProductQuery(1),
            () => new ValueTask<string>("fallback-result"),
            CancellationToken.None);

        result.Should().Be("fallback-result");
    }

    [Fact]
    public async Task BatchingBehavior_ShouldThrow_WhenNoBatchHandlerAndThrowEnabled()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        var options = new BatchingOptions { ThrowIfNoBatchHandler = true };
        var behavior = new BatchingBehavior<GetProductQuery, string>(sp, options);

        var act = async () => await behavior.Handle(
            new GetProductQuery(1),
            () => new ValueTask<string>("should not reach"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No batch handler*");
    }

    [Fact]
    public async Task BatchingBehavior_ShouldProcessSingleItem_WithBatchHandler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBatchHandler<GetProductQuery, string>, GetProductBatchHandler>();
        var sp = services.BuildServiceProvider();

        // MaxBatchSize = 1 forces immediate processing
        var options = new BatchingOptions { MaxBatchSize = 1 };
        var behavior = new BatchingBehavior<GetProductQuery, string>(sp, options);

        var result = await behavior.Handle(
            new GetProductQuery(42),
            () => new ValueTask<string>("fallback"),
            CancellationToken.None);

        result.Should().Be("Product-42");
    }

    [Fact]
    public void BatchingOptions_ShouldHaveReasonableDefaults()
    {
        var options = new BatchingOptions();
        options.MaxBatchSize.Should().Be(100);
        options.MaxWaitTime.Should().Be(TimeSpan.FromMilliseconds(10));
        options.ThrowIfNoBatchHandler.Should().BeFalse();
        options.OnBatchProcessed.Should().BeNull();
    }

    [Fact]
    public void BatchingBehavior_ShouldThrowOnNullServiceProvider()
    {
        var act = () => new BatchingBehavior<GetProductQuery, string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

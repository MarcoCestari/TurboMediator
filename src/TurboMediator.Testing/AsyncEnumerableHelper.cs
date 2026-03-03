using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Testing;

/// <summary>
/// Helper class for creating empty async enumerables.
/// </summary>
internal static class AsyncEnumerableHelper
{
    public static IAsyncEnumerable<T> Empty<T>() => EmptyAsyncEnumerable<T>.Instance;

    private sealed class EmptyAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        public static readonly EmptyAsyncEnumerable<T> Instance = new();

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => EmptyAsyncEnumerator<T>.Instance;
    }

    private sealed class EmptyAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        public static readonly EmptyAsyncEnumerator<T> Instance = new();

        public T Current => default!;

        public ValueTask DisposeAsync()
        {
#if NET8_0_OR_GREATER
            return ValueTask.CompletedTask;
#else
            return default;
#endif
        }

        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(false);
    }
}

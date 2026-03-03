using System;

namespace TurboMediator.Resilience.Retry;

#if !NET6_0_OR_GREATER
/// <summary>
/// Thread-safe shared random for netstandard2.0 compatibility.
/// </summary>
internal static class SharedRandom
{
    [ThreadStatic]
    private static Random? _random;

    private static Random Instance => _random ??= new Random();

    public static double NextDouble() => Instance.NextDouble();
}
#endif

using System;
using System.Diagnostics.CodeAnalysis;

namespace TurboMediator.Resilience.Fallback;

/// <summary>
/// Attribute to specify fallback behavior for a message handler.
/// When the primary handler fails, the specified fallback handler will be invoked.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class FallbackAttribute : Attribute
{
    /// <summary>
    /// Gets the type of the fallback handler.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type FallbackHandlerType { get; }

    /// <summary>
    /// Gets or sets the exception types that should trigger the fallback.
    /// If not specified, all exceptions will trigger the fallback.
    /// </summary>
    public Type[]? OnExceptionTypes { get; set; }

    /// <summary>
    /// Gets or sets the order in which fallbacks should be tried.
    /// Lower values are tried first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Creates a new FallbackAttribute.
    /// </summary>
    /// <param name="fallbackHandlerType">The type implementing IFallbackHandler.</param>
    /// <exception cref="ArgumentNullException">Thrown when fallbackHandlerType is null.</exception>
    public FallbackAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type fallbackHandlerType)
    {
        FallbackHandlerType = fallbackHandlerType ?? throw new ArgumentNullException(nameof(fallbackHandlerType));
    }
}

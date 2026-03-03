using System;

namespace TurboMediator.Observability.Correlation;

/// <summary>
/// Provides access to the current mediator context.
/// </summary>
public interface IMediatorContextAccessor
{
    /// <summary>
    /// Gets or sets the current mediator context.
    /// </summary>
    IMediatorContext? Context { get; set; }
}

/// <summary>
/// Default implementation of IMediatorContextAccessor using AsyncLocal.
/// </summary>
public sealed class MediatorContextAccessor : IMediatorContextAccessor
{
    private static readonly AsyncLocal<ContextHolder> _contextCurrent = new();

    /// <inheritdoc />
    public IMediatorContext? Context
    {
        get => _contextCurrent.Value?.Context;
        set
        {
            var holder = _contextCurrent.Value;
            if (holder != null)
            {
                holder.Context = null;
            }

            if (value != null)
            {
                _contextCurrent.Value = new ContextHolder { Context = value };
            }
        }
    }

    private sealed class ContextHolder
    {
        public IMediatorContext? Context;
    }
}

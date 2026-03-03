using System;
using System.Collections.Generic;
using System.Linq;

namespace TurboMediator.Testing;

/// <summary>
/// Extension methods for test assertions.
/// </summary>
public static class TestingExtensions
{
    /// <summary>
    /// Gets messages of a specific type from a collection.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="records">The message records.</param>
    /// <returns>The messages of the specified type.</returns>
    public static IEnumerable<T> OfMessageType<T>(this IEnumerable<MessageRecord> records)
        => records.Where(r => r.Message is T).Select(r => (T)r.Message);

    /// <summary>
    /// Filters records by message type.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="records">The message records.</param>
    /// <returns>Records with messages of the specified type.</returns>
    public static IEnumerable<MessageRecord> WhereMessage<T>(this IEnumerable<MessageRecord> records)
        => records.Where(r => r.Message is T);

    /// <summary>
    /// Filters records by message type and a predicate.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="records">The message records.</param>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>Records with messages of the specified type matching the predicate.</returns>
    public static IEnumerable<MessageRecord> WhereMessage<T>(this IEnumerable<MessageRecord> records, Func<T, bool> predicate)
        => records.Where(r => r.Message is T msg && predicate(msg));

    /// <summary>
    /// Gets only successful records.
    /// </summary>
    public static IEnumerable<MessageRecord> Successful(this IEnumerable<MessageRecord> records)
        => records.Where(r => r.IsSuccess);

    /// <summary>
    /// Gets only failed records.
    /// </summary>
    public static IEnumerable<MessageRecord> Failed(this IEnumerable<MessageRecord> records)
        => records.Where(r => r.Exception != null);
}

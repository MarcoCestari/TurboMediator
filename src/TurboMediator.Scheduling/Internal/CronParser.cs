using System;
using Cronos;

namespace TurboMediator.Scheduling.Internal;

/// <summary>
/// Thin wrapper over the Cronos library for cron expression parsing.
/// </summary>
internal static class CronParser
{
    /// <summary>
    /// Calculates the next occurrence from the given time using the cron expression.
    /// Supports both 5-field (minute) and 6-field (second) cron expressions.
    /// </summary>
    public static DateTimeOffset? GetNextOccurrence(string cronExpression, DateTimeOffset from, TimeZoneInfo? timeZone = null)
    {
        var format = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6
            ? CronFormat.IncludeSeconds
            : CronFormat.Standard;

        var expression = CronExpression.Parse(cronExpression, format);
        var tz = timeZone ?? TimeZoneInfo.Utc;

        return expression.GetNextOccurrence(from, tz);
    }

    /// <summary>
    /// Validates whether a cron expression is parseable.
    /// </summary>
    public static bool IsValid(string cronExpression)
    {
        try
        {
            var format = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6
                ? CronFormat.IncludeSeconds
                : CronFormat.Standard;

            CronExpression.Parse(cronExpression, format);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

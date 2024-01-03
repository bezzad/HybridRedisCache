using System;
using System.Globalization;

namespace HybridRedisCache;

/// <summary>
/// Argument check.
/// </summary>
internal static class ArgumentCheck
{
    /// <summary>
    /// Validates that <paramref name="argument"/> is not null , otherwise throws an exception.
    /// </summary>
    /// <param name="argument">Argument.</param>
    /// <param name="argumentName">Argument name.</param>
    /// <exception cref="ArgumentNullException" />
    public static void NotNull(this object argument, string argumentName)
    {
        if (argument == null)
        {
            throw new ArgumentNullException(argumentName);
        }
    }

    /// <summary>
    /// Validates that <paramref name="argument"/> is not null , otherwise throws an exception.
    /// </summary>
    /// <param name="argument">Argument.</param>
    /// <param name="argumentName">Argument name.</param>
    /// <param name="allowNulls">Allow nulls.</param>
    /// <exception cref="ArgumentNullException" />
    public static void NotNull(this object argument, string argumentName, bool allowNulls)
    {
        if (argument == null && !allowNulls)
        {
            throw new ArgumentNullException(argumentName);
        }
    }

    /// <summary>
    /// Validates that <paramref name="argument"/> is not null or white space , otherwise throws an exception.
    /// </summary>
    /// <param name="argument">Argument.</param>
    /// <param name="argumentName">Argument name.</param>
    /// <exception cref="ArgumentNullException" />
    public static void NotNullOrWhiteSpace(this string argument, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentNullException(argumentName);
        }
    }

    /// <summary>
    /// Validates that <paramref name="argument"/> is not negative or zero , otherwise throws an exception.
    /// </summary>
    /// <param name="argument">Argument.</param>
    /// <param name="argumentName">Argument name.</param>
    public static void NotNegativeOrZero(this TimeSpan argument, string argumentName)
    {
        if (argument <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(argumentName);
        }
    }

    /// <summary>
    /// Nots the null and count gt Zero.
    /// </summary>
    /// <param name="argument">Argument.</param>
    /// <param name="argumentName">Argument name.</param>
    public static void NotNullAndCountGTZero<T>(this IEnumerable<T> argument, string argumentName)
    {
        if (argument == null || argument.Count() <= 0)
        {
            throw new ArgumentNullException(argumentName);
        }
    }


    /// <summary>
    /// Nots the null and count gt Zero.
    /// </summary>
    /// <param name="argument">Argument.</param>
    /// <param name="argumentName">Argument name.</param>
    public static void NotNullAndCountGTZero<T>(this IDictionary<string, T> argument, string argumentName)
    {
        if (argument == null || argument.Count() <= 0)
        {
            throw new ArgumentNullException(argumentName);
        }
    }

    public static TimeSpan GetNonZeroDurationFromNow(this DateTimeOffset dateTimeOffset, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        if (dateTimeOffset.UtcDateTime > now)
            return dateTimeOffset.UtcDateTime - now;

        return TimeSpan.FromMinutes(1);
    }

    public static DateTimeOffset GetNextUtcDateTime(this TimeOnly time, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        if (time.ToTimeSpan() >= now.TimeOfDay) 
        {
            return new DateTime(now.Year, now.Month, now.Day, time.Hour, time.Minute, time.Second, DateTimeKind.Utc);
        }
        else
        {
            // it's too late to today, so go to tomarrow
            now = now.AddDays(1);
            return new DateTime(now.Year, now.Month, now.Day, time.Hour, time.Minute, time.Second, DateTimeKind.Utc);
        }
    }

    public static DateTimeOffset GetNextUtcDateTime(this string time, DateTime? nowUtc = null)
    {
        return GetNextUtcDateTime(TimeOnly.Parse(time), nowUtc);
    }
}
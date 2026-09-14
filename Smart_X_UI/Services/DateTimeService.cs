using System;

namespace Smart_X_UI.Services;

public static class DateTimeService
{
    // Makes sure the timestamp is treated as time.
    public static DateTime EnsureUtc( DateTime timestamp)
    {
        return timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(
                timestamp,
                DateTimeKind.Utc),
            _ => timestamp
        };
    }

    // Getsage of  a timestamp is.
    public static TimeSpan GetAge(DateTime timestamp)
    {
        return DateTime.UtcNow - EnsureUtc(timestamp);
    }

    // Checks if a timestamp is has the correct number of seconds.
    public static bool IsRecent(DateTime timestamp, int seconds = 10)
    {
        var age = GetAge(timestamp);

        return age >= TimeSpan.Zero &&
               age <= TimeSpan.FromSeconds(seconds);
    }

    // Converts a timestamp to local time.
    public static string FormatLocalTime(DateTime timestamp)
    {
        return EnsureUtc(timestamp)
            .ToLocalTime()
            .ToString("HH:mm:ss");
    }
}

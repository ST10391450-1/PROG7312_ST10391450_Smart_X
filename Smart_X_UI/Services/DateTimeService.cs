using System;

namespace Smart_X_UI.Services;

public static class DateTimeService
{
    public static DateTime EnsureUtc(DateTime timestamp)
    {
        return timestamp.Kind switch
        {
            DateTimeKind.Utc =>
                timestamp,

            DateTimeKind.Local =>
                timestamp.ToUniversalTime(),

            DateTimeKind.Unspecified =>
                DateTime.SpecifyKind(
                    timestamp,
                    DateTimeKind.Utc),

            _ =>
                timestamp
        };
    }

    public static TimeSpan GetAge(DateTime timestamp)
    {
        return DateTime.UtcNow - EnsureUtc(timestamp);
    }

    public static bool IsRecent(
        DateTime timestamp,
        int seconds = 10)
    {
        TimeSpan age = GetAge(timestamp);

        return age >= TimeSpan.Zero &&
               age <= TimeSpan.FromSeconds(seconds);
    }

    public static string FormatLocalTime(
        DateTime timestamp)
    {
        return EnsureUtc(timestamp)
            .ToLocalTime()
            .ToString("HH:mm:ss");
    }
}
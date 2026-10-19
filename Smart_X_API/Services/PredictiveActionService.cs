
using Smart_X_API.Models;

namespace Smart_X_API.Services;

public sealed record DeviceSearchRecord(
    string Query,
    DateTime Timestamp);

public sealed record DeviceCommandRecord(
    string DeviceId,
    string Command,
    DateTime Timestamp);

public sealed record DeviceMeasurementRecord(
    string DeviceId,
    double Value,
    DateTime Timestamp);

public sealed class PredictiveActionService
{
    private readonly object _sync = new();

    private readonly Queue<DeviceSearchRecord> _searchHistory = new();
    private readonly Queue<DeviceCommandRecord> _commandHistory = new();

    private readonly Dictionary<string, int> _searchCounts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Queue<DeviceMeasurementRecord>> _recentValues =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> _commandPatternCounts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, DateTime> _lastPatternOccurrences =
        new(StringComparer.OrdinalIgnoreCase);

    private const int MaxSearchHistory = 1000;
    private const int MaxCommandHistory = 1000;
    private const int MaxValuesPerDevice = 20;
    private const int MinimumSearchCount = 3;
    private const int MinimumPatternCount = 2;

    private static readonly TimeSpan PatternWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RecentActivityWindow = TimeSpan.FromHours(24);

    public void RecordSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        var normalized = query.Trim();
        var now = DateTime.UtcNow;

        lock (_sync)
        {
            _searchHistory.Enqueue(new DeviceSearchRecord(normalized, now));

            _searchCounts[normalized] =
                _searchCounts.GetValueOrDefault(normalized) + 1;

            while (_searchHistory.Count > MaxSearchHistory)
            {
                var removed = _searchHistory.Dequeue();
                var remaining = _searchHistory.Count(x =>
                    x.Query.Equals(
                        removed.Query,
                        StringComparison.OrdinalIgnoreCase));

                if (remaining == 0)
                    _searchCounts.Remove(removed.Query);
                else
                    _searchCounts[removed.Query] = remaining;
            }
        }
    }

    public void RecordMeasurement(string deviceId, double value)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || !double.IsFinite(value))
            return;

        var normalizedId = deviceId.Trim();
        var now = DateTime.UtcNow;

        lock (_sync)
        {
            if (!_recentValues.TryGetValue(normalizedId, out var values))
            {
                values = new Queue<DeviceMeasurementRecord>();
                _recentValues[normalizedId] = values;
            }

            values.Enqueue(new DeviceMeasurementRecord(
                normalizedId,
                value,
                now));

            while (values.Count > MaxValuesPerDevice)
                values.Dequeue();

            while (values.Count > 1 &&
                   now - values.Peek().Timestamp > RecentActivityWindow)
            {
                values.Dequeue();
            }
        }
    }

    public void RecordCommand(string deviceId, string command)
    {
        if (string.IsNullOrWhiteSpace(deviceId) ||
            string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var normalizedDeviceId = deviceId.Trim();
        var normalizedCommand = command.Trim();
        var now = DateTime.UtcNow;

        lock (_sync)
        {
            _commandHistory.Enqueue(new DeviceCommandRecord(
                normalizedDeviceId,
                normalizedCommand,
                now));

            while (_commandHistory.Count > MaxCommandHistory)
                _commandHistory.Dequeue();

            foreach (var entry in _recentValues)
            {
                var latestMeasurement = entry.Value.LastOrDefault();

                if (latestMeasurement is null ||
                    now - latestMeasurement.Timestamp > PatternWindow)
                {
                    continue;
                }

                if (!IsPotentiallyRelatedMeasurement(
                        entry.Key,
                        latestMeasurement.Value,
                        normalizedCommand))
                {
                    continue;
                }

                var patternKey = BuildPatternKey(
                    entry.Key,
                    normalizedCommand);

                _commandPatternCounts[patternKey] =
                    _commandPatternCounts.GetValueOrDefault(patternKey) + 1;

                _lastPatternOccurrences[patternKey] = now;
            }
        }
    }

    public IReadOnlyList<DeviceRecommendation> GetRecommendations()
    {
        lock (_sync)
        {
            var recommendations = new List<DeviceRecommendation>();
            var now = DateTime.UtcNow;

            AddSearchRecommendations(recommendations);
            AddMeasurementRecommendations(recommendations, now);
            AddCommandPatternRecommendations(recommendations, now);
            AddCommandActivityRecommendations(recommendations, now);

            return recommendations
                .GroupBy(
                    x => $"{x.DeviceId}|{x.Category}|{x.Recommendation}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(x => x.Score)
                    .First())
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
                .Take(50)
                .ToList();
        }
    }

    public IReadOnlyList<DeviceSearchRecord> GetSearchHistory(int count = 50)
    {
        lock (_sync)
        {
            return _searchHistory
                .Reverse()
                .Take(Math.Clamp(count, 1, MaxSearchHistory))
                .ToList();
        }
    }

    public IReadOnlyList<DeviceCommandRecord> GetCommandHistory(int count = 50)
    {
        lock (_sync)
        {
            return _commandHistory
                .Reverse()
                .Take(Math.Clamp(count, 1, MaxCommandHistory))
                .ToList();
        }
    }

    private void AddSearchRecommendations(
        ICollection<DeviceRecommendation> recommendations)
    {
        foreach (var search in _searchCounts
                     .Where(x => x.Value >= MinimumSearchCount)
                     .OrderByDescending(x => x.Value)
                     .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var score = Math.Min(100, 40 + search.Value * 5);

            recommendations.Add(new DeviceRecommendation
            {
                DeviceId = search.Key,
                Category = "SearchPattern",
                Recommendation = "Review this frequently searched device or category.",
                Reason = $"The query appeared {search.Value} times in the retained search history.",
                Score = score
            });
        }
    }

    private void AddMeasurementRecommendations(
        ICollection<DeviceRecommendation> recommendations,
        DateTime now)
    {
        foreach (var entry in _recentValues.OrderBy(x => x.Key))
        {
            var measurements = entry.Value
                .Where(x => now - x.Timestamp <= RecentActivityWindow)
                .ToArray();

            if (measurements.Length == 0)
                continue;

            var latest = measurements[^1];
            var deviceId = entry.Key;

            if (latest.Value < 0 || latest.Value > 100)
            {
                recommendations.Add(new DeviceRecommendation
                {
                    DeviceId = deviceId,
                    Category = "OutOfRange",
                    Recommendation = "Inspect the sensor and validate its reading.",
                    Reason = $"The latest reading is {latest.Value:F2}. This generic range check assumes a 0–100 scale; confirm the valid range for this sensor type.",
                    Score = 85
                });
            }

            if (IsMoistureDevice(deviceId) && latest.Value < 20)
            {
                var learnedPump = FindLearnedPumpCommand(deviceId);

                recommendations.Add(new DeviceRecommendation
                {
                    DeviceId = deviceId,
                    Category = "ThresholdAlert",
                    Recommendation = learnedPump is null
                        ? "Check the soil moisture and consider whether irrigation is needed."
                        : $"Low moisture detected. Review the learned command '{learnedPump}' before deciding whether to activate the pump.",
                    Reason = $"The latest moisture reading is {latest.Value:F2}, below the configured 20% alert threshold.",
                    Score = 95
                });
            }

            if (IsTemperatureDevice(deviceId) &&
                (latest.Value < 0 || latest.Value > 45))
            {
                recommendations.Add(new DeviceRecommendation
                {
                    DeviceId = deviceId,
                    Category = "ThresholdAlert",
                    Recommendation = "Inspect the temperature sensor and check the monitored environment.",
                    Reason = $"The latest temperature reading is {latest.Value:F2}, outside the configured 0–45 range.",
                    Score = 90
                });
            }

            if (measurements.Length < 5)
                continue;

            var values = measurements
                .Select(x => x.Value)
                .ToArray();

            var midpoint = values.Length / 2;

            if (midpoint == 0 || midpoint == values.Length)
                continue;

            var earlierAverage = values.Take(midpoint).Average();
            var recentAverage = values.Skip(midpoint).Average();
            var difference = recentAverage - earlierAverage;

            if (Math.Abs(difference) < 0.001)
                continue;

            var scale = Math.Max(
                Math.Abs(earlierAverage),
                1);

            var percentageChange = Math.Abs(difference) / scale * 100;

            if (percentageChange < 10)
                continue;

            recommendations.Add(new DeviceRecommendation
            {
                DeviceId = deviceId,
                Category = "TelemetryTrend",
                Recommendation = difference > 0
                    ? "Investigate the rising sensor trend."
                    : "Investigate the falling sensor trend.",
                Reason = $"The recent average changed from {earlierAverage:F2} to {recentAverage:F2}, a {percentageChange:F1}% change.",
                Score = Math.Clamp(
                    (int)Math.Round(45 + percentageChange),
                    45,
                    90)
            });
        }
    }

    private void AddCommandPatternRecommendations(
        ICollection<DeviceRecommendation> recommendations,
        DateTime now)
    {
        foreach (var pattern in _commandPatternCounts
                     .Where(x => x.Value >= MinimumPatternCount)
                     .OrderByDescending(x => x.Value))
        {
            if (!_lastPatternOccurrences.TryGetValue(
                    pattern.Key,
                    out var lastOccurrence) ||
                now - lastOccurrence > RecentActivityWindow)
            {
                continue;
            }

            var parts = pattern.Key.Split(
                '\u001F',
                StringSplitOptions.None);

            if (parts.Length != 2)
                continue;

            var sensorId = parts[0];
            var command = parts[1];
            var score = Math.Min(95, 50 + pattern.Value * 10);

            recommendations.Add(new DeviceRecommendation
            {
                DeviceId = sensorId,
                Category = "LearnedCommandPattern",
                Recommendation = $"Review whether '{command}' is appropriate when this sensor condition recurs.",
                Reason = $"This sensor condition was observed before the command '{command}' on {pattern.Value} recorded occasions.",
                Score = score
            });
        }
    }

    private void AddCommandActivityRecommendations(
        ICollection<DeviceRecommendation> recommendations,
        DateTime now)
    {
        var recentCommands = _commandHistory
            .Where(x => now - x.Timestamp <= RecentActivityWindow)
            .ToArray();

        foreach (var group in recentCommands
                     .GroupBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
                     .Where(x => x.Count() >= 5))
        {
            recommendations.Add(new DeviceRecommendation
            {
                DeviceId = group.Key,
                Category = "CommandActivity",
                Recommendation = "Review recent command activity for repeated manual intervention.",
                Reason = $"{group.Count()} commands were recorded for this device in the last 24 hours.",
                Score = Math.Min(85, 45 + group.Count() * 3)
            });
        }
    }

    private string? FindLearnedPumpCommand(string sensorId)
    {
        var matchingPattern = _commandPatternCounts
            .Where(x => x.Value >= MinimumPatternCount)
            .Select(x => new
            {
                Parts = x.Key.Split('\u001F', StringSplitOptions.None),
                Count = x.Value
            })
            .Where(x =>
                x.Parts.Length == 2 &&
                x.Parts[0].Equals(
                    sensorId,
                    StringComparison.OrdinalIgnoreCase) &&
                IsPumpCommand(x.Parts[1]))
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        return matchingPattern?.Parts[1];
    }

    private static bool IsPotentiallyRelatedMeasurement(
        string sensorId,
        double value,
        string command)
    {
        if (IsMoistureDevice(sensorId) && value < 20 && IsPumpCommand(command))
            return true;

        if (IsTemperatureDevice(sensorId) &&
            (value < 0 || value > 45) &&
            (command.Contains("cool", StringComparison.OrdinalIgnoreCase) ||
             command.Contains("fan", StringComparison.OrdinalIgnoreCase) ||
             command.Contains("heat", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool IsMoistureDevice(string deviceId)
    {
        return deviceId.Contains("moisture", StringComparison.OrdinalIgnoreCase) ||
               deviceId.Contains("soil", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemperatureDevice(string deviceId)
    {
        return deviceId.Contains("temperature", StringComparison.OrdinalIgnoreCase) ||
               deviceId.Contains("temp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPumpCommand(string command)
    {
        return command.Contains("pump", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("irrigat", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("water", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPatternKey(
        string sensorId,
        string command)
    {
        return $"{sensorId}\u001F{command}";
    }
}

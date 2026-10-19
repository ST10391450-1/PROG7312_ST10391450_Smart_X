
using Smart_X_API.Models;

namespace Smart_X_API.Services;

public sealed record DeviceSearchRecord(
    string Query,
    DateTime Timestamp);

public sealed class PredictiveActionService
{
    private readonly object _sync = new();

    private readonly Queue<DeviceSearchRecord> _searchHistory = new();
    private readonly Dictionary<string, int> _searchCounts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<double>> _recentValues =
        new(StringComparer.OrdinalIgnoreCase);

    private const int MaxSearchHistory = 1000;
    private const int MaxValuesPerDevice = 20;

    public void RecordSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        var normalized = query.Trim();

        lock (_sync)
        {
            _searchHistory.Enqueue(
                new DeviceSearchRecord(normalized, DateTime.UtcNow));

            _searchCounts[normalized] =
                _searchCounts.GetValueOrDefault(normalized) + 1;

            while (_searchHistory.Count > MaxSearchHistory)
            {
                var removed = _searchHistory.Dequeue();
                var remaining = _searchHistory.Count(
                    x => x.Query.Equals(
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

        lock (_sync)
        {
            if (!_recentValues.TryGetValue(deviceId, out var values))
            {
                values = new Queue<double>();
                _recentValues[deviceId] = values;
            }

            values.Enqueue(value);

            while (values.Count > MaxValuesPerDevice)
                values.Dequeue();
        }
    }

    public IReadOnlyList<DeviceRecommendation> GetRecommendations()
    {
        lock (_sync)
        {
            var recommendations = new List<DeviceRecommendation>();

            foreach (var search in _searchCounts
                         .Where(x => x.Value >= 3)
                         .OrderByDescending(x => x.Value)
                         .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                recommendations.Add(new DeviceRecommendation
                {
                    DeviceId = search.Key,
                    Category = "SearchPattern",
                    Recommendation = "Review this frequently searched device or category.",
                    Reason = $"The query has appeared {search.Value} times in recent search history.",
                    Score = Math.Min(100, search.Value * 10)
                });
            }

            foreach (var entry in _recentValues.OrderBy(x => x.Key))
            {
                if (entry.Value.Count < 5)
                    continue;

                var values = entry.Value.ToArray();
                var firstAverage = values.Take(values.Length / 2).Average();
                var lastAverage = values.Skip(values.Length / 2).Average();
                var difference = lastAverage - firstAverage;

                if (Math.Abs(difference) < 0.001)
                    continue;

                recommendations.Add(new DeviceRecommendation
                {
                    DeviceId = entry.Key,
                    Category = "TelemetryTrend",
                    Recommendation = difference > 0
                        ? "Review the rising sensor readings."
                        : "Review the falling sensor readings.",
                    Reason = $"The recent average changed by {difference:F2} compared with earlier readings.",
                    Score = Math.Min(
                        100,
                        (int)Math.Round(Math.Abs(difference) * 10))
                });
            }

            return recommendations
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
}

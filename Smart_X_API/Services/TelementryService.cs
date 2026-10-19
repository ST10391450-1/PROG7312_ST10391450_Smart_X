using Smart_X_API.Models;

namespace Smart_X_API.Services;

public class TelemetryService
{
    private readonly List<SensorReadingRecord> _readings = new();
    private readonly List<double> _history = new();
    private readonly object _lock = new();

    public SensorReadingRecord AddReading(
        string deviceId,
        string sensorCategory,
        double value,
        DateTime timestamp)
    {
        var reading = new SensorReadingRecord
        {
            DeviceId = deviceId,
            SensorCategory = sensorCategory,
            Value = value,
            Timestamp = timestamp
        };

        lock (_lock)
        {
            _readings.Add(reading);
            _history.Add(value);
        }

        return reading;
    }

    public IReadOnlyList<SensorReadingRecord> GetReadings(
        string? deviceId = null)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return _readings.ToList();

            return _readings
                .Where(reading => reading.DeviceId.Equals(
                    deviceId,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public IReadOnlyList<double> ProcessRawBatch(double[,] rawBatch)
    {
        ArgumentNullException.ThrowIfNull(rawBatch);

        var values = new List<double>();

        for (int row = 0; row < rawBatch.GetLength(0); row++)
        {
            for (int column = 0; column < rawBatch.GetLength(1); column++)
            {
                values.Add(rawBatch[row, column]);
            }
        }

        lock (_lock)
        {
            _history.AddRange(values);
        }

        return values;
    }

    public IReadOnlyList<double> ProcessJaggedBatch(double[][] rawBatch)
    {
        ArgumentNullException.ThrowIfNull(rawBatch);

        var values = new List<double>();

        foreach (var row in rawBatch)
        {
            if (row == null)
                continue;

            values.AddRange(row);
        }

        lock (_lock)
        {
            _history.AddRange(values);
        }

        return values;
    }

    public SensorReading Aggregate(IEnumerable<double> values)
    {
        SensorReading total = new(0);

        foreach (var value in values)
        {
            total += new SensorReading(value);
        }

        return total;
    }

    public SensorReading CalculateDelta(double previous, double current)
    {
        return new SensorReading(current) - new SensorReading(previous);
    }

    public bool ExceedsThreshold(double value, double threshold)
    {
        return new SensorReading(value) > new SensorReading(threshold);
    }

    public IReadOnlyList<double> GetHistoricalValues()
    {
        lock (_lock)
        {
            return _history.ToList();
        }
    }
}
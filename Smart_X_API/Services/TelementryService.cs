
using Smart_X_API.Models;

namespace Smart_X_API.Services;

public class TelemetryService
{
    private readonly List<SensorReadingRecord> _readings = new();
    private readonly List<double> _history = new();
    private readonly object _lock = new();

    // Saves a new sensor reading.
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

    // Returns all readings, or only readings from a specific device.
    public IReadOnlyList<SensorReadingRecord> GetReadings(
        string? deviceId = null)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return _readings.ToList();

            return _readings
                .Where(reading =>
                    reading.DeviceId.Equals(
                        deviceId,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    // Flattens a 2D array of sensor values into a single list.
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

    // Flattens a jagged array of sensor values into a single list.
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

    // Adds a collection of readings together.
    public SensorReading Aggregate(IEnumerable<double> values)
    {
        SensorReading total = new(0);

        foreach (var value in values)
        {
            total += new SensorReading(value);
        }

        return total;
    }

    // Calculates the difference between two readings.
    public SensorReading CalculateDelta(double previous, double current)
    {
        return new SensorReading(current) - new SensorReading(previous);
    }

    // Checks whether a reading is above the given threshold.
    public bool ExceedsThreshold(double value, double threshold)
    {
        return new SensorReading(value) > new SensorReading(threshold);
    }

    // Returns a copy of all historical sensor values.
    public IReadOnlyList<double> GetHistoricalValues()
    {
        lock (_lock)
        {
            return _history.ToList();
        }
    }
}

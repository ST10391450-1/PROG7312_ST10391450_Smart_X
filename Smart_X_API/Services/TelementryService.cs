using Smart_X_API.Models;

namespace Smart_X_API.Services;

public class TelemetryService
{
    private readonly List<SensorReadingRecord> _processedTelemetry = new();
    private readonly List<double> _historicalValues = new();
    private readonly object _lock = new();

    public SensorReadingRecord AddReading(string deviceId, string sensorCategory, double value, DateTime timestamp)
    {
        var record = new SensorReadingRecord
        {
            DeviceId = deviceId,
            SensorCategory = sensorCategory,
            Value = value,
            Timestamp = timestamp
        };

        lock (_lock)
        {
            _processedTelemetry.Add(record);
            _historicalValues.Add(value);
        }

        return record;
    }

    public IReadOnlyList<SensorReadingRecord> GetReadings(string? deviceId = null)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return _processedTelemetry.ToList();
            }

            return _processedTelemetry
                .Where(x => x.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public IReadOnlyList<double> ProcessRawBatch(double[,] rawBatch)
    {
        ArgumentNullException.ThrowIfNull(rawBatch);

        var processedBatch = new List<double>(rawBatch.GetLength(0) * rawBatch.GetLength(1));

        for (int row = 0; row < rawBatch.GetLength(0); row++)
        {
            for (int column = 0; column < rawBatch.GetLength(1); column++)
            {
                processedBatch.Add(rawBatch[row, column]);
            }
        }

        lock (_lock)
        {
            _historicalValues.AddRange(processedBatch);
        }

        return processedBatch;
    }

    public IReadOnlyList<double> ProcessJaggedBatch(double[][] rawBatch)
    {
        ArgumentNullException.ThrowIfNull(rawBatch);

        var processedBatch = new List<double>();

        foreach (double[] row in rawBatch)
        {
            if (row == null)
            {
                continue;
            }

            processedBatch.AddRange(row);
        }

        lock (_lock)
        {
            _historicalValues.AddRange(processedBatch);
        }

        return processedBatch;
    }

    public SensorReading Aggregate(IEnumerable<double> values)
    {
        SensorReading total = new(0);

        foreach (double value in values)
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
            return _historicalValues.ToList();
        }
    }
}

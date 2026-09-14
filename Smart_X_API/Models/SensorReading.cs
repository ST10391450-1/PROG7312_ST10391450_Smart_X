namespace Smart_X_API.Models;

public readonly struct SensorReading
{
    public double Value { get; }

    public SensorReading(double value)
    {
        Value = value;
    }

    public static SensorReading operator +(SensorReading left, SensorReading right) => new(left.Value + right.Value);
    public static SensorReading operator -(SensorReading left, SensorReading right) => new(left.Value - right.Value);

    public static bool operator >(SensorReading left, SensorReading right) => left.Value > right.Value;
    public static bool operator <(SensorReading left, SensorReading right) => left.Value < right.Value;
    public static bool operator >=(SensorReading left, SensorReading right) => left.Value >= right.Value;
    public static bool operator <=(SensorReading left, SensorReading right) => left.Value <= right.Value;
    public static bool operator ==(SensorReading left, SensorReading right) => left.Value == right.Value;
    public static bool operator !=(SensorReading left, SensorReading right) => left.Value != right.Value;

    public override bool Equals(object? obj) => obj is SensorReading reading && this == reading;

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString("F2");
}

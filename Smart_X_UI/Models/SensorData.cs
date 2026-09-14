using Avalonia.Media;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Smart_X_UI.Models;

public class SensorRegistration : INotifyPropertyChanged
{
    public string MacAddress { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string NodeId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    private bool _isActive;

    [JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
        }
    }

    [JsonIgnore]
    public string StatusText =>
        IsActive ? "ACTIVE" : "INACTIVE";

    [JsonIgnore]
    public IBrush StatusColor =>
        IsActive ? ActiveBrush : InactiveBrush;

    private string _currentDataText = "No data";

    [JsonIgnore]
    public string CurrentDataText
    {
        get => _currentDataText;
        set
        {
            if (_currentDataText == value)
            {
                return;
            }

            _currentDataText = value;
            OnPropertyChanged();
        }
    }

    private string _currentDataTimestampText = "--";

    [JsonIgnore]
    public string CurrentDataTimestampText
    {
        get => _currentDataTimestampText;
        set
        {
            if (_currentDataTimestampText == value)
            {
                return;
            }

            _currentDataTimestampText = value;
            OnPropertyChanged();
        }
    }

    private static readonly IBrush ActiveBrush =
        new SolidColorBrush(Color.Parse("#39D98A"));

    private static readonly IBrush InactiveBrush =
        new SolidColorBrush(Color.Parse("#FFB15C"));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString()
    {
        return $"{NodeId} — {Category} — {Location}";
    }
}

public class TelemetryPacket<T>
{
    public string DeviceId { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public string SensorCategory { get; set; } = string.Empty;

    public T Value { get; set; } = default!;
}

public class SensorReadingRecord
{
    public string DeviceId { get; set; } = string.Empty;

    public string SensorCategory { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTime Timestamp { get; set; }
}

public class SensorAttachment
{
    public Guid Id { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; }

    public string DisplaySize
    {
        get
        {
            if (FileSize < 1024)
            {
                return $"{FileSize} B";
            }

            if (FileSize < 1024 * 1024)
            {
                return $"{FileSize / 1024.0:F1} KB";
            }

            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }
}
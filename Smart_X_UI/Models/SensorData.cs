
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
    public string Unit { get; set; } = string.Empty;

    private bool _isEnabled = true;

    [JsonIgnore]
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
        }
    }

    [JsonIgnore]
    public bool IsActive
    {
        get => IsEnabled;
        set => IsEnabled = value;
    }

    [JsonIgnore]
    public string StatusText => IsEnabled ? "ACTIVE" : "INACTIVE";

    [JsonIgnore]
    public IBrush StatusColor => IsEnabled ? ActiveBrush : InactiveBrush;

    private string _currentDataText = "No data";

    [JsonIgnore]
    public string CurrentDataText
    {
        get => _currentDataText;
        set
        {
            if (_currentDataText == value)
                return;

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
                return;

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

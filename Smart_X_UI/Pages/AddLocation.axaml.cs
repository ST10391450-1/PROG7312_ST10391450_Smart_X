
using Avalonia.Controls;
using Avalonia.Interactivity;
using Smart_X_UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class AddLocation : UserControl
{
    #region Fields

    private readonly LocationService _locationService =
        new(ApiClient.HttpClient);

    private readonly List<string> _locations = new();

    private bool _isSubmitting;

    #endregion

    #region Events

    public event EventHandler? BackRequested;
    public event EventHandler? LocationAdded;

    #endregion

    #region Constructor

    public AddLocation()
    {
        InitializeComponent();

        _ = LoadLocationsAsync();
        UpdateLocationPreview();
    }

    #endregion

    #region Locations

    private async Task LoadLocationsAsync()
    {
        try
        {
            var locations =
                await _locationService.GetLocationsAsync();

            _locations.Clear();

            _locations.AddRange(
                locations.Distinct(StringComparer.OrdinalIgnoreCase));

            ParentLocationComboBox.ItemsSource = null;
            ParentLocationComboBox.ItemsSource = _locations;
        }
        catch (Exception ex)
        {
            ShowStatus(
                $"Could not load existing locations: {ex.Message}",
                true);
        }
    }

    private void LocationNameTextBox_TextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        LocationNameErrorText.IsVisible =
            string.IsNullOrWhiteSpace(LocationNameTextBox.Text);

        UpdateLocationPreview();
    }

    private void ParentLocationComboBox_SelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        UpdateLocationPreview();
    }

    private void UpdateLocationPreview()
    {
        var name =
            LocationNameTextBox?.Text?.Trim() ?? string.Empty;

        var parent =
            ParentLocationComboBox?.SelectedItem?.ToString()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            LocationPreviewText.Text =
                string.IsNullOrWhiteSpace(parent)
                    ? "New location"
                    : $"{parent} / New location";

            return;
        }

        LocationPreviewText.Text =
            string.IsNullOrWhiteSpace(parent)
                ? name
                : $"{parent} / {name}";
    }

    #endregion

    #region Adding Locations

    private async void AddLocationButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_isSubmitting)
            return;

        _isSubmitting = true;

        var addButton = sender as Button;

        if (addButton != null)
            addButton.IsEnabled = false;

        try
        {
            var name =
                LocationNameTextBox.Text?.Trim() ?? string.Empty;

            var parent =
                ParentLocationComboBox.SelectedItem?.ToString()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                LocationNameErrorText.Text =
                    "Location name is required.";

                LocationNameErrorText.IsVisible = true;
                LocationNameTextBox.Focus();

                return;
            }

            if (name.Contains('/') || name.Contains('\\'))
            {
                LocationNameErrorText.Text =
                    "Location name cannot contain / or \\.";

                LocationNameErrorText.IsVisible = true;
                LocationNameTextBox.Focus();

                return;
            }

            if (!string.IsNullOrWhiteSpace(parent))
            {
                var parentName = parent
                    .Split(
                        '/',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .LastOrDefault();

                if (string.Equals(
                        parentName,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ShowStatus(
                        "A location cannot have the same name as its parent.",
                        true);

                    return;
                }
            }

            var location =
                string.IsNullOrWhiteSpace(parent)
                    ? name
                    : $"{parent} / {name}";

            await LoadLocationsAsync();

            var alreadyExists = _locations.Any(existing =>
                string.Equals(
                    existing,
                    location,
                    StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
            {
                ShowStatus(
                    "This location already exists.",
                    true);

                return;
            }

            await _locationService.AddLocationAsync(location);

            ShowStatus(
                $"Location added successfully: {location}",
                false);

            LocationNameTextBox.Clear();
            ParentLocationComboBox.SelectedItem = null;

            LocationNameErrorText.IsVisible = false;

            await LoadLocationsAsync();

            UpdateLocationPreview();

            LocationAdded?.Invoke(
                this,
                EventArgs.Empty);
        }
        catch (HttpRequestException ex)
        {
            ShowStatus(
                $"API error: {ex.Message}",
                true);
        }
        catch (TaskCanceledException)
        {
            ShowStatus(
                "The request timed out.",
                true);
        }
        catch (Exception ex)
        {
            ShowStatus(
                $"Could not add location: {ex.Message}",
                true);
        }
        finally
        {
            _isSubmitting = false;

            if (addButton != null)
                addButton.IsEnabled = true;
        }
    }

    #endregion

    #region Form

    private void ClearButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        LocationNameTextBox.Clear();
        ParentLocationComboBox.SelectedItem = null;

        LocationNameErrorText.IsVisible = false;
        StatusBorder.IsVisible = false;

        UpdateLocationPreview();

        LocationNameTextBox.Focus();
    }

    #endregion

    #region Navigation

    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    #endregion

    #region Status

    private void ShowStatus(
        string message,
        bool isError)
    {
        StatusText.Text = message;

        StatusText.Foreground =
            isError
                ? Avalonia.Media.Brushes.IndianRed
                : Avalonia.Media.Brushes.LightGreen;

        StatusBorder.IsVisible = true;
    }

    #endregion
}

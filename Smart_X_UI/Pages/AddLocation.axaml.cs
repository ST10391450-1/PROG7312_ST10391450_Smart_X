using Avalonia.Controls;
using Avalonia.Interactivity;
using Smart_X_UI.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class AddLocation : UserControl
{
    private readonly LocationService _locationService =
        new(ApiClient.HttpClient);

    private readonly List<string> _locations = new();

    public event EventHandler? BackRequested;
    public event EventHandler? LocationAdded;

    public AddLocation()
    {
        InitializeComponent();

        _ = LoadLocationsAsync();
        UpdateLocationPreview();
    }

    private async Task LoadLocationsAsync()
    {
        try
        {
            var locations =
                await _locationService.GetLocationsAsync();

            _locations.Clear();
            _locations.AddRange(locations);

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
        string name =
            LocationNameTextBox?.Text?.Trim() ?? string.Empty;

        string parent =
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

    private async void AddLocationButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var addButton = sender as Button;

        if (addButton != null)
        {
            addButton.IsEnabled = false;
        }

        try
        {
            string name =
                LocationNameTextBox.Text?.Trim() ?? string.Empty;

            string parent =
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

            if (name.Contains('/') ||
                name.Contains('\\'))
            {
                LocationNameErrorText.Text =
                    "Location name cannot contain / or \\.";

                LocationNameErrorText.IsVisible = true;
                LocationNameTextBox.Focus();

                return;
            }

            string location =
                string.IsNullOrWhiteSpace(parent)
                    ? name
                    : $"{parent} / {name}";

            bool alreadyExists =
                _locations.Exists(existing =>
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

            LocationAdded?.Invoke(
                this,
                EventArgs.Empty);

            await LoadLocationsAsync();
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
            if (addButton != null)
            {
                addButton.IsEnabled = true;
            }
        }
    }

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

    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

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
}
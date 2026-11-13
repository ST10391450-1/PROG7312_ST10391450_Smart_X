
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Smart_X_UI.Models;
using Smart_X_UI.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class SensorAdd : UserControl
{
    private readonly SensorService _sensorService =
        new(ApiClient.HttpClient);

    private readonly LocationService _locationService =
        new(ApiClient.HttpClient);

    private readonly SensorCategoryService _categoryService =
        new(ApiClient.HttpClient);

    private readonly SensorImportService _importService =
        new(ApiClient.HttpClient);

    private static readonly Regex MacAddressRegex = new(
        @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$",
        RegexOptions.Compiled);

    public event EventHandler<SensorRegistration>? SensorRegistered;
    public event EventHandler? BackRequested;

    public SensorAdd()
    {
        InitializeComponent();

        _ = LoadLocationsAsync();
        _ = LoadCategoriesAsync();
    }

    private async Task LoadLocationsAsync()
    {
        try
        {
            var locations = await _locationService.GetLocationsAsync();
            LocationComboBox.ItemsSource = locations;
        }
        catch
        {
            LocationComboBox.ItemsSource = Array.Empty<string>();
        }
    }

    private async Task LoadCategoriesAsync()
    {
        CategoryComboBox.IsEnabled = false;

        try
        {
            var categories = await _categoryService.GetAllAsync();

            CategoryComboBox.ItemsSource = categories;
            CategoryComboBox.SelectedItem = null;
            CategoryErrorText.IsVisible = false;

            if (categories.Count == 0)
            {
                RegistrationStatusText.Text =
                    "No categories are available. Create a category using Manage Categories first.";

                RegistrationStatusText.Foreground = Brushes.Goldenrod;
                RegistrationStatusText.IsVisible = true;
            }
        }
        catch (HttpRequestException ex)
        {
            CategoryComboBox.ItemsSource = Array.Empty<SensorCategory>();

            RegistrationStatusText.Text =
                $"Could not load categories from the API.\n\n{ex.Message}";

            RegistrationStatusText.Foreground = Brushes.OrangeRed;
            RegistrationStatusText.IsVisible = true;
        }
        catch (Exception ex)
        {
            CategoryComboBox.ItemsSource = Array.Empty<SensorCategory>();

            RegistrationStatusText.Text =
                $"Could not load categories: {ex.Message}";

            RegistrationStatusText.Foreground = Brushes.OrangeRed;
            RegistrationStatusText.IsVisible = true;
        }
        finally
        {
            CategoryComboBox.IsEnabled = true;
        }
    }

    private async void RegisterSensorButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (!ValidateForm())
            return;

        RegisterSensorButton.IsEnabled = false;

        try
        {
            var selectedCategory =
                CategoryComboBox.SelectedItem as SensorCategory;

            if (selectedCategory is null)
            {
                CategoryErrorText.IsVisible = true;
                return;
            }

            var sensor = new SensorRegistration
            {
                MacAddress = MacAddressTextBox.Text!.Trim(),
                NodeId = NodeIdTextBox.Text!.Trim(),
                Category = selectedCategory.Name,
                Location = LocationComboBox.SelectedItem?.ToString()
                           ?? string.Empty
            };

            var registeredSensor =
                await _sensorService.RegisterSensorAsync(sensor);

            RegistrationStatusText.Text =
                $"Sensor '{registeredSensor.NodeId}' registered successfully.";

            RegistrationStatusText.Foreground = Brushes.LimeGreen;
            RegistrationStatusText.IsVisible = true;

            SensorRegistered?.Invoke(this, registeredSensor);

            ClearForm();
        }
        catch (HttpRequestException ex)
        {
            RegistrationStatusText.Text =
                $"Could not connect to the Smart X API.\n\n{ex.Message}";

            RegistrationStatusText.Foreground = Brushes.OrangeRed;
            RegistrationStatusText.IsVisible = true;
        }
        catch (Exception ex)
        {
            RegistrationStatusText.Text = ex.Message;
            RegistrationStatusText.Foreground = Brushes.OrangeRed;
            RegistrationStatusText.IsVisible = true;
        }
        finally
        {
            RegisterSensorButton.IsEnabled = true;
        }
    }

    private bool ValidateForm()
    {
        var valid = true;

        var mac = MacAddressTextBox.Text?.Trim() ?? string.Empty;

        if (!MacAddressRegex.IsMatch(mac))
        {
            MacAddressTextBox.BorderBrush = Brushes.OrangeRed;
            MacAddressErrorText.IsVisible = true;
            valid = false;
        }
        else
        {
            MacAddressTextBox.ClearValue(Border.BorderBrushProperty);
            MacAddressErrorText.IsVisible = false;
        }

        var nodeId = NodeIdTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            NodeIdTextBox.BorderBrush = Brushes.OrangeRed;
            NodeIdErrorText.IsVisible = true;
            valid = false;
        }
        else
        {
            NodeIdTextBox.ClearValue(Border.BorderBrushProperty);
            NodeIdErrorText.IsVisible = false;
        }

        if (CategoryComboBox.SelectedItem is not SensorCategory)
        {
            CategoryErrorText.IsVisible = true;
            valid = false;
        }
        else
        {
            CategoryErrorText.IsVisible = false;
        }

        if (LocationComboBox.SelectedItem is null)
        {
            LocationErrorText.IsVisible = true;
            valid = false;
        }
        else
        {
            LocationErrorText.IsVisible = false;
        }

        return valid;
    }

    private void MacAddressTextBox_TextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        MacAddressErrorText.IsVisible =
            !string.IsNullOrWhiteSpace(MacAddressTextBox.Text) &&
            !MacAddressRegex.IsMatch(MacAddressTextBox.Text.Trim());
    }

    private void NodeIdTextBox_TextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        NodeIdErrorText.IsVisible =
            string.IsNullOrWhiteSpace(NodeIdTextBox.Text);
    }

    private async void ImportJsonButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage is null)
        {
            ShowStatus("File picker is not available.", Brushes.OrangeRed);
            return;
        }

        ImportJsonButton.IsEnabled = false;

        try
        {
            var files = await storage.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Import sensors from JSON",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("JSON files")
                        {
                            Patterns = new[] { "*.json" }
                        }
                    }
                });

            var file = files.FirstOrDefault();

            if (file is null)
                return;

            ShowStatus($"Importing '{file.Name}'...", Brushes.Goldenrod);

            await using var stream = await file.OpenReadAsync();

            var (request, warnings) =
                await _importService.ParseStreamAsync(stream);

            var result = await _importService.ImportAsync(request);

            var lines = new System.Collections.Generic.List<string>
            {
                $"Import complete: '{file.Name}'",
                $"Sensors imported: {result.SensorsImported}  |  skipped: {result.SensorsSkipped}",
                $"Categories created: {result.CategoriesCreated}  |  already existed/skipped: {result.CategoriesSkipped}",
                $"Locations created: {result.LocationsCreated}"
            };

            var notes = warnings.Concat(result.Messages).ToList();

            if (notes.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Notes:");
                lines.AddRange(notes.Take(15).Select(x => "• " + x));

                if (notes.Count > 15)
                    lines.Add($"• ...and {notes.Count - 15} more.");
            }

            ShowStatus(
                string.Join(Environment.NewLine, lines),
                result.SensorsImported > 0 || result.CategoriesCreated > 0
                    ? Brushes.LimeGreen
                    : Brushes.Goldenrod);

            // Refresh the dropdowns so imported data is selectable.
            await LoadCategoriesAsync();
            await LoadLocationsAsync();

            // Tell the dashboard to refresh.
            if (result.SensorsImported > 0)
            {
                var last = request.Sensors.LastOrDefault();

                if (last is not null)
                    SensorRegistered?.Invoke(this, last);
            }
        }
        catch (HttpRequestException ex)
        {
            ShowStatus(
                $"Could not import. The API said:\n\n{ex.Message}",
                Brushes.OrangeRed);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, Brushes.OrangeRed);
        }
        finally
        {
            ImportJsonButton.IsEnabled = true;
        }
    }

    private void ShowStatus(string message, IBrush brush)
    {
        RegistrationStatusText.Text = message;
        RegistrationStatusText.Foreground = brush;
        RegistrationStatusText.IsVisible = true;
    }

    private async void RefreshCategoriesButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        await LoadCategoriesAsync();
    }

    private void ClearButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ClearForm();
    }

    private void ClearForm()
    {
        MacAddressTextBox.Text = string.Empty;
        NodeIdTextBox.Text = string.Empty;
        CategoryComboBox.SelectedItem = null;
        LocationComboBox.SelectedItem = null;

        MacAddressErrorText.IsVisible = false;
        NodeIdErrorText.IsVisible = false;
        CategoryErrorText.IsVisible = false;
        LocationErrorText.IsVisible = false;

        MacAddressTextBox.ClearValue(Border.BorderBrushProperty);
        NodeIdTextBox.ClearValue(Border.BorderBrushProperty);

        RegistrationStatusText.IsVisible = false;
        RegistrationStatusText.Text = string.Empty;
    }

    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}

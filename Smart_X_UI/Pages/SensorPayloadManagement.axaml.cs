using Avalonia.Controls;
using Avalonia.Interactivity;
using Smart_X_UI;

namespace Smart_X_UI.Pages;

public partial class SensorPayloadManagement : UserControl
{
    public SensorPayloadManagement()
    {
        InitializeComponent();
    }

    private void BackToDashboardButton_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            window.ShowDashboard();
        }
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        MacAddressTextBox.Text = string.Empty;
        LocationTextBox.Text = string.Empty;
        NodeIdTextBox.Text = string.Empty;
        SensorCategoryComboBox.SelectedIndex = 0;
    }

    private void RegisterSensorButton_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: validate the form fields, POST to the API, add a row to the sensor list
    }

    private void SimulatePayloadButton_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: only usable once there's at least one registered sensor to simulate from
    }
}
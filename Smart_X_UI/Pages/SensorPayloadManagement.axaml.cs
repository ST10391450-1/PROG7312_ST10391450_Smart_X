using Avalonia.Controls;
using Avalonia.Interactivity;
using Smart_X_UI.Models;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Mail;

namespace Smart_X_UI.Pages;

public partial class SensorPayloadManagement : UserControl
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("http://localhost:8080/"),
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly ObservableCollection<SensorRegistration> _sensors = new();
    private readonly ObservableCollection<SensorAttachment> _attachments = new();

    public SensorPayloadManagement()
    {
        InitializeComponent();

        RegisteredSensorsItemsControl.ItemsSource = _sensors;
        SimulationSensorComboBox.ItemsSource = _sensors;
        AttachmentSensorComboBox.ItemsSource = _sensors;
        AttachmentsItemsControl.ItemsSource = _attachments;

        _ = LoadSensorsAsync();
    }

    private void BackToDashboardButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            window.ShowDashboard();
        }
    }
}
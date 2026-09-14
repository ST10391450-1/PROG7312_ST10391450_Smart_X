using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class SensorPayloadManagement
{
    private async Task ShowMessage(string message)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            Console.WriteLine(message);
            return;
        }

        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 90,
            HorizontalAlignment =
                HorizontalAlignment.Right
        };

        var panel = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(20)
        };

        panel.Children.Add(textBlock);
        panel.Children.Add(okButton);

        var dialog = new Window
        {
            Title = "Smart X",
            Width = 420,
            MinHeight = 170,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
            Content = panel
        };

        okButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(owner);
    }
}
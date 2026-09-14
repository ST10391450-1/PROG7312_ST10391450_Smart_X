using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Threading.Tasks;

namespace Smart_X_UI.Services;

public class DialogService
{
    public async Task ShowMessageAsync(
        TopLevel? topLevel,
        string message)
    {
        if (topLevel is not Window owner)
        {
            Console.WriteLine(message);
            return;
        }

        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right
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
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        okButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(owner);
    }
}
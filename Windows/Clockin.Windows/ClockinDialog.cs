using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfControl = System.Windows.Controls.Control;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace Clockin.Windows;

public static class ClockinDialog
{
    public static bool Confirm(Window? owner, string title, string message, string confirmText = "Confirm", bool destructive = false)
    {
        var window = Build(owner, title, message, confirmText, destructive, confirm: true);
        return window.ShowDialog() == true;
    }

    public static void Alert(Window? owner, string title, string message, string buttonText = "OK")
    {
        var window = Build(owner, title, message, buttonText, destructive: false, confirm: false);
        window.ShowDialog();
    }

    private static Window Build(Window? owner, string title, string message, string actionText, bool destructive, bool confirm)
    {
        var window = new Window
        {
            Width = 410,
            MinHeight = confirm ? 190 : 165,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner,
            ShowInTaskbar = false,
            Background = WpfBrushes.Transparent,
            Foreground = WpfBrushes.White
        };

        var content = new StackPanel { Margin = new Thickness(18, 16, 18, 16) };
        var messageBlock = new TextBlock
        {
            Text = message,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 18)
        };
        content.Children.Add(messageBlock);

        var buttons = new StackPanel { Orientation = WpfOrientation.Horizontal, HorizontalAlignment = WpfHorizontalAlignment.Right };
        if (confirm)
        {
            var cancel = Button("Cancel");
            cancel.IsCancel = true;
            cancel.Click += (_, _) => window.DialogResult = false;
            buttons.Children.Add(cancel);
        }

        var action = Button(actionText);
        action.IsDefault = true;
        action.Click += (_, _) => window.DialogResult = true;
        buttons.Children.Add(action);
        content.Children.Add(buttons);

        window.Content = content;
        CustomChrome.Attach(window, title.ToUpperInvariant(), SettingStore.Shared);
        messageBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        if (destructive)
        {
            action.SetResourceReference(WpfControl.BackgroundProperty, "CardBrush");
            action.Foreground = new SolidColorBrush(WpfColor.FromRgb(224, 122, 120));
            action.SetResourceReference(WpfControl.BorderBrushProperty, "CardStrokeBrush");
        }
        else
        {
            action.SetResourceReference(WpfControl.BackgroundProperty, "AccentBrush");
            action.SetResourceReference(WpfControl.ForegroundProperty, "ActionForegroundBrush");
            action.FontWeight = FontWeights.Bold;
        }
        return window;
    }

    private static WpfButton Button(string text)
    {
        return new WpfButton { Content = text, MinWidth = 76, Margin = new Thickness(3, 0, 0, 0) };
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfControl = System.Windows.Controls.Control;

namespace Clockin.Windows;

public static class ThemeManager
{
    private static readonly DependencyProperty BaseFontSizeProperty = DependencyProperty.RegisterAttached(
        "BaseFontSize", typeof(double), typeof(ThemeManager), new FrameworkPropertyMetadata(double.NaN));

    public static double TextScale(SettingStore settings) => settings.Get("TextSize", settings.Get("PinnedFontSize", "Comfortable")) switch
    {
        "Small" => 0.94,
        "Large" => 1.12,
        _ => 1.0
    };

    public static double PinnedTextScale(SettingStore settings) => settings.Get("TextSize", settings.Get("PinnedFontSize", "Comfortable")) switch
    {
        "Small" => 0.94,
        "Large" => 1.24,
        _ => 1.12
    };

    public static void Apply(FrameworkElement target, SettingStore settings)
    {
        var theme = ThemePalette.For(settings.Get("Theme", "Carbon"));
        target.Resources["WindowBackgroundBrush"] = new SolidColorBrush(theme.Color(theme.Background));
        target.Resources["CardBrush"] = new SolidColorBrush(theme.Color(theme.Card));
        target.Resources["CardStrokeBrush"] = new SolidColorBrush(theme.Color(theme.Stroke));
        target.Resources["AccentBrush"] = new SolidColorBrush(theme.Color(theme.Accent));
        target.Resources["SecondaryAccentBrush"] = new SolidColorBrush(theme.Color(theme.Secondary));
        target.Resources["TextBrush"] = new SolidColorBrush(theme.Color(theme.Text));
        target.Resources["MutedBrush"] = new SolidColorBrush(theme.Color(theme.Muted));
        target.Resources["ActionForegroundBrush"] = new SolidColorBrush(theme.Color(theme.ActionForeground));
        target.Resources["SuccessBrush"] = new SolidColorBrush(theme.Color(theme.Success));
        target.Resources["WarningBrush"] = new SolidColorBrush(theme.Color(theme.Warning));
        target.Resources["DangerBrush"] = new SolidColorBrush(theme.Color(theme.Danger));
        if (target is Window window)
        {
            window.FontFamily = new System.Windows.Media.FontFamily(theme.FontFamily);
            window.FontSize = 12 * TextScale(settings);
            window.Background = window.AllowsTransparency ? System.Windows.Media.Brushes.Transparent : (System.Windows.Media.Brush)target.Resources["WindowBackgroundBrush"];
            window.Foreground = (System.Windows.Media.Brush)target.Resources["TextBrush"];
            ApplyTextScale(window, settings);
        }
    }

    public static void ApplyTextScale(FrameworkElement target, SettingStore settings) => ApplyTextScale(target, TextScale(settings));

    public static void ApplyTextScale(FrameworkElement target, double scale)
    {
        if (target is Window window) window.FontSize = 12;
        ScaleVisualTree(target, scale, isRoot: true);
        if (target is Window scaledWindow) scaledWindow.FontSize = 12 * scale;
    }

    private static void ScaleVisualTree(DependencyObject current, double scale, bool isRoot)
    {
        if (!isRoot)
        {
            if (current is WpfControl control)
            {
                control.FontSize = BaseSize(control, control.FontSize) * scale;
            }
            else if (current is TextBlock textBlock)
            {
                textBlock.FontSize = BaseSize(textBlock, textBlock.FontSize) * scale;
            }
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
        {
            ScaleVisualTree(VisualTreeHelper.GetChild(current, index), scale, isRoot: false);
        }
    }

    private static double BaseSize(DependencyObject element, double current)
    {
        var baseSize = (double)element.GetValue(BaseFontSizeProperty);
        if (double.IsNaN(baseSize) || baseSize <= 0)
        {
            baseSize = current > 0 ? current : 12;
            element.SetValue(BaseFontSizeProperty, baseSize);
        }
        return baseSize;
    }
}

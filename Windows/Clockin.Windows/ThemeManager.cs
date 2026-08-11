using System.Windows;
using System.Windows.Media;

namespace Clockin.Windows;

public static class ThemeManager
{
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
        if (target is Window window)
        {
            window.FontFamily = new System.Windows.Media.FontFamily(theme.FontFamily);
            window.FontSize = 12 * TextScale(settings);
            window.Background = window.AllowsTransparency ? System.Windows.Media.Brushes.Transparent : (System.Windows.Media.Brush)target.Resources["WindowBackgroundBrush"];
            window.Foreground = (System.Windows.Media.Brush)target.Resources["TextBrush"];
        }
    }
}

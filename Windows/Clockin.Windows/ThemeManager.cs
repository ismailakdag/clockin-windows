using System.Windows;
using System.Windows.Media;

namespace Clockin.Windows;

public static class ThemeManager
{
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
        if (target is Window window) { window.FontFamily = new System.Windows.Media.FontFamily(theme.FontFamily); window.Background = (System.Windows.Media.Brush)target.Resources["WindowBackgroundBrush"]; window.Foreground = (System.Windows.Media.Brush)target.Resources["TextBrush"]; }
    }
}

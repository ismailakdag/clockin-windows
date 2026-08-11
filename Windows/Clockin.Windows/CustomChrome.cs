using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Clockin.Windows;

public static class CustomChrome
{
    private static readonly HashSet<Window> Attached = [];

    public static void Attach(Window window, string title, SettingStore settings)
    {
        if (Attached.Contains(window) || window.Content is not UIElement content) return;
        Attached.Add(window); ThemeManager.Apply(window, settings); window.WindowStyle = WindowStyle.None; window.ResizeMode = ResizeMode.NoResize; window.ShowInTaskbar = false;
        var shell = new Grid { Background = (System.Windows.Media.Brush)window.Resources["WindowBackgroundBrush"], Opacity = 0, RenderTransformOrigin = new System.Windows.Point(0.5, 0.5), RenderTransform = new System.Windows.Media.ScaleTransform(0.97, 0.97) };
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) }); shell.RowDefinitions.Add(new RowDefinition());
        var header = new Border { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(22, 0, 0, 0)), BorderBrush = (System.Windows.Media.Brush)window.Resources["CardStrokeBrush"], BorderThickness = new Thickness(0, 0, 0, 1) };
        var bar = new DockPanel { LastChildFill = true, Margin = new Thickness(15, 0, 10, 0) };
        var label = new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.Black, Foreground = (System.Windows.Media.Brush)window.Resources["TextBrush"], VerticalAlignment = VerticalAlignment.Center };
        var controls = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        controls.Children.Add(MakeButton("—", "Minimize", () => window.WindowState = WindowState.Minimized, window)); controls.Children.Add(MakeButton("×", "Close", window.Close, window)); DockPanel.SetDock(controls, Dock.Right); bar.Children.Add(controls); bar.Children.Add(label); header.Child = bar;
        header.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) window.DragMove(); };
        window.Content = null;
        Grid.SetRow(header, 0); Grid.SetRow(content, 1); shell.Children.Add(header); shell.Children.Add(content); window.Content = shell;
        window.Loaded += (_, _) => { shell.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))); if (shell.RenderTransform is System.Windows.Media.ScaleTransform scale) { scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(220))); scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(220))); } };
    }

    private static System.Windows.Controls.Button MakeButton(string text, string tooltip, Action action, Window owner)
    {
        var button = new System.Windows.Controls.Button { Content = text, ToolTip = tooltip, Width = 30, Height = 28, Padding = new Thickness(0), Margin = new Thickness(2, 0, 0, 0), Background = System.Windows.Media.Brushes.Transparent, BorderBrush = System.Windows.Media.Brushes.Transparent, Foreground = (System.Windows.Media.Brush)owner.Resources["MutedBrush"] };
        button.Click += (_, _) => action(); return button;
    }
}

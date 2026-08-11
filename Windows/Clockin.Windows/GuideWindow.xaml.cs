using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Clockin.Windows;

public partial class GuideWindow : Window
{
    public GuideWindow()
    {
        InitializeComponent(); CustomChrome.Attach(this, "HOW TO USE CLOCKIN", SettingStore.Shared); Build();
    }

    private void Build()
    {
        var sections = new[]
        {
            ("1", "Start a session", "Press Clock in on the home screen. Pause keeps the session open without adding time; Resume continues it. Clock out saves the session and opens its summary. Use Start with elapsed time when you started before opening Clockin."),
            ("2", "Bring in My Time history", "Select the complete date range in your timecard page, export CSV, then open History or Settings → Data → Import CSV. Review NEW, MATCHED and SKIP rows before importing."),
            ("3", "Paste approved timecards", "If CSV export is unavailable, paste the full approved page or a task block. Clockin recognizes complete date/start/end patterns, checks the approved total and skips exact duplicates."),
            ("4", "Keep historical rates correct", "Open Settings → Pay & Currency → Manage under Rate Schedule. Add effective start/end dates and the hourly rate for each period; old sessions keep their historical rate."),
            ("5", "Read your progress", "Earnings History shows money and hours. Work Heatmap shows rhythm by day, week, month or all time. Progress contains levels, goals, streaks, records, badges, weekly metrics, reports and shareable stats."),
            ("6", "Use desktop tools", "Pin the widget to keep the timer visible. Compact, Money, Goal and All modes have different layouts. The Windows tray and Ctrl+Alt+I/P/O/E shortcuts keep Clockin usable while another app is focused."),
            ("7", "Back up safely", "Clockin creates automatic backups before saves. Settings → Data lets you export JSON, restore a file or restore the latest automatic backup."),
        };
        foreach (var section in sections)
        {
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Brush("AccentBrush"), Child = new TextBlock { Text = section.Item1, Foreground = Brush("ActionForegroundBrush"), FontWeight = FontWeights.Black, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center } });
            var body = new StackPanel { Margin = new Thickness(10, 0, 0, 0) }; body.Children.Add(Text(section.Item2, 12, "TextBrush", FontWeights.Bold)); body.Children.Add(Text(section.Item3, 9, "MutedBrush")); Grid.SetColumn(body, 1); grid.Children.Add(body);
            GuideContent.Children.Add(new Border { Child = grid, Background = Brush("CardBrush"), BorderBrush = Brush("CardStrokeBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(11), Margin = new Thickness(0, 0, 0, 10) });
        }
        GuideContent.Children.Add(new Border { Child = Text("Tip: import the oldest history first, check the comparison totals, then add newer exports. This makes rate periods and duplicate matching easier to audit.", 10, "AccentBrush", FontWeights.SemiBold), Background = Brush("CardBrush"), CornerRadius = new CornerRadius(11), Padding = new Thickness(12) });
        ThemeManager.ApplyTextScale(this, SettingStore.Shared);
    }

    private TextBlock Text(string value, double size, string color, FontWeight? weight = null) => new() { Text = value, FontSize = size, Foreground = Brush(color), FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
    private SolidColorBrush Brush(string key) => (SolidColorBrush)FindResource(key);
}

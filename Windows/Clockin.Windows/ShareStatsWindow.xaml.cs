using Microsoft.Win32;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Clockin.Windows;

public partial class ShareStatsWindow : Window
{
    private readonly ClockStore _store;
    private readonly SettingStore _settings;
    private int _page;
    private bool _allPages;
    private string _lastText = "";

    public ShareStatsWindow(ClockStore store, SettingStore settings)
    {
        InitializeComponent(); _store = store; _settings = settings; CustomChrome.Attach(this, "SHARE YOUR STATS", settings); VisibilityBox.SelectedIndex = 0; Loaded += (_, _) => Render();
    }

    private bool IsPublic => VisibilityBox.SelectedIndex == 0;
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsInitialized) Render(); }
    private void Previous_Click(object sender, RoutedEventArgs e) { _allPages = false; _page = Math.Max(0, _page - 1); Render(); }
    private void Next_Click(object sender, RoutedEventArgs e) { _allPages = false; _page = Math.Min(2, _page + 1); Render(); }
    private void AllPages_Click(object sender, RoutedEventArgs e) { _allPages = !_allPages; Render(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Render()
    {
        var snapshot = ProgressCalculator.Calculate(_store, _settings);
        _lastText = BuildText(snapshot);
        PreviousButton.IsEnabled = !_allPages && _page > 0; NextButton.IsEnabled = !_allPages && _page < 2; AllPagesButton.Content = _allPages ? "Current page" : "All 3 pages";
        PageText.Text = _allPages ? "ALL 3 PAGES · STATS REWIND" : $"PAGE {_page + 1} / 3 · {new[] { "OVERVIEW", "RHYTHM", "MILESTONES" }[_page]}";
        ShareContent.Children.Clear();
        if (_allPages) { for (var page = 0; page < 3; page++) AddPage(snapshot, page); } else AddPage(snapshot, _page);
        ThemeManager.ApplyTextScale(this, _settings);
    }

    private void AddPage(ProgressSnapshot snapshot, int page)
    {
        if (page > 0) ShareContent.Children.Add(new Separator());
        var title = page switch { 0 => IsPublic ? "FOCUS IN NUMBERS" : "FOCUS JOURNEY", 1 => "RHYTHM REPORT", _ => "MILESTONES & MOMENTUM" };
        ShareContent.Children.Add(Text("CLOCKIN", 13, "TextBrush", FontWeights.Black)); ShareContent.Children.Add(Text(title, 26, "TextBrush", FontWeights.Black)); ShareContent.Children.Add(Text(snapshot.Now.ToString("MMMM d, yyyy"), 10, "MutedBrush", FontWeights.SemiBold));
        if (page == 0)
        {
            if (IsPublic) { AddMetric("TIME INVESTED", DurationText.Compact(snapshot.TotalHours * 3600), true); AddMetric("EARNED", MoneyText.Money(_store.AllEarnings(snapshot.Now), _store.CurrencyCode), false); AddMiniRow(("SESSIONS", _store.Sessions.Count.ToString()), ("ACTIVE DAYS", snapshot.ActiveDays.ToString()), ("STREAK", $"{snapshot.CurrentStreak}d")); }
            else { ShareContent.Children.Add(Text("✦  FOCUS JOURNEY", 13, "AccentBrush", FontWeights.Black)); ShareContent.Children.Add(Text("Momentum, milestones and rhythm — ready to share.", 11, "MutedBrush", FontWeights.Normal)); AddMiniRow(("STREAK", $"{snapshot.CurrentStreak}d"), ("BADGES", snapshot.Badges.Count(badge => badge.Unlocked).ToString()), ("XP", snapshot.Xp.ToString("N0"))); }
        }
        else if (page == 1)
        {
            AddMetric("BEST DAY", snapshot.BestDay is { } date ? $"{date:MMM d} · {DurationText.Compact(snapshot.BestDayDuration)}" : "—", true); AddMetric("BEST STREAK", $"{snapshot.LongestStreak} days", false); AddMetric("ACTIVE DAYS", snapshot.ActiveDays.ToString(), false); AddMetric("BEST WEEKDAY", snapshot.BestWeekday, false); AddMetric("POWER HOUR", snapshot.BestStartHour, false); AddMetric("SESSIONS", _store.Sessions.Count.ToString(), false);
        }
        else
        {
            AddMetric("LEVEL", snapshot.Level.ToString(), true); AddMetric("TOTAL XP", snapshot.Xp.ToString("N0"), false); AddMetric("BADGES UNLOCKED", snapshot.Badges.Count(badge => badge.Unlocked).ToString(), false); AddMetric("GOAL DAYS", snapshot.GoalDays.ToString(), false); AddMetric("2× GOAL DAYS", snapshot.DoubleGoalDays.ToString(), false); AddMetric("MONTH GOALS", snapshot.MonthlyGoalDays.ToString(), false); AddMetric("MOMENTUM", $"{snapshot.Trend:+0%;-0%;0%}", false);
        }
        ShareContent.Children.Add(Text(IsPublic ? "CLOCKIN · STATS REWIND · PUBLIC" : "CLOCKIN · STATS REWIND · PRIVATE", 8, "MutedBrush", FontWeights.Bold));
    }

    private void AddMetric(string label, string value, bool accent) { var grid = new Grid { Margin = new Thickness(0, 13, 0, 0) }; grid.Children.Add(Text(label, 9, "MutedBrush", FontWeights.Bold)); var valueText = Text(value, accent ? 24 : 19, accent ? "AccentBrush" : "TextBrush", FontWeights.Black, TextAlignment.Right); valueText.HorizontalAlignment = System.Windows.HorizontalAlignment.Right; grid.Children.Add(valueText); ShareContent.Children.Add(grid); }
    private void AddMiniRow(params (string label, string value)[] values) { var grid = new UniformGrid { Columns = values.Length, Margin = new Thickness(0, 16, 0, 0) }; foreach (var (label, value) in values) { var stack = new StackPanel(); stack.Children.Add(Text(label, 8, "MutedBrush", FontWeights.Bold)); stack.Children.Add(Text(value, 15, "TextBrush", FontWeights.Black)); grid.Children.Add(stack); } ShareContent.Children.Add(grid); }
    private TextBlock Text(string value, double size, string color, FontWeight weight, TextAlignment alignment = TextAlignment.Left) => new() { Text = value, FontSize = size, Foreground = (SolidColorBrush)FindResource(color), FontWeight = weight, TextWrapping = TextWrapping.Wrap, TextAlignment = alignment, Margin = new Thickness(0, 3, 0, 3) };
    private Separator Separator() => new() { Margin = new Thickness(0, 20, 0, 5), Opacity = 0.25 };
    private string BuildText(ProgressSnapshot snapshot) => $"CLOCKIN STATS REWIND\n\n{(IsPublic ? "PUBLIC" : "PRIVATE")}\nLEVEL {snapshot.Level}\nTIME INVESTED {DurationText.Compact(snapshot.TotalHours * 3600)}\nEARNED {_store.AllEarnings(snapshot.Now):0.00} {_store.CurrencyCode}\nSESSIONS {_store.Sessions.Count}\nACTIVE DAYS {snapshot.ActiveDays}\nSTREAK {snapshot.CurrentStreak} days\nBADGES {snapshot.Badges.Count(badge => badge.Unlocked)}\nXP {snapshot.Xp:N0}\nBEST WEEKDAY {snapshot.BestWeekday}\nPOWER HOUR {snapshot.BestStartHour}";
    private void Copy_Click(object sender, RoutedEventArgs e) { System.Windows.Clipboard.SetText(_lastText); }
    private void Save_Click(object sender, RoutedEventArgs e) { var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Text files (*.txt)|*.txt", FileName = $"clockin-stats-{DateTime.Now:yyyyMMdd}.txt" }; if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, _lastText, Encoding.UTF8); }
}

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Clockin.Windows;

using WpfButton = System.Windows.Controls.Button;
using WpfProgressBar = System.Windows.Controls.ProgressBar;

public partial class ProgressWindow : Window
{
    private readonly ClockStore _store;
    private readonly SettingStore _settings;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _tab;

    public ProgressWindow(ClockStore store, SettingStore settings)
    {
        InitializeComponent();
        _store = store;
        _settings = settings;
        CustomChrome.Attach(this, "PROGRESS", settings);
        _timer.Tick += (_, _) => Render();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
        _store.Changed += (_, _) => Dispatcher.Invoke(Render);
        Loaded += (_, _) => Render();
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton button && int.TryParse(button.Tag?.ToString(), out var tab))
        {
            _tab = tab;
            Render();
        }
    }

    private void Render()
    {
        if (!IsLoaded && !IsInitialized) return;
        var snapshot = ProgressCalculator.Calculate(_store, _settings);
        UpdateTabButtons();
        ProgressContent.Children.Clear();
        switch (_tab)
        {
            case 1: RenderBadges(snapshot); break;
            case 2: RenderRecords(snapshot); break;
            case 3: RenderWeekly(snapshot); break;
            case 4: RenderReports(snapshot); break;
            default: RenderOverview(snapshot); break;
        }
        ThemeManager.ApplyTextScale(this, _settings);
    }

    private void UpdateTabButtons()
    {
        var buttons = new[] { OverviewTab, BadgesTab, RecordsTab, WeeklyTab, ReportsTab };
        for (var index = 0; index < buttons.Length; index++)
        {
            buttons[index].Background = index == _tab ? Brush("AccentBrush") : System.Windows.Media.Brushes.Transparent;
            buttons[index].Foreground = index == _tab ? Brush("ActionForegroundBrush") : Brush("MutedBrush");
        }
    }

    private void RenderOverview(ProgressSnapshot snapshot)
    {
        var level = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        level.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        level.ColumnDefinitions.Add(new ColumnDefinition());
        level.Children.Add(new TextBlock { Text = Avatar(snapshot.Level), FontSize = 48, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center });
        var levelInfo = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        levelInfo.Children.Add(Text($"LEVEL {snapshot.Level}", 24, "AccentBrush", FontWeights.Black));
        levelInfo.Children.Add(Text($"{snapshot.Xp:N0} XP  ·  {500 - snapshot.Xp % 500:N0} XP to next level", 10, "MutedBrush"));
        var progress = new WpfProgressBar { Height = 8, Maximum = 1, Value = snapshot.LevelProgress, Margin = new Thickness(0, 9, 0, 0), Foreground = Brush("AccentBrush"), Background = Brush("CardStrokeBrush") };
        levelInfo.Children.Add(progress);
        levelInfo.Children.Add(Text($"Base {snapshot.BaseXp:N0}  ·  Goal +{snapshot.GoalBonusXp:N0}  ·  Streak +{snapshot.StreakBonusXp:N0}", 9, "AccentBrush"));
        Grid.SetColumn(levelInfo, 1); level.Children.Add(levelInfo);
        ProgressContent.Children.Add(Card(level));

        var stats = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 10) };
        stats.Children.Add(Stat("🔥", "STREAK", $"{snapshot.CurrentStreak} days"));
        stats.Children.Add(Stat("⏱", "TOTAL", DurationText.Compact(snapshot.TotalHours * 3600)));
        stats.Children.Add(Stat("⚡", "XP RATE", "100 / hour + bonus"));
        ProgressContent.Children.Add(Card(stats, 12));

        var bonus = new StackPanel();
        bonus.Children.Add(Text("BONUS ENGINE", 9, "MutedBrush", FontWeights.Bold));
        bonus.Children.Add(Text(snapshot.GoalBonusXp == 0 && snapshot.MonthlyGoalDays == 0 ? "Set daily or monthly goals to earn bonus XP." : $"+{snapshot.GoalBonusXp:N0} XP from goals  ·  {snapshot.GoalDays} daily  ·  {snapshot.DoubleGoalDays} double-goal  ·  {snapshot.MonthlyGoalDays} monthly", 10, snapshot.GoalBonusXp == 0 ? "MutedBrush" : "AccentBrush"));
        bonus.Children.Add(Text("A completed day gives +100 XP; a 2× goal day gives an additional +250 XP.", 8, "MutedBrush"));
        ProgressContent.Children.Add(Card(bonus));

        var eta = new StackPanel();
        eta.Children.Add(Text("TARGET ETA", 9, "MutedBrush", FontWeights.Bold));
        var dailyGoal = _settings.GetDouble("GoalDailyHours");
        var today = _store.DurationOn(snapshot.Now) / 3600d;
        if (dailyGoal <= 0 && _settings.GetDouble("GoalMonthlyHours") <= 0) eta.Children.Add(Text("Set a daily or monthly goal in Settings.", 11, "MutedBrush"));
        else if (dailyGoal > 0 && today >= dailyGoal) eta.Children.Add(Text("Daily goal reached 🎉", 11, "AccentBrush", FontWeights.SemiBold));
        else if (_store.Running is { IsPaused: false } && dailyGoal > 0)
        {
            var remaining = dailyGoal - today;
            eta.Children.Add(Text($"At the current pace, today’s goal lands around {snapshot.Now.AddHours(remaining):HH:mm}.", 11, "TextBrush"));
        }
        else if (dailyGoal > 0 && snapshot.AverageDay > 0) eta.Children.Add(Text($"At your average, today’s goal is about {Math.Ceiling((dailyGoal - today) / snapshot.AverageDay):0} day(s) away.", 11, "TextBrush"));
        else eta.Children.Add(Text("Start working to generate a live finish estimate.", 11, "MutedBrush"));
        ProgressContent.Children.Add(Card(eta));
    }

    private void RenderBadges(ProgressSnapshot snapshot)
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(Text($"{snapshot.Badges.Count(badge => badge.Unlocked)} / {snapshot.Badges.Count} BADGES UNLOCKED", 10, "MutedBrush", FontWeights.Bold));
        ProgressContent.Children.Add(header);
        var grid = new UniformGrid { Columns = 2 };
        foreach (var badge in snapshot.Badges)
        {
            var content = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
            content.Children.Add(new TextBlock { Text = badge.Unlocked ? badge.Icon : "🔒", FontSize = 25, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Opacity = badge.Unlocked ? 1 : 0.6 });
            content.Children.Add(Text(badge.Title, 10, badge.Unlocked ? "TextBrush" : "MutedBrush", FontWeights.Bold, TextAlignment.Center));
            content.Children.Add(Text(badge.Unlocked ? $"Unlocked · {badge.Requirement}" : badge.Requirement, 8, badge.Unlocked ? "AccentBrush" : "MutedBrush", TextAlignment: TextAlignment.Center));
            content.Children.Add(Text($"Current: {badge.Progress}", 8, "MutedBrush", TextAlignment: TextAlignment.Center));
            var button = new WpfButton { Content = content, Tag = badge, Margin = new Thickness(4), Padding = new Thickness(9), Background = Brush("CardBrush"), BorderBrush = Brush("CardStrokeBrush"), BorderThickness = new Thickness(1), Opacity = badge.Unlocked ? 1 : 0.68 };
            button.Click += Badge_Click;
            grid.Children.Add(button);
        }
        ProgressContent.Children.Add(grid);
    }

    private void RenderRecords(ProgressSnapshot snapshot)
    {
        ProgressContent.Children.Add(Record("🏆", "Longest session", DurationText.Compact(snapshot.LongestSession)));
        ProgressContent.Children.Add(Record("🗓", "Best day", snapshot.BestDay is { } day ? $"{day:MMM d} · {DurationText.Compact(snapshot.BestDayDuration)}" : "—"));
        ProgressContent.Children.Add(Record("🔥", "Current streak", $"{snapshot.CurrentStreak} days"));
        ProgressContent.Children.Add(Record("🔥", "Longest streak", $"{snapshot.LongestStreak} days"));
        ProgressContent.Children.Add(Record("🎯", "Goal days", $"{snapshot.GoalDays}"));
        ProgressContent.Children.Add(Record("⭐", "Total XP", $"{snapshot.Xp:N0} XP"));
        ProgressContent.Children.Add(Record("💰", "All-time earnings", MoneyText.Money(_store.AllEarnings(snapshot.Now), _store.CurrencyCode)));
    }

    private void RenderWeekly(ProgressSnapshot snapshot)
    {
        var start = snapshot.Now.Date.AddDays(-6);
        var current = snapshot.DailyDurations.Where(pair => pair.Key >= start).Sum(pair => pair.Value);
        var previousStart = snapshot.Now.Date.AddDays(-13);
        var previous = _store.Sessions.Where(session => session.Start.Date >= previousStart && session.Start.Date < start).Sum(session => session.Duration);
        var delta = previous > 0 ? (current - previous) / previous : current > 0 ? 1 : 0;
        ProgressContent.Children.Add(Record("📈", "THIS WEEK", DurationText.Compact(current)));
        ProgressContent.Children.Add(Record("↩", "LAST WEEK", DurationText.Compact(previous)));
        ProgressContent.Children.Add(Record("↗", "CHANGE", $"{delta:+0%;-0%;0%}"));
        var bars = new UniformGrid { Columns = 7, Margin = new Thickness(0, 8, 0, 0) };
        var max = Math.Max(1, Enumerable.Range(0, 7).Select(offset => snapshot.DailyDurations.GetValueOrDefault(start.AddDays(offset))).Max());
        for (var offset = 0; offset < 7; offset++)
        {
            var value = snapshot.DailyDurations.GetValueOrDefault(start.AddDays(offset));
            var column = new StackPanel { VerticalAlignment = System.Windows.VerticalAlignment.Bottom, Margin = new Thickness(3) };
            column.Children.Add(Text(DurationText.Compact(value), 8, "MutedBrush", TextAlignment: TextAlignment.Center));
            column.Children.Add(new Border { Height = Math.Max(4, 70 * value / max), Background = Brush("AccentBrush"), CornerRadius = new CornerRadius(4), Margin = new Thickness(5, 5, 5, 3) });
            column.Children.Add(Text(start.AddDays(offset).ToString("ddd", CultureInfo.CurrentCulture), 8, "MutedBrush", TextAlignment: TextAlignment.Center));
            bars.Children.Add(column);
        }
        ProgressContent.Children.Add(Card(bars));
        ProgressContent.Children.Add(Card(Text("Keep the streak alive and beat your previous week.", 11, "MutedBrush")));
    }

    private void RenderReports(ProgressSnapshot snapshot)
    {
        ProgressContent.Children.Add(ReportMetric("ACTIVE DAYS", snapshot.ActiveDays.ToString()));
        ProgressContent.Children.Add(ReportMetric("AVERAGE SESSION", DurationText.Compact(snapshot.AverageSession)));
        ProgressContent.Children.Add(ReportMetric("BEST WEEKDAY", snapshot.BestWeekday));
        ProgressContent.Children.Add(ReportMetric("BEST START HOUR", snapshot.BestStartHour));
        var hourly = snapshot.TotalHours > 0 ? _store.AllEarnings(snapshot.Now) / snapshot.TotalHours : 0;
        ProgressContent.Children.Add(ReportMetric("AVERAGE EARNINGS / HOUR", MoneyText.Money(hourly, _store.CurrencyCode)));
        ProgressContent.Children.Add(ReportMetric("LAST 30D TREND", $"{snapshot.Trend:+0%;-0%;0%}"));
        ProgressContent.Children.Add(Card(Text("Reports are calculated from completed sessions and update after each clock-out.", 9, "MutedBrush")));
    }

    private void Badge_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: ProgressBadgeInfo badge })
            ClockinDialog.Alert(this, badge.Title, $"{badge.Requirement}\n\nCurrent: {badge.Progress}\n\nStatus: {(badge.Unlocked ? "UNLOCKED" : "LOCKED")}");
    }

    private void ShareStats_Click(object sender, RoutedEventArgs e) => new ShareStatsWindow(_store, _settings) { Owner = this }.ShowDialog();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private Border Card(UIElement content, double padding = 15) => new() { Child = content, Background = Brush("CardBrush"), BorderBrush = Brush("CardStrokeBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), Padding = new Thickness(padding), Margin = new Thickness(0, 0, 0, 10) };
    private TextBlock Text(string value, double size, string color = "TextBrush", FontWeight? weight = null, TextAlignment TextAlignment = TextAlignment.Left)
    {
        return new TextBlock { Text = value, FontSize = size, Foreground = Brush(color), FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment, Margin = new Thickness(0, 2, 0, 2) };
    }
    private Border Record(string icon, string title, string value)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = icon, FontSize = 19, Foreground = Brush("AccentBrush"), VerticalAlignment = VerticalAlignment.Center });
        var titleBlock = Text(title, 11, "TextBrush", FontWeights.SemiBold); Grid.SetColumn(titleBlock, 1); grid.Children.Add(titleBlock);
        var valueBlock = Text(value, 11, "AccentBrush", FontWeights.Bold, TextAlignment.Right); Grid.SetColumn(valueBlock, 2); grid.Children.Add(valueBlock);
        return Card(grid, 12);
    }
    private Border ReportMetric(string title, string value)
    {
        var grid = new Grid(); var label = Text(title, 9, "MutedBrush", FontWeights.Bold); grid.Children.Add(label); var metric = Text(value, 16, "TextBrush", FontWeights.Bold, TextAlignment.Right); metric.HorizontalAlignment = System.Windows.HorizontalAlignment.Right; grid.Children.Add(metric); return Card(grid, 14);
    }
    private Border Stat(string icon, string title, string value)
    {
        var stack = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Center }; stack.Children.Add(new TextBlock { Text = icon, FontSize = 17, HorizontalAlignment = System.Windows.HorizontalAlignment.Center }); stack.Children.Add(Text(title, 8, "MutedBrush", FontWeights.Bold, TextAlignment.Center)); stack.Children.Add(Text(value, 11, "TextBrush", FontWeights.SemiBold, TextAlignment.Center)); return new Border { Child = stack, Padding = new Thickness(5) };
    }
    private SolidColorBrush Brush(string key) => (SolidColorBrush)FindResource(key);
    private static string Avatar(int level) => new[] { "🌱", "⚡", "🚀", "🪐", "👑" }[Math.Min((level - 1) / 3, 4)];
}

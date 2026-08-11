using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Clockin.Windows;

public partial class PinnedWindow : Window
{
    private readonly ClockStore _store;
    private readonly ExchangeRateStore _rates;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string _mode = "";

    public PinnedWindow(ClockStore store, ExchangeRateStore rates)
    {
        InitializeComponent();
        _store = store;
        _rates = rates;
        ThemeManager.Apply(this, SettingStore.Shared);
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Loaded += (_, _) => { if (Left == 0 && Top == 0) { Left = SystemParameters.WorkArea.Right - Width - 25; Top = SystemParameters.WorkArea.Top + 35; } Refresh(); };
    }

    private void Refresh()
    {
        var now = DateTime.Now;
        var mode = SettingStore.Shared.Get("PinnedMode", "Money");
        ApplyMode(mode);
        var theme = ThemePalette.For(SettingStore.Shared.Get("Theme", "Carbon"));
        var active = _store.Running?.IsPaused == false;
        var value = _store.CurrentEarnings(now);
        var tryText = _store.CurrencyCode == "USD" && _rates.LatestRate is { } rate ? $"≈ {MoneyText.Money(value * rate, "TRY")}" : "";
        var status = _store.Running is null ? "READY" : active ? "MONEY IS MOVING" : "PAUSED";
        var statusColor = new SolidColorBrush(theme.Color(active ? theme.Accent : _store.Running is null ? theme.Muted : "#FF9F43"));

        PinnedStatus.Text = status; PinnedElapsed.Text = DurationText.Clock(_store.Elapsed(now)); PinnedMoney.Text = MoneyText.Money(value, _store.CurrencyCode); PinnedTry.Text = tryText;
        PinnedMomentum.Text = mode == "Goal" ? $"TODAY {DurationText.Compact(_store.DurationOn(now))}  ·  MONTH {DurationText.Compact(_store.MonthDuration(now))}" : $"+{MoneyText.Money(active ? _store.HourlyRate / 3600 : 0, _store.CurrencyCode, 4)}/sec";
        PinnedMoney.Foreground = new SolidColorBrush(theme.Color(theme.Accent)); PinnedMomentum.Foreground = new SolidColorBrush(theme.Color(theme.Accent)); PinnedStatus.Foreground = new SolidColorBrush(theme.Color(theme.Muted)); Dot.Fill = statusColor;

        CompactStatus.Text = status; CompactElapsed.Text = DurationText.Clock(_store.Elapsed(now)); CompactMoney.Text = MoneyText.Money(value, _store.CurrencyCode); CompactTry.Text = tryText; CompactDot.Fill = statusColor;
        GoalElapsed.Text = DurationText.Clock(_store.Elapsed(now));
        var dailyGoal = SettingStore.Shared.GetDouble("GoalDailyHours"); var monthGoal = SettingStore.Shared.GetDouble("GoalMonthlyHours");
        SetGoal(GoalTodayText, GoalTodayProgress, "TODAY", _store.DurationOn(now) / 3600d, dailyGoal, theme.Accent);
        SetGoal(GoalMonthText, GoalMonthProgress, "MONTH", _store.MonthDuration(now) / 3600d, monthGoal, theme.Secondary);

        AllStatus.Text = status; AllElapsed.Text = DurationText.Clock(_store.Elapsed(now)); AllMoney.Text = MoneyText.Money(value, _store.CurrencyCode); AllTry.Text = tryText; AllPerSecond.Text = $"+{MoneyText.Money(active ? _store.HourlyRate / 3600 : 0, _store.CurrencyCode, 4)}/sec"; AllDot.Fill = statusColor;
        var averages = AllTimeAverages(now); AllAvgDay.Text = DurationText.Hours(averages.day); AllAvgWeek.Text = DurationText.Hours(averages.week); AllAvgMonth.Text = DurationText.Hours(averages.month);
        AllGoalsPanel.Children.Clear();
        if (dailyGoal > 0) AllGoalsPanel.Children.Add(GoalRow("DAY", _store.DurationOn(now) / 3600d, dailyGoal, theme.Accent));
        if (monthGoal > 0) AllGoalsPanel.Children.Add(GoalRow("MONTH", _store.MonthDuration(now) / 3600d, monthGoal, theme.Secondary));
        AllRadioIcon.Foreground = new SolidColorBrush(theme.Color(RadioPlayer.Shared.IsPlaying ? theme.Accent : theme.Muted)); AllRadioText.Text = RadioPlayer.Shared.IsPlaying ? "FOCUS RADIO ON" : "FOCUS RADIO OFF"; AllRadioButton.Content = RadioPlayer.Shared.IsPlaying ? "■" : "▶"; AllRadioVolume.Value = RadioPlayer.Shared.Volume;
    }

    private void ApplyMode(string mode)
    {
        if (mode == _mode) return;
        _mode = mode;
        CompactView.Visibility = mode == "Compact" ? Visibility.Visible : Visibility.Collapsed;
        MoneyView.Visibility = mode == "Money" ? Visibility.Visible : Visibility.Collapsed;
        GoalView.Visibility = mode == "Goal" ? Visibility.Visible : Visibility.Collapsed;
        AllView.Visibility = mode == "All" ? Visibility.Visible : Visibility.Collapsed;
        var size = mode switch { "Compact" => (246d, 72d), "Goal" => (300d, 150d), "All" => (370d, 230d), _ => (320d, 112d) };
        Width = size.Item1; Height = size.Item2;
        if (Left == 0 && Top == 0) { Left = SystemParameters.WorkArea.Right - Width - 25; Top = SystemParameters.WorkArea.Top + 35; }
    }

    private void SetGoal(TextBlock label, System.Windows.Controls.ProgressBar bar, string name, double value, double goal, string color)
    {
        label.Text = goal > 0 ? $"{DurationText.Hours(value)} / {DurationText.Hours(goal)}" : "Set in Settings";
        bar.Value = goal > 0 ? Math.Min(1, Math.Max(0, value / goal)) : 0;
        bar.Foreground = new SolidColorBrush(ThemePalette.For(SettingStore.Shared.Get("Theme", "Carbon")).Color(color));
    }

    private Border GoalRow(string name, double value, double goal, string color)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };
        var row = new DockPanel(); row.Children.Add(new TextBlock { Text = name, FontSize = 8, Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush") }); var valueText = new TextBlock { Text = $"{DurationText.Hours(value)} / {DurationText.Hours(goal)}", FontSize = 8, HorizontalAlignment = System.Windows.HorizontalAlignment.Right }; DockPanel.SetDock(valueText, Dock.Right); row.Children.Add(valueText); stack.Children.Add(row);
        var bar = new System.Windows.Controls.ProgressBar { Height = 5, Maximum = 1, Value = Math.Min(1, Math.Max(0, value / goal)), Foreground = new SolidColorBrush(ThemePalette.For(SettingStore.Shared.Get("Theme", "Carbon")).Color(color)), Background = (System.Windows.Media.Brush)FindResource("CardStrokeBrush"), Margin = new Thickness(0, 3, 0, 0) }; stack.Children.Add(bar);
        return new Border { Child = stack, Margin = new Thickness(0, 0, 0, 4) };
    }

    private (double day, double week, double month) AllTimeAverages(DateTime now)
    {
        var earliest = _store.Sessions.Select(session => session.Start).Concat(_store.Running is { } running ? [running.Start] : []).DefaultIfEmpty(now).Min();
        var days = Math.Max(1, (now.Date - earliest.Date).Days + 1);
        var daily = _store.AllDuration(now) / 3600d / days;
        return (daily, daily * 7, daily * 30.44);
    }

    private void Radio_Click(object sender, RoutedEventArgs e)
    {
        if (RadioPlayer.Shared.IsPlaying) RadioPlayer.Shared.Stop(); else RadioPlayer.Shared.Play();
        Refresh();
    }

    private void RadioVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (IsLoaded) RadioPlayer.Shared.Volume = AllRadioVolume.Value; }
    private void DragWindow(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}

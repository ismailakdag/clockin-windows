using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        SettingStore.Shared.Changed += SettingsChanged;
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Closed += (_, _) =>
        {
            SettingStore.Shared.Changed -= SettingsChanged;
            _timer.Stop();
        };
        Loaded += (_, _) => { if (double.IsNaN(Left) || double.IsNaN(Top) || (Left == 0 && Top == 0)) { Left = SystemParameters.WorkArea.Right - Width - 25; Top = SystemParameters.WorkArea.Top + 35; } ClampToWorkArea(); Refresh(); };
    }

    private void SettingsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => SettingsChanged(sender, e)); return; }
        ThemeManager.Apply(this, SettingStore.Shared);
        Refresh();
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
        var statusColor = new SolidColorBrush(theme.Color(active ? theme.Success : _store.Running is null ? theme.Muted : theme.Warning));

        PinnedStatus.Text = status; PinnedElapsed.Text = DurationText.Clock(_store.Elapsed(now)); PinnedMoney.Text = MoneyText.Money(value, _store.CurrencyCode); PinnedTry.Text = tryText;
        PinnedMomentum.Text = mode == "Goal" ? $"TODAY {DurationText.Compact(_store.DurationOn(now))}  ·  MONTH {DurationText.Compact(_store.MonthDuration(now))}" : $"+{MoneyText.Money(active ? _store.HourlyRate / 3600 : 0, _store.CurrencyCode, 4)}/sec";
        PinnedMoney.Foreground = new SolidColorBrush(theme.Color(theme.Accent)); PinnedMomentum.Foreground = new SolidColorBrush(theme.Color(theme.Accent)); PinnedStatus.Foreground = statusColor; Dot.Fill = statusColor;

        CompactStatus.Text = status; CompactStatus.Foreground = statusColor; CompactElapsed.Text = DurationText.Clock(_store.Elapsed(now)); CompactMoney.Text = MoneyText.Money(value, _store.CurrencyCode); CompactTry.Text = tryText; CompactDot.Fill = statusColor;
        GoalElapsed.Text = DurationText.Clock(_store.Elapsed(now));
        var dailyGoal = SettingStore.Shared.GetDouble("GoalDailyHours"); var monthGoal = SettingStore.Shared.GetDouble("GoalMonthlyHours");
        SetGoal(GoalTodayText, GoalTodayProgress, "TODAY", _store.DurationOn(now) / 3600d, dailyGoal, theme.Accent);
        SetGoal(GoalMonthText, GoalMonthProgress, "MONTH", _store.MonthDuration(now) / 3600d, monthGoal, theme.Secondary);

        AllStatus.Text = status; AllStatus.Foreground = statusColor; AllElapsed.Text = DurationText.Clock(_store.Elapsed(now)); AllMoney.Text = MoneyText.Money(value, _store.CurrencyCode); AllTry.Text = tryText; AllPerSecond.Text = $"+{MoneyText.Money(active ? _store.HourlyRate / 3600 : 0, _store.CurrencyCode, 4)}/sec"; AllDot.Fill = statusColor;
        var averages = AllTimeAverages(now); AllAvgDay.Text = DurationText.Hours(averages.day); AllAvgWeek.Text = DurationText.Hours(averages.week); AllAvgMonth.Text = DurationText.Hours(averages.month);
        AllGoalsPanel.Children.Clear();
        if (dailyGoal > 0) AllGoalsPanel.Children.Add(GoalRow("DAY", _store.DurationOn(now) / 3600d, dailyGoal, theme.Accent));
        if (monthGoal > 0) AllGoalsPanel.Children.Add(GoalRow("MONTH", _store.MonthDuration(now) / 3600d, monthGoal, theme.Secondary));
        AllRadioIcon.Foreground = new SolidColorBrush(theme.Color(RadioPlayer.Shared.IsPlaying ? theme.Accent : theme.Muted)); AllRadioText.Text = RadioPlayer.Shared.IsPlaying ? "FOCUS RADIO ON" : "FOCUS RADIO OFF"; AllRadioButton.Content = RadioPlayer.Shared.IsPlaying ? "■" : "▶"; AllRadioVolume.Value = RadioPlayer.Shared.Volume;
        ApplyTextScale();
    }

    private void ApplyMode(string mode)
    {
        if (mode == _mode) return;
        var oldWidth = ActualWidth > 0 ? ActualWidth : Width;
        var oldHeight = ActualHeight > 0 ? ActualHeight : Height;
        var oldRight = !double.IsNaN(Left) ? Left + oldWidth : double.NaN;
        var oldBottom = !double.IsNaN(Top) ? Top + oldHeight : double.NaN;
        _mode = mode;
        CompactView.Visibility = mode == "Compact" ? Visibility.Visible : Visibility.Collapsed;
        MoneyView.Visibility = mode == "Money" ? Visibility.Visible : Visibility.Collapsed;
        GoalView.Visibility = mode == "Goal" ? Visibility.Visible : Visibility.Collapsed;
        AllView.Visibility = mode == "All" ? Visibility.Visible : Visibility.Collapsed;
        var allHeight = (SettingStore.Shared.GetDouble("GoalDailyHours") > 0 || SettingStore.Shared.GetDouble("GoalMonthlyHours") > 0 ? 230d : 190d) * FontScale;
        var defaults = mode switch { "Compact" => (246d, 72d), "Goal" => (300d, 116d), "All" => (370d, allHeight), _ => (320d, 112d) };
        Width = Math.Clamp(SettingStore.Shared.GetDouble($"PinnedWidth.{mode}", defaults.Item1), MinWidth, MaxWidth);
        Height = Math.Clamp(SettingStore.Shared.GetDouble($"PinnedHeight.{mode}", defaults.Item2), MinHeight, MaxHeight);
        if (IsLoaded && !double.IsNaN(oldRight) && !double.IsNaN(oldBottom))
        {
            Left = oldRight - Width;
            Top = oldBottom - Height;
            ClampToWorkArea();
        }
    }

    private double FontScale => ThemeManager.PinnedTextScale(SettingStore.Shared);

    private void ApplyTextScale()
    {
        ThemeManager.ApplyTextScale(this, FontScale);
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb { Tag: string edges }) return;
        var width = Width;
        var height = Height;
        if (edges.Contains('R')) width = Math.Clamp(width + e.HorizontalChange, MinWidth, MaxWidth);
        if (edges.Contains('B')) height = Math.Clamp(height + e.VerticalChange, MinHeight, MaxHeight);
        if (edges.Contains('L'))
        {
            var next = Math.Clamp(width - e.HorizontalChange, MinWidth, MaxWidth);
            Left += width - next;
            width = next;
        }
        if (edges.Contains('T'))
        {
            var next = Math.Clamp(height - e.VerticalChange, MinHeight, MaxHeight);
            Top += height - next;
            height = next;
        }
        Width = width;
        Height = height;
        ClampToWorkArea();
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e) => SaveCurrentSize();

    private void SaveCurrentSize()
    {
        if (string.IsNullOrWhiteSpace(_mode) || Width <= 0 || Height <= 0) return;
        SettingStore.Shared.Set($"PinnedWidth.{_mode}", Width);
        SettingStore.Shared.Set($"PinnedHeight.{_mode}", Height);
    }

    private void ClampToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        if (!double.IsNaN(Left)) Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
        if (!double.IsNaN(Top)) Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
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
    private void DragWindow(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is Thumb) return; if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}

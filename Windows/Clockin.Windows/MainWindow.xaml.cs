using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Clockin.Windows;

using WpfButton = System.Windows.Controls.Button;

public partial class MainWindow : Window
{
    private readonly ClockStore _store;
    private readonly ExchangeRateStore _rates;
    private readonly App _app;
    private readonly SettingStore _settings = SettingStore.Shared;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _mascotTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly DispatcherTimer _effectTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    private int _mascotFrame;
    private string _lastMascotMode = "";
    private string _lastMascotMessage = "";
    private bool _allowClose;
    private WpfButton? _clearLatestButton;

    public MainWindow(ClockStore store, ExchangeRateStore rates, App app)
    {
        InitializeComponent(); _store = store; _rates = rates; _app = app;
        InstallBottomActions();
        MascotImage.Width = 88; MascotImage.Height = 88; MascotImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5); MascotImage.RenderTransform = new ScaleTransform(1, 1);
        Opacity = 0; Loaded += (_, _) => BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        ApplyTheme();
        _timer.Tick += (_, _) => RefreshFromStore(); _timer.Start();
        _mascotTimer.Tick += (_, _) => AdvanceMascot(); _mascotTimer.Start();
        _effectTimer.Tick += (_, _) => ShowMascotEffect(); _effectTimer.Start();
        _store.Changed += (_, _) => Dispatcher.Invoke(RefreshFromStore);
        _store.StatusChanged += (_, message) => Dispatcher.Invoke(() => RateStatus.Text = message);
        _rates.Changed += (_, _) => Dispatcher.Invoke(RefreshFromStore);
        RefreshFromStore();
    }

    private ThemePalette Theme => ThemePalette.For(_settings.Get("Theme", "Carbon"));

    private void ApplyTheme()
    {
        var theme = Theme;
        Resources["WindowBackgroundBrush"] = new SolidColorBrush(theme.Color(theme.Background));
        Resources["CardBrush"] = new SolidColorBrush(theme.Color(theme.Card));
        Resources["CardStrokeBrush"] = new SolidColorBrush(theme.Color(theme.Stroke));
        Resources["AccentBrush"] = new SolidColorBrush(theme.Color(theme.Accent));
        Resources["SecondaryAccentBrush"] = new SolidColorBrush(theme.Color(theme.Secondary));
        Resources["TextBrush"] = new SolidColorBrush(theme.Color(theme.Text));
        Resources["MutedBrush"] = new SolidColorBrush(theme.Color(theme.Muted));
        Resources["ActionForegroundBrush"] = new SolidColorBrush(theme.Color(theme.ActionForeground));
        Resources["SuccessBrush"] = new SolidColorBrush(theme.Color(theme.Success));
        Resources["WarningBrush"] = new SolidColorBrush(theme.Color(theme.Warning));
        Resources["DangerBrush"] = new SolidColorBrush(theme.Color(theme.Danger));
        FontFamily = new System.Windows.Media.FontFamily(theme.FontFamily);
        FontSize = 12 * ThemeManager.TextScale(_settings);
        ApplyTextScale();
        ClockInButton.SetResourceReference(WpfButton.BackgroundProperty, "SuccessBrush");
        ClockInButton.SetResourceReference(WpfButton.ForegroundProperty, "ActionForegroundBrush");
        ClockOutButton.SetResourceReference(WpfButton.BackgroundProperty, "DangerBrush");
        ClockOutButton.SetResourceReference(WpfButton.ForegroundProperty, "ActionForegroundBrush");
        ClockOutButton.SetResourceReference(WpfButton.BorderBrushProperty, "DangerBrush");
    }

    private void ApplyTextScale()
    {
        var scale = ThemeManager.TextScale(_settings);
        StatusText.FontSize = 10 * scale; ElapsedText.FontSize = 48 * scale; EarningsText.FontSize = 18 * scale; TryText.FontSize = 11 * scale;
        MomentumIcon.FontSize = 15 * scale; MomentumTitle.FontSize = 8 * scale; MomentumText.FontSize = 10 * scale; MilestoneText.FontSize = 8 * scale; MilestoneRemaining.FontSize = 9 * scale;
        ClockInButton.FontSize = 13 * scale; PauseButton.FontSize = 10 * scale; MascotMessage.FontSize = 11 * scale;
        TodayHours.FontSize = 14 * scale; TodayEarned.FontSize = 13 * scale; TodayTry.FontSize = 9 * scale; AllHours.FontSize = 14 * scale; AllEarned.FontSize = 9 * scale;
        GoalHint.FontSize = 9 * scale; GoalText.FontSize = 10 * scale; RateText.FontSize = 13 * scale; RateStatus.FontSize = 9 * scale; RateDate.FontSize = 9 * scale; FooterText.FontSize = 9 * scale; ProgressButton.FontSize = 13 * scale;
        if (_clearLatestButton is not null) _clearLatestButton.FontSize = 10 * scale;
    }

    public void RefreshFromStore()
    {
        ApplyTheme();
        var now = DateTime.Now; var running = _store.Running; var current = _store.CurrentEarnings(now); var theme = Theme;
        ElapsedText.Text = DurationText.Clock(_store.Elapsed(now));
        EarningsText.Text = MoneyText.Money(current, _store.CurrencyCode);
        TryText.Text = _store.CurrencyCode == "USD" && _rates.LatestRate is { } rate ? $"≈ {MoneyText.Money(current * rate, "TRY")}" : "";
        StatusText.Text = running is null ? "READY TO FOCUS" : running.IsPaused ? "PAUSED" : "FOCUS SESSION";
        var statusBrush = new SolidColorBrush(theme.Color(running is null ? theme.Muted : running.IsPaused ? theme.Warning : theme.Success));
        StatusDot.Fill = statusBrush;
        StatusText.Foreground = statusBrush;
        IdleControls.Visibility = running is null ? Visibility.Visible : Visibility.Collapsed;
        RunningControls.Visibility = running is null ? Visibility.Collapsed : Visibility.Visible;
        PauseButton.Content = running?.IsPaused == true ? "▶  Resume" : "Ⅱ  Pause";
        PauseButton.SetResourceReference(WpfButton.BackgroundProperty, running?.IsPaused == true ? "SuccessBrush" : "WarningBrush");
        PauseButton.SetResourceReference(WpfButton.ForegroundProperty, "ActionForegroundBrush");
        MomentumTitle.Text = running is not null && !running.IsPaused ? "MONEY MOMENTUM" : "YOUR EARNING POWER";
        MomentumIcon.Text = running is not null && !running.IsPaused ? "🔥" : "✦";
        MomentumText.Text = $"+{MoneyText.Money(_store.HourlyRate / 3600, _store.CurrencyCode, 4)}/sec";
        var milestone = Math.Max(10, Math.Ceiling(Math.Max(current, 0.01) / 10) * 10); var milestoneProgress = current % 10 / 10;
        MilestoneText.Text = running is null ? "NEXT $10.00" : $"NEXT {MoneyText.Money(milestone, _store.CurrencyCode)}";
        MilestoneRemaining.Text = running is null ? "Clock in to build momentum" : $"{MoneyText.Money(Math.Max(0, milestone - current), _store.CurrencyCode)} to go";
        MomentumProgress.Value = running is null ? 0 : milestoneProgress;
        TodayHours.Text = DurationText.Compact(_store.DurationOn(now)); TodayEarned.Text = MoneyText.Money(_store.EarningsOn(now), _store.CurrencyCode);
        TodayTry.Text = _store.CurrencyCode == "USD" && _rates.LatestRate is { } todayRate ? $"≈ {MoneyText.Money(_store.EarningsOn(now) * todayRate, "TRY")}" : "";
        AllHours.Text = DurationText.Compact(_store.AllDuration(now)); AllEarned.Text = MoneyText.Money(_store.AllEarnings(now), _store.CurrencyCode);
        var dailyGoal = _settings.GetDouble("GoalDailyHours"); var monthGoal = _settings.GetDouble("GoalMonthlyHours"); var daily = _store.DurationOn(now) / 3600; var monthly = _store.MonthDuration(now) / 3600;
        GoalHint.Text = dailyGoal > 0 || monthGoal > 0 ? "BONUS XP ACTIVE" : "Set in Settings";
        GoalText.Text = dailyGoal > 0 || monthGoal > 0 ? $"Today {DurationText.Hours(daily)} / {DurationText.Hours(dailyGoal)}  ·  Month {DurationText.Hours(monthly)} / {DurationText.Hours(monthGoal)}" : "Set daily or monthly goals to earn bonus XP.";
        DailyGoalProgress.Maximum = dailyGoal > 0 ? dailyGoal : 1; DailyGoalProgress.Value = dailyGoal > 0 ? Math.Min(dailyGoal, daily) : 0;
        RateText.Text = _rates.LatestRate is { } latest ? $"1 USD = {latest:0.###} TRY" : (_rates.IsLoading ? "Fetching live rate…" : "RATE UNAVAILABLE");
        RateDate.Text = _rates.LatestDate ?? ""; RateStatus.Text = _rates.ErrorMessage ?? (_rates.LastSuccessfulCheck is { } checkedAt ? $"API OK · {checkedAt:t}" : "Cached rate");
        var progress = CalculateProgress(now); ProgressButton.Content = $"LV {progress.level}"; FooterText.Text = $"ALL  {DurationText.Compact(_store.AllDuration(now))}  •  {_store.AllEarnings(now):0.00} {_store.CurrencyCode}";
        if (_clearLatestButton is not null) _clearLatestButton.IsEnabled = _store.Sessions.Count > 0;
        var message = running?.IsPaused == true ? "Taking a reset break" : running is null ? "Ready when you are" : "You are doing great — keep going!";
        var mascotEnabled = _settings.GetBool("MascotEnabled", true); MascotImage.Visibility = mascotEnabled ? Visibility.Visible : Visibility.Collapsed; MascotEffectText.Visibility = mascotEnabled ? Visibility.Visible : Visibility.Collapsed; SetMascotMessage(mascotEnabled ? message : "Mascot is disabled in Settings");
        RecentPanel.Children.Clear();
        foreach (var session in _store.Sessions.Take(4)) AddSessionRow(session);
        if (mascotEnabled) UpdateMascotMode();
    }

    private void AddSessionRow(WorkSession session)
    {
        var theme = Theme;
        var scale = ThemeManager.TextScale(_settings);
        var row = new Border { Background = new SolidColorBrush(theme.Color(theme.Card)), BorderBrush = new SolidColorBrush(theme.Color(theme.Stroke)), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(10, 9, 10, 9) };
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var sourceIcon = new Border { Background = new SolidColorBrush(theme.Color(theme.Background)), CornerRadius = new CornerRadius(8), Width = 27, Height = 27, Child = new TextBlock { Text = session.Source == "Clockin" ? "ϟ" : "↓", FontSize = 15, Foreground = new SolidColorBrush(theme.Color(session.Source == "Clockin" ? theme.Accent : theme.Secondary)), HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center } };
        var info = new StackPanel { Margin = new Thickness(10, 0, 0, 0) }; info.Children.Add(new TextBlock { Text = session.Start.ToString("MMM d · HH:mm"), FontSize = 12 * scale, Foreground = new SolidColorBrush(theme.Color(theme.Text)) }); info.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(session.Note) ? session.Source : session.Note, FontSize = 9 * scale, Foreground = new SolidColorBrush(theme.Color(theme.Muted)), TextTrimming = TextTrimming.CharacterEllipsis });
        var money = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right }; money.Children.Add(new TextBlock { Text = DurationText.Compact(session.Duration), FontSize = 12 * scale, HorizontalAlignment = System.Windows.HorizontalAlignment.Right }); money.Children.Add(new TextBlock { Text = MoneyText.Money(_store.Earnings(session), _store.CurrencyCode), FontSize = 10 * scale, Foreground = new SolidColorBrush(theme.Color(theme.Accent)), HorizontalAlignment = System.Windows.HorizontalAlignment.Right });
        Grid.SetColumn(sourceIcon, 0); Grid.SetColumn(info, 1); Grid.SetColumn(money, 2); grid.Children.Add(sourceIcon); grid.Children.Add(info); grid.Children.Add(money); row.Child = grid; RecentPanel.Children.Add(row);
    }

    private void UpdateMascotMode()
    {
        var configured = _settings.Get("MascotDefault", "Auto"); var mode = configured != "Auto" ? configured : _store.Running is null ? "Idle" : _store.Running.IsPaused ? "Coffee" : "Typing";
        if (mode == _lastMascotMode) return; _lastMascotMode = mode; _mascotFrame = 0;
        if (mode == "Idle") { _mascotTimer.Stop(); SetMascot("Assets/Mascot/idle.png"); }
        else if (mode == "Coffee") { _mascotTimer.Interval = TimeSpan.FromMilliseconds(280); _mascotTimer.Start(); AdvanceMascot(); }
        else if (mode == "Typing") { _mascotTimer.Interval = TimeSpan.FromMilliseconds(180); _mascotTimer.Start(); AdvanceMascot(); }
        else if (mode is "Victory" or "Stretch" or "Dance" or "Music") { _mascotTimer.Stop(); var index = mode switch { "Victory" => 1, "Stretch" => 2, "Dance" => 3, _ => 4 }; SetMascot($"Assets/Mascot/poses/pose{index}.png"); }
        AnimateMascotTransition();
    }
    private void AdvanceMascot()
    {
        var mode = _lastMascotMode; if (mode == "Idle" || mode is "Victory" or "Stretch" or "Dance" or "Music") { SetMascot(mode == "Idle" ? "Assets/Mascot/idle.png" : $"Assets/Mascot/poses/pose{(mode switch { "Victory" => 1, "Stretch" => 2, "Dance" => 3, _ => 4 })}.png"); return; }
        var prefix = mode == "Coffee" ? "Assets/Mascot/coffee/coffee" : "Assets/Mascot/typing/frame";
        _mascotFrame = _mascotFrame % 4 + 1; SetMascot($"{prefix}{_mascotFrame}.png");
    }
    private void AnimateMascotTransition()
    {
        if (MascotImage.RenderTransform is not ScaleTransform scale) return; scale.ScaleX = 0.84; scale.ScaleY = 0.84; MascotImage.Opacity = 0.35; var ease = new CubicEase { EasingMode = EasingMode.EaseOut }; scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.84, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease }); scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.84, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease }); MascotImage.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(360)) { EasingFunction = ease });
    }
    private void SetMascot(string path)
    {
        var full = Path.Combine(AppContext.BaseDirectory, path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full)) return;
        MascotImage.Source = new BitmapImage(new Uri(full, UriKind.Absolute));
    }
    private void ShowMascotEffect()
    {
        string[] effects = ["$  $  $", "✦  ✧  ✦", "♪  ♫  ♪", "✹  ✹", "+XP"]; MascotEffectText.Text = effects[Random.Shared.Next(effects.Length)]; MascotEffectTransform.Y = 0;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1.6)) { BeginTime = TimeSpan.FromMilliseconds(250) }; var rise = new DoubleAnimation(0, -24, TimeSpan.FromSeconds(1.6)); MascotEffectText.BeginAnimation(OpacityProperty, fade); MascotEffectTransform.BeginAnimation(TranslateTransform.YProperty, rise);
    }
    private void SetMascotMessage(string message)
    {
        if (message == _lastMascotMessage) return;
        _lastMascotMessage = message; MascotMessage.Text = message; MascotMessage.Opacity = 0; MascotMessageTransform.Y = 8;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        MascotMessage.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        MascotMessageTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
    }

    private (int level, long xp) CalculateProgress(DateTime now)
    {
        var snapshot = ProgressCalculator.Calculate(_store, _settings, now);
        return (snapshot.Level, snapshot.Xp);
    }
    private static int LongestStreak(IEnumerable<DateTime> dates) { var values = dates.OrderBy(x => x).ToArray(); if (values.Length == 0) return 0; var current = 1; var best = 1; for (var i = 1; i < values.Length; i++) { current = (values[i] - values[i - 1]).TotalDays == 1 ? current + 1 : 1; best = Math.Max(best, current); } return best; }

    private void ClockIn_Click(object sender, RoutedEventArgs e) => _store.ClockIn();
    private void Pause_Click(object sender, RoutedEventArgs e) { if (_store.Running?.IsPaused == true) _store.Resume(); else _store.Pause(); }
    private void ClockOut_Click(object sender, RoutedEventArgs e) { var session = _store.ClockOut(); if (session is not null) new SessionSummaryWindow(_store, session) { Owner = this }.ShowDialog(); }
    private void Manual_Click(object sender, RoutedEventArgs e) { new ManualStartWindow(_store) { Owner = this }.ShowDialog(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { if (ClockinDialog.Confirm(this, "Cancel session", "Discard the active session? No earnings will be added.", "Cancel session", destructive: true)) _store.CancelRunning(); }
    private void Pin_Click(object sender, RoutedEventArgs e) => _store.SetPinned(!_store.PinVisible);
    private void Settings_Click(object sender, RoutedEventArgs e) { new SettingsWindow(_store, _rates) { Owner = this }.ShowDialog(); ApplyTheme(); RefreshFromStore(); }
    private void History_Click(object sender, RoutedEventArgs e) { new HistoryWindow(_store, _rates) { Owner = this }.ShowDialog(); RefreshFromStore(); }
    private void Heatmap_Click(object sender, RoutedEventArgs e) => new HeatmapWindow(_store, _rates) { Owner = this }.ShowDialog();
    private void Progress_Click(object sender, RoutedEventArgs e) => new ProgressWindow(_store, _settings) { Owner = this }.ShowDialog();
    private void Guide_Click(object sender, RoutedEventArgs e) => new GuideWindow { Owner = this }.ShowDialog();
    private async void RefreshRate_Click(object sender, RoutedEventArgs e) => await _rates.RefreshAsync(_store.Sessions.Select(x => x.Start));
    private void Minimize_Click(object sender, RoutedEventArgs e) => Hide();
    private void CloseToTray_Click(object sender, RoutedEventArgs e) => Hide();
    private void Quit_Click(object sender, RoutedEventArgs e) => _app.ShutdownApp();
    private void ClearLatest_Click(object sender, RoutedEventArgs e)
    {
        if (!_store.Sessions.Any()) return;
        if (ClockinDialog.Confirm(this, "Clear latest entry", "Clear the latest entry so you can enter it again?", "Clear last", destructive: true)) _store.DeleteLatestSession();
    }
    private void InstallBottomActions()
    {
        if (Content is not Border shell || shell.Child is not Grid root) return;
        var actions = new Border { HorizontalAlignment = System.Windows.HorizontalAlignment.Right, VerticalAlignment = System.Windows.VerticalAlignment.Bottom, Margin = new Thickness(15, 0, 15, 12), Padding = new Thickness(5, 3, 5, 3), CornerRadius = new CornerRadius(10), Opacity = 0.98 };
        actions.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        actions.SetResourceReference(Border.BorderBrushProperty, "CardStrokeBrush"); actions.BorderThickness = new Thickness(1);
        var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        _clearLatestButton = new WpfButton { Content = "Clear last", Width = 78, Height = 28, Padding = new Thickness(7, 3, 7, 3), ToolTip = "Remove the latest completed entry" };
        _clearLatestButton.Click += ClearLatest_Click;
        var quit = new WpfButton { Content = "Quit", Width = 54, Height = 28, Padding = new Thickness(7, 3, 7, 3), ToolTip = "Quit Clockin" };
        quit.Click += Quit_Click;
        buttons.Children.Add(_clearLatestButton); buttons.Children.Add(quit); actions.Child = buttons;
        Grid.SetRow(actions, 1); System.Windows.Controls.Panel.SetZIndex(actions, 100); root.Children.Add(actions);
    }
    private void DragWindow(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) { if (!_allowClose) { e.Cancel = true; Hide(); } }
    public void CloseFromApp() { _allowClose = true; Close(); }
}

using System.Windows;
using WpfPoint = System.Windows.Point;

namespace Clockin.Windows;

public partial class TrayStatusWindow : Window
{
    private readonly ClockStore _store;
    private readonly ExchangeRateStore _rates;
    private readonly SettingStore _settings = SettingStore.Shared;
    private readonly App _app;
    private WpfPoint _dragStart;
    private WpfPoint _windowStart;
    private bool _dragging;

    public TrayStatusWindow(ClockStore store, ExchangeRateStore rates, App app)
    {
        InitializeComponent();
        _store = store; _rates = rates; _app = app;
        ThemeManager.Apply(this, _settings);
        Loaded += (_, _) => Position();
        SizeChanged += (_, _) => Position();
        _settings.Changed += SettingsChanged;
        Closed += (_, _) => _settings.Changed -= SettingsChanged;
        Update();
    }

    private void SettingsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => SettingsChanged(sender, e)); return; }
        ThemeManager.Apply(this, _settings);
        Update();
    }

    public void Update()
    {
        var running = _store.Running;
        StatusText.Text = running is null ? "READY" : running.IsPaused ? "PAUSED" : "FOCUS SESSION";
        var parts = new List<string>();
        if (_settings.GetBool("MinimalShowHours", true)) parts.Add(DurationText.Clock(_store.Elapsed()));
        if (_settings.GetBool("MinimalShowEarnings", true)) parts.Add(MoneyText.Money(_store.CurrentEarnings(), _store.CurrencyCode));
        if (_settings.GetBool("MinimalShowTRY", true) && _store.CurrencyCode == "USD" && _rates.LatestRate is { } rate) parts.Add(MoneyText.Money(_store.CurrentEarnings() * rate, "TRY"));
        InfoText.Text = parts.Count == 0 ? DurationText.Clock(_store.Elapsed()) : string.Join("  ·  ", parts);
        var dailyGoal = _settings.GetDouble("GoalDailyHours");
        GoalText.Text = _settings.GetBool("MinimalShowGoal") && dailyGoal > 0 ? $"{Math.Min(100, _store.DurationOn(DateTime.Now) / 3600d / dailyGoal * 100):0}%" : "";
    }

    private void Position()
    {
        var area = SystemParameters.WorkArea;
        var savedLeft = _settings.GetDouble("TrayStatusLeft", double.NaN);
        var savedTop = _settings.GetDouble("TrayStatusTop", double.NaN);
        Left = double.IsNaN(savedLeft) ? area.Right - ActualWidth - 8 : savedLeft;
        Top = double.IsNaN(savedTop) ? area.Bottom - ActualHeight - 7 : savedTop;
        ClampToWorkArea();
    }

    private void ClampToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - ActualWidth));
        Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - ActualHeight));
    }

    private void TrayStatus_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        _dragStart = PointToScreen(e.GetPosition(this));
        _windowStart = new WpfPoint(Left, Top);
        _dragging = false;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void TrayStatus_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!IsMouseCaptured || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        var current = PointToScreen(e.GetPosition(this));
        var dx = current.X - _dragStart.X;
        var dy = current.Y - _dragStart.Y;
        if (!_dragging && Math.Abs(dx) + Math.Abs(dy) < 4) return;
        _dragging = true;
        Left = _windowStart.X + dx;
        Top = _windowStart.Y + dy;
        ClampToWorkArea();
        e.Handled = true;
    }

    private void TrayStatus_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        ((UIElement)sender).ReleaseMouseCapture();
        if (_dragging)
        {
            _settings.Set("TrayStatusLeft", Left);
            _settings.Set("TrayStatusTop", Top);
        }
        else _app.ShowMain();
        _dragging = false;
        e.Handled = true;
    }
}

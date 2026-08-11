using System.Windows;

namespace Clockin.Windows;

public partial class TrayStatusWindow : Window
{
    private readonly ClockStore _store;
    private readonly ExchangeRateStore _rates;
    private readonly SettingStore _settings = SettingStore.Shared;
    private readonly App _app;

    public TrayStatusWindow(ClockStore store, ExchangeRateStore rates, App app)
    {
        InitializeComponent();
        _store = store; _rates = rates; _app = app;
        ThemeManager.Apply(this, _settings);
        Loaded += (_, _) => Position();
        SizeChanged += (_, _) => Position();
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
        Left = Math.Max(area.Left, area.Right - ActualWidth - 8);
        Top = Math.Max(area.Top, area.Bottom - ActualHeight - 7);
    }

    private void Open_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) _app.ShowMain(); }
}

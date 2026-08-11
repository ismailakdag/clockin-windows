using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace Clockin.Windows;

public partial class App : Application
{
    private NotifyIcon? _tray;
    private MainWindow? _main;
    private PinnedWindow? _pinned;
    private TrayStatusWindow? _trayStatus;
    private readonly DispatcherTimer _trayTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public ClockStore Store { get; } = new();
    public ExchangeRateStore ExchangeRates { get; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _main = new MainWindow(Store, ExchangeRates, this);
        var trayOnly = SettingStore.Shared.GetBool("MinimalMode");
        if (!trayOnly) { _main.Show(); _main.Activate(); }
        else Store.SetPinned(false);

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Clockin",
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowMain();
        _tray.ContextMenuStrip = BuildTrayMenu();
        _trayTimer.Tick += (_, _) => UpdateTrayPresentation();
        _trayTimer.Start();

        Store.Changed += OnStoreChanged;
        HotKeyManager.Start(_main, Store, ShowMain);
        FocusChime.Start(Store);
        if (Store.PinVisible) OnStoreChanged(this, EventArgs.Empty);
        await ExchangeRates.RefreshAsync(Store.Sessions.Select(x => x.Start));
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Clockin", null, (_, _) => ShowMain());
        menu.Items.Add(new ToolStripSeparator());
        var action = menu.Items.Add(Store.Running is null ? "Clock in" : Store.Running.IsPaused ? "Resume" : "Pause");
        action.Click += (_, _) =>
        {
            if (Store.Running is null) Store.ClockIn();
            else if (Store.Running.IsPaused) Store.Resume();
            else Store.Pause();
            RebuildTrayMenu();
        };
        if (Store.Running is not null)
        {
            menu.Items.Add("Clock out", null, (_, _) => { Store.ClockOut(); RebuildTrayMenu(); });
            menu.Items.Add("Cancel session", null, (_, _) => { Store.CancelRunning(); RebuildTrayMenu(); });
        }
        menu.Items.Add(Store.PinVisible ? "Hide pinned timer" : "Show pinned timer", null, (_, _) =>
        {
            Store.SetPinned(!Store.PinVisible);
            RebuildTrayMenu();
        });
        menu.Items.Add(SettingStore.Shared.GetBool("MinimalMode") ? "Exit tray-only mode" : "Use tray-only mode", null, (_, _) =>
        {
            var next = !SettingStore.Shared.GetBool("MinimalMode"); SettingStore.Shared.Set("MinimalMode", next);
            if (next) { SettingStore.Shared.Set("PinVisibleBeforeMinimal", Store.PinVisible); Store.SetPinned(false); _main?.Hide(); EnsureTrayStatus(); } else { _trayStatus?.Hide(); ShowMain(); Store.SetPinned(SettingStore.Shared.GetBool("PinVisibleBeforeMinimal")); }
            RebuildTrayMenu();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit Clockin", null, (_, _) => ShutdownApp());
        return menu;
    }

    private void RebuildTrayMenu()
    {
        if (_tray is null) return;
        var old = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = BuildTrayMenu();
        old?.Dispose();
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateTrayPresentation();
            if (Store.PinVisible)
            {
                _pinned ??= new PinnedWindow(Store, ExchangeRates);
                if (!_pinned.IsVisible) _pinned.Show();
            }
            else _pinned?.Hide();
            RebuildTrayMenu();
            _main?.RefreshFromStore();
        });
    }

    private void EnsureTrayStatus()
    {
        _trayStatus ??= new TrayStatusWindow(Store, ExchangeRates, this);
        if (!_trayStatus.IsVisible) _trayStatus.Show();
        _trayStatus.Update();
    }

    private void UpdateTrayPresentation()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(UpdateTrayPresentation); return; }
        var minimal = SettingStore.Shared.GetBool("MinimalMode");
        var status = Store.Running is null ? "Ready" : Store.Running.IsPaused ? "Paused" : "Clocked in";
        var text = $"Clockin · {status} · {DurationText.Clock(Store.Elapsed())}";
        if (minimal && SettingStore.Shared.GetBool("MinimalShowEarnings", true)) text += $" · {MoneyText.Money(Store.CurrentEarnings(), Store.CurrencyCode)}";
        if (_tray is not null) _tray.Text = text.Length > 63 ? text[..63] : text;
        if (minimal) EnsureTrayStatus(); else _trayStatus?.Hide();
    }

    public void ShowMain()
    {
        if (_main is null) return;
        if (!_main.IsVisible) _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void ShutdownApp()
    {
        FocusChime.Stop();
        HotKeyManager.Stop();
        _pinned?.Close();
        _trayStatus?.Close();
        _trayTimer.Stop();
        _tray?.Dispose();
        _main?.CloseFromApp();
        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayTimer.Stop();
        _trayStatus?.Close();
        _tray?.Dispose();
        base.OnExit(e);
    }
}

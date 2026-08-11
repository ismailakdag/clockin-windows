using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfPanel = System.Windows.Controls.Panel;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;
using WpfToolTip = System.Windows.Controls.ToolTip;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Clockin.Windows;

public partial class HistoryWindow : Window
{
    private readonly ClockStore _store;
    private readonly ExchangeRateStore _rates;
    private readonly SettingStore _settings = SettingStore.Shared;
    private string _range;

    public HistoryWindow(ClockStore store, ExchangeRateStore? rates = null)
    {
        InitializeComponent();
        _store = store; _rates = rates ?? new ExchangeRateStore(); _range = _settings.Get("HistoryRange", "30D");
        if (_range is not ("7D" or "30D" or "3M" or "ALL")) _range = "30D";
        CustomChrome.Attach(this, "EARNINGS HISTORY", _settings);
        _store.Changed += Changed; _rates.Changed += Changed; Closed += (_, _) => { _store.Changed -= Changed; _rates.Changed -= Changed; };
        Build();
    }

    private void Changed(object? sender, EventArgs e) { if (Dispatcher.CheckAccess()) Build(); else Dispatcher.BeginInvoke(Build); }

    private void Range_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button || button.Tag is not string range) return;
        _range = range; _settings.Set("HistoryRange", range); Build();
    }

    private void Build()
    {
        var today = DateTime.Today;
        var earliest = _store.Sessions.Select(x => x.Start.Date).Append(_store.Running?.Start.Date ?? today).DefaultIfEmpty(today).Min();
        var days = _range switch { "7D" => 7, "30D" => 30, "3M" => 90, _ => Math.Max(1, (today - earliest).Days + 1) };
        var cutoff = _range == "ALL" ? earliest : today.AddDays(-days + 1);
        var sessions = _store.Sessions.Where(x => x.Start.Date >= cutoff && x.Start.Date <= today).ToList();
        var includeRunning = _store.Running is { } running && running.Start.Date >= cutoff && running.Start.Date <= today;
        var totalDuration = sessions.Sum(x => x.Duration) + (includeRunning ? _store.Elapsed() : 0);
        var totalEarnings = sessions.Sum(_store.Earnings) + (includeRunning ? _store.CurrentEarnings() : 0);
        var daily = BuildDailyPoints(sessions, includeRunning);
        var activeDays = daily.Count(x => x.Value.Duration > 0);
        var calendarDays = Math.Max(1, days);
        var totalHours = totalDuration / 3600d;
        var activeAverage = totalHours / Math.Max(1, activeDays);
        PeriodButtons(); HistoryContent.Children.Clear();
        HistoryContent.Children.Add(SummaryCard(totalEarnings, totalDuration, sessions.Count, includeRunning));
        HistoryContent.Children.Add(ChartCard(daily));
        HistoryContent.Children.Add(AveragesCard(totalHours / calendarDays, totalHours / calendarDays * 7, totalHours / calendarDays * 30.44, activeDays, activeAverage));
        var title = new DockPanel { Margin = new Thickness(2, 4, 2, 5) };
        title.Children.Add(new TextBlock { Text = $"ALL SESSIONS • {sessions.Count}", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brush("MutedBrush") });
        HistoryContent.Children.Add(title);
        if (sessions.Count == 0) HistoryContent.Children.Add(Card(Text("No earnings in this period.", 11, "MutedBrush"), 12));
        foreach (var session in sessions.Take(100)) HistoryContent.Children.Add(SessionRow(session));
        TotalText.Text = $"{_range} · {DurationText.Compact(totalDuration)} · {MoneyText.Money(totalEarnings, _store.CurrencyCode)}";
        ThemeManager.ApplyTextScale(this, _settings);
    }

    private Dictionary<DateTime, DayPoint> BuildDailyPoints(IEnumerable<WorkSession> sessions, bool includeRunning)
    {
        var points = new Dictionary<DateTime, DayPoint>();
        foreach (var session in sessions)
        {
            var day = session.Start.Date; var old = points.GetValueOrDefault(day); points[day] = new DayPoint(old.Duration + session.Duration, old.Earnings + _store.Earnings(session));
        }
        if (includeRunning && _store.Running is { } running)
        {
            var day = running.Start.Date; var old = points.GetValueOrDefault(day); points[day] = new DayPoint(old.Duration + _store.Elapsed(), old.Earnings + _store.CurrentEarnings());
        }
        return points;
    }

    private Border SummaryCard(double earnings, double duration, int sessionCount, bool includesRunning)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel(); left.Children.Add(Text("TOTAL EARNED", 9, "MutedBrush", FontWeights.Bold)); left.Children.Add(Text(MoneyText.Money(earnings, _store.CurrencyCode), 25, "TextBrush", FontWeights.Bold));
        if (_store.CurrencyCode == "USD" && _rates.LatestRate is { } rate) left.Children.Add(Text($"{_range} · {(includesRunning ? "includes active" : "completed")} · ≈ {MoneyText.Money(earnings * rate, "TRY")}", 10, "AccentBrush"));
        else left.Children.Add(Text($"{_range} · {(includesRunning ? "includes active" : "completed")}", 10, "AccentBrush"));
        grid.Children.Add(left); var right = new StackPanel { HorizontalAlignment = WpfHorizontalAlignment.Right, VerticalAlignment = WpfVerticalAlignment.Center }; right.Children.Add(Text(DurationText.Compact(duration), 15, "TextBrush", FontWeights.SemiBold, TextAlignment.Right)); right.Children.Add(Text($"{sessionCount} sessions", 10, "MutedBrush", TextAlignment.Right)); Grid.SetColumn(right, 1); grid.Children.Add(right);
        return Card(grid, 15);
    }

    private Border ChartCard(IReadOnlyDictionary<DateTime, DayPoint> points)
    {
        var stack = new StackPanel(); var header = new DockPanel(); header.Children.Add(Text("DAILY EARNINGS", 9, "MutedBrush", FontWeights.Bold)); header.Children.Add(Text(_store.CurrencyCode == "USD" ? "Historical daily USD / TRY" : _store.CurrencyCode, 9, "MutedBrush", TextAlignment.Right)); stack.Children.Add(header);
        stack.Children.Add(Card(Text(points.Count == 0 ? "No earnings in this period." : "Hover a bar for hours, earnings, TRY and daily rate", 9, "MutedBrush"), 9));
        if (points.Count > 0)
        {
            var max = Math.Max(1, points.Values.Max(x => x.Earnings)); var bars = new StackPanel { Orientation = WpfOrientation.Horizontal, VerticalAlignment = WpfVerticalAlignment.Bottom, Height = 168 };
            foreach (var point in points.OrderBy(x => x.Key))
            {
                var column = new StackPanel { Width = 36, Margin = new Thickness(2, 0, 2, 0), VerticalAlignment = WpfVerticalAlignment.Bottom };
                var track = new Border { Height = 112, Width = 24, Background = Brush("CardBrush"), BorderBrush = Brush("CardStrokeBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), VerticalAlignment = WpfVerticalAlignment.Bottom };
                track.Child = new Border { Height = Math.Max(3, 104 * point.Value.Earnings / max), Width = 16, Background = Brush("AccentBrush"), CornerRadius = new CornerRadius(4), VerticalAlignment = WpfVerticalAlignment.Bottom, Margin = new Thickness(3) };
                track.ToolTip = DailyTooltip(point.Key, point.Value); ToolTipService.SetInitialShowDelay(track, 120); ToolTipService.SetShowDuration(track, 10000); ToolTipService.SetPlacement(track, PlacementMode.Mouse);
                column.Children.Add(track); column.Children.Add(Text(point.Key.ToString(_range == "ALL" ? "MMM d" : "d MMM", CultureInfo.CurrentCulture), 8, "MutedBrush", TextAlignment.Center)); bars.Children.Add(column);
            }
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = bars, Height = 180 }; stack.Children.Add(scroll);
        }
        return Card(stack, 14);
    }

    private Border AveragesCard(double daily, double weekly, double monthly, int activeDays, double activeAverage)
    {
        var grid = new UniformGrid { Columns = 4 };
        grid.Children.Add(MiniMetric("DAILY AVG", DurationText.Hours(daily))); grid.Children.Add(MiniMetric("WEEKLY AVG", DurationText.Hours(weekly))); grid.Children.Add(MiniMetric("MONTHLY AVG", DurationText.Hours(monthly))); grid.Children.Add(MiniMetric("ACTIVE DAY AVG", $"{DurationText.Hours(activeAverage)}\n{activeDays} days"));
        return Card(grid, 8);
    }

    private Border MiniMetric(string title, string value) => new() { Background = Brush("WindowBackgroundBrush"), CornerRadius = new CornerRadius(7), Margin = new Thickness(2), Padding = new Thickness(7), Child = new StackPanel { Children = { Text(title, 7, "MutedBrush", FontWeights.Bold), Text(value, 9, "TextBrush", FontWeights.SemiBold) } } };

    private Border SessionRow(WorkSession session)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        var info = new StackPanel(); info.Children.Add(Text(session.Start.ToString("ddd, MMM d · HH:mm"), 11, "TextBrush", FontWeights.SemiBold)); info.Children.Add(Text($"{session.End:HH:mm} · {(string.IsNullOrWhiteSpace(session.Note) ? session.Source : session.Note)}", 9, "MutedBrush")); grid.Children.Add(info);
        var money = new StackPanel { HorizontalAlignment = WpfHorizontalAlignment.Right }; money.Children.Add(Text(MoneyText.Money(_store.Earnings(session), _store.CurrencyCode), 11, "TextBrush", FontWeights.SemiBold, TextAlignment.Right)); money.Children.Add(Text(DurationText.Compact(session.Duration), 9, "MutedBrush", TextAlignment.Right)); Grid.SetColumn(money, 1); grid.Children.Add(money);
        var delete = new WpfButton { Content = "×", Width = 26, Height = 26, Padding = new Thickness(0), Background = WpfBrushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brush("MutedBrush"), Tag = session }; delete.Click += DeleteRow_Click; Grid.SetColumn(delete, 2); grid.Children.Add(delete);
        return Card(grid, 10);
    }

    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: WorkSession session }) return;
        if (ClockinDialog.Confirm(this, "Delete session", "Delete this session? Its time and earnings will be removed permanently.", "Delete", destructive: true)) _store.DeleteSession(session.Id);
    }

    private WpfToolTip DailyTooltip(DateTime day, DayPoint point)
    {
        var stack = new StackPanel { Width = 190 }; stack.Children.Add(Text(day.ToString("ddd, MMM d, yyyy", CultureInfo.CurrentCulture), 11, "TextBrush", FontWeights.Bold)); stack.Children.Add(Text($"{DurationText.Compact(point.Duration)} worked", 10, "TextBrush")); stack.Children.Add(Text(MoneyText.Money(point.Earnings, _store.CurrencyCode), 10, "AccentBrush"));
        if (_store.CurrencyCode == "USD" && _rates.RateOn(day) is { } rate) stack.Children.Add(Text($"≈ {MoneyText.Money(point.Earnings * rate, "TRY")} · rate {rate:0.###}", 9, "MutedBrush"));
        return new WpfToolTip { Content = new Border { Background = Brush("CardBrush"), BorderBrush = Brush("CardStrokeBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10), Child = stack }, Background = WpfBrushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), HasDropShadow = true };
    }

    private void PeriodButtons()
    {
        foreach (var pair in new[] { (Range7Button, "7D"), (Range30Button, "30D"), (Range90Button, "3M"), (RangeAllButton, "ALL") }) { pair.Item1.Background = _range == pair.Item2 ? Brush("CardStrokeBrush") : WpfBrushes.Transparent; pair.Item1.Foreground = Brush(_range == pair.Item2 ? "TextBrush" : "MutedBrush"); pair.Item1.FontWeight = _range == pair.Item2 ? FontWeights.Bold : FontWeights.Normal; }
    }

    private Border Card(UIElement content, double padding) => new() { Child = content, Background = Brush("CardBrush"), BorderBrush = Brush("CardStrokeBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), Padding = new Thickness(padding), Margin = new Thickness(0, 0, 0, 10) };
    private TextBlock Text(string value, double size, string color = "TextBrush", FontWeight? weight = null, TextAlignment alignment = TextAlignment.Left) => new() { Text = value, FontSize = size, Foreground = Brush(color), FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap, TextAlignment = alignment, Margin = new Thickness(0, 2, 0, 2) };
    private TextBlock Text(string value, double size, string color, TextAlignment alignment) => Text(value, size, color, null, alignment);
    private SolidColorBrush Brush(string key) => (SolidColorBrush)FindResource(key);
    private void ImportCsv_Click(object sender, RoutedEventArgs e) { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*" }; if (dialog.ShowDialog() != true) return; try { new CsvPreviewWindow(_store, _store.PreviewCsv(dialog.FileName)) { Owner = this }.ShowDialog(); Build(); } catch (Exception ex) { ClockinDialog.Alert(this, "CSV import", ex.Message); } }
    private void Paste_Click(object sender, RoutedEventArgs e) { new PasteWindow(_store) { Owner = this }.ShowDialog(); Build(); }
    private void DeleteAll_Click(object sender, RoutedEventArgs e) { if (!_store.Sessions.Any()) { ClockinDialog.Alert(this, "Delete all entries", "There are no completed entries to delete."); return; } if (ClockinDialog.Confirm(this, "Delete all entries", $"Delete all {_store.Sessions.Count} completed entries? This cannot be undone.", "Delete all", destructive: true)) _store.DeleteAllSessions(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private readonly record struct DayPoint(double Duration, double Earnings);
}

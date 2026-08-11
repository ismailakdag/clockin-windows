using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfPanel = System.Windows.Controls.Panel;
using WpfToolTip = System.Windows.Controls.ToolTip;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;

namespace Clockin.Windows;

public partial class HeatmapWindow : Window
{
    private readonly ClockStore _store;
    private readonly ExchangeRateStore _rates;
    private readonly SettingStore _settings = SettingStore.Shared;
    private string _range;

    public HeatmapWindow(ClockStore store, ExchangeRateStore? rates = null)
    {
        InitializeComponent();
        _store = store;
        _rates = rates ?? new ExchangeRateStore();
        _range = _settings.Get("HeatmapRange", "All");
        if (_range is not ("Week" or "Month" or "All")) _range = "All";
        CustomChrome.Attach(this, "WORK HEATMAP", _settings);
        StoreChanged(null, EventArgs.Empty);
        _store.Changed += StoreChanged;
        _rates.Changed += StoreChanged;
        Closed += (_, _) => { _store.Changed -= StoreChanged; _rates.Changed -= StoreChanged; };
    }

    private void StoreChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => StoreChanged(sender, e)); return; }
        Build();
    }

    private void Range_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button || button.Tag is not string range) return;
        _range = range;
        _settings.Set("HeatmapRange", range);
        Build();
    }

    private void Build()
    {
        var today = DateTime.Today;
        var firstDay = _store.Sessions.Select(x => x.Start.Date).Append(_store.Running?.Start.Date ?? today).DefaultIfEmpty(today).Min();
        if (firstDay > today) firstDay = today;
        var start = _range == "Week" ? StartOfWeek(firstDay) : _range == "Month" ? new DateTime(firstDay.Year, firstDay.Month, 1) : StartOfWeek(firstDay);
        var end = today;
        var daily = DailyStats(firstDay, end);
        var active = daily.Values.Count(x => x.Duration > 0);
        var best = daily.OrderByDescending(x => x.Value.Duration).FirstOrDefault();
        var total = daily.Values.Sum(x => x.Duration);
        PeriodLabel.Text = _range switch { "Week" => "This week", "Month" => today.ToString("MMMM yyyy", CultureInfo.CurrentCulture), _ => $"{firstDay:MMM d, yyyy} — {today:MMM d, yyyy}" };
        SummaryGrid.Children.Clear();
        SummaryGrid.Children.Add(SummaryCard("PERIOD TOTAL", DurationText.Compact(total), "AccentBrush"));
        SummaryGrid.Children.Add(SummaryCard("BEST DAY", best.Value.Duration > 0 ? $"{best.Key:MMM d} · {DurationText.Compact(best.Value.Duration)}" : "No sessions yet", "AccentBrush"));
        SummaryGrid.Children.Add(SummaryCard("ACTIVE DAYS", active == 0 ? "0" : $"{active} day{(active == 1 ? "" : "s")}", "SecondaryAccentBrush"));
        SetRangeButton(WeekButton, _range == "Week"); SetRangeButton(MonthButton, _range == "Month"); SetRangeButton(AllButton, _range == "All");
        BuildLegend();
        HeatmapHost.Children.Clear();
        if (_range == "All") BuildDailyGrid(firstDay, today, daily); else BuildAggregate(start, today, _range == "Week" ? TimeSpan.FromDays(7) : null, daily);
    }

    private Dictionary<DateTime, DayStat> DailyStats(DateTime first, DateTime last)
    {
        var result = new Dictionary<DateTime, DayStat>();
        for (var day = first.Date; day <= last.Date; day = day.AddDays(1))
        {
            var duration = _store.DurationOn(day);
            var earnings = _store.EarningsOn(day);
            result[day] = new DayStat(duration, earnings);
        }
        return result;
    }

    private void BuildDailyGrid(DateTime firstDay, DateTime today, IReadOnlyDictionary<DateTime, DayStat> daily)
    {
        var start = StartOfWeek(firstDay);
        var last = StartOfWeek(today);
        var weeks = (int)((last - start).TotalDays / 7) + 1;
        var max = daily.Values.DefaultIfEmpty().Max(x => x.Duration);
        var frame = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        frame.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
        frame.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var labels = new StackPanel { Margin = new Thickness(0, 23, 4, 0) };
        foreach (var label in new[] { "M", "T", "W", "T", "F", "S", "S" }) labels.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = Brush("MutedBrush"), Height = 26, VerticalAlignment = WpfVerticalAlignment.Center, HorizontalAlignment = WpfHorizontalAlignment.Center });
        Grid.SetColumn(labels, 0); frame.Children.Add(labels);
        var horizontal = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = true };
        var columns = new StackPanel { Orientation = WpfOrientation.Horizontal };
        for (var week = 0; week < weeks; week++)
        {
            var weekStart = start.AddDays(week * 7);
            var column = new StackPanel { Width = 27 };
            var month = weekStart.Day <= 7 || week == 0 ? new TextBlock { Text = weekStart.ToString("MMM", CultureInfo.CurrentCulture), FontSize = 8, Foreground = Brush("MutedBrush"), Height = 21, HorizontalAlignment = WpfHorizontalAlignment.Left } : new TextBlock { Height = 21 };
            column.Children.Add(month);
            for (var offset = 0; offset < 7; offset++)
            {
                var day = weekStart.AddDays(offset);
                var hasData = day <= today && day >= firstDay;
                var stat = hasData && daily.TryGetValue(day, out var value) ? value : new DayStat(0, 0);
                var cell = new Border { Width = 21, Height = 21, Margin = new Thickness(2, 2, 4, 2), CornerRadius = new CornerRadius(5), Background = HeatBrush(stat.Duration, max, hasData), BorderBrush = Brush("CardStrokeBrush"), BorderThickness = new Thickness(0.5), ToolTip = BuildTooltip(day, stat, hasData) };
                ToolTipService.SetInitialShowDelay(cell, 120); ToolTipService.SetShowDuration(cell, 10000); ToolTipService.SetPlacement(cell, PlacementMode.Mouse);
                column.Children.Add(cell);
            }
            columns.Children.Add(column);
        }
        horizontal.Content = columns; Grid.SetColumn(horizontal, 1); frame.Children.Add(horizontal);
        var pan = new DockPanel { Margin = new Thickness(25, 0, 0, 5) };
        pan.Children.Add(new TextBlock { Text = "PAN", FontSize = 8, FontWeight = FontWeights.Bold, Foreground = Brush("MutedBrush"), VerticalAlignment = WpfVerticalAlignment.Center });
        var panButtons = new StackPanel { Orientation = WpfOrientation.Horizontal, HorizontalAlignment = WpfHorizontalAlignment.Right };
        var startButton = new WpfButton { Content = "Start", Padding = new Thickness(7, 3, 7, 3), FontSize = 9, Background = WpfBrushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brush("MutedBrush") };
        var todayButton = new WpfButton { Content = "Today", Padding = new Thickness(7, 3, 7, 3), FontSize = 9, Background = WpfBrushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brush("AccentBrush"), FontWeight = FontWeights.Bold };
        startButton.Click += (_, _) => horizontal.ScrollToLeftEnd(); todayButton.Click += (_, _) => horizontal.ScrollToRightEnd(); panButtons.Children.Add(startButton); panButtons.Children.Add(todayButton); DockPanel.SetDock(panButtons, Dock.Right); pan.Children.Add(panButtons);
        var dailyRoot = new StackPanel(); dailyRoot.Children.Add(pan); dailyRoot.Children.Add(frame); HeatmapHost.Children.Add(dailyRoot);
    }

    private void BuildAggregate(DateTime start, DateTime today, TimeSpan? step, IReadOnlyDictionary<DateTime, DayStat> daily)
    {
        var periods = new List<AggregateStat>();
        var cursor = start;
        while (cursor <= today)
        {
            var periodEnd = step.HasValue ? cursor.AddDays(6) : new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            var visibleEnd = periodEnd > today ? today : periodEnd;
            var days = Enumerable.Range(0, Math.Max(1, (visibleEnd - cursor).Days + 1)).Select(offset => cursor.AddDays(offset)).ToList();
            var duration = days.Sum(day => daily.TryGetValue(day, out var value) ? value.Duration : _store.DurationOn(day));
            var earnings = days.Sum(day => daily.TryGetValue(day, out var value) ? value.Earnings : _store.EarningsOn(day));
            periods.Add(new AggregateStat(cursor, visibleEnd, duration, earnings));
            cursor = step.HasValue ? cursor.AddDays(7) : cursor.AddMonths(1);
        }
        var max = periods.DefaultIfEmpty().Max(x => x.Earnings);
        var aggregateScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var root = new StackPanel { Orientation = WpfOrientation.Horizontal, VerticalAlignment = WpfVerticalAlignment.Bottom, MinHeight = 300 };
        foreach (var period in periods)
        {
            var stack = new StackPanel { Width = 74, Margin = new Thickness(3, 0, 3, 0), VerticalAlignment = WpfVerticalAlignment.Bottom };
            stack.Children.Add(new TextBlock { Text = DurationText.Compact(period.Duration), FontSize = 8, Foreground = Brush("TextBrush"), HorizontalAlignment = WpfHorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) });
            var track = new Border { Height = 190, Width = 42, Background = Brush("CardBrush"), BorderBrush = Brush("CardStrokeBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), VerticalAlignment = WpfVerticalAlignment.Bottom };
            var fill = new Border { Height = max <= 0 ? 2 : Math.Max(2, 176 * period.Earnings / max), Width = 26, Background = HeatBrush(period.Earnings, max, true), CornerRadius = new CornerRadius(7), VerticalAlignment = WpfVerticalAlignment.Bottom, Margin = new Thickness(7) };
            track.Child = fill; track.ToolTip = BuildAggregateTooltip(period); ToolTipService.SetInitialShowDelay(track, 120); ToolTipService.SetShowDuration(track, 10000); ToolTipService.SetPlacement(track, PlacementMode.Mouse);
            stack.Children.Add(track);
            stack.Children.Add(new TextBlock { Text = step.HasValue ? $"{period.Start:MMM d}" : period.Start.ToString("MMM", CultureInfo.CurrentCulture), FontSize = 9, Foreground = Brush("MutedBrush"), HorizontalAlignment = WpfHorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
            root.Children.Add(stack);
        }
        aggregateScroll.Content = root;
        var pan = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
        pan.Children.Add(new TextBlock { Text = "PAN", FontSize = 8, FontWeight = FontWeights.Bold, Foreground = Brush("MutedBrush"), VerticalAlignment = WpfVerticalAlignment.Center });
        var panButtons = new StackPanel { Orientation = WpfOrientation.Horizontal, HorizontalAlignment = WpfHorizontalAlignment.Right };
        var startButton = new WpfButton { Content = "Start", Padding = new Thickness(7, 3, 7, 3), FontSize = 9, Background = WpfBrushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brush("MutedBrush") };
        var todayButton = new WpfButton { Content = "Today", Padding = new Thickness(7, 3, 7, 3), FontSize = 9, Background = WpfBrushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brush("AccentBrush"), FontWeight = FontWeights.Bold };
        startButton.Click += (_, _) => aggregateScroll.ScrollToLeftEnd(); todayButton.Click += (_, _) => aggregateScroll.ScrollToRightEnd(); panButtons.Children.Add(startButton); panButtons.Children.Add(todayButton); DockPanel.SetDock(panButtons, Dock.Right); pan.Children.Add(panButtons);
        var aggregateRoot = new StackPanel(); aggregateRoot.Children.Add(pan); aggregateRoot.Children.Add(aggregateScroll); HeatmapHost.Children.Add(aggregateRoot);
    }

    private WpfToolTip BuildTooltip(DateTime day, DayStat stat, bool visible)
    {
        var title = new TextBlock { Text = visible ? day.ToString("ddd, MMM d, yyyy", CultureInfo.CurrentCulture) : day.ToString("ddd, MMM d, yyyy", CultureInfo.CurrentCulture), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush("TextBrush") };
        var stack = new StackPanel { Width = 190 };
        stack.Children.Add(title);
        stack.Children.Add(new TextBlock { Text = visible ? $"{DurationText.Compact(stat.Duration)} worked" : "Future date", FontSize = 10, Margin = new Thickness(0, 5, 0, 0), Foreground = Brush("TextBrush") });
        stack.Children.Add(new TextBlock { Text = visible ? MoneyText.Money(stat.Earnings, _store.CurrencyCode) : "—", FontSize = 10, Foreground = Brush("AccentBrush") });
        if (visible && _store.CurrencyCode == "USD" && _rates.RateOn(day) is { } rate) stack.Children.Add(new TextBlock { Text = $"≈ {MoneyText.Money(stat.Earnings * rate, "TRY")}  ·  1 USD = ₺{rate:0.##}", FontSize = 9, Foreground = Brush("MutedBrush"), Margin = new Thickness(0, 2, 0, 0) });
        return MakeTooltip(stack);
    }

    private WpfToolTip BuildAggregateTooltip(AggregateStat period)
    {
        var stack = new StackPanel { Width = 190 };
        stack.Children.Add(new TextBlock { Text = _range == "Week" ? $"Week of {period.Start:MMM d}" : period.Start.ToString("MMMM yyyy", CultureInfo.CurrentCulture), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush("TextBrush") });
        stack.Children.Add(new TextBlock { Text = $"{period.Start:MMM d} — {period.End:MMM d}", FontSize = 9, Foreground = Brush("MutedBrush"), Margin = new Thickness(0, 4, 0, 0) });
        stack.Children.Add(new TextBlock { Text = DurationText.Compact(period.Duration), FontSize = 10, Foreground = Brush("TextBrush"), Margin = new Thickness(0, 5, 0, 0) });
        stack.Children.Add(new TextBlock { Text = MoneyText.Money(period.Earnings, _store.CurrencyCode), FontSize = 10, Foreground = Brush("AccentBrush") });
        return MakeTooltip(stack);
    }

    private WpfToolTip MakeTooltip(WpfPanel content) => new() { Content = new Border { Background = Brush("CardBrush"), BorderBrush = Brush("CardStrokeBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(11), Child = content }, Background = WpfBrushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), HasDropShadow = true };

    private Border SummaryCard(string label, string value, string valueBrush) => new() { Background = Brush("CardBrush"), BorderBrush = Brush("CardStrokeBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Margin = new Thickness(2), Padding = new Thickness(11), Child = new StackPanel { Children = { new TextBlock { Text = label, FontSize = 8, FontWeight = FontWeights.Bold, Foreground = Brush("MutedBrush") }, new TextBlock { Text = value, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brush(valueBrush), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap } } } };

    private void BuildLegend()
    {
        LegendGrid.Children.Clear();
        var max = 3600d;
        foreach (var value in new[] { 0d, 0.25 * max, 0.5 * max, 0.75 * max, max }) LegendGrid.Children.Add(new Border { Margin = new Thickness(2), CornerRadius = new CornerRadius(3), Background = HeatBrush(value, max, true) });
    }

    private void SetRangeButton(WpfButton button, bool selected)
    {
        button.Background = selected ? Brush("CardStrokeBrush") : WpfBrushes.Transparent;
        button.Foreground = Brush(selected ? "TextBrush" : "MutedBrush");
        button.FontWeight = selected ? FontWeights.Bold : FontWeights.Normal;
    }

    private SolidColorBrush HeatBrush(double seconds, double max, bool visible)
    {
        var accent = ((SolidColorBrush)FindResource("AccentBrush")).Color;
        if (!visible) return new SolidColorBrush(WpfColor.FromArgb(18, accent.R, accent.G, accent.B));
        var intensity = max <= 0 ? 0 : Math.Clamp(seconds / max, 0, 1);
        return new SolidColorBrush(WpfColor.FromArgb((byte)(32 + intensity * 210), accent.R, accent.G, accent.B));
    }

    private SolidColorBrush Brush(string key) => (SolidColorBrush)FindResource(key);

    private static DateTime StartOfWeek(DateTime date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-offset);
    }

    private readonly record struct DayStat(double Duration, double Earnings);
    private readonly record struct AggregateStat(DateTime Start, DateTime End, double Duration, double Earnings);
}

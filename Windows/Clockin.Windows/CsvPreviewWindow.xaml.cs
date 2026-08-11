using System.Windows;
using System.Windows.Input;

namespace Clockin.Windows;

public partial class CsvPreviewWindow : Window
{
    private readonly ClockStore _store; private readonly CsvParseResult _result; private readonly ImportComparisonSummary _comparison;
    public CsvPreviewWindow(ClockStore store, CsvParseResult result)
    {
        InitializeComponent(); _store = store; _result = result; _comparison = store.CompareImportedSessions(result.Sessions); ThemeManager.Apply(this, SettingStore.Shared); Rows.ItemsSource = _comparison.Items; Summary.Text = $"NEW {_comparison.NewCount} · MATCHED {_comparison.MatchedCount} · SKIP {_comparison.DuplicateCount} · {DurationText.Compact(result.Sessions.Sum(x => x.Duration))} · {MoneyText.Money(result.Sessions.Sum(store.Earnings), store.CurrencyCode)}"; Issues.Text = result.SkippedRows > 0 || result.Issues.Count > 0 ? $"Skipped {result.SkippedRows} invalid row(s).\n" + string.Join("\n", result.Issues.Take(5)) : "All rows parsed successfully."; ImportButton.Content = $"Import {_comparison.ImportableCount} entries"; ImportButton.IsEnabled = _comparison.ImportableCount > 0;
    }
    private void Import_Click(object sender, RoutedEventArgs e) { _store.ImportSessions(_result.Sessions); DialogResult = true; }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void DragWindow(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}

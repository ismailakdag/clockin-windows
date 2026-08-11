using System.Windows;
namespace Clockin.Windows;
public partial class RateScheduleWindow : Window
{
    private readonly ClockStore _store;
    public RateScheduleWindow(ClockStore store) { InitializeComponent(); _store = store; CustomChrome.Attach(this, "RATE SCHEDULE", SettingStore.Shared); Refresh(); }
    private void Refresh() => Rules.ItemsSource = _store.RateRules.ToList();
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RateRuleInputWindow { Owner = this }; if (dialog.ShowDialog() != true) return;
        if (!_store.AddRateRule(dialog.From, dialog.Until, dialog.Rate, out var error)) Status.Text = error; else { Status.Text = "Rule added."; Refresh(); }
    }
    private void Delete_Click(object sender, RoutedEventArgs e) { if (Rules.SelectedItem is RateRule rule) { _store.DeleteRateRule(rule.Id); Refresh(); } }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

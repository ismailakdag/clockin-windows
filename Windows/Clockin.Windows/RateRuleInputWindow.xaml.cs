using System.Windows;
namespace Clockin.Windows;
public partial class RateRuleInputWindow : Window
{
    public DateTime From { get; private set; } public DateTime? Until { get; private set; } public double Rate { get; private set; }
    public RateRuleInputWindow() { InitializeComponent(); CustomChrome.Attach(this, "NEW RATE RULE", SettingStore.Shared); }
    private void Add_Click(object sender, RoutedEventArgs e) { if (!DateTime.TryParse(FromText.Text, out var from) || (!string.IsNullOrWhiteSpace(UntilText.Text) && !DateTime.TryParse(UntilText.Text, out var until)) || !double.TryParse(RateText.Text, out var rate)) { Error.Text = "Enter valid dates and rate."; return; } From = from; Until = string.IsNullOrWhiteSpace(UntilText.Text) ? null : DateTime.Parse(UntilText.Text); Rate = rate; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

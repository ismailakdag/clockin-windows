using System.Windows;

namespace Clockin.Windows;
public partial class ManualStartWindow : Window
{
    private readonly ClockStore _store;
    public ManualStartWindow(ClockStore store) { InitializeComponent(); _store = store; CustomChrome.Attach(this, "START WITH ELAPSED TIME", SettingStore.Shared); Hours.Text = "0"; Minutes.Text = "0"; }
    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(Hours.Text, out var hours) || !double.TryParse(Minutes.Text, out var minutes) || hours < 0 || minutes < 0) { System.Windows.MessageBox.Show("Enter valid non-negative hours and minutes.", "Clockin"); return; }
        _store.ClockIn(hours * 3600 + minutes * 60); DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

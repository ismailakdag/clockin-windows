using System.Windows;

namespace Clockin.Windows;

public partial class SessionSummaryWindow : Window
{
    public SessionSummaryWindow(ClockStore store, WorkSession session)
    {
        InitializeComponent();
        CustomChrome.Attach(this, "SESSION COMPLETE", SettingStore.Shared);
        SessionLabel.Text = string.IsNullOrWhiteSpace(session.Note) ? "Focus session" : session.Note;
        TimeText.Text = DurationText.Compact(session.Duration);
        EarnedText.Text = MoneyText.Money(store.Earnings(session), store.CurrencyCode);
        XpText.Text = $"+{(int)(session.Duration / 3600d * 100)}";
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}

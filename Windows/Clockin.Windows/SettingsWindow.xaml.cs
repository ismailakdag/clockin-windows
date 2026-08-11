using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;

namespace Clockin.Windows;

public partial class SettingsWindow : Window
{
    private readonly ClockStore _store; private readonly ExchangeRateStore _rates; private readonly SettingStore _settings = SettingStore.Shared;
    public SettingsWindow(ClockStore store, ExchangeRateStore rates)
    {
        InitializeComponent(); _store = store; _rates = rates; CustomChrome.Attach(this, "SETTINGS", _settings); ThemeBox.SelectionChanged += Theme_Changed; MascotDefaultBox.SelectionChanged += Mascot_Changed;
        RateBox.Text = store.HourlyRate.ToString("0.##"); SelectItem(CurrencyBox, store.CurrencyCode); SelectItem(ThemeBox, _settings.Get("Theme", "Carbon")); SelectItem(PinnedModeBox, _settings.Get("PinnedMode", "Money")); SelectItem(TextSizeBox, _settings.Get("TextSize", _settings.Get("PinnedFontSize", "Comfortable"))); SelectItem(MascotDefaultBox, _settings.Get("MascotDefault", "Auto"));
        DailyGoal.Text = _settings.GetDouble("GoalDailyHours").ToString("0.##"); MonthlyGoal.Text = _settings.GetDouble("GoalMonthlyHours").ToString("0.##");
        PinEnabled.IsChecked = store.PinVisible; MascotEnabled.IsChecked = _settings.GetBool("MascotEnabled", true);
        ChimeEnabled.IsChecked = FocusChime.Settings.GetBool("ChimeEnabled"); ChimeInterval.Text = FocusChime.Settings.GetDouble("ChimeIntervalMinutes", 10).ToString("0"); ChimeVolume.Value = FocusChime.Settings.GetDouble("ChimeVolume", 0.75); ChimeSound.ItemsSource = FocusChime.Sounds; ChimeSound.SelectedItem = FocusChime.Settings.Get("ChimeSound", "Glass"); RadioVolume.Value = SettingStore.Shared.GetDouble("RadioVolume", 0.7); RadioPlayer.Shared.Volume = RadioVolume.Value; RadioButton.Content = RadioPlayer.Shared.IsPlaying ? "Playing" : "Play";
        _store.StatusChanged += (_, message) => Dispatcher.Invoke(() => Status.Text = message);
    }
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(RateBox.Text, out var rate) || rate < 0) { Status.Text = "Enter a valid hourly rate."; return; }
        var textSize = Item(TextSizeBox, "Comfortable"); _store.UpdateRate(rate); _store.UpdateCurrency(Item(CurrencyBox, "USD")); _settings.Set("Theme", Item(ThemeBox, "Carbon")); _settings.Set("PinnedMode", Item(PinnedModeBox, "Money")); _settings.Set("TextSize", textSize); _settings.Set("PinnedFontSize", textSize); _settings.Set("MascotDefault", Item(MascotDefaultBox, "Auto")); _settings.Set("MascotEnabled", MascotEnabled.IsChecked == true); _settings.Set("GoalDailyHours", Parse(DailyGoal.Text)); _settings.Set("GoalMonthlyHours", Parse(MonthlyGoal.Text));
        var shouldPin = PinEnabled.IsChecked == true;
        _store.SetPinned(shouldPin); FocusChime.Settings.Set("ChimeEnabled", ChimeEnabled.IsChecked == true); FocusChime.Settings.Set("ChimeIntervalMinutes", Math.Clamp(Parse(ChimeInterval.Text, 10), 1, 120)); FocusChime.Settings.Set("ChimeVolume", ChimeVolume.Value); FocusChime.Settings.Set("ChimeSound", ChimeSound.SelectedItem?.ToString() ?? "Glass"); SettingStore.Shared.Set("RadioVolume", RadioVolume.Value); RadioPlayer.Shared.Volume = RadioVolume.Value; FocusChime.Reset(); Status.Text = "Settings saved.";
    }
    private void RateSchedule_Click(object sender, RoutedEventArgs e) => new RateScheduleWindow(_store) { Owner = this }.ShowDialog();
    private void Preview_Click(object sender, RoutedEventArgs e) { Apply_Click(sender, e); FocusChime.Preview(); }
    private void Radio_Click(object sender, RoutedEventArgs e) { RadioPlayer.Shared.Volume = RadioVolume.Value; RadioPlayer.Shared.Play(); RadioButton.Content = "Playing"; }
    private void RadioStop_Click(object sender, RoutedEventArgs e) { RadioPlayer.Shared.Stop(); RadioButton.Content = "Play"; }
    private void Mascot_Changed(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsInitialized || MascotDefaultBox.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return;
        var value = item.Content?.ToString() ?? "Auto";
        var required = value switch { "Victory" => 10d, "Stretch" => 25d, "Dance" => 50d, "Music" => 100d, _ => 0d };
        if (required > 0 && _store.AllDuration() / 3600d < required)
        {
            Status.Text = $"{value} unlocks after {required:0} total hours.";
            SelectItem(MascotDefaultBox, "Auto");
        }
    }
    private void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*" }; if (dialog.ShowDialog() != true) return;
        try { new CsvPreviewWindow(_store, _store.PreviewCsv(dialog.FileName)) { Owner = this }.ShowDialog(); } catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void Paste_Click(object sender, RoutedEventArgs e) => new PasteWindow(_store) { Owner = this }.ShowDialog();
    private void Export_Click(object sender, RoutedEventArgs e) { var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON backup (*.json)|*.json", FileName = $"clockin-backup-{DateTime.Now:yyyyMMdd}.json" }; if (dialog.ShowDialog() == true) _store.ExportBackup(dialog.FileName); }
    private void Import_Click(object sender, RoutedEventArgs e) { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "JSON backup (*.json)|*.json" }; if (dialog.ShowDialog() == true) _store.ImportBackup(dialog.FileName); }
    private void Restore_Click(object sender, RoutedEventArgs e) { _store.RestoreLatestBackup(); }
    private void ClearLatest_Click(object sender, RoutedEventArgs e)
    {
        if (!_store.Sessions.Any()) { Status.Text = "There is no completed entry to clear."; return; }
        if (ClockinDialog.Confirm(this, "Clear latest entry", "Clear the latest entry so you can enter it again?", "Clear last", destructive: true)) _store.DeleteLatestSession();
    }
    private void DeleteAll_Click(object sender, RoutedEventArgs e)
    {
        if (!_store.Sessions.Any()) { Status.Text = "There are no completed entries to delete."; return; }
        if (ClockinDialog.Confirm(this, "Delete all entries", $"Delete all {_store.Sessions.Count} completed entries? This cannot be undone.", "Delete all", destructive: true)) _store.DeleteAllSessions();
    }
    private void OpenData_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("explorer.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clockin")) { UseShellExecute = true });
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Theme_Changed(object? sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (IsInitialized && ThemeBox.SelectedItem is not null) { _settings.Set("Theme", Item(ThemeBox, "Carbon")); ThemeManager.Apply(this, _settings); } }
    private static double Parse(string value, double fallback = 0) => Math.Max(0, double.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : fallback);
    private static string Item(System.Windows.Controls.ComboBox box, string fallback) => (box.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? box.Text ?? fallback;
    private static void SelectItem(System.Windows.Controls.ComboBox box, string value) => box.SelectedItem = box.Items.OfType<System.Windows.Controls.ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase));
}

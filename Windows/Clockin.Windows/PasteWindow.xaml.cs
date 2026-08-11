using System.Windows;

namespace Clockin.Windows;

public partial class PasteWindow : Window
{
    private readonly ClockStore _store;
    public PasteWindow(ClockStore store)
    {
        InitializeComponent(); _store = store; CustomChrome.Attach(this, "PASTE APPROVED TIMECARDS", SettingStore.Shared); if (System.Windows.Forms.Clipboard.ContainsText()) PasteText.Text = System.Windows.Forms.Clipboard.GetText(); UpdatePreview();
    }
    private void Paste_Click(object sender, RoutedEventArgs e) { if (System.Windows.Forms.Clipboard.ContainsText()) PasteText.Text = System.Windows.Forms.Clipboard.GetText(); }
    private void Paste_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) { if (IsInitialized) UpdatePreview(); }
    private void UpdatePreview()
    {
        var preview = _store.PreviewPastedText(PasteText.Text); var duration = preview.Sum(session => session.Duration); var approved = PastedTextImporter.ApprovedSummaryDuration(PasteText.Text);
        Summary.Text = $"{preview.Count} entries recognized · {DurationText.Compact(duration)}" + (approved is null ? "" : $"\nPage Approved: {DurationText.Compact(approved.Value)}" + (Math.Abs(approved.Value - duration) > 60 ? " · copied rows are partial — totals do not match." : " · copied rows match the page Approved total."));
        PreviewButton.IsEnabled = preview.Count > 0;
    }
    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var preview = _store.PreviewPastedText(PasteText.Text); if (preview.Count == 0) return;
        var window = new CsvPreviewWindow(_store, new CsvParseResult(preview, 0, [])) { Owner = this }; if (window.ShowDialog() == true) Close();
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}

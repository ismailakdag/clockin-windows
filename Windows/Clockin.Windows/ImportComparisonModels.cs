namespace Clockin.Windows;

public enum ImportMatchKind { New, Matched, Duplicate }

public sealed record ImportComparisonItem(WorkSession Session, ImportMatchKind Kind, WorkSession? LocalMatch = null)
{
    public string KindText => Kind switch { ImportMatchKind.New => "NEW", ImportMatchKind.Matched => "MATCHED", _ => "SKIP" };
    public string StartText => Session.Start.ToString("MMM d, yyyy HH:mm");
    public string EndText => Session.End.ToString("HH:mm");
    public string DurationText => Clockin.Windows.DurationText.Compact(Session.Duration);
    public string SourceText => Session.Source;
}

public sealed class ImportComparisonSummary
{
    public required IReadOnlyList<ImportComparisonItem> Items { get; init; }
    public int NewCount => Items.Count(item => item.Kind == ImportMatchKind.New);
    public int MatchedCount => Items.Count(item => item.Kind == ImportMatchKind.Matched);
    public int DuplicateCount => Items.Count(item => item.Kind == ImportMatchKind.Duplicate);
    public int ImportableCount => NewCount + MatchedCount;
    public double TotalDuration => Items.Sum(item => item.Session.Duration);
}

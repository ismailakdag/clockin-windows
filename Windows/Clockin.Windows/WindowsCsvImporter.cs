using System.Globalization;
using System.Text;

namespace Clockin.Windows;

public sealed record CsvParseResult(IReadOnlyList<WorkSession> Sessions, int SkippedRows, IReadOnlyList<string> Issues);

/// <summary>Matches the Mac importer: ISO-8601 start/end columns and Duration in milliseconds.</summary>
public static class WindowsCsvImporter
{
    public static CsvParseResult Parse(string text, double hourlyRate)
    {
        text = text.Replace("\uFEFF", "");
        var rows = ParseRows(text);
        if (rows.Count == 0) throw new InvalidDataException("The CSV file is empty.");
        var names = rows[0].Select(x => x.Trim().ToLowerInvariant()).ToArray();
        var startIndex = Array.IndexOf(names, "start time");
        var endIndex = Array.IndexOf(names, "end time");
        if (startIndex < 0 || endIndex < 0) throw new InvalidDataException("Start Time and End Time columns are required.");
        var durationIndex = Array.IndexOf(names, "duration");
        var notesIndex = Array.IndexOf(names, "notes");
        var sourceIndex = Array.IndexOf(names, "time sheet source");
        var sessions = new List<WorkSession>(); var issues = new List<string>(); var skipped = 0;
        foreach (var row in rows.Skip(1))
        {
            if (!TryValue(row, startIndex, out var startText) || !TryValue(row, endIndex, out var endText) || !ParseDate(startText, out var start) || !ParseDate(endText, out var end) || end < start)
            { skipped++; continue; }
            var measured = (end - start).TotalSeconds; var duration = measured;
            if (TryValue(row, durationIndex, out var rawDuration) && double.TryParse(rawDuration, NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds) && milliseconds >= 0)
            {
                var candidate = milliseconds / 1000d;
                var suspicious = candidate > TimeSpan.FromDays(1).TotalSeconds || (measured > 10 * 60 && (candidate > measured * 3 || candidate < measured / 3));
                duration = suspicious ? measured : candidate;
                if (suspicious) issues.Add($"{start:yyyy-MM-dd HH:mm}: Duration column did not match Start/End; used {DurationText.Compact(measured)} from timestamps.");
            }
            sessions.Add(new WorkSession { Start = start, End = end, Duration = duration, Note = Value(row, notesIndex), HourlyRate = hourlyRate, Source = string.IsNullOrWhiteSpace(Value(row, sourceIndex)) ? "CSV import" : Value(row, sourceIndex) });
        }
        if (sessions.Count == 0) throw new InvalidDataException("No valid time entries were found.");
        return new CsvParseResult(sessions, skipped, issues);
    }

    private static bool TryValue(IReadOnlyList<string> row, int index, out string value) { value = index >= 0 && index < row.Count ? row[index].Trim() : ""; return index >= 0 && index < row.Count; }
    private static string Value(IReadOnlyList<string> row, int index) => index >= 0 && index < row.Count ? row[index].Trim() : "";
    private static bool ParseDate(string value, out DateTime date) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date) && value.Contains('T');

    public static List<List<string>> ParseRows(string text)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n'); var rows = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else field.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (ch == '\n') { row.Add(field.ToString()); field.Clear(); if (row.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add(row); row = []; }
            else field.Append(ch);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); if (row.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add(row); }
        return rows;
    }
}

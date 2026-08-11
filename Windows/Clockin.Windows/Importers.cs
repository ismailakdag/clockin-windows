using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Clockin.Windows;

public static class PastedTextImporter
{
    private static readonly string[] Months = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    private static readonly Regex CompactRows = new(@"(?i)(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2})\s+(Approved|Submitted|Draft|Unapproved)\s+(.+?)\s+(\d{1,2}:\d{2})\s+(\d{1,2}:\d{2})(?:\s+(\d+)\s*([MH]))?", RegexOptions.Compiled);
    public static List<WorkSession> Parse(string text, double hourlyRate, out string? error)
    {
        error = null;
        var flattened = Regex.Replace(text.Replace("**", " "), @"\s+", " ").Trim();
        var result = new List<WorkSession>();
        foreach (Match match in CompactRows.Matches(flattened))
        {
            var month = Array.FindIndex(Months, x => x.Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) + 1;
            if (month == 0 || !int.TryParse(match.Groups[2].Value, out var day) || !TryTime(match.Groups[5].Value, out var start) || !TryTime(match.Groups[6].Value, out var end)) continue;
            var date = ResolveDate(month, day); var from = date.Date.Add(start); var to = date.Date.Add(end); if (to < from) to = to.AddDays(1);
            result.Add(new WorkSession { Start = from, End = to, Duration = (to - from).TotalSeconds, Note = $"{match.Groups[3].Value} • {match.Groups[4].Value}", Source = match.Groups[4].Value, HourlyRate = hourlyRate });
        }
        if (result.Count == 0)
        {
            var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            for (var i = 0; i < lines.Length; i++)
            {
                if (!TryMonthDay(lines[i], out var month, out var day)) continue;
                var times = lines.Skip(i + 1).Take(10).Select(x => TryTime(x, out var time) ? time : (TimeSpan?)null).Where(x => x is not null).Select(x => x!.Value).Take(2).ToArray();
                if (times.Length < 2) continue;
                var date = ResolveDate(month, day); var from = date.Date.Add(times[0]); var to = date.Date.Add(times[1]); if (to < from) to = to.AddDays(1);
                result.Add(new WorkSession { Start = from, End = to, Duration = (to - from).TotalSeconds, Note = "Pasted timecard", Source = "Pasted timecard", HourlyRate = hourlyRate });
            }
        }
        if (result.Count == 0) error = "No approved timecard entries were found.";
        return result;
    }
    public static double? ApprovedSummaryDuration(string text)
    {
        var match = Regex.Match(Regex.Replace(text, @"\s+", " "), @"(?i)(\d+)h\s*(\d+)m?\s+Approved\b");
        return match.Success && int.TryParse(match.Groups[1].Value, out var h) && int.TryParse(match.Groups[2].Value, out var m) ? h * 3600 + m * 60 : null;
    }
    private static DateTime ResolveDate(int month, int day)
    {
        var now = DateTime.Now; var candidate = new DateTime(now.Year, month, day);
        return candidate > now.AddDays(1) ? candidate.AddYears(-1) : candidate;
    }
    private static bool TryMonthDay(string value, out int month, out int day)
    {
        month = 0; day = 0; var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && (month = Array.FindIndex(Months, x => x.Equals(parts[0], StringComparison.OrdinalIgnoreCase)) + 1) > 0 && int.TryParse(parts[1], out day);
    }
    private static bool TryTime(string value, out TimeSpan time) => TimeSpan.TryParseExact(value.Trim(), ["h\\:mm", "hh\\:mm", "H\\:mm", "HH\\:mm"], CultureInfo.InvariantCulture, out time) && time.TotalHours < 24;
}

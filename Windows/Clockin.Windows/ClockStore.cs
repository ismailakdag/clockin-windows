using System.Text;
using System.Text.Json;

namespace Clockin.Windows;

public sealed class ClockStore
{
    private readonly string _filePath;
    private readonly string _backupDirectory;
    private readonly object _gate = new();
    public ClockinData Data { get; private set; }
    public event EventHandler? Changed;
    public event EventHandler<string>? StatusChanged;

    public ClockStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clockin", "clockin.json");
        _backupDirectory = Path.Combine(Path.GetDirectoryName(_filePath)!, "Backups");
        Data = Load() ?? new ClockinData();
        var correctedDurations = NormalizeSessionDurations();
        if (Data.RateRules is null)
        {
            Data.RateRules = [new RateRule { EffectiveFrom = new DateTime(2026, 7, 1), HourlyRate = Data.HourlyRate }];
            Save();
        }
        else CreateAutomaticBackupIfNeeded();
        if (correctedDurations) Save();
    }

    public RunningSession? Running => Data.Running;
    public IReadOnlyList<WorkSession> Sessions => Data.Sessions.OrderByDescending(x => x.Start).ToList();
    public double HourlyRate => Data.HourlyRate;
    public string CurrencyCode => Data.CurrencyCode;
    public bool PinVisible => Data.PinVisible;
    public IReadOnlyList<RateRule> RateRules => (Data.RateRules ?? []).OrderBy(x => x.EffectiveFrom).ToList();
    public double Elapsed(DateTime? at = null) => Data.Running?.Elapsed(at) ?? 0;
    public double EffectiveRate(DateTime date, double fallback) => RateRules.Where(x => x.Applies(date)).OrderByDescending(x => x.EffectiveFrom).FirstOrDefault()?.HourlyRate ?? fallback;
    public double CurrentEarnings(DateTime? at = null) { var date = at ?? DateTime.Now; return Elapsed(date) / 3600d * EffectiveRate(date, HourlyRate); }
    public double Earnings(WorkSession session) => session.Duration / 3600d * EffectiveRate(session.Start, session.HourlyRate);
    public double TotalDuration => Data.Sessions.Sum(x => x.Duration);
    public double TotalEarnings => Data.Sessions.Sum(Earnings);
    public double AllDuration(DateTime? at = null) => TotalDuration + Elapsed(at);
    public double AllEarnings(DateTime? at = null) => TotalEarnings + CurrentEarnings(at);

    public void ClockIn(double elapsed = 0, string note = "", DateTime? at = null)
    {
        if (Data.Running is not null) return;
        var date = at ?? DateTime.Now;
        var safe = Math.Max(0, elapsed);
        Data.Running = new RunningSession { Start = date.AddSeconds(-safe), Accumulated = safe, ResumedAt = date, Note = note };
        SaveAndNotify();
    }
    public void CancelRunning()
    {
        if (Data.Running is null) return;
        Data.Running = null; SaveAndNotify("Active session cancelled. No earnings were added.");
    }
    public void Pause(DateTime? at = null)
    {
        if (Data.Running is not { ResumedAt: not null } running) return;
        var date = at ?? DateTime.Now;
        running.Accumulated += Math.Max(0, (date - running.ResumedAt.Value).TotalSeconds);
        running.ResumedAt = null; SaveAndNotify();
    }
    public void Resume(DateTime? at = null)
    {
        if (Data.Running is not { ResumedAt: null } running) return;
        running.ResumedAt = at ?? DateTime.Now; SaveAndNotify();
    }
    public WorkSession? ClockOut(DateTime? at = null)
    {
        if (Data.Running is null) return null;
        var date = at ?? DateTime.Now;
        var running = Data.Running;
        var session = new WorkSession { Start = running.Start, End = date, Duration = running.Elapsed(date), Note = running.Note, HourlyRate = HourlyRate, Source = "Clockin" };
        Data.Sessions.Add(session); Data.Running = null; SaveAndNotify(); return session;
    }
    public void UpdateRate(double value)
    {
        if (value is < 0 or double.NaN or double.PositiveInfinity or double.NegativeInfinity) return;
        Data.HourlyRate = value;
        var current = RateRules.Where(x => x.Applies(DateTime.Now)).OrderByDescending(x => x.EffectiveFrom).FirstOrDefault();
        if (current is not null) current.HourlyRate = value;
        SaveAndNotify();
    }
    public bool AddRateRule(DateTime from, DateTime? until, double value, out string error)
    {
        error = ""; from = from.Date; until = until?.Date;
        if (value < 0 || double.IsNaN(value) || double.IsInfinity(value)) { error = "Rate must be a valid positive number."; return false; }
        if (until < from) { error = "Rate period end must be on or after its start."; return false; }
        if (Overlaps(from, until, null)) { error = "Rate period overlaps an existing period."; return false; }
        (Data.RateRules ??= []).Add(new RateRule { EffectiveFrom = from, EffectiveUntil = until, HourlyRate = value }); SyncCurrentRate(); SaveAndNotify(); return true;
    }
    public bool UpdateRateRule(Guid id, DateTime from, DateTime? until, double value, out string error)
    {
        error = ""; from = from.Date; until = until?.Date;
        var rule = Data.RateRules?.FirstOrDefault(x => x.Id == id);
        if (rule is null) { error = "Rate rule not found."; return false; }
        if (until < from) { error = "Rate period end must be on or after its start."; return false; }
        if (Overlaps(from, until, id)) { error = "Rate period overlaps an existing period."; return false; }
        rule.EffectiveFrom = from; rule.EffectiveUntil = until; rule.HourlyRate = value; SyncCurrentRate(); SaveAndNotify(); return true;
    }
    public void DeleteRateRule(Guid id)
    {
        if ((Data.RateRules?.Count ?? 0) <= 1) return;
        Data.RateRules!.RemoveAll(x => x.Id == id); SyncCurrentRate(); SaveAndNotify();
    }
    public void UpdateCurrency(string value) { Data.CurrencyCode = string.IsNullOrWhiteSpace(value) ? "USD" : value.Trim().ToUpperInvariant(); SaveAndNotify(); }
    public void SetPinned(bool value) { Data.PinVisible = value; SaveAndNotify(); }

    public void ImportSessions(IEnumerable<WorkSession> imported)
    {
        var incoming = imported.ToList();
        foreach (var session in incoming) NormalizeSessionDuration(session);
        var fresh = new List<WorkSession>(); var matched = 0;
        foreach (var session in incoming)
        {
            var key = Key(session);
            var duplicate = Data.Sessions.FirstOrDefault(x => Key(x) == key);
            if (duplicate is not null)
            {
                if (session.Source != "Clockin" && duplicate.Source == "Clockin" && duplicate.MatchedExternalSource is null) { duplicate.MatchedExternalSource = session.Source; matched++; }
                continue;
            }
            if (fresh.Any(x => Key(x) == key)) continue;
            var local = FindClockinMatch(session);
            if (session.Source != "Clockin" && local is not null) { local.MatchedExternalSource = session.Source; matched++; }
            else fresh.Add(session);
        }
        Data.Sessions.AddRange(fresh); SaveAndNotify(fresh.Count == 0 && matched == 0 ? "All entries were already imported." : $"Imported {fresh.Count}{(matched > 0 ? $", matched {matched} with Clockin" : "")}. ");
    }
    public ImportComparisonSummary CompareImportedSessions(IEnumerable<WorkSession> imported)
    {
        var seen = new HashSet<string>();
        var items = imported.Select(session =>
        {
            var key = Key(session);
            if (Data.Sessions.Any(existing => Key(existing) == key) || !seen.Add(key)) return new ImportComparisonItem(session, ImportMatchKind.Duplicate);
            var match = session.Source != "Clockin" ? FindClockinMatch(session) : null;
            return match is null ? new ImportComparisonItem(session, ImportMatchKind.New) : new ImportComparisonItem(session, ImportMatchKind.Matched, match);
        }).ToList();
        return new ImportComparisonSummary { Items = items };
    }
    public void DeleteSession(Guid id) { if (Data.Sessions.RemoveAll(x => x.Id == id) > 0) SaveAndNotify("Session deleted."); }
    public bool DeleteLatestSession()
    {
        var latest = Data.Sessions.OrderByDescending(x => x.End).FirstOrDefault();
        if (latest is null) return false;
        Data.Sessions.RemoveAll(x => x.Id == latest.Id);
        SaveAndNotify("Latest entry cleared. You can enter it again.");
        return true;
    }
    public bool DeleteAllSessions()
    {
        if (Data.Sessions.Count == 0) return false;
        Data.Sessions.Clear();
        SaveAndNotify("All completed entries deleted.");
        return true;
    }
    public double DurationOn(DateTime date) => Data.Sessions.Where(x => x.Start.Date == date.Date).Sum(x => x.Duration) + (Data.Running?.Start.Date == date.Date ? Elapsed() : 0);
    public double EarningsOn(DateTime date) => Data.Sessions.Where(x => x.Start.Date == date.Date).Sum(Earnings) + (Data.Running?.Start.Date == date.Date ? CurrentEarnings() : 0);
    public double MonthDuration(DateTime date) => Data.Sessions.Where(x => x.Start.Year == date.Year && x.Start.Month == date.Month).Sum(x => x.Duration) + (Data.Running?.Start.Year == date.Year && Data.Running.Start.Month == date.Month ? Elapsed(date) : 0);
    public double MonthEarnings(DateTime date) => Data.Sessions.Where(x => x.Start.Year == date.Year && x.Start.Month == date.Month).Sum(Earnings) + (Data.Running?.Start.Year == date.Year && Data.Running.Start.Month == date.Month ? CurrentEarnings(date) : 0);

    public List<WorkSession> PreviewPastedText(string text) => PastedTextImporter.Parse(text, HourlyRate, out _);
    public void ImportPastedText(string text)
    {
        var parsed = PastedTextImporter.Parse(text, HourlyRate, out var error);
        if (parsed.Count == 0) { StatusChanged?.Invoke(this, error ?? "No entries found."); return; }
        ImportSessions(parsed);
    }
    public CsvParseResult PreviewCsv(string path) => WindowsCsvImporter.Parse(File.ReadAllText(path), HourlyRate);
    public void ImportCsv(string path)
    {
        try { ImportSessions(WindowsCsvImporter.Parse(File.ReadAllText(path), HourlyRate).Sessions); }
        catch (Exception ex) { StatusChanged?.Invoke(this, ex.Message); }
    }
    public void ExportBackup(string path) { File.Copy(_filePath, path, true); StatusChanged?.Invoke(this, "Backup exported."); }
    public void ImportBackup(string path)
    {
        try
        {
            var data = JsonSerializer.Deserialize<ClockinData>(File.ReadAllText(path), ClockinJson.Options) ?? throw new InvalidDataException("Invalid Clockin backup.");
            if (data.Sessions.Any(x => x.Duration < 0 || x.End < x.Start)) throw new InvalidDataException("Backup contains invalid sessions.");
            Data = data; SaveAndNotify("Backup restored.");
        }
        catch (Exception ex) { StatusChanged?.Invoke(this, $"Could not restore backup: {ex.Message}"); }
    }
    public DateTime? LatestBackupDate => Directory.Exists(_backupDirectory) ? Directory.EnumerateFiles(_backupDirectory).Select(File.GetLastWriteTime).OrderByDescending(x => x).FirstOrDefault() : null;
    public int BackupCount => Directory.Exists(_backupDirectory) ? Directory.EnumerateFiles(_backupDirectory).Count() : 0;
    public void RestoreLatestBackup()
    {
        var latest = Directory.Exists(_backupDirectory) ? Directory.EnumerateFiles(_backupDirectory).OrderByDescending(File.GetLastWriteTime).FirstOrDefault() : null;
        if (latest is null) StatusChanged?.Invoke(this, "No automatic backup exists yet."); else ImportBackup(latest);
    }

    private ClockinData? Load()
    {
        try { return File.Exists(_filePath) ? JsonSerializer.Deserialize<ClockinData>(File.ReadAllText(_filePath), ClockinJson.Options) : null; } catch { return null; }
    }
    private bool NormalizeSessionDurations()
    {
        var changed = false;
        foreach (var session in Data.Sessions) changed |= NormalizeSessionDuration(session);
        return changed;
    }
    private static bool NormalizeSessionDuration(WorkSession session)
    {
        var measured = (session.End - session.Start).TotalSeconds;
        if (double.IsNaN(session.Duration) || double.IsInfinity(session.Duration) || session.Duration < 0)
        {
            session.Duration = Math.Max(0, measured); return true;
        }
        if (measured >= 0 && measured <= TimeSpan.FromDays(1).TotalSeconds && session.Duration > TimeSpan.FromDays(1).TotalSeconds)
        {
            session.Duration = measured; return true;
        }
        return false;
    }
    private void SaveAndNotify(string? message = null) { Save(); if (message is not null) StatusChanged?.Invoke(this, message); Changed?.Invoke(this, EventArgs.Empty); }
    private void Save()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(Data, ClockinJson.Options), Encoding.UTF8);
        }
    }
    private void CreateAutomaticBackupIfNeeded()
    {
        if (!File.Exists(_filePath)) return;
        Directory.CreateDirectory(_backupDirectory);
        if (Directory.EnumerateFiles(_backupDirectory).Any(x => File.GetLastWriteTime(x).Date == DateTime.Now.Date)) return;
        File.Copy(_filePath, Path.Combine(_backupDirectory, $"clockin-{DateTime.Now:yyyyMMdd-HHmmss}.json"), true);
        foreach (var file in Directory.EnumerateFiles(_backupDirectory).OrderByDescending(File.GetLastWriteTime).Skip(30)) File.Delete(file);
    }
    private bool Overlaps(DateTime start, DateTime? end, Guid? excluded)
    {
        if (end is null) return false;
        return RateRules.Any(rule => rule.Id != excluded && rule.EffectiveUntil is not null && start <= rule.EffectiveUntil.Value && rule.EffectiveFrom <= end.Value);
    }
    private void SyncCurrentRate()
    {
        var current = RateRules.Where(x => x.Applies(DateTime.Now)).OrderByDescending(x => x.EffectiveFrom).FirstOrDefault();
        if (current is not null) Data.HourlyRate = current.HourlyRate;
    }
    private WorkSession? FindClockinMatch(WorkSession external) => Data.Sessions.FirstOrDefault(local => local.Source == "Clockin" && local.MatchedExternalSource is null && local.Start.Date == external.Start.Date && Math.Abs((local.Start - external.Start).TotalSeconds) <= 90 && Math.Abs((local.End - external.End).TotalSeconds) <= 90 && Math.Abs(local.Duration - external.Duration) <= 120);
    private static string Key(WorkSession session) => $"{session.Start.Ticks / TimeSpan.TicksPerSecond}|{session.End.Ticks / TimeSpan.TicksPerSecond}|{(int)session.Duration}";
}

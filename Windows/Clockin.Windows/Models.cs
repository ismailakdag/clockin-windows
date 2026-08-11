using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clockin.Windows;

public sealed class WorkSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double Duration { get; set; }
    public string Note { get; set; } = "";
    public double HourlyRate { get; set; }
    public string Source { get; set; } = "Clockin";
    public string? MatchedExternalSource { get; set; }
    [JsonIgnore] public double Earnings => Duration / 3600d * HourlyRate;
}

public sealed class RateRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public double HourlyRate { get; set; }

    public bool Applies(DateTime date)
    {
        if (date < EffectiveFrom) return false;
        return EffectiveUntil is null || date.Date <= EffectiveUntil.Value.Date;
    }
}

public sealed class RunningSession
{
    public DateTime Start { get; set; }
    public double Accumulated { get; set; }
    public DateTime? ResumedAt { get; set; }
    public string Note { get; set; } = "";
    [JsonIgnore] public bool IsPaused => ResumedAt is null;
    public double Elapsed(DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        return Accumulated + (ResumedAt is null ? 0 : Math.Max(0, (now - ResumedAt.Value).TotalSeconds));
    }
}

public sealed class ClockinData
{
    public double HourlyRate { get; set; } = 25;
    public string CurrencyCode { get; set; } = "USD";
    public RunningSession? Running { get; set; }
    public List<WorkSession> Sessions { get; set; } = [];
    public bool PinVisible { get; set; }
    public List<RateRule>? RateRules { get; set; }
}

public static class DurationText
{
    public static string Clock(double seconds)
    {
        var value = Math.Max(0L, (long)Math.Min(seconds, TimeSpan.FromDays(3650).TotalSeconds));
        return $"{value / 3600:00}:{value % 3600 / 60:00}:{value % 60:00}";
    }

    public static string Compact(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "—";
        var minutes = Math.Max(0L, (long)Math.Min(seconds / 60, TimeSpan.FromDays(3650).TotalMinutes));
        if (minutes < 60) return $"{minutes}m";
        var hours = minutes / 60;
        var remainder = minutes % 60;
        return remainder == 0 ? $"{hours}h" : $"{hours}h {remainder}m";
    }

    public static string Hours(double hours)
    {
        var minutes = Math.Max(0L, (long)Math.Min(Math.Round(hours * 60), TimeSpan.FromDays(3650).TotalMinutes));
        return $"{minutes / 60}h {minutes % 60}m";
    }
}

public static class MoneyText
{
    public static string Money(double value, string code, int maxFractionDigits = 2)
    {
        var culture = CultureInfo.GetCultureInfo(code == "TRY" ? "tr-TR" : "en-US");
        var format = code == "TRY" ? "₺" : code == "USD" ? "$" : code + " ";
        return format + value.ToString("N" + Math.Max(0, maxFractionDigits), culture);
    }
}

/// <summary>Reads both Swift JSONEncoder date values (seconds since 2001) and ISO dates.</summary>
public sealed class SwiftDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly DateTime Reference = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return Reference.AddSeconds(reader.GetDouble()).ToLocalTime();
        if (reader.TokenType == JsonTokenType.String && DateTime.TryParse(reader.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value))
            return value.ToLocalTime();
        throw new JsonException("Invalid date");
    }
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToUniversalTime().ToString("O"));
}

public sealed class NullableSwiftDateTimeConverter : JsonConverter<DateTime?>
{
    private readonly SwiftDateTimeConverter _inner = new();
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType == JsonTokenType.Null ? null : _inner.Read(ref reader, typeof(DateTime), options);
    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue(); else _inner.Write(writer, value.Value, options);
    }
}

public static class ClockinJson
{
    public static JsonSerializerOptions Options => new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new SwiftDateTimeConverter(), new NullableSwiftDateTimeConverter() }
    };
}

public sealed class SettingStore
{
    public static SettingStore Shared { get; } = new();
    private static readonly string Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clockin", "settings.json");
    private readonly Dictionary<string, string> _values = [];
    public event EventHandler? Changed;
    public SettingStore()
    {
        try { if (File.Exists(Path)) _values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path)) ?? []; } catch { }
    }
    public string Get(string key, string fallback = "") => _values.TryGetValue(key, out var value) ? value : fallback;
    public bool GetBool(string key, bool fallback = false) => bool.TryParse(Get(key), out var value) ? value : fallback;
    public double GetDouble(string key, double fallback = 0) => double.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    public void Set(string key, string value)
    {
        if (_values.TryGetValue(key, out var current) && string.Equals(current, value, StringComparison.Ordinal)) return;
        _values[key] = value;
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Set(string key, bool value) => Set(key, value.ToString());
    public void Set(string key, double value) => Set(key, value.ToString(CultureInfo.InvariantCulture));
    private void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true }));
    }
}

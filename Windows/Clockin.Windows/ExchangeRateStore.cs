using System.Net.Http;
using System.Text.Json;

namespace Clockin.Windows;

public sealed class ExchangeRateStore
{
    private readonly string _cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clockin", "usd-try-rates.json");
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    public Dictionary<string, double> RatesByDay { get; } = [];
    public string? LatestDate => RatesByDay.Keys.OrderByDescending(x => x).FirstOrDefault();
    public double? LatestRate => LatestDate is { } key && RatesByDay.TryGetValue(key, out var value) ? value : null;
    public bool IsLoading { get; private set; }
    public bool LiveCheckFailed { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? LastSuccessfulCheck { get; private set; }
    public event EventHandler? Changed;

    public ExchangeRateStore()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                var saved = JsonSerializer.Deserialize<RateCache>(File.ReadAllText(_cachePath));
                if (saved is not null) { foreach (var pair in saved.Rates) RatesByDay[pair.Key] = pair.Value; LastSuccessfulCheck = saved.Updated; }
            }
        }
        catch { }
    }
    public double? RateOn(DateTime date)
    {
        var key = date.ToString("yyyy-MM-dd");
        return RatesByDay.TryGetValue(key, out var exact) ? exact : RatesByDay.Where(x => string.CompareOrdinal(x.Key, key) <= 0).OrderByDescending(x => x.Key).Select(x => (double?)x.Value).FirstOrDefault();
    }
    public async Task RefreshAsync(IEnumerable<DateTime> dates)
    {
        var requested = dates.Select(x => x.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList();
        var missing = requested.Where(x => !RatesByDay.ContainsKey(x)).ToList();
        if (LastSuccessfulCheck is not null && DateTime.Now - LastSuccessfulCheck.Value < TimeSpan.FromHours(1) && missing.Count == 0) return;
        IsLoading = true; LiveCheckFailed = false; Changed?.Invoke(this, EventArgs.Empty);
        try
        {
            var latest = await FetchAsync(null);
            if (latest is not null) { RatesByDay[latest.Value.actualDay] = latest.Value.rate; LastSuccessfulCheck = DateTime.Now; }
            else LiveCheckFailed = true;
            foreach (var day in missing)
            {
                var result = await FetchAsync(day);
                if (result is not null) { RatesByDay[day] = result.Value.rate; RatesByDay[result.Value.actualDay] = result.Value.rate; }
            }
            ErrorMessage = RatesByDay.Count == 0 ? "Exchange rate unavailable" : LiveCheckFailed ? "Live check failed — showing cached rate" : missing.Any(x => !RatesByDay.ContainsKey(x)) ? "Some historical rates are still updating" : null;
            SaveCache();
        }
        finally { IsLoading = false; Changed?.Invoke(this, EventArgs.Empty); }
    }
    private async Task<(string actualDay, double rate)?> FetchAsync(string? requestedDay)
    {
        var url = "https://api.frankfurter.dev/v2/rate/USD/TRY" + (requestedDay is null ? "" : $"?date={requestedDay}");
        try
        {
            using var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            var payload = await response.Content.ReadFromJsonAsync<SingleRateResponse>();
            return payload is null ? null : (payload.Date, payload.Rate);
        }
        catch { return null; }
    }
    private void SaveCache()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
        File.WriteAllText(_cachePath, JsonSerializer.Serialize(new RateCache(RatesByDay, LastSuccessfulCheck), new JsonSerializerOptions { WriteIndented = true }));
    }
    private sealed record RateCache(Dictionary<string, double> Rates, DateTime? Updated);
    private sealed class SingleRateResponse { public string Date { get; set; } = ""; public double Rate { get; set; } }
}

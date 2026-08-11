using System.Media;
using System.Windows.Threading;

namespace Clockin.Windows;

public static class FocusChime
{
    private static ClockStore? _store; private static DispatcherTimer? _timer; private static DateTime? _trackedStart; private static int _lastBucket; private static SoundPlayer? _player; private static MemoryStream? _audio;
    public static readonly string[] Sounds = ["Glass", "Ping", "Pop", "Tink", "Funk", "Submarine", "Sosumi"];
    public static SettingStore Settings { get; } = SettingStore.Shared;
    public static void Start(ClockStore store) { _store = store; Reset(); _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; _timer.Tick += (_, _) => Tick(); _timer.Start(); }
    public static void Stop() { _timer?.Stop(); _timer = null; _player?.Stop(); _audio?.Dispose(); }
    public static void Reset() { _trackedStart = _store?.Running?.Start; _lastBucket = (int)(_store?.Elapsed() ?? 0) / IntervalSeconds; }
    public static void Preview() => Play();
    private static void Tick() { if (!Settings.GetBool("ChimeEnabled") || _store?.Running is null) return; if (_trackedStart != _store.Running.Start) { Reset(); return; } if (_store.Running.IsPaused) return; var bucket = (int)_store.Elapsed() / IntervalSeconds; if (bucket > _lastBucket && bucket > 0) { _lastBucket = bucket; Play(); } }
    private static int IntervalSeconds => Math.Clamp((int)Settings.GetDouble("ChimeIntervalMinutes", 10), 1, 120) * 60;
    private static void Play()
    {
        var sound = Settings.Get("ChimeSound", "Glass"); var volume = Math.Clamp(Settings.GetDouble("ChimeVolume", 0.75), 0.1, 1); var tones = sound switch { "Ping" => new[] { 988d }, "Pop" => new[] { 220d, 330d }, "Tink" => new[] { 1760d }, "Funk" => new[] { 440d, 550d, 660d }, "Submarine" => new[] { 110d, 82d }, "Sosumi" => new[] { 660d, 880d, 1320d }, _ => new[] { 880d, 1318d } }; _audio?.Dispose(); _audio = BuildWave(tones, volume); _player = new SoundPlayer(_audio); _player.Play();
    }
    private static MemoryStream BuildWave(IReadOnlyList<double> frequencies, double volume)
    {
        const int sampleRate = 44100; const int toneSamples = sampleRate / 8; const int gapSamples = sampleRate / 40; var samples = new List<short>();
        foreach (var frequency in frequencies) { for (var i = 0; i < toneSamples; i++) { var envelope = Math.Min(1, Math.Min(i / 500d, (toneSamples - i) / 1800d)); samples.Add((short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * short.MaxValue * volume * envelope)); } samples.AddRange(Enumerable.Repeat((short)0, gapSamples)); }
        var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true); var bytes = samples.Count * 2; writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate); writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes); foreach (var sample in samples) writer.Write(sample); stream.Position = 0; return stream;
    }
}

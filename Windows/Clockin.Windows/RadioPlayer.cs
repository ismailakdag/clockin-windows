using System.Windows.Media;

namespace Clockin.Windows;
public sealed class RadioPlayer
{
    public static RadioPlayer Shared { get; } = new();
    private readonly MediaPlayer _player = new();
    public bool IsPlaying { get; private set; }
    public double Volume { get => _player.Volume; set => _player.Volume = Math.Clamp(value, 0, 1); }
    public string StationName { get; private set; } = "Radio Paradise";
    public void Play(string station = "Radio Paradise")
    {
        StationName = station; _player.Open(new Uri("https://stream.radioparadise.com/aac-320")); _player.Play(); IsPlaying = true;
    }
    public void Stop() { _player.Stop(); IsPlaying = false; }
}

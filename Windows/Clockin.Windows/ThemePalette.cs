using System.Windows.Media;

namespace Clockin.Windows;

public sealed record ThemePalette(string Name, string Background, string Accent, string Secondary, string Muted, string Text, string ActionForeground, string Card, string Stroke)
{
    public string FontFamily => Name switch
    {
        "Data Dense" or "Terminal Amber" => "Cascadia Mono",
        "Neon Orange" or "Synthwave" => "Aptos Display",
        _ => "Segoe UI Variable Display"
    };
    public static ThemePalette For(string? name) => name switch
    {
        "Neon Orange" => new(name!, "#130B06", "#FF6E14", "#FFC52E", "#B49A82", "#FFF4E8", "#1B0A02", "#21140D", "#533222"),
        "Electric Blue" => new(name!, "#071326", "#35AEFF", "#63F1FF", "#8FA9C5", "#EEF7FF", "#04101B", "#0D1D35", "#234B73"),
        "Synthwave" => new(name!, "#160722", "#FF3DB5", "#42F2FF", "#B99CC6", "#FFF0FE", "#210617", "#271039", "#68316B"),
        "Data Dense" => new(name!, "#050806", "#B7FF2E", "#B9C7BC", "#819187", "#F0FFF2", "#0A1105", "#101A13", "#2E4535"),
        "Aurora" => new(name!, "#062022", "#49FFD2", "#6BC7FF", "#92B6BD", "#EEFFFF", "#061816", "#0C2A2D", "#205258"),
        "Terminal Amber" => new(name!, "#110D04", "#FFD329", "#FF792A", "#C2A875", "#FFF8DD", "#1C1200", "#241B08", "#594622"),
        "Daylight" => new(name!, "#F0F4FB", "#2057C7", "#7045B8", "#647086", "#172236", "#FFFFFF", "#FFFFFF", "#C7D0DF"),
        _ => new("Carbon", "#0E1118", "#59F29A", "#67DDEB", "#93A0B5", "#EEF2F8", "#07120D", "#171D29", "#2B3548")
    };

    public System.Windows.Media.Color Color(string hex) => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
}

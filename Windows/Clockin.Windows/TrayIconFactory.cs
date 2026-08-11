using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Clockin.Windows;

internal static class TrayIconFactory
{
    public static Icon Create(ThemePalette theme)
    {
        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var background = ColorTranslator.FromHtml(theme.Background);
        var accent = ColorTranslator.FromHtml(theme.Accent);
        var text = ColorTranslator.FromHtml(theme.Text);
        using var face = new SolidBrush(Color.FromArgb(245, background));
        using var ring = new Pen(accent, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var hands = new Pen(text, 2.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var center = new SolidBrush(accent);

        graphics.FillEllipse(face, 4, 4, 24, 24);
        graphics.DrawEllipse(ring, 4.5f, 4.5f, 23, 23);
        graphics.DrawLine(hands, 16, 16, 16, 9.5f);
        graphics.DrawLine(hands, 16, 16, 22.2f, 16);
        graphics.FillEllipse(center, 14.2f, 14.2f, 3.6f, 3.6f);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}

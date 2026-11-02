using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DeskRhythm.Infrastructure;

namespace DeskRhythm.Tray;

internal enum TrayGlyph
{
    Stopped,
    Running,
    Due,
    Paused,
}

/// <summary>
/// Draws the notification-area icon: a ring that fills as the phase goes on.
/// A solid disc means "action due"; two bars mean "paused". Simple shapes stay
/// readable at 16 px.
/// </summary>
internal static class TrayIconRenderer
{
    public static Icon Render(int size, TrayGlyph glyph, Color accent, double progress, bool darkTaskbar)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            var stroke = Math.Max(2f, size / 7f);
            var inset = stroke / 2 + 0.5f;
            var ring = new RectangleF(inset, inset, size - 2 * inset, size - 2 * inset);
            var track = darkTaskbar ? Color.FromArgb(105, 255, 255, 255) : Color.FromArgb(80, 0, 0, 0);
            var neutral = darkTaskbar ? Color.FromArgb(235, 255, 255, 255) : Color.FromArgb(225, 32, 32, 32);

            switch (glyph)
            {
                case TrayGlyph.Running:
                {
                    using var trackPen = new Pen(track, stroke);
                    g.DrawEllipse(trackPen, ring);
                    using var arcPen = new Pen(accent, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    var sweep = (float)Math.Clamp(progress * 360, 10, 360);
                    g.DrawArc(arcPen, ring, -90, sweep);
                    FillDot(g, size, size * 0.17f, accent);
                    break;
                }

                case TrayGlyph.Due:
                {
                    using var fill = new SolidBrush(accent);
                    g.FillEllipse(fill, 0.5f, 0.5f, size - 1f, size - 1f);
                    FillDot(g, size, size * 0.15f, Color.White);
                    break;
                }

                case TrayGlyph.Paused:
                {
                    using var trackPen = new Pen(track, stroke);
                    g.DrawEllipse(trackPen, ring);
                    using var bars = new SolidBrush(accent);
                    var barWidth = Math.Max(1.5f, size * 0.13f);
                    var barHeight = size * 0.36f;
                    var gap = size * 0.1f;
                    var top = (size - barHeight) / 2;
                    g.FillRectangle(bars, size / 2f - gap / 2 - barWidth, top, barWidth, barHeight);
                    g.FillRectangle(bars, size / 2f + gap / 2, top, barWidth, barHeight);
                    break;
                }

                default:
                {
                    using var pen = new Pen(neutral, Math.Max(1.5f, stroke * 0.8f));
                    g.DrawEllipse(pen, ring);
                    break;
                }
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private static void FillDot(Graphics g, int size, float radius, Color color)
    {
        using var brush = new SolidBrush(color);
        var c = size / 2f;
        g.FillEllipse(brush, c - radius, c - radius, radius * 2, radius * 2);
    }
}

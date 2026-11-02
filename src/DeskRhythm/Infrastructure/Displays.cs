using System.Runtime.InteropServices;
using System.Windows;
using DeskRhythm.Core;

namespace DeskRhythm.Infrastructure;

/// <summary>A rectangle in physical screen pixels. Can have negative coordinates.</summary>
internal readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public static PixelRect From(NativeMethods.RECT r) => new(r.Left, r.Top, r.Right, r.Bottom);
}

internal sealed record DisplayInfo(IntPtr Handle, string DeviceName, PixelRect Bounds, PixelRect WorkArea, bool IsPrimary, uint Dpi)
{
    public int ScalePercent => (int)Math.Round(Dpi * 100.0 / 96);
}

/// <summary>
/// Reads the connected displays from Windows each time it is asked, so the list is
/// always current after a monitor is attached, removed or rearranged.
/// </summary>
internal static class Displays
{
    public static IReadOnlyList<DisplayInfo> All()
    {
        var list = new List<DisplayInfo>();
        NativeMethods.MonitorEnumProc callback = (IntPtr handle, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
        {
            if (TryGet(handle) is { } display)
            {
                list.Add(display);
            }

            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        if (list.Count == 0)
        {
            list.Add(Fallback());
        }

        // Left to right, then top to bottom: "Display 1" is the leftmost one.
        return list.OrderBy(d => d.Bounds.Left).ThenBy(d => d.Bounds.Top).ToList();
    }

    public static DisplayInfo Primary()
    {
        var all = All();
        return all.FirstOrDefault(d => d.IsPrimary) ?? all[0];
    }

    public static DisplayInfo UnderCursor()
    {
        if (NativeMethods.GetCursorPos(out var point)
            && TryGet(NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST)) is { } display)
        {
            return display;
        }

        return Primary();
    }

    public static IReadOnlyList<DisplayInfo> Select(MonitorMode mode) => mode switch
    {
        MonitorMode.MouseMonitor => [UnderCursor()],
        MonitorMode.PrimaryMonitor => [Primary()],
        _ => All(),
    };

    private static DisplayInfo? TryGet(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var info = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(handle, ref info))
        {
            return null;
        }

        var dpi = NativeMethods.GetDpiForMonitor(handle, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 ? dpiX : 96u;
        return new DisplayInfo(
            handle,
            info.szDevice ?? string.Empty,
            PixelRect.From(info.rcMonitor),
            PixelRect.From(info.rcWork),
            (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
            dpi);
    }

    private static DisplayInfo Fallback()
    {
        var area = SystemParameters.WorkArea;
        return new DisplayInfo(
            IntPtr.Zero,
            "primary",
            new PixelRect(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight),
            new PixelRect((int)area.Left, (int)area.Top, (int)area.Right, (int)area.Bottom),
            true,
            96);
    }
}

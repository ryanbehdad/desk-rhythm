using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DeskRhythm.Core;

namespace DeskRhythm.Infrastructure;

/// <summary>
/// Puts a small borderless window in a corner of one display's working area.
/// <para>
/// Mixed DPI is the hard part. A new window is created directly on its target
/// display, so it starts with that display's DPI and never has to rescale. A window
/// that already exists (the status panel) is moved there while it is still fully
/// transparent, so WPF can adopt the new DPI. Only then do we measure the window in
/// physical pixels, move it into the corner and fade it in.
/// </para>
/// </summary>
internal static class WindowPlacer
{
    public static void ShowAt(Window window, DisplayInfo display, PopupCorner corner, double marginDip, bool activate, Action? placed = null)
    {
        window.BeginAnimation(UIElement.OpacityProperty, null);
        window.Opacity = 0;

        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            // Before the window exists, WPF reads Left/Top in system-DPI units.
            var toDip = 96.0 / Math.Max(96u, NativeMethods.GetDpiForSystem());
            window.Left = (display.WorkArea.Left + display.WorkArea.Width / 2) * toDip;
            window.Top = (display.WorkArea.Top + display.WorkArea.Height / 2) * toDip;
        }

        window.Show();
        var hwnd = helper.Handle;
        if (NativeMethods.GetDpiForWindow(hwnd) != display.Dpi || !IsOn(hwnd, display))
        {
            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                display.WorkArea.Left + display.WorkArea.Width / 2,
                display.WorkArea.Top + display.WorkArea.Height / 2,
                0,
                0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!window.IsVisible)
            {
                return;
            }

            Place(window, display, corner, marginDip);
            placed?.Invoke();
            if (activate)
            {
                window.Activate();
            }

            window.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));

            // Check once more after layout has fully settled.
            window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
            {
                if (window.IsVisible)
                {
                    Place(window, display, corner, marginDip);
                }
            });
        });
    }

    public static void Place(Window window, DisplayInfo display, PopupCorner corner, double marginDip)
    {
        window.UpdateLayout();
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var dpi = NativeMethods.GetDpiForWindow(hwnd);
        var margin = (int)Math.Round(marginDip * (dpi == 0 ? display.Dpi : dpi) / 96.0);
        var area = display.WorkArea;

        var right = corner is PopupCorner.BottomRight or PopupCorner.TopRight;
        var bottom = corner is PopupCorner.BottomRight or PopupCorner.BottomLeft;
        var x = right ? area.Right - margin - width : area.Left + margin;
        var y = bottom ? area.Bottom - margin - height : area.Top + margin;

        // Never leave any part of the window outside the working area.
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - width));
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - height));

        if (x != rect.Left || y != rect.Top)
        {
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }
    }

    private static bool IsOn(IntPtr hwnd, DisplayInfo display)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var r))
        {
            return false;
        }

        var cx = (r.Left + r.Right) / 2;
        var cy = (r.Top + r.Bottom) / 2;
        return cx >= display.Bounds.Left && cx < display.Bounds.Right && cy >= display.Bounds.Top && cy < display.Bounds.Bottom;
    }
}

using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DeskRhythm.Core;
using DeskRhythm.Infrastructure;
using Microsoft.Win32;

namespace DeskRhythm.Theming;

/// <summary>Follows the Windows light / dark setting and owns the phase accent colours.</summary>
internal static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    // Key -> (light, dark)
    private static readonly (string Key, string Light, string Dark)[] Palette =
    [
        ("SurfaceBrush", "#FBFBFB", "#2B2B2B"),
        ("WindowBrush", "#F3F3F3", "#202020"),
        ("SectionBrush", "#FFFFFF", "#2B2B2B"),
        ("StrokeBrush", "#E3E3E3", "#3D3D3D"),
        ("TextBrush", "#1B1B1B", "#F3F3F3"),
        ("SubtleTextBrush", "#5C5C5C", "#C5C5C5"),
        ("ControlBrush", "#FFFFFF", "#373737"),
        ("ControlHoverBrush", "#F2F2F2", "#404040"),
        ("ControlStrokeBrush", "#D2D2D2", "#4D4D4D"),
        ("TrackBrush", "#DADADA", "#4F4F4F"),
        ("FocusBrush", "#1B1B1B", "#FFFFFF"),
        ("ErrorBrush", "#B3261E", "#FF9A8F"),
    ];

    public static bool IsDark { get; private set; }

    /// <summary>The taskbar can be dark while apps are light. The tray icon follows the taskbar.</summary>
    public static bool IsTaskbarDark { get; private set; }

    public static void Apply()
    {
        IsDark = ReadDword("AppsUseLightTheme", 1) == 0;
        IsTaskbarDark = ReadDword("SystemUsesLightTheme", 0) == 0;

        var resources = Application.Current.Resources;
        foreach (var (key, light, dark) in Palette)
        {
            resources[key] = Brush(IsDark ? dark : light);
        }

        resources["AccentBrush"] = Brush(Accent(Phase.Sitting));
        resources["AccentSoftBrush"] = Brush(AccentSoft(Phase.Sitting));
        resources["OnAccentBrush"] = Brush(OnAccent);
    }

    /// <summary>Muted blue for sitting, teal for standing, warm amber for moving.</summary>
    public static Color Accent(Phase phase) => Parse(phase switch
    {
        Phase.Sitting => IsDark ? "#8AB4E8" : "#3D6FA5",
        Phase.Standing => IsDark ? "#6FCFB5" : "#227564",
        _ => IsDark ? "#F0B865" : "#95570F",
    });

    public static Color AccentSoft(Phase phase) => Parse(phase switch
    {
        Phase.Sitting => IsDark ? "#2C3A4B" : "#E4EDF7",
        Phase.Standing => IsDark ? "#243F39" : "#DDF1EB",
        _ => IsDark ? "#45381F" : "#FAEBD6",
    });

    public static Color OnAccent => Parse(IsDark ? "#1A1A1A" : "#FFFFFF");

    /// <summary>Accent shades that read well on both a light and a dark taskbar.</summary>
    public static System.Drawing.Color TrayAccent(Phase phase) => phase switch
    {
        Phase.Sitting => System.Drawing.Color.FromArgb(0x4A, 0x86, 0xC5),
        Phase.Standing => System.Drawing.Color.FromArgb(0x2F, 0xA5, 0x8A),
        _ => System.Drawing.Color.FromArgb(0xE0, 0x9A, 0x3A),
    };

    /// <summary>Give one window (and everything in it) the colours of a phase.</summary>
    public static void ApplyAccent(FrameworkElement element, Phase phase)
    {
        element.Resources["AccentBrush"] = Brush(Accent(phase));
        element.Resources["AccentSoftBrush"] = Brush(AccentSoft(phase));
        element.Resources["OnAccentBrush"] = Brush(OnAccent);
    }

    /// <summary>Dark title bar for normal windows in dark mode.</summary>
    public static void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var value = IsDark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private static SolidColorBrush Brush(string hex) => Brush(Parse(hex));

    private static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static int ReadDword(string name, int fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int value ? value : fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}

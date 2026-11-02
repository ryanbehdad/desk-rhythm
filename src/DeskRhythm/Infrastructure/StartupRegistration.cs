using Microsoft.Win32;

namespace DeskRhythm.Infrastructure;

/// <summary>
/// "Launch at Windows sign-in" through the per-user Run key. No administrator
/// rights are needed. The entry also shows in Task Manager › Startup apps, where
/// the user can turn it off.
/// </summary>
internal static class StartupRegistration
{
    public const string StartupArgument = "--startup";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DeskRhythm";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, Command());
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>If the exe was moved, point the existing entry at the new location.</summary>
    public static void RefreshPathIfEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is string current && !string.Equals(current, Command(), StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(ValueName, Command());
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            ErrorLog.Write(ex, "Startup registration");
        }
    }

    private static string Command() => $"\"{Environment.ProcessPath}\" {StartupArgument}";
}

namespace DeskRhythm.Infrastructure;

/// <summary>All files live in %LOCALAPPDATA%\DeskRhythm. Nothing leaves the machine.</summary>
internal static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskRhythm");

    public static string Settings => Path.Combine(DataDirectory, "settings.json");

    public static string State(bool testMode) => Path.Combine(DataDirectory, testMode ? "state.test.json" : "state.json");

    public static string ErrorLog => Path.Combine(DataDirectory, "error.log");
}

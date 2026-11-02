namespace DeskRhythm.Core;

public enum MonitorMode
{
    AllMonitors,
    MouseMonitor,
    PrimaryMonitor,
}

public enum PopupCorner
{
    BottomRight,
    BottomLeft,
    TopRight,
    TopLeft,
}

/// <summary>User settings. Stored as JSON. Out-of-range values are corrected by <see cref="Normalize"/>.</summary>
public sealed class AppSettings
{
    public const int SittingMin = 5, SittingMax = 120;
    public const int StandingMin = 1, StandingMax = 60;
    public const int MovingMin = 1, MovingMax = 15;
    public const int RepromptMin = 1, RepromptMax = 30;
    public const int IdleMin = 2, IdleMax = 60;
    public const int SnoozeMin = 1, SnoozeMax = 120;

    public int SittingMinutes { get; set; } = 30;

    public int StandingMinutes { get; set; } = 15;

    public int MovingMinutes { get; set; } = 2;

    public MonitorMode PopupMonitors { get; set; } = MonitorMode.AllMonitors;

    public PopupCorner PopupCorner { get; set; } = PopupCorner.BottomRight;

    public bool SoundEnabled { get; set; }

    public int RepromptMinutes { get; set; } = 5;

    /// <summary>The first value is the one-click snooze. All values are in the "More" menu.</summary>
    public int[] SnoozeMinutes { get; set; } = [5, 10, 15];

    public bool PauseOnLockOrSleep { get; set; } = true;

    public bool PauseWhenIdle { get; set; }

    public int IdleMinutes { get; set; } = 10;

    public int MinutesFor(Phase phase) => phase switch
    {
        Phase.Sitting => SittingMinutes,
        Phase.Standing => StandingMinutes,
        _ => MovingMinutes,
    };

    public AppSettings Clone()
    {
        var copy = (AppSettings)MemberwiseClone();
        copy.SnoozeMinutes = [.. SnoozeMinutes];
        return copy;
    }

    /// <summary>Clamp every value into its valid range. Returns this instance.</summary>
    public AppSettings Normalize()
    {
        SittingMinutes = Math.Clamp(SittingMinutes, SittingMin, SittingMax);
        StandingMinutes = Math.Clamp(StandingMinutes, StandingMin, StandingMax);
        MovingMinutes = Math.Clamp(MovingMinutes, MovingMin, MovingMax);
        RepromptMinutes = Math.Clamp(RepromptMinutes, RepromptMin, RepromptMax);
        IdleMinutes = Math.Clamp(IdleMinutes, IdleMin, IdleMax);

        if (!Enum.IsDefined(PopupMonitors))
        {
            PopupMonitors = MonitorMode.AllMonitors;
        }

        if (!Enum.IsDefined(PopupCorner))
        {
            PopupCorner = PopupCorner.BottomRight;
        }

        SnoozeMinutes = NormalizeSnooze(SnoozeMinutes);
        return this;
    }

    /// <summary>
    /// Parse text like "5, 10, 15". Returns null if the text is not 1 to 3 whole numbers in range.
    /// </summary>
    public static int[]? TryParseSnooze(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split([',', ';', ' ', '/'], StringSplitOptions.RemoveEmptyEntries);
        var values = new List<int>();
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var value) || value < SnoozeMin || value > SnoozeMax)
            {
                return null;
            }

            values.Add(value);
        }

        var result = values.Distinct().ToArray();
        return result.Length is >= 1 and <= 3 ? result.Order().ToArray() : null;
    }

    private static int[] NormalizeSnooze(int[]? values)
    {
        var clean = (values ?? [])
            .Where(v => v is >= SnoozeMin and <= SnoozeMax)
            .Distinct()
            .Order()
            .Take(3)
            .ToArray();
        return clean.Length == 0 ? [5, 10, 15] : clean;
    }
}

using System.Globalization;
using DeskRhythm.Core;

namespace DeskRhythm;

/// <summary>All the words the UI uses for phases and durations, in one place.</summary>
internal static class PhaseText
{
    public static string Name(Phase phase) => phase switch
    {
        Phase.Sitting => "Sitting",
        Phase.Standing => "Standing",
        _ => "Moving",
    };

    public static string Gerund(Phase phase) => Name(phase).ToLowerInvariant();

    /// <summary>Reminder title when <paramref name="phase"/> ends and <paramref name="next"/> comes after it.</summary>
    public static string DueTitle(Phase phase, Phase next) => (phase, next) switch
    {
        (Phase.Sitting, Phase.Standing) => "Time to stand",
        (Phase.Sitting, _) => "Time for a break",
        (Phase.Standing, _) => "Time to move",
        _ => "Ready to sit?",
    };

    /// <summary>What the user should do when <paramref name="phase"/> ends ("it is time to ...").</summary>
    public static string DueAction(Phase phase, Phase next) => (phase, next) switch
    {
        (Phase.Sitting, Phase.Standing) => "stand",
        (Phase.Sitting, _) => "get up and take a short walk",
        (Phase.Standing, _) => "take a short walk",
        _ => "sit down again",
    };

    /// <summary>The button the user presses after changing position.</summary>
    public static string AcknowledgeLabel(Phase phase, Phase next) => (phase, next) switch
    {
        (Phase.Sitting, Phase.Standing) => "I'm standing",
        (Phase.Sitting, _) => "Start break",
        (Phase.Standing, _) => "Start walk",
        _ => "I'm sitting",
    };

    /// <summary>Label of the switch that turns the standing desk on or off.</summary>
    public static string DeskModeSwitchLabel(bool hasStandingDesk) =>
        hasStandingDesk ? "No standing desk here" : "I have a standing desk here";

    public static string SwitchLabel(Phase phase) => phase switch
    {
        Phase.Sitting => "I'm sitting now",
        Phase.Standing => "I'm standing now",
        _ => "I'm walking now",
    };

    /// <summary>"30:00", "4:05", "1:02:03". Rounds up, so a fresh 30-minute phase shows 30:00.</summary>
    public static string Clock(TimeSpan time)
    {
        var seconds = (long)Math.Ceiling(Math.Max(0, time.TotalSeconds));
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }

    /// <summary>"30 minutes", "1 minute", "10 seconds", "2 hours".</summary>
    public static string Long(TimeSpan time)
    {
        if (time < TimeSpan.FromMinutes(1))
        {
            var s = (int)Math.Round(time.TotalSeconds);
            return s == 1 ? "1 second" : $"{s} seconds";
        }

        if (time.TotalMinutes >= 60 && time.Minutes == 0 && time.Seconds == 0)
        {
            var h = (int)time.TotalHours;
            return h == 1 ? "1 hour" : $"{h} hours";
        }

        var m = (int)Math.Round(time.TotalMinutes);
        return m == 1 ? "1 minute" : $"{m} minutes";
    }

    /// <summary>"5 min", "1 hour", "2 hours", "30 s".</summary>
    public static string Short(TimeSpan time)
    {
        if (time < TimeSpan.FromMinutes(1))
        {
            return $"{(int)Math.Round(time.TotalSeconds)} s";
        }

        if (time.TotalMinutes >= 60 && time.Minutes == 0)
        {
            var h = (int)time.TotalHours;
            return h == 1 ? "1 hour" : $"{h} hours";
        }

        return $"{(int)Math.Round(time.TotalMinutes)} min";
    }

    /// <summary>"2-minute", "10-second" (as in "a 2-minute walk").</summary>
    public static string Adjective(TimeSpan time) => time < TimeSpan.FromMinutes(1)
        ? $"{(int)Math.Round(time.TotalSeconds)}-second"
        : $"{(int)Math.Round(time.TotalMinutes)}-minute";

    /// <summary>Local wall-clock time, for example "2:30 PM".</summary>
    public static string At(TimeSpan fromNow) => DateTime.Now.Add(fromNow).ToString("t", CultureInfo.CurrentCulture);
}

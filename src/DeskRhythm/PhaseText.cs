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

    /// <summary>Reminder title when <paramref name="phase"/> ends.</summary>
    public static string DueTitle(Phase phase) => phase switch
    {
        Phase.Sitting => "Time to stand",
        Phase.Standing => "Time to move",
        _ => "Ready to sit?",
    };

    /// <summary>What the user should do when <paramref name="phase"/> ends ("it is time to ...").</summary>
    public static string DueAction(Phase phase) => phase switch
    {
        Phase.Sitting => "stand",
        Phase.Standing => "take a short walk",
        _ => "sit down again",
    };

    /// <summary>The button the user presses after changing position.</summary>
    public static string AcknowledgeLabel(Phase phase) => phase switch
    {
        Phase.Sitting => "I'm standing",
        Phase.Standing => "Start walk",
        _ => "I'm sitting",
    };

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

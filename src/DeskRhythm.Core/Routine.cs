namespace DeskRhythm.Core;

/// <summary>The posture the user is in right now.</summary>
public enum Phase
{
    Sitting,
    Standing,
    Moving,
}

/// <summary>Where the routine is in its life cycle.</summary>
public enum RoutineState
{
    /// <summary>Nothing is running. The user has to press Start.</summary>
    Stopped,

    /// <summary>The current phase is counting down.</summary>
    Running,

    /// <summary>The phase has ended. We wait for the user to change position.</summary>
    ReminderPending,

    /// <summary>The same transition is still pending, but the prompt is postponed.</summary>
    Snoozed,

    /// <summary>The countdown is frozen (manual pause, quiet mode or away).</summary>
    Paused,

    /// <summary>After a long absence or a restart: wait for "resume" or "restart".</summary>
    NeedsResync,
}

public enum PauseReason
{
    /// <summary>The user chose Pause (with or without an end time).</summary>
    User,

    /// <summary>Quiet / presentation mode. Stays on until the user turns it off.</summary>
    Quiet,

    /// <summary>Windows is locked, asleep or the user is idle.</summary>
    Away,
}

/// <summary>What the UI must show after a tick.</summary>
public enum PromptKind
{
    None,

    /// <summary>The current phase has ended: ask the user to change position.</summary>
    PhaseEnded,

    /// <summary>Ask the user to resume the previous phase or restart the cycle.</summary>
    WelcomeBack,
}

/// <summary>
/// A time source. The engine only uses <see cref="Monotonic"/>, so changes to the
/// Windows clock or time zone cannot make a reminder fire early or late.
/// </summary>
public interface IClock
{
    /// <summary>Monotonic time. Only differences between two values have meaning.</summary>
    TimeSpan Monotonic { get; }

    /// <summary>Wall-clock time, for display and persistence only.</summary>
    DateTimeOffset WallNow { get; }
}

public sealed class SystemClock : IClock
{
    // GetTickCount64 is monotonic, is not changed by clock or time-zone edits,
    // and continues to count while the machine sleeps. That lets us see a long gap.
    public TimeSpan Monotonic => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public DateTimeOffset WallNow => DateTimeOffset.Now;
}

/// <summary>Durations the engine reads at the moment it needs them, so edits apply to the next phase.</summary>
public sealed class RoutineTimings
{
    public required Func<Phase, TimeSpan> PhaseDuration { get; init; }

    /// <summary>Delay before an ignored or closed prompt shows again.</summary>
    public required Func<TimeSpan> RepromptDelay { get; init; }

    /// <summary>Away for less than this: continue silently. Longer: ask the user.</summary>
    public TimeSpan ShortAwayGrace { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>A timed pause this long or longer ends with a "welcome back" prompt.</summary>
    public TimeSpan LongAbsence { get; init; } = TimeSpan.FromMinutes(60);
}

/// <summary>The minimum state we keep on disk to recover after a restart or crash.</summary>
public sealed record RoutineSnapshot(bool IsActive, Phase Phase, TimeSpan Remaining, bool WasPending, DateTimeOffset SavedAt)
{
    public static RoutineSnapshot Inactive(DateTimeOffset now) => new(false, Phase.Sitting, TimeSpan.Zero, false, now);
}

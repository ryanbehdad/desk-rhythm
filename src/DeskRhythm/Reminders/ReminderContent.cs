using DeskRhythm.Core;

namespace DeskRhythm.Reminders;

internal enum ReminderKind
{
    /// <summary>A phase has ended (from the engine).</summary>
    PhaseEnded,

    /// <summary>Resume or restart after an absence or a restart (from the engine).</summary>
    WelcomeBack,

    /// <summary>At launch when the routine is stopped. Closing it does not come back.</summary>
    Ready,

    /// <summary>"Test reminder". Changes nothing.</summary>
    Test,
}

internal sealed record ReminderAction(string Label, Action Execute, bool IsPrimary = false, string? ToolTip = null);

/// <summary>An on/off switch on a card. <see cref="Set"/> returns false if the change failed.</summary>
internal sealed record ReminderToggle(string Label, string Hint, Func<bool> IsOn, Func<bool, bool> Set);

/// <summary>What one logical reminder shows. Every monitor gets a copy of the same content.</summary>
internal sealed class ReminderContent
{
    public required ReminderKind Kind { get; init; }

    public required Phase Accent { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public string? Detail { get; init; }

    public string Caption { get; init; } = "Desk Rhythm";

    public IReadOnlyList<ReminderAction> Actions { get; init; } = [];

    /// <summary>Optional switch shown above the buttons (for example "Start with Windows").</summary>
    public ReminderToggle? Toggle { get; init; }

    /// <summary>Items for the "More" menu. Null is a separator.</summary>
    public IReadOnlyList<ReminderAction?> MoreActions { get; init; } = [];

    /// <summary>Test reminders show "Display 2 of 3 · 150 %" so each screen can be checked.</summary>
    public bool ShowDisplayInfo { get; init; }

    public bool ComesFromEngine => Kind is ReminderKind.PhaseEnded or ReminderKind.WelcomeBack;
}

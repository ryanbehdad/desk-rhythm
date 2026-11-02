namespace DeskRhythm.Core;

/// <summary>
/// The single source of truth for the sit / stand / move routine.
/// <para>
/// All times are deadlines on a monotonic clock. The engine does not count ticks:
/// <see cref="Tick"/> only compares "now" with the deadlines, so a late or missed
/// tick cannot make the timer drift. The engine has no UI and no Windows
/// dependencies, so the tests drive it with a fake clock.
/// </para>
/// </summary>
public sealed class RoutineEngine
{
    private readonly IClock _clock;
    private readonly RoutineTimings _timings;

    private TimeSpan _deadline;            // Running: when the phase ends. Pending: when it ended.
    private TimeSpan _frozenRemaining;     // Paused / NeedsResync: time left in the phase.
    private bool _frozenWasPending;        // Paused / NeedsResync: the transition was already due.
    private TimeSpan? _promptAt;           // When the prompt must show (again). Null: not scheduled.
    private TimeSpan? _pauseUntil;         // Paused: automatic resume time. Null: until the user resumes.
    private TimeSpan _pausedAt;
    private TimeSpan _awaySince;

    public RoutineEngine(IClock clock, RoutineTimings timings)
    {
        _clock = clock;
        _timings = timings;
        PhaseDuration = timings.PhaseDuration(Phase.Sitting);
    }

    /// <summary>Raised after every state change (not on plain countdown ticks).</summary>
    public event EventHandler? Changed;

    public Phase Phase { get; private set; } = Phase.Sitting;

    public RoutineState State { get; private set; } = RoutineState.Stopped;

    /// <summary>Set only while <see cref="State"/> is <see cref="RoutineState.Paused"/>.</summary>
    public PauseReason? PauseReason { get; private set; }

    /// <summary>The full length of the current phase, fixed when the phase started.</summary>
    public TimeSpan PhaseDuration { get; private set; }

    /// <summary>True while Windows is locked, asleep or the user is idle.</summary>
    public bool IsAway { get; private set; }

    /// <summary>True while a prompt is on screen. The engine never asks for a second copy.</summary>
    public bool IsPromptVisible { get; private set; }

    private TimeSpan Now => _clock.Monotonic;

    /// <summary>Time left in the current phase. Zero when the transition is due.</summary>
    public TimeSpan Remaining => State switch
    {
        RoutineState.Running => Max(TimeSpan.Zero, _deadline - Now),
        RoutineState.Paused or RoutineState.NeedsResync => _frozenRemaining,
        _ => TimeSpan.Zero,
    };

    /// <summary>0 at the start of the phase, 1 when it is due.</summary>
    public double Progress => State == RoutineState.Stopped || PhaseDuration <= TimeSpan.Zero
        ? 0
        : Math.Clamp(1 - Remaining / PhaseDuration, 0, 1);

    /// <summary>Paused / NeedsResync: true if the transition was already due when the timer froze.</summary>
    public bool FrozenWhilePending => _frozenWasPending;

    public TimeSpan? SnoozeRemaining =>
        State == RoutineState.Snoozed && _promptAt is { } at ? Max(TimeSpan.Zero, at - Now) : null;

    public TimeSpan? PauseRemaining =>
        State == RoutineState.Paused && _pauseUntil is { } until ? Max(TimeSpan.Zero, until - Now) : null;

    /// <summary>When a hidden prompt shows again (after a time-out or close). Null if none is scheduled.</summary>
    public TimeSpan? RepromptRemaining =>
        State is RoutineState.ReminderPending or RoutineState.NeedsResync && !IsPromptVisible && _promptAt is { } at
            ? Max(TimeSpan.Zero, at - Now)
            : null;

    public bool IsActive => State != RoutineState.Stopped;

    public bool IsTransitionDue => State is RoutineState.ReminderPending or RoutineState.Snoozed;

    /// <summary>Skip is offered for the standing break and for the walk, not for "sit again".</summary>
    public bool CanSkip => IsTransitionDue && Phase != Phase.Moving;

    public static Phase Next(Phase phase) => phase switch
    {
        Phase.Sitting => Phase.Standing,
        Phase.Standing => Phase.Moving,
        _ => Phase.Sitting,
    };

    public TimeSpan DurationOf(Phase phase) => _timings.PhaseDuration(phase);

    // ---------------------------------------------------------------- commands

    /// <summary>Start the routine in Sitting with a full countdown.</summary>
    public void Start() => BeginPhase(Phase.Sitting);

    /// <summary>Start a fresh sitting phase (from "welcome back" or the tray).</summary>
    public void RestartCycle() => BeginPhase(Phase.Sitting);

    /// <summary>The user has changed position. The next phase starts now.</summary>
    public bool Acknowledge()
    {
        if (!IsTransitionDue)
        {
            return false;
        }

        BeginPhase(Next(Phase));
        return true;
    }

    /// <summary>Hide the prompt. The same transition stays pending and returns after <paramref name="delay"/>.</summary>
    public bool Snooze(TimeSpan delay)
    {
        if (!IsTransitionDue)
        {
            return false;
        }

        State = RoutineState.Snoozed;
        IsPromptVisible = false;
        _promptAt = Now + delay;
        Raise();
        return true;
    }

    /// <summary>Skip the standing break or the walk. A new sitting phase starts. The break does not count as done.</summary>
    public bool Skip()
    {
        if (!CanSkip)
        {
            return false;
        }

        BeginPhase(Phase.Sitting);
        return true;
    }

    /// <summary>The prompt timed out or the user closed it. It stays pending and shows again later.</summary>
    public void DismissPrompt()
    {
        if (!IsPromptVisible)
        {
            return;
        }

        IsPromptVisible = false;
        if (State is RoutineState.ReminderPending or RoutineState.NeedsResync)
        {
            _promptAt = Now + _timings.RepromptDelay();
        }

        Raise();
    }

    /// <summary>Resynchronise: the user is in <paramref name="phase"/> now. Starts a full phase.</summary>
    public void SwitchTo(Phase phase) => BeginPhase(phase);

    /// <summary>Freeze the countdown. <paramref name="duration"/> null means "until the user resumes".</summary>
    public void Pause(TimeSpan? duration, PauseReason reason = Core.PauseReason.User)
    {
        if (State == RoutineState.Stopped || IsAway)
        {
            return;
        }

        if (State != RoutineState.Paused)
        {
            Freeze(Now);
        }

        State = RoutineState.Paused;
        PauseReason = reason;
        _pausedAt = Now;
        _pauseUntil = duration is { } d ? Now + d : null;
        Raise();
    }

    /// <summary>Continue from where the timer froze (manual resume, or "resume previous phase").</summary>
    public void Resume()
    {
        if (State is not (RoutineState.Paused or RoutineState.NeedsResync) || IsAway)
        {
            return;
        }

        Unfreeze();
        Raise();
    }

    public void Stop()
    {
        State = RoutineState.Stopped;
        PauseReason = null;
        IsPromptVisible = false;
        _promptAt = null;
        _pauseUntil = null;
        _frozenRemaining = TimeSpan.Zero;
        _frozenWasPending = false;
        Raise();
    }

    /// <summary>
    /// Advance the routine. Call this often (for example once a second). It is cheap
    /// and returns the prompt that the UI must show now, if any. It never returns a
    /// prompt while one is already visible, so duplicates cannot stack.
    /// </summary>
    public PromptKind Tick()
    {
        if (IsAway)
        {
            return PromptKind.None;
        }

        var now = Now;
        var changed = false;

        switch (State)
        {
            case RoutineState.Running when now >= _deadline:
                State = RoutineState.ReminderPending;
                _promptAt = now;
                changed = true;
                break;

            case RoutineState.Snoozed when _promptAt is { } at && now >= at:
                State = RoutineState.ReminderPending;
                changed = true;
                break;

            case RoutineState.Paused when _pauseUntil is { } until && now >= until:
                if (now - _pausedAt >= _timings.LongAbsence)
                {
                    EnterResync();
                }
                else
                {
                    Unfreeze();
                }

                changed = true;
                break;
        }

        var prompt = PromptKind.None;
        if (State is RoutineState.ReminderPending or RoutineState.NeedsResync
            && !IsPromptVisible
            && _promptAt is { } promptAt
            && now >= promptAt)
        {
            IsPromptVisible = true;
            _promptAt = null;
            prompt = State == RoutineState.NeedsResync ? PromptKind.WelcomeBack : PromptKind.PhaseEnded;
            changed = true;
        }

        if (changed)
        {
            Raise();
        }

        return prompt;
    }

    // ------------------------------------------------------ lock, sleep, idle

    /// <summary>
    /// Windows is locked, asleep or idle. Freeze the countdown and hide prompts.
    /// <paramref name="alreadyAwayFor"/> backdates the start (idle detection knows
    /// the user left some minutes ago).
    /// </summary>
    public void BeginAway(TimeSpan alreadyAwayFor = default)
    {
        if (IsAway)
        {
            return;
        }

        IsAway = true;
        _awaySince = Now - Max(TimeSpan.Zero, alreadyAwayFor);
        IsPromptVisible = false;

        if (State is RoutineState.Running or RoutineState.ReminderPending or RoutineState.Snoozed)
        {
            Freeze(_awaySince);
            State = RoutineState.Paused;
            PauseReason = Core.PauseReason.Away;
            _pausedAt = _awaySince;
            _pauseUntil = null;
        }
        else if (State == RoutineState.NeedsResync)
        {
            _promptAt = null;
        }

        Raise();
    }

    /// <summary>The user is back. A short absence continues silently; a long one asks the user once.</summary>
    public void EndAway()
    {
        if (!IsAway)
        {
            return;
        }

        IsAway = false;
        var awayFor = Now - _awaySince;

        if (State == RoutineState.Paused && PauseReason == Core.PauseReason.Away)
        {
            if (awayFor < _timings.ShortAwayGrace)
            {
                Unfreeze();
            }
            else
            {
                EnterResync();
            }
        }
        else if (State == RoutineState.NeedsResync)
        {
            _promptAt = Now;
        }

        // A manual pause stays as it is. Tick() handles its automatic end.
        Raise();
    }

    /// <summary>
    /// Ticks stopped for <paramref name="gap"/> (for example the machine slept and
    /// Windows did not tell us). Treat a long gap as an absence.
    /// </summary>
    public void ReportMissedTime(TimeSpan gap)
    {
        if (IsAway || gap < _timings.ShortAwayGrace)
        {
            return;
        }

        BeginAway(gap);
        EndAway();
    }

    // ------------------------------------------------------------ persistence

    public RoutineSnapshot CreateSnapshot()
    {
        var now = _clock.WallNow;
        return State switch
        {
            RoutineState.Stopped => RoutineSnapshot.Inactive(now),
            RoutineState.Running => new(true, Phase, Remaining, false, now),
            RoutineState.ReminderPending or RoutineState.Snoozed => new(true, Phase, TimeSpan.Zero, true, now),
            _ => new(true, Phase, _frozenRemaining, _frozenWasPending, now),
        };
    }

    /// <summary>
    /// Load the state from the last session. We never assume the user kept the same
    /// posture while the app was closed, so an active routine waits for the user to choose.
    /// </summary>
    public void Restore(RoutineSnapshot? snapshot)
    {
        if (snapshot is not { IsActive: true })
        {
            return;
        }

        Phase = snapshot.Phase;
        PhaseDuration = _timings.PhaseDuration(Phase);
        _frozenRemaining = snapshot.Remaining < TimeSpan.Zero
            ? TimeSpan.Zero
            : snapshot.Remaining > PhaseDuration ? PhaseDuration : snapshot.Remaining;
        _frozenWasPending = snapshot.WasPending || _frozenRemaining == TimeSpan.Zero;
        EnterResync();
        Raise();
    }

    // ---------------------------------------------------------------- helpers

    private void BeginPhase(Phase phase)
    {
        Phase = phase;
        PhaseDuration = _timings.PhaseDuration(phase);
        State = RoutineState.Running;
        PauseReason = null;
        IsPromptVisible = false;
        _deadline = Now + PhaseDuration;
        _promptAt = null;
        _pauseUntil = null;
        _frozenRemaining = TimeSpan.Zero;
        _frozenWasPending = false;
        Raise();
    }

    private void Freeze(TimeSpan asOf)
    {
        switch (State)
        {
            case RoutineState.Running:
                _frozenRemaining = Max(TimeSpan.Zero, _deadline - asOf);
                _frozenWasPending = _frozenRemaining == TimeSpan.Zero;
                break;
            case RoutineState.ReminderPending or RoutineState.Snoozed:
                _frozenRemaining = TimeSpan.Zero;
                _frozenWasPending = true;
                break;

            // Paused and NeedsResync already hold frozen values.
        }

        IsPromptVisible = false;
        _promptAt = null;
    }

    private void Unfreeze()
    {
        PauseReason = null;
        _pauseUntil = null;
        IsPromptVisible = false;

        if (!_frozenWasPending && _frozenRemaining > TimeSpan.Zero)
        {
            State = RoutineState.Running;
            _deadline = Now + _frozenRemaining;
            _promptAt = null;
        }
        else
        {
            // The transition was already due: show it again now.
            State = RoutineState.ReminderPending;
            _deadline = Now;
            _promptAt = Now;
        }
    }

    private void EnterResync()
    {
        State = RoutineState.NeedsResync;
        PauseReason = null;
        _pauseUntil = null;
        IsPromptVisible = false;
        _promptAt = Now;
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}

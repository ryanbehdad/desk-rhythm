using DeskRhythm.Core;

namespace DeskRhythm.Core.Tests;

public sealed class FakeClock : IClock
{
    public TimeSpan Monotonic { get; private set; } = TimeSpan.FromHours(10);

    public DateTimeOffset WallNow { get; set; } = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(8));

    public void Advance(TimeSpan by)
    {
        Monotonic += by;
        WallNow += by;
    }

    public void AdvanceMinutes(double minutes) => Advance(TimeSpan.FromMinutes(minutes));
}

public class RoutineEngineTests
{
    private readonly FakeClock _clock = new();
    private readonly AppSettings _settings = new();
    private readonly RoutineEngine _engine;

    public RoutineEngineTests()
    {
        _engine = new RoutineEngine(_clock, new RoutineTimings
        {
            PhaseDuration = p => TimeSpan.FromMinutes(_settings.MinutesFor(p)),
            RepromptDelay = () => TimeSpan.FromMinutes(_settings.RepromptMinutes),
        });
    }

    /// <summary>Advance minute by minute and collect every prompt, like the 1-second UI timer would.</summary>
    private List<(double Minute, PromptKind Kind)> Run(double minutes, double step = 0.25)
    {
        var prompts = new List<(double, PromptKind)>();
        for (double t = step; t <= minutes + 1e-9; t += step)
        {
            _clock.AdvanceMinutes(step);
            var prompt = _engine.Tick();
            if (prompt != PromptKind.None)
            {
                prompts.Add((t, prompt));
            }
        }

        return prompts;
    }

    [Fact]
    public void Starts_stopped_and_never_prompts_until_started()
    {
        Assert.Equal(RoutineState.Stopped, _engine.State);
        Assert.Empty(Run(120));
    }

    [Fact]
    public void Start_begins_thirty_minute_sitting_phase()
    {
        _engine.Start();

        Assert.Equal(Phase.Sitting, _engine.Phase);
        Assert.Equal(RoutineState.Running, _engine.State);
        Assert.Equal(TimeSpan.FromMinutes(30), _engine.Remaining);
    }

    [Fact]
    public void Sitting_end_creates_exactly_one_prompt()
    {
        _engine.Start();

        var prompts = Run(29.75);
        Assert.Empty(prompts);

        Assert.Equal(PromptKind.None, _engine.Tick());
        _clock.AdvanceMinutes(0.25);
        Assert.Equal(PromptKind.PhaseEnded, _engine.Tick());
        Assert.Equal(RoutineState.ReminderPending, _engine.State);
        Assert.True(_engine.IsPromptVisible);

        // The prompt is still on screen: no duplicates, however long we wait.
        Assert.Empty(Run(60));
    }

    [Fact]
    public void Next_phase_starts_when_user_acknowledges_not_when_prompt_appeared()
    {
        _engine.Start();
        Run(30);
        Assert.Equal(RoutineState.ReminderPending, _engine.State);

        // The user is busy for 4 more minutes, then stands up.
        _clock.AdvanceMinutes(4);
        _engine.Tick();
        Assert.Equal(Phase.Sitting, _engine.Phase);

        Assert.True(_engine.Acknowledge());
        Assert.Equal(Phase.Standing, _engine.Phase);
        Assert.Equal(RoutineState.Running, _engine.State);
        Assert.Equal(TimeSpan.FromMinutes(15), _engine.Remaining);
    }

    [Fact]
    public void Full_cycle_repeats_sitting_standing_moving()
    {
        _engine.Start();

        Run(30);
        _engine.Acknowledge();
        Assert.Equal(Phase.Standing, _engine.Phase);

        var standing = Run(15);
        Assert.Single(standing);
        _engine.Acknowledge();
        Assert.Equal(Phase.Moving, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(2), _engine.Remaining);

        var moving = Run(2);
        Assert.Single(moving);
        _engine.Acknowledge();
        Assert.Equal(Phase.Sitting, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(30), _engine.Remaining);
    }

    [Fact]
    public void Snooze_keeps_same_transition_pending_and_returns_exactly_once_after_delay()
    {
        _engine.Start();
        Run(30);

        Assert.True(_engine.Snooze(TimeSpan.FromMinutes(10)));
        Assert.Equal(RoutineState.Snoozed, _engine.State);
        Assert.Equal(Phase.Sitting, _engine.Phase);
        Assert.False(_engine.IsPromptVisible);

        var prompts = Run(9.75);
        Assert.Empty(prompts);

        _clock.AdvanceMinutes(0.25);
        Assert.Equal(PromptKind.PhaseEnded, _engine.Tick());
        Assert.Equal(Phase.Sitting, _engine.Phase);
        Assert.Equal(RoutineState.ReminderPending, _engine.State);
        Assert.Empty(Run(30));
    }

    [Fact]
    public void Snooze_does_not_start_the_next_phase_or_reset_the_routine()
    {
        _engine.Start();
        Run(30);
        _engine.Snooze(TimeSpan.FromMinutes(5));
        Run(5);

        _engine.Acknowledge();

        Assert.Equal(Phase.Standing, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(15), _engine.Remaining);
    }

    [Fact]
    public void Snooze_and_acknowledge_do_nothing_when_no_transition_is_due()
    {
        _engine.Start();
        Run(10);

        Assert.False(_engine.Snooze(TimeSpan.FromMinutes(5)));
        Assert.False(_engine.Acknowledge());
        Assert.Equal(RoutineState.Running, _engine.State);
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);
    }

    [Fact]
    public void Ignored_prompt_returns_after_reprompt_delay_and_phase_stays_active()
    {
        _engine.Start();
        Run(30);

        _engine.DismissPrompt();
        Assert.Equal(RoutineState.ReminderPending, _engine.State);
        Assert.False(_engine.IsPromptVisible);

        var prompts = Run(5);
        Assert.Single(prompts);
        Assert.Equal(5, prompts[0].Minute, 3);
        Assert.Equal(Phase.Sitting, _engine.Phase);
    }

    [Fact]
    public void Skip_standing_break_starts_fresh_sitting_phase()
    {
        _engine.Start();
        Run(30);

        Assert.True(_engine.CanSkip);
        Assert.True(_engine.Skip());

        Assert.Equal(Phase.Sitting, _engine.Phase);
        Assert.Equal(RoutineState.Running, _engine.State);
        Assert.Equal(TimeSpan.FromMinutes(30), _engine.Remaining);
    }

    [Fact]
    public void Skip_walk_starts_new_sitting_phase()
    {
        _engine.SwitchTo(Phase.Standing);
        Run(15);

        Assert.True(_engine.Skip());

        Assert.Equal(Phase.Sitting, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(30), _engine.Remaining);
    }

    [Fact]
    public void Skip_is_not_offered_at_end_of_walk()
    {
        _engine.SwitchTo(Phase.Moving);
        Run(2);

        Assert.False(_engine.CanSkip);
        Assert.False(_engine.Skip());
        Assert.Equal(Phase.Moving, _engine.Phase);
    }

    [Fact]
    public void Switch_phase_resynchronises_immediately_with_full_duration()
    {
        _engine.Start();
        Run(12);

        _engine.SwitchTo(Phase.Standing);

        Assert.Equal(Phase.Standing, _engine.Phase);
        Assert.Equal(RoutineState.Running, _engine.State);
        Assert.Equal(TimeSpan.FromMinutes(15), _engine.Remaining);
    }

    [Fact]
    public void Manual_pause_freezes_remaining_time_and_suppresses_prompts()
    {
        _engine.Start();
        Run(10);

        _engine.Pause(null);
        Assert.Equal(RoutineState.Paused, _engine.State);
        Assert.Equal(PauseReason.User, _engine.PauseReason);

        Assert.Empty(Run(180));
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);

        _engine.Resume();
        Assert.Equal(RoutineState.Running, _engine.State);
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);

        var prompts = Run(20);
        Assert.Single(prompts);
    }

    [Fact]
    public void Timed_pause_resumes_countdown_automatically()
    {
        _engine.Start();
        Run(10);

        _engine.Pause(TimeSpan.FromMinutes(30));
        Assert.Equal(TimeSpan.FromMinutes(30), _engine.PauseRemaining);

        Assert.Empty(Run(29.75));
        Assert.Equal(RoutineState.Paused, _engine.State);

        Run(0.25);
        Assert.Equal(RoutineState.Running, _engine.State);
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);
    }

    [Fact]
    public void Long_timed_pause_ends_with_one_welcome_back_prompt()
    {
        _engine.Start();
        Run(10);

        _engine.Pause(TimeSpan.FromHours(2));
        var prompts = Run(150);

        Assert.Single(prompts);
        Assert.Equal(PromptKind.WelcomeBack, prompts[0].Kind);
        Assert.Equal(RoutineState.NeedsResync, _engine.State);
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);
    }

    [Fact]
    public void Pause_from_reminder_brings_the_same_reminder_back_after_pause()
    {
        _engine.Start();
        Run(30);

        _engine.Pause(TimeSpan.FromMinutes(30));
        Assert.False(_engine.IsPromptVisible);

        var prompts = Run(30);
        Assert.Single(prompts);
        Assert.Equal(PromptKind.PhaseEnded, prompts[0].Kind);
        Assert.Equal(Phase.Sitting, _engine.Phase);
    }

    [Fact]
    public void Quiet_mode_holds_until_turned_off()
    {
        _engine.Start();
        _engine.Pause(null, PauseReason.Quiet);

        Assert.Empty(Run(240));
        Assert.Equal(PauseReason.Quiet, _engine.PauseReason);

        _engine.Resume();
        Assert.Equal(TimeSpan.FromMinutes(30), _engine.Remaining);
    }

    [Fact]
    public void Lock_freezes_countdown_and_short_absence_continues_silently()
    {
        _engine.Start();
        Run(10);

        _engine.BeginAway();
        Assert.Equal(RoutineState.Paused, _engine.State);
        Assert.Equal(PauseReason.Away, _engine.PauseReason);
        Assert.Empty(Run(3));

        _engine.EndAway();
        Assert.Equal(RoutineState.Running, _engine.State);
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);
    }

    [Fact]
    public void Long_lock_produces_no_backlog_and_one_welcome_back_prompt()
    {
        _engine.Start();
        Run(10);

        _engine.BeginAway();
        Assert.Empty(Run(8 * 60)); // a whole night: nothing fires
        _engine.EndAway();

        var prompts = Run(60);
        Assert.Single(prompts);
        Assert.Equal(PromptKind.WelcomeBack, prompts[0].Kind);

        _engine.Resume();
        Assert.Equal(Phase.Sitting, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);
    }

    [Fact]
    public void Welcome_back_can_restart_the_sitting_cycle()
    {
        _engine.SwitchTo(Phase.Standing);
        _engine.BeginAway();
        _clock.AdvanceMinutes(45);
        _engine.EndAway();

        _engine.RestartCycle();

        Assert.Equal(Phase.Sitting, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(30), _engine.Remaining);
    }

    [Fact]
    public void Lock_while_reminder_visible_hides_it_and_brings_it_back_after_unlock()
    {
        _engine.Start();
        Run(30);
        Assert.True(_engine.IsPromptVisible);

        _engine.BeginAway();
        Assert.False(_engine.IsPromptVisible);
        _clock.AdvanceMinutes(2);
        _engine.EndAway();

        Assert.Equal(PromptKind.PhaseEnded, _engine.Tick());
        Assert.Equal(Phase.Sitting, _engine.Phase);
    }

    [Fact]
    public void Idle_detection_backdates_the_absence()
    {
        _engine.Start();
        Run(20);

        // Idle detection notices after 10 idle minutes: the user left at minute 10.
        _engine.BeginAway(TimeSpan.FromMinutes(10));
        _engine.EndAway();

        Assert.Equal(RoutineState.NeedsResync, _engine.State);
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);
    }

    [Fact]
    public void Missed_sleep_gap_is_treated_as_an_absence()
    {
        _engine.Start();
        Run(10);

        // The machine slept for 3 hours and no power event reached the app.
        _clock.AdvanceMinutes(180);
        _engine.ReportMissedTime(TimeSpan.FromMinutes(180));

        Assert.Equal(RoutineState.NeedsResync, _engine.State);
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);
        Assert.Equal(PromptKind.WelcomeBack, _engine.Tick());
    }

    [Fact]
    public void Small_tick_gap_is_ignored()
    {
        _engine.Start();
        _engine.ReportMissedTime(TimeSpan.FromSeconds(30));
        Assert.Equal(RoutineState.Running, _engine.State);
    }

    [Fact]
    public void Wall_clock_and_time_zone_changes_do_not_affect_deadlines()
    {
        _engine.Start();
        Run(10);

        _clock.WallNow = _clock.WallNow.AddHours(5).ToOffset(TimeSpan.FromHours(-5));
        Assert.Equal(PromptKind.None, _engine.Tick());
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);

        _clock.WallNow = _clock.WallNow.AddDays(-2);
        Assert.Equal(PromptKind.None, _engine.Tick());
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);
    }

    [Fact]
    public void Edited_durations_apply_to_the_next_phase_only()
    {
        _engine.Start();
        Run(5);

        _settings.SittingMinutes = 45;
        _settings.StandingMinutes = 20;
        Assert.Equal(TimeSpan.FromMinutes(25), _engine.Remaining);

        Run(25);
        _engine.Acknowledge();
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);
    }

    [Fact]
    public void Stop_clears_everything()
    {
        _engine.Start();
        Run(30);
        _engine.Stop();

        Assert.Equal(RoutineState.Stopped, _engine.State);
        Assert.False(_engine.IsPromptVisible);
        Assert.Empty(Run(120));
    }

    [Fact]
    public void Snapshot_restore_asks_user_instead_of_assuming_posture()
    {
        _engine.SwitchTo(Phase.Standing);
        Run(5);
        var snapshot = _engine.CreateSnapshot();
        Assert.True(snapshot.IsActive);
        Assert.Equal(TimeSpan.FromMinutes(10), snapshot.Remaining);

        var clock = new FakeClock();
        var restored = new RoutineEngine(clock, new RoutineTimings
        {
            PhaseDuration = p => TimeSpan.FromMinutes(_settings.MinutesFor(p)),
            RepromptDelay = () => TimeSpan.FromMinutes(5),
        });
        restored.Restore(snapshot);

        Assert.Equal(RoutineState.NeedsResync, restored.State);
        Assert.Equal(Phase.Standing, restored.Phase);
        Assert.Equal(PromptKind.WelcomeBack, restored.Tick());

        restored.Resume();
        Assert.Equal(RoutineState.Running, restored.State);
        Assert.Equal(TimeSpan.FromMinutes(10), restored.Remaining);
    }

    [Fact]
    public void Restoring_a_pending_transition_shows_the_reminder_after_resume()
    {
        _engine.Start();
        Run(30);
        var snapshot = _engine.CreateSnapshot();
        Assert.True(snapshot.WasPending);

        var restored = new RoutineEngine(new FakeClock(), new RoutineTimings
        {
            PhaseDuration = p => TimeSpan.FromMinutes(_settings.MinutesFor(p)),
            RepromptDelay = () => TimeSpan.FromMinutes(5),
        });
        restored.Restore(snapshot);
        restored.Resume();

        Assert.Equal(RoutineState.ReminderPending, restored.State);
        Assert.Equal(PromptKind.PhaseEnded, restored.Tick());
    }

    [Fact]
    public void Restoring_inactive_snapshot_keeps_routine_stopped()
    {
        _engine.Restore(RoutineSnapshot.Inactive(DateTimeOffset.Now));
        Assert.Equal(RoutineState.Stopped, _engine.State);
        _engine.Restore(null);
        Assert.Equal(RoutineState.Stopped, _engine.State);
    }

    [Fact]
    public void Ignored_welcome_back_prompt_returns_later_once()
    {
        _engine.Start();
        _engine.BeginAway();
        _clock.AdvanceMinutes(30);
        _engine.EndAway();
        Assert.Equal(PromptKind.WelcomeBack, _engine.Tick());

        _engine.DismissPrompt();
        var prompts = Run(5);
        Assert.Single(prompts);
        Assert.Equal(PromptKind.WelcomeBack, prompts[0].Kind);
    }
}

using DeskRhythm.Core;

namespace DeskRhythm.Core.Tests;

/// <summary>The sitting-only routine (no standing desk): Sitting -> Moving (walking break) -> Sitting.</summary>
public class NoStandingDeskTests
{
    private readonly FakeClock _clock = new();
    private readonly AppSettings _settings = new() { HasStandingDesk = false };
    private readonly RoutineEngine _engine;

    public NoStandingDeskTests()
    {
        _engine = NewEngine(_clock);
    }

    private RoutineEngine NewEngine(FakeClock clock) => new(clock, new RoutineTimings
    {
        PhaseDuration = p => TimeSpan.FromMinutes(_settings.MinutesFor(p)),
        RepromptDelay = () => TimeSpan.FromMinutes(_settings.RepromptMinutes),
        HasStandingDesk = () => _settings.HasStandingDesk,
    });

    private PromptKind RunMinutes(double minutes)
    {
        var last = PromptKind.None;
        for (var t = 0.25; t <= minutes + 1e-9; t += 0.25)
        {
            _clock.AdvanceMinutes(0.25);
            var prompt = _engine.Tick();
            if (prompt != PromptKind.None)
            {
                last = prompt;
            }
        }

        return last;
    }

    [Theory]
    [InlineData(Phase.Sitting, true, Phase.Standing)]
    [InlineData(Phase.Standing, true, Phase.Moving)]
    [InlineData(Phase.Moving, true, Phase.Sitting)]
    [InlineData(Phase.Sitting, false, Phase.Moving)]
    [InlineData(Phase.Standing, false, Phase.Moving)]
    [InlineData(Phase.Moving, false, Phase.Sitting)]
    public void Next_phase_depends_on_the_desk(Phase phase, bool hasStandingDesk, Phase expected)
    {
        Assert.Equal(expected, RoutineEngine.Next(phase, hasStandingDesk));
    }

    [Fact]
    public void Timings_default_to_a_standing_desk()
    {
        var engine = new RoutineEngine(new FakeClock(), new RoutineTimings
        {
            PhaseDuration = _ => TimeSpan.FromMinutes(10),
            RepromptDelay = () => TimeSpan.FromMinutes(5),
        });

        Assert.True(engine.HasStandingDesk);
        Assert.Equal(Phase.Standing, engine.NextPhase);
    }

    [Fact]
    public void Cycle_is_sit_forty_then_walk_five_then_sit()
    {
        _engine.Start();
        Assert.Equal(TimeSpan.FromMinutes(40), _engine.Remaining);
        Assert.Equal(Phase.Moving, _engine.NextPhase);

        Assert.Equal(PromptKind.None, RunMinutes(39.75));
        Assert.Equal(PromptKind.PhaseEnded, RunMinutes(0.25));

        Assert.True(_engine.Acknowledge());
        Assert.Equal(Phase.Moving, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), _engine.Remaining);

        Assert.Equal(PromptKind.PhaseEnded, RunMinutes(5));
        Assert.True(_engine.Acknowledge());
        Assert.Equal(Phase.Sitting, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(40), _engine.Remaining);
    }

    [Fact]
    public void Skip_from_sitting_skips_the_break_and_starts_sitting_again()
    {
        _engine.Start();
        RunMinutes(40);
        Assert.True(_engine.CanSkip);

        Assert.True(_engine.Skip());
        Assert.Equal(Phase.Sitting, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(40), _engine.Remaining);
    }

    [Fact]
    public void Break_cannot_be_skipped_into_more_sitting()
    {
        _engine.Start();
        RunMinutes(40);
        _engine.Acknowledge();
        RunMinutes(5);

        Assert.False(_engine.CanSkip);
    }

    [Fact]
    public void Mode_change_mid_phase_keeps_the_running_phase_and_applies_at_the_next_transition()
    {
        _settings.HasStandingDesk = true;
        _engine.Start();
        RunMinutes(10);
        var progress = _engine.Progress;

        _settings.HasStandingDesk = false;

        Assert.Equal(TimeSpan.FromMinutes(30), _engine.PhaseDuration);
        Assert.Equal(TimeSpan.FromMinutes(20), _engine.Remaining);
        Assert.Equal(progress, _engine.Progress);
        Assert.Equal(Phase.Moving, _engine.NextPhase);

        RunMinutes(20);
        _engine.Acknowledge();
        Assert.Equal(Phase.Moving, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), _engine.PhaseDuration);
    }

    [Fact]
    public void Mode_change_while_snoozed_applies_on_acknowledge()
    {
        _settings.HasStandingDesk = true;
        _engine.Start();
        RunMinutes(30);
        _engine.Snooze(TimeSpan.FromMinutes(5));

        _settings.HasStandingDesk = false;
        Assert.True(_engine.Acknowledge());

        Assert.Equal(Phase.Moving, _engine.Phase);
    }

    [Fact]
    public void Restart_cycle_without_a_desk_starts_a_sit_only_sitting_phase()
    {
        _settings.HasStandingDesk = true;
        _engine.SwitchTo(Phase.Standing);

        _settings.HasStandingDesk = false;
        _engine.RestartCycle();

        Assert.Equal(Phase.Sitting, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(40), _engine.Remaining);
        Assert.Equal(Phase.Moving, _engine.NextPhase);
    }

    [Fact]
    public void Mode_change_after_an_ignored_prompt_changes_the_next_phase_of_the_reprompt()
    {
        _settings.HasStandingDesk = true;
        _engine.Start();
        Assert.Equal(PromptKind.PhaseEnded, RunMinutes(30));
        _engine.DismissPrompt();

        _settings.HasStandingDesk = false;
        Assert.Equal(PromptKind.PhaseEnded, RunMinutes(5));
        Assert.Equal(Phase.Moving, _engine.NextPhase);

        _engine.Acknowledge();
        Assert.Equal(Phase.Moving, _engine.Phase);
    }

    [Fact]
    public void Standing_phase_running_when_desk_is_turned_off_ends_with_a_walk()
    {
        _settings.HasStandingDesk = true;
        _engine.SwitchTo(Phase.Standing);
        _settings.HasStandingDesk = false;

        RunMinutes(15);
        _engine.Acknowledge();

        Assert.Equal(Phase.Moving, _engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), _engine.PhaseDuration);
    }

    [Fact]
    public void Restored_standing_phase_without_a_desk_resumes_then_goes_to_a_walk()
    {
        _settings.HasStandingDesk = true;
        _engine.SwitchTo(Phase.Standing);
        RunMinutes(5);
        var snapshot = _engine.CreateSnapshot();

        _settings.HasStandingDesk = false;
        var clock = new FakeClock();
        var restored = NewEngine(clock);
        restored.Restore(snapshot);
        Assert.Equal(PromptKind.WelcomeBack, restored.Tick());

        restored.Resume();
        Assert.Equal(Phase.Standing, restored.Phase);
        Assert.Equal(TimeSpan.FromMinutes(10), restored.Remaining);

        clock.AdvanceMinutes(10);
        Assert.Equal(PromptKind.PhaseEnded, restored.Tick());
        restored.Acknowledge();
        Assert.Equal(Phase.Moving, restored.Phase);
    }

    [Fact]
    public void Restored_long_sitting_phase_is_clamped_to_the_sit_only_length()
    {
        _settings.HasStandingDesk = true;
        _settings.SittingMinutes = 60;
        _engine.Start();
        var snapshot = _engine.CreateSnapshot();

        _settings.HasStandingDesk = false;
        var restored = NewEngine(new FakeClock());
        restored.Restore(snapshot);

        Assert.Equal(TimeSpan.FromMinutes(40), restored.Remaining);
    }
}

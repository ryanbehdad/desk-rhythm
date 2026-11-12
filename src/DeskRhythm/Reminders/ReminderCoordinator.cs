using System.Windows.Threading;
using DeskRhythm.Core;
using DeskRhythm.Infrastructure;

namespace DeskRhythm.Reminders;

/// <summary>
/// Turns engine prompts into one logical reminder on screen, and turns clicks on any
/// copy back into one engine command. Also owns the 30-second time-out.
/// </summary>
internal sealed class ReminderCoordinator : IDisposable
{
    private readonly AppController _app;
    private readonly RoutineEngine _engine;
    private readonly MonitorOverlayManager _overlays = new();
    private readonly DispatcherTimer _timeout;
    private readonly DispatcherTimer _displayDebounce;
    private ReminderContent? _current;
    private int _attention;
    private bool _hasStandingDesk;

    public ReminderCoordinator(AppController app, RoutineEngine engine, TimeSpan popupTimeout)
    {
        _app = app;
        _engine = engine;
        _hasStandingDesk = app.Settings.HasStandingDesk;
        _timeout = new DispatcherTimer { Interval = popupTimeout };
        _timeout.Tick += (_, _) => HideAndKeepPending();
        _displayDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _displayDebounce.Tick += (_, _) =>
        {
            _displayDebounce.Stop();
            if (_overlays.IsShowing)
            {
                _overlays.Reshow();
            }
        };

        _overlays.ActionChosen += OnActionChosen;
        _overlays.CloseRequested += HideAndKeepPending;
        _overlays.AttentionChanged += OnAttentionChanged;
        _engine.Changed += OnEngineChanged;
    }

    public void ShowPrompt(PromptKind kind)
    {
        if (kind == PromptKind.None)
        {
            return;
        }

        Show(kind == PromptKind.WelcomeBack ? BuildWelcomeBack() : BuildPhaseEnded());
    }

    public void ShowReady() => Show(BuildReady());

    public void ShowTest() => Show(BuildTest());

    /// <summary>Remove every copy now. Does not change the routine.</summary>
    public void HideAll()
    {
        _timeout.Stop();
        _attention = 0;
        _current = null;
        _overlays.Hide();
    }

    public void OnDisplaysChanged()
    {
        if (_overlays.IsShowing)
        {
            _displayDebounce.Stop();
            _displayDebounce.Start();
        }
    }

    /// <summary>
    /// Settings changed. If the desk mode changed, a visible card that talks about the
    /// next phase is out of date: rebuild it in place, with no chime and no new time-out.
    /// </summary>
    public void OnSettingsChanged()
    {
        var hasStandingDesk = _app.Settings.HasStandingDesk;
        if (hasStandingDesk == _hasStandingDesk)
        {
            return;
        }

        _hasStandingDesk = hasStandingDesk;
        var rebuilt = _current?.Kind switch
        {
            ReminderKind.PhaseEnded => BuildPhaseEnded(),
            ReminderKind.WelcomeBack => BuildWelcomeBack(),
            ReminderKind.Ready => BuildReady(),
            _ => null,
        };

        if (rebuilt is null)
        {
            return;
        }

        // The old copies close without reporting "pointer left", so start the count again.
        _current = rebuilt;
        _attention = 0;
        var settings = _app.Settings;
        _overlays.Show(rebuilt, settings.PopupMonitors, settings.PopupCorner);
        if (!_timeout.IsEnabled)
        {
            _timeout.Start();
        }
    }

    public void Dispose()
    {
        _engine.Changed -= OnEngineChanged;
        HideAll();
    }

    private void Show(ReminderContent content, bool chime = true)
    {
        HideAll();
        _current = content;
        var settings = _app.Settings;
        _overlays.Show(content, settings.PopupMonitors, settings.PopupCorner);
        _timeout.Start();

        if (chime && settings.SoundEnabled)
        {
            Chime.Play();
        }
    }

    private void OnActionChosen(ReminderAction action)
    {
        var fromEngine = _current?.ComesFromEngine == true;
        HideAll();
        action.Execute();

        // If the action did not resolve the prompt, it must still come back later.
        if (fromEngine && _engine.IsPromptVisible)
        {
            _engine.DismissPrompt();
        }
    }

    /// <summary>Time-out, close button, Esc or Alt+F4: hide, keep the transition pending.</summary>
    private void HideAndKeepPending()
    {
        var fromEngine = _current?.ComesFromEngine == true;
        HideAll();
        if (fromEngine)
        {
            _engine.DismissPrompt();
        }
    }

    private void OnAttentionChanged(bool attention)
    {
        _attention = Math.Max(0, _attention + (attention ? 1 : -1));
        _timeout.Stop();
        if (_attention == 0 && _current is not null)
        {
            _timeout.Start();
        }
    }

    private void OnEngineChanged(object? sender, EventArgs e)
    {
        // The routine changed from somewhere else (tray, status panel, lock): the card is out of date.
        if (_current is null)
        {
            return;
        }

        if ((_current.ComesFromEngine && !_engine.IsPromptVisible) || (_current.Kind == ReminderKind.Ready && _engine.IsActive))
        {
            HideAll();
        }
    }

    // ------------------------------------------------------------------ content

    private ReminderContent BuildPhaseEnded()
    {
        var phase = _engine.Phase;
        var next = _engine.NextPhase;
        var nextDuration = _engine.DurationOf(next);
        var snoozes = _app.Settings.SnoozeMinutes.Select(_app.Minutes).ToArray();

        var more = new List<ReminderAction?>();
        more.AddRange(snoozes.Skip(1).Select(s => new ReminderAction($"Snooze {PhaseText.Short(s)}", () => _app.Snooze(s))));
        if (more.Count > 0)
        {
            more.Add(null);
        }

        more.AddRange(_app.PausePresets.Select(p => new ReminderAction($"Pause for {PhaseText.Short(p)}", () => _app.Pause(p))));
        if (_engine.CanSkip)
        {
            more.Add(null);
            var skipLabel = (phase, next) switch
            {
                (Phase.Sitting, Phase.Standing) => "Skip this standing break",
                (Phase.Sitting, _) => "Skip this break",
                _ => "Skip this walk",
            };
            more.Add(new ReminderAction(skipLabel, _app.Skip, ToolTip: "Start a new sitting phase now"));
        }

        var (message, detail) = (phase, next) switch
        {
            (Phase.Sitting, Phase.Standing) => ($"You've been sitting for at least {PhaseText.Long(_engine.PhaseDuration)}.",
                                                $"Try {PhaseText.Long(nextDuration)} standing at your desk."),
            (Phase.Sitting, _) => ($"You've been sitting for at least {PhaseText.Long(_engine.PhaseDuration)}.",
                                   $"Stand up and walk for {PhaseText.Long(nextDuration)}. A walk to the kitchen and back counts."),
            (Phase.Standing, _) => ($"You've been standing for about {PhaseText.Long(_engine.PhaseDuration)}.",
                               $"Take a gentle {PhaseText.Adjective(nextDuration)} walk."),
            _ => ($"Your {PhaseText.Adjective(_engine.PhaseDuration)} movement break has ended.",
                  "Sit down whenever you're ready."),
        };

        return new ReminderContent
        {
            Kind = ReminderKind.PhaseEnded,
            Accent = next,
            Title = PhaseText.DueTitle(phase, next),
            Message = message,
            Detail = detail,
            Actions =
            [
                new ReminderAction(PhaseText.AcknowledgeLabel(phase, next), _app.Acknowledge, IsPrimary: true,
                    ToolTip: $"Start the {PhaseText.Gerund(next)} timer now"),
                new ReminderAction($"Snooze {PhaseText.Short(snoozes[0])}", () => _app.Snooze(snoozes[0])),
            ],
            MoreActions = more,
        };
    }

    private ReminderContent BuildWelcomeBack()
    {
        var phase = _engine.Phase;
        var message = _engine.FrozenWhilePending
            ? $"When the timer paused, it was time to {PhaseText.DueAction(phase, _engine.NextPhase)}."
            : $"You were {PhaseText.Gerund(phase)}, with {PhaseText.Long(_engine.Remaining)} left.";

        var more = new List<ReminderAction?>();
        foreach (var p in _app.SelectablePhases)
        {
            more.Add(new ReminderAction(PhaseText.SwitchLabel(p), () => _app.SwitchTo(p)));
        }

        // A change of place (office <-> home) usually ends on this card, so offer the desk switch here.
        var desk = _app.Settings.HasStandingDesk;
        more.Add(null);
        more.Add(new ReminderAction(PhaseText.DeskModeSwitchLabel(desk), () => _app.SwitchDeskAndRestart(!desk),
            ToolTip: "Change the routine and start a fresh sitting phase"));
        more.Add(null);
        more.Add(new ReminderAction("Stop the routine", _app.Stop));

        return new ReminderContent
        {
            Kind = ReminderKind.WelcomeBack,
            Accent = phase,
            Title = "Welcome back",
            Message = message,
            Detail = "Resume where you left off, or restart with a fresh sitting phase.",
            Actions =
            [
                new ReminderAction("Resume", _app.Resume, IsPrimary: true),
                new ReminderAction("Restart cycle", _app.RestartCycle,
                    ToolTip: $"Start a new {PhaseText.Long(_engine.DurationOf(Phase.Sitting))} sitting phase"),
            ],
            MoreActions = more,
        };
    }

    private ReminderContent BuildReady()
    {
        var desk = _app.Settings.HasStandingDesk;
        var sitting = PhaseText.Long(_engine.DurationOf(Phase.Sitting));

        var actions = new List<ReminderAction> { new("Start sitting", _app.Start, IsPrimary: true) };
        if (desk)
        {
            actions.Add(new ReminderAction("I'm standing", () => _app.SwitchTo(Phase.Standing)));
        }

        return new ReminderContent
        {
            Kind = ReminderKind.Ready,
            Accent = Phase.Sitting,
            Title = "Ready when you are",
            Message = "Press Start when you sit down.",
            Detail = desk
                ? $"Desk Rhythm will remind you to stand after {sitting}."
                : $"No standing desk: Desk Rhythm will remind you to take a {PhaseText.Adjective(_engine.DurationOf(Phase.Moving))} walk after {sitting}.",
            Actions = actions,
            MoreActions =
            [
                // A chosen action closes the card, so show the updated Ready card again (no chime).
                new ReminderAction(
                    PhaseText.DeskModeSwitchLabel(desk),
                    () =>
                    {
                        _app.SetStandingDesk(!desk);
                        Show(BuildReady(), chime: false);
                    },
                    ToolTip: "Change the routine. You can also change it in the tray menu or in Settings."),
            ],
            Toggle = new ReminderToggle(
                "Start with Windows",
                "Opens in the tray when you sign in.",
                () => _app.LaunchAtSignIn,
                _app.SetLaunchAtSignIn),
        };
    }

    private ReminderContent BuildTest() => new()
    {
        Kind = ReminderKind.Test,
        Accent = Phase.Standing,
        Title = "Test reminder",
        Message = "If you can see this card on each display, reminders will work.",
        Detail = "Your routine has not changed.",
        Actions = [new ReminderAction("Looks good", () => { }, IsPrimary: true)],
        ShowDisplayInfo = true,
    };
}

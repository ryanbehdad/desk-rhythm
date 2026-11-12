using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using DeskRhythm.Core;
using DeskRhythm.Infrastructure;
using DeskRhythm.Theming;

namespace DeskRhythm.Tray;

/// <summary>
/// The small panel that opens from the tray icon: current phase, time left, next
/// step and the main actions. It hides when it loses focus. Hiding never exits the app.
/// </summary>
public partial class StatusWindow : Window
{
    private readonly AppController _app;
    private readonly Dictionary<Phase, Button> _switchButtons = [];
    private string _actionsKey = string.Empty;
    private Phase? _accent;
    private DateTime _hiddenAtUtc;

    internal StatusWindow(AppController app)
    {
        InitializeComponent();
        _app = app;
        TestBadge.Visibility = app.TestMode ? Visibility.Visible : Visibility.Collapsed;

        CloseButton.Click += (_, _) => HidePanel();
        SettingsButton.Click += (_, _) =>
        {
            HidePanel();
            _app.ShowSettings();
        };
        DeskModeButton.Click += (_, _) => _app.SetStandingDesk(!_app.Settings.HasStandingDesk);
        Deactivated += (_, _) => HidePanel();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                HidePanel();
            }
        };
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);

        foreach (var phase in Enum.GetValues<Phase>())
        {
            var button = new Button { Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(6, 6, 6, 6) };
            var content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var glyph = new PhaseGlyph { Phase = phase, Width = 18, Height = 18 };
            glyph.SetResourceReference(PhaseGlyph.ForegroundProperty, "TextBrush");
            content.Children.Add(glyph);
            content.Children.Add(new TextBlock { Text = PhaseText.Name(phase), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            button.Content = content;
            AutomationProperties.SetName(button, PhaseText.SwitchLabel(phase));
            button.Click += (_, _) => _app.SwitchTo(phase);
            _switchButtons[phase] = button;
            SwitchPanel.Children.Add(button);
        }
    }

    /// <summary>True just after the panel hid because the user clicked the tray icon (so that click must not reopen it).</summary>
    internal bool JustHidden => (DateTime.UtcNow - _hiddenAtUtc).TotalMilliseconds < 400;

    internal void ShowPanel()
    {
        Refresh(force: true);
        WindowPlacer.ShowAt(this, Displays.UnderCursor(), PopupCorner.BottomRight, marginDip: 0, activate: true);
    }

    internal void HidePanel()
    {
        if (!IsVisible)
        {
            return;
        }

        _hiddenAtUtc = DateTime.UtcNow;
        Hide();
    }

    /// <summary>Update the texts. Buttons are rebuilt only when the state really changes.</summary>
    internal void Refresh(bool force = false)
    {
        var e = _app.Engine;
        var accent = e.State == RoutineState.Stopped ? Phase.Sitting : e.Phase;
        if (force || _accent != accent)
        {
            _accent = accent;
            ThemeManager.ApplyAccent(this, accent);
        }

        Glyph.Phase = accent;
        var (label, big, detail) = Describe(e);
        PhaseLabel.Text = label;
        BigText.Text = big;
        BigText.FontSize = big.Length <= 8 ? 34 : 24;
        DetailText.Text = detail;

        var progress = e.Progress;
        ProgressDone.Width = new GridLength(progress, GridUnitType.Star);
        ProgressLeft.Width = new GridLength(1 - progress, GridUnitType.Star);

        var hasDesk = _app.Settings.HasStandingDesk;
        _switchButtons[Phase.Standing].Visibility = hasDesk ? Visibility.Visible : Visibility.Collapsed;
        SwitchPanel.Columns = hasDesk ? 3 : 2;
        DeskModeButton.Content = PhaseText.DeskModeSwitchLabel(hasDesk);
        DeskModeButton.ToolTip = hasDesk
            ? "Sit, then take a short walking break. Applies from your next change of position."
            : "Sit, stand, then take a short walk. Applies from your next change of position.";

        foreach (var (phase, button) in _switchButtons)
        {
            var current = e.IsActive && e.Phase == phase;
            button.SetResourceReference(BackgroundProperty, current ? "AccentSoftBrush" : "ControlBrush");
            button.SetResourceReference(BorderBrushProperty, current ? "AccentBrush" : "ControlStrokeBrush");
            button.ToolTip = current
                ? $"Restart the {PhaseText.Gerund(phase)} timer ({PhaseText.Short(e.DurationOf(phase))})"
                : $"Start a fresh {PhaseText.Gerund(phase)} phase ({PhaseText.Short(e.DurationOf(phase))})";
        }

        var key = $"{e.State}|{e.Phase}|{e.PauseReason}|{hasDesk}";
        if (force || key != _actionsKey)
        {
            _actionsKey = key;
            BuildActions(e);
        }
    }

    private (string Label, string Big, string Detail) Describe(RoutineEngine e)
    {
        var name = PhaseText.Name(e.Phase);
        var frozenBig = e.FrozenWhilePending ? "Due" : PhaseText.Clock(e.Remaining);

        switch (e.State)
        {
            case RoutineState.Stopped:
                return ("Not running", "Ready",
                    _app.Settings.HasStandingDesk
                        ? $"Press Start when you sit down. You'll get a reminder to stand after {PhaseText.Long(e.DurationOf(Phase.Sitting))}."
                        : $"Press Start when you sit down. You'll get a reminder to take a walking break after {PhaseText.Long(e.DurationOf(Phase.Sitting))}.");

            case RoutineState.Running:
                var next = e.NextPhase;
                var nextText = e.Phase == Phase.Moving
                    ? "sit down again"
                    : $"{(next == Phase.Standing ? "stand" : "walk")} for {PhaseText.Long(e.DurationOf(next))}";
                return (name, PhaseText.Clock(e.Remaining), $"Next: {nextText} · reminder at {PhaseText.At(e.Remaining)}");

            case RoutineState.ReminderPending:
                return ($"{name} · action due", PhaseText.DueTitle(e.Phase, e.NextPhase),
                    e.RepromptRemaining is { } again
                        ? $"The reminder shows again in {PhaseText.Clock(again)}. Change position when you can."
                        : "Change position when you can, then confirm below.");

            case RoutineState.Snoozed:
                var snooze = e.SnoozeRemaining ?? TimeSpan.Zero;
                return ($"{name} · snoozed", PhaseText.Clock(snooze),
                    $"\"{PhaseText.DueTitle(e.Phase, e.NextPhase)}\" comes back at {PhaseText.At(snooze)}.");

            case RoutineState.NeedsResync:
                return ("Waiting for you", frozenBig,
                    $"You were {PhaseText.Gerund(e.Phase)}. Resume, or restart with a fresh sitting phase.");

            default:
                return e.PauseReason switch
                {
                    PauseReason.Quiet => ("Quiet mode", frozenBig, "Reminders are hidden while you present. Turn quiet mode off to continue."),
                    PauseReason.Away => ("Paused while you're away", frozenBig, "The timer continues when you're back."),
                    _ when e.PauseRemaining is { } left => ($"Paused · {name}", frozenBig, $"Resumes automatically at {PhaseText.At(left)}."),
                    _ => ($"Paused · {name}", frozenBig, "Paused until you resume."),
                };
        }
    }

    private void BuildActions(RoutineEngine e)
    {
        ActionPanel.Children.Clear();

        switch (e.State)
        {
            case RoutineState.Stopped:
                AddButton("Start sitting", _app.Start, primary: true);
                break;

            case RoutineState.Running:
                AddPauseMenuButton();
                AddButton("Stop", _app.Stop, toolTip: "Stop the routine. Desk Rhythm stays in the tray.");
                break;

            case RoutineState.ReminderPending or RoutineState.Snoozed:
                AddButton(PhaseText.AcknowledgeLabel(e.Phase, e.NextPhase), _app.Acknowledge, primary: true);
                var snooze = _app.Minutes(_app.Settings.SnoozeMinutes[0]);
                AddButton($"Snooze {PhaseText.Short(snooze)}", () => _app.Snooze(snooze));
                AddPauseMenuButton();
                break;

            case RoutineState.NeedsResync:
                AddButton("Resume", _app.Resume, primary: true);
                AddButton("Restart cycle", _app.RestartCycle);
                break;

            default:
                if (e.PauseReason == PauseReason.Quiet)
                {
                    AddButton("Turn off quiet mode", () => _app.SetQuietMode(false), primary: true);
                }
                else
                {
                    AddButton("Resume", _app.Resume, primary: true, enabled: e.PauseReason != PauseReason.Away);
                }

                AddButton("Stop", _app.Stop);
                break;
        }
    }

    private void AddButton(string label, Action action, bool primary = false, bool enabled = true, string? toolTip = null)
    {
        var button = new Button
        {
            Content = label,
            Margin = new Thickness(0, 0, 8, 8),
            IsEnabled = enabled,
            ToolTip = toolTip,
        };
        if (primary)
        {
            button.Style = (Style)FindResource("PrimaryButton");
        }

        button.Click += (_, _) => action();
        ActionPanel.Children.Add(button);
    }

    private void AddPauseMenuButton()
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock { Text = "Pause", VerticalAlignment = VerticalAlignment.Center });
        content.Children.Add(new TextBlock
        {
            Text = "",
            FontFamily = (System.Windows.Media.FontFamily)FindResource("IconFont"),
            FontSize = 10,
            Margin = new Thickness(8, 1, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var button = new Button { Content = content, Margin = new Thickness(0, 0, 8, 8) };
        AutomationProperties.SetName(button, "Pause options");
        button.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
            foreach (var preset in _app.PausePresets)
            {
                menu.Items.Add(MenuItem($"For {PhaseText.Short(preset)}", () => _app.Pause(preset)));
            }

            menu.Items.Add(MenuItem("Until I resume", () => _app.Pause(null)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("Quiet mode (presenting)", () => _app.SetQuietMode(true)));
            menu.IsOpen = true;
        };
        ActionPanel.Children.Add(button);
    }

    private static MenuItem MenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}

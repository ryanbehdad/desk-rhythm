using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DeskRhythm.Core;
using DeskRhythm.Infrastructure;
using DeskRhythm.Theming;

namespace DeskRhythm.Settings;

/// <summary>Every change is saved and applied at once. There is no Save button to forget.</summary>
public partial class SettingsWindow : Window
{
    private readonly AppController _app;
    private bool _loading;

    internal SettingsWindow(AppController app)
    {
        InitializeComponent();
        _app = app;
        MaxHeight = SystemParameters.WorkArea.Height;
        TestModeNote.Visibility = app.TestMode ? Visibility.Visible : Visibility.Collapsed;

        SitGlyph.SetResourceReference(PhaseGlyph.ForegroundProperty, "TextBrush");
        StandGlyph.SetResourceReference(PhaseGlyph.ForegroundProperty, "TextBrush");
        MoveGlyph.SetResourceReference(PhaseGlyph.ForegroundProperty, "TextBrush");
        SitOnlyGlyph.SetResourceReference(PhaseGlyph.ForegroundProperty, "TextBrush");
        BreakGlyph.SetResourceReference(PhaseGlyph.ForegroundProperty, "TextBrush");

        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);

        foreach (var slider in new[] { SittingSlider, StandingSlider, MovingSlider, SitOnlySittingSlider, SitOnlyBreakSlider, RepromptSlider, IdleSlider })
        {
            slider.ValueChanged += (_, _) => OnChanged();
        }

        foreach (var radio in new[] { DeskYes, DeskNo, MonitorsAll, MonitorsMouse, MonitorsPrimary, CornerBottomRight, CornerBottomLeft, CornerTopRight, CornerTopLeft })
        {
            radio.Checked += (_, _) => OnChanged();
        }

        foreach (var toggle in new[] { SoundToggle, LockToggle, IdleToggle })
        {
            toggle.Checked += (_, _) => OnChanged();
            toggle.Unchecked += (_, _) => OnChanged();
        }

        SnoozeBox.LostKeyboardFocus += (_, _) => OnChanged();
        SnoozeBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                OnChanged();
            }
        };

        StartupToggle.Checked += (_, _) => SetStartup(true);
        StartupToggle.Unchecked += (_, _) => SetStartup(false);
        QuietToggle.Checked += (_, _) => SetQuiet(true);
        QuietToggle.Unchecked += (_, _) => SetQuiet(false);

        TestButton.Click += (_, _) => _app.TestReminder();
        ResetButton.Click += (_, _) =>
        {
            _app.ApplySettings(new AppSettings());
            Load();
        };
        FolderButton.Click += (_, _) => OpenFolder();
        UninstallButton.Click += (_, _) => ConfirmUninstall();
        DoneButton.Click += (_, _) => Close();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };

        Load();
    }

    /// <summary>The routine changed elsewhere: update the quiet-mode switch.</summary>
    internal void RefreshRoutineState()
    {
        var wasLoading = _loading;
        _loading = true;
        var engine = _app.Engine;
        QuietToggle.IsChecked = engine.State == RoutineState.Paused && engine.PauseReason == PauseReason.Quiet;
        QuietToggle.IsEnabled = engine.IsActive;
        QuietHint.Text = engine.IsActive
            ? "Hides reminders and holds the timer until you turn it off."
            : "Available while the routine is running.";
        _loading = wasLoading;
    }

    /// <summary>The desk mode changed (here, in the tray, the status panel or on a card): show the real value.</summary>
    internal void RefreshDeskMode()
    {
        var wasLoading = _loading;
        _loading = true;
        var desk = _app.Settings.HasStandingDesk;
        DeskYes.IsChecked = desk;
        DeskNo.IsChecked = !desk;
        UpdateLabels();
        _loading = wasLoading;
    }

    private void Load()
    {
        _loading = true;
        var s = _app.Settings;

        SittingSlider.Value = s.SittingMinutes;
        StandingSlider.Value = s.StandingMinutes;
        MovingSlider.Value = s.MovingMinutes;
        SitOnlySittingSlider.Value = s.SitOnlySittingMinutes;
        SitOnlyBreakSlider.Value = s.SitOnlyBreakMinutes;
        DeskYes.IsChecked = s.HasStandingDesk;
        DeskNo.IsChecked = !s.HasStandingDesk;
        RepromptSlider.Value = s.RepromptMinutes;
        IdleSlider.Value = s.IdleMinutes;

        MonitorsAll.IsChecked = s.PopupMonitors == MonitorMode.AllMonitors;
        MonitorsMouse.IsChecked = s.PopupMonitors == MonitorMode.MouseMonitor;
        MonitorsPrimary.IsChecked = s.PopupMonitors == MonitorMode.PrimaryMonitor;

        CornerBottomRight.IsChecked = s.PopupCorner == PopupCorner.BottomRight;
        CornerBottomLeft.IsChecked = s.PopupCorner == PopupCorner.BottomLeft;
        CornerTopRight.IsChecked = s.PopupCorner == PopupCorner.TopRight;
        CornerTopLeft.IsChecked = s.PopupCorner == PopupCorner.TopLeft;

        SnoozeBox.Text = string.Join(", ", s.SnoozeMinutes);
        SoundToggle.IsChecked = s.SoundEnabled;
        LockToggle.IsChecked = s.PauseOnLockOrSleep;
        IdleToggle.IsChecked = s.PauseWhenIdle;

        RefreshStartupState();
        RefreshRoutineState();
        UpdateLabels();
        ShowSnoozeHint(valid: true);
        _loading = false;
    }

    private void OnChanged()
    {
        UpdateLabels();
        if (_loading)
        {
            return;
        }

        var s = _app.Settings.Clone();
        s.SittingMinutes = (int)SittingSlider.Value;
        s.StandingMinutes = (int)StandingSlider.Value;
        s.MovingMinutes = (int)MovingSlider.Value;
        s.SitOnlySittingMinutes = (int)SitOnlySittingSlider.Value;
        s.SitOnlyBreakMinutes = (int)SitOnlyBreakSlider.Value;
        s.HasStandingDesk = DeskNo.IsChecked != true;
        s.RepromptMinutes = (int)RepromptSlider.Value;
        s.IdleMinutes = (int)IdleSlider.Value;
        s.PopupMonitors = MonitorsMouse.IsChecked == true ? MonitorMode.MouseMonitor
            : MonitorsPrimary.IsChecked == true ? MonitorMode.PrimaryMonitor
            : MonitorMode.AllMonitors;
        s.PopupCorner = CornerBottomLeft.IsChecked == true ? PopupCorner.BottomLeft
            : CornerTopRight.IsChecked == true ? PopupCorner.TopRight
            : CornerTopLeft.IsChecked == true ? PopupCorner.TopLeft
            : PopupCorner.BottomRight;
        s.SoundEnabled = SoundToggle.IsChecked == true;
        s.PauseOnLockOrSleep = LockToggle.IsChecked == true;
        s.PauseWhenIdle = IdleToggle.IsChecked == true;

        var snooze = AppSettings.TryParseSnooze(SnoozeBox.Text);
        ShowSnoozeHint(snooze is not null);
        if (snooze is not null)
        {
            s.SnoozeMinutes = snooze;
        }

        _app.ApplySettings(s);
    }

    private void UpdateLabels()
    {
        SittingValue.Text = $"{(int)SittingSlider.Value} min";
        StandingValue.Text = $"{(int)StandingSlider.Value} min";
        MovingValue.Text = $"{(int)MovingSlider.Value} min";
        SitOnlySittingValue.Text = $"{(int)SitOnlySittingSlider.Value} min";
        SitOnlyBreakValue.Text = $"{(int)SitOnlyBreakSlider.Value} min";

        var desk = DeskNo.IsChecked != true;
        StandingDeskPanel.Visibility = desk ? Visibility.Visible : Visibility.Collapsed;
        SitOnlyPanel.Visibility = desk ? Visibility.Collapsed : Visibility.Visible;
        DeskHint.Text = desk
            ? "Sit, stand, then take a short walk."
            : "Sit, then get up and walk. In one study, a 5-minute walk every 30 minutes gave the clearest benefit. Set Sitting to 30 min for that pattern.";
        RepromptValue.Text = $"{(int)RepromptSlider.Value} min";
        IdleValue.Text = $"{(int)IdleSlider.Value} min";
        IdleRow.IsEnabled = IdleToggle.IsChecked == true;
        IdleRow.Opacity = IdleRow.IsEnabled ? 1 : 0.5;
    }

    private void ShowSnoozeHint(bool valid)
    {
        SnoozeHint.Text = valid
            ? "Up to three numbers, for example 5, 10, 15. The first one is on the reminder."
            : $"Use one to three whole numbers from {AppSettings.SnoozeMin} to {AppSettings.SnoozeMax}, for example 5, 10, 15.";
        SnoozeHint.SetResourceReference(TextBlock.ForegroundProperty, valid ? "SubtleTextBrush" : "ErrorBrush");
    }

    /// <summary>"Start with Windows" changed from the tray menu or the Ready card: show the real state.</summary>
    internal void RefreshStartupState()
    {
        var wasLoading = _loading;
        _loading = true;
        StartupToggle.IsChecked = _app.LaunchAtSignIn;
        _loading = wasLoading;
    }

    private void SetStartup(bool enabled)
    {
        if (_loading)
        {
            return;
        }

        if (_app.SetLaunchAtSignIn(enabled))
        {
            StartupHint.Text = "For your user account only. No admin rights needed.";
            StartupHint.SetResourceReference(TextBlock.ForegroundProperty, "SubtleTextBrush");
        }
        else
        {
            StartupHint.Text = "Windows did not allow this change. Your IT policy may block startup apps.";
            StartupHint.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        }
    }

    private void SetQuiet(bool on)
    {
        if (!_loading)
        {
            _app.SetQuietMode(on);
        }
    }

    private void ConfirmUninstall()
    {
        var answer = MessageBox.Show(
            this,
            "Remove Desk Rhythm from this PC?\n\n" +
            "• Launch at Windows sign-in is turned off.\n" +
            "• Your settings and saved state are deleted.\n" +
            "• Desk Rhythm closes.\n\n" +
            "Then File Explorer opens at DeskRhythm.exe. Delete that file to finish.",
            "Uninstall Desk Rhythm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (answer == MessageBoxResult.Yes)
        {
            _app.Uninstall();
        }
    }

    private static void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.DataDirectory}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Open folder");
        }
    }
}

using System.Windows;
using System.Windows.Threading;
using DeskRhythm.Core;
using DeskRhythm.Infrastructure;
using DeskRhythm.Reminders;
using DeskRhythm.Settings;
using DeskRhythm.Theming;
using DeskRhythm.Tray;

namespace DeskRhythm;

/// <summary>
/// Connects the routine engine to Windows and to the UI. It owns the one-second
/// UI timer. That timer only asks the engine "is anything due?" and refreshes the
/// display; all timing decisions are in <see cref="RoutineEngine"/>.
/// </summary>
internal sealed class AppController : IDisposable
{
    private static readonly TimeSpan MissedTimeThreshold = TimeSpan.FromMinutes(2);

    private readonly JsonFileStore<AppSettings> _settingsStore;
    private readonly JsonFileStore<RoutineSnapshot> _stateStore;
    private readonly DispatcherTimer _timer;
    private readonly WindowsSessionMonitor _session;
    private readonly ReminderCoordinator _reminders;
    private readonly TrayHost _tray;
    private StatusWindow? _status;
    private SettingsWindow? _settingsWindow;
    private long _lastTickMs;
    private long _lastSaveMs;
    private bool _exiting;
    private bool _uninstalling;

    public AppController(bool testMode)
    {
        TestMode = testMode;
        _settingsStore = new JsonFileStore<AppSettings>(AppPaths.Settings, () => new AppSettings());
        _stateStore = new JsonFileStore<RoutineSnapshot>(AppPaths.State(testMode), () => RoutineSnapshot.Inactive(DateTimeOffset.Now));
        Settings = _settingsStore.Load().Normalize();

        Engine = new RoutineEngine(new SystemClock(), new RoutineTimings
        {
            PhaseDuration = PhaseDuration,
            RepromptDelay = () => Minutes(Settings.RepromptMinutes),
            ShortAwayGrace = testMode ? TimeSpan.FromSeconds(5) : TimeSpan.FromMinutes(5),
            LongAbsence = testMode ? TimeSpan.FromSeconds(60) : TimeSpan.FromMinutes(60),
        });

        _session = new WindowsSessionMonitor(Dispatcher.CurrentDispatcher);
        ApplySessionSettings();
        _session.AwayChanged += OnAwayChanged;
        _session.DisplaysChanged += OnDisplaysChanged;
        _session.ThemeChanged += OnThemeChanged;

        _reminders = new ReminderCoordinator(this, Engine, testMode ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(30));
        _tray = new TrayHost(this);
        Engine.Changed += OnEngineChanged;

        _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => OnTick();
    }

    public bool TestMode { get; }

    public AppSettings Settings { get; private set; }

    public RoutineEngine Engine { get; }

    /// <summary>The pause lengths offered everywhere: 30 min, 1 hour, 2 hours.</summary>
    public IReadOnlyList<TimeSpan> PausePresets => [Minutes(30), Minutes(60), Minutes(120)];

    /// <summary>A "minute" from the settings. In test mode it is one second, so flows can be checked quickly.</summary>
    public TimeSpan Minutes(int minutes) => TestMode ? TimeSpan.FromSeconds(minutes) : TimeSpan.FromMinutes(minutes);

    public void Start(bool launchedAtSignIn)
    {
        StartupRegistration.RefreshPathIfEnabled();

        // Never assume the user kept the same posture while the app was closed.
        Engine.Restore(_stateStore.Load());

        _lastTickMs = Environment.TickCount64;
        _lastSaveMs = _lastTickMs;
        _timer.Start();
        _tray.Refresh();

        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            if (Engine.State == RoutineState.Stopped)
            {
                _reminders.ShowReady();
            }
            else
            {
                OnTick();
            }
        });
    }

    // ------------------------------------------------------------- commands

    public void Start() => Engine.Start();

    public void Acknowledge() => Engine.Acknowledge();

    public void Snooze(TimeSpan delay) => Engine.Snooze(delay);

    public void Skip() => Engine.Skip();

    public void Pause(TimeSpan? duration) => Engine.Pause(duration);

    public void Resume() => Engine.Resume();

    public void RestartCycle() => Engine.RestartCycle();

    public void SwitchTo(Phase phase) => Engine.SwitchTo(phase);

    public void Stop() => Engine.Stop();

    public void SetQuietMode(bool on)
    {
        if (on)
        {
            Engine.Pause(null, PauseReason.Quiet);
        }
        else if (Engine.State == RoutineState.Paused && Engine.PauseReason == PauseReason.Quiet)
        {
            Engine.Resume();
        }
    }

    public void TestReminder() => _reminders.ShowTest();

    /// <summary>"Start with Windows". The registry is the only source of truth, so Task Manager changes are seen too.</summary>
    public bool LaunchAtSignIn
    {
        get
        {
            try
            {
                return StartupRegistration.IsEnabled();
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, "Read startup setting");
                return false;
            }
        }
    }

    /// <summary>Turn "Start with Windows" on or off. Returns false if Windows or a policy refused.</summary>
    public bool SetLaunchAtSignIn(bool enabled)
    {
        try
        {
            StartupRegistration.SetEnabled(enabled);
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Startup registration");
            return false;
        }
        finally
        {
            _settingsWindow?.RefreshStartupState();
        }
    }

    public void ShowStatus()
    {
        _status ??= new StatusWindow(this);
        if (!_status.IsVisible)
        {
            _status.ShowPanel();
        }
        else
        {
            _status.Activate();
        }
    }

    public void ToggleStatus()
    {
        _status ??= new StatusWindow(this);
        if (_status.IsVisible)
        {
            _status.HidePanel();
        }
        else if (!_status.JustHidden)
        {
            _status.ShowPanel();
        }
    }

    public void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
    }

    public void ApplySettings(AppSettings settings)
    {
        Settings = settings.Clone().Normalize();
        try
        {
            _settingsStore.Save(Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(ex, "Save settings");
        }

        ApplySessionSettings();
        _tray.Refresh();
        _status?.Refresh(force: true);
    }

    public void Exit()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Remove every trace except the exe: the startup entry and the data folder.
    /// A running exe cannot delete itself, so File Explorer opens at it instead.
    /// </summary>
    public void Uninstall()
    {
        _uninstalling = true;
        _timer.Stop();
        try
        {
            StartupRegistration.SetEnabled(false);
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Uninstall: startup entry");
        }

        Exit();
    }

    public void SaveState()
    {
        if (_uninstalling)
        {
            return;
        }

        try
        {
            _stateStore.Save(Engine.CreateSnapshot());
            _lastSaveMs = Environment.TickCount64;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(ex, "Save state");
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        SaveState();
        Engine.Changed -= OnEngineChanged;
        _reminders.Dispose();
        _session.Dispose();
        _tray.Dispose();
        _settingsWindow?.Close();
        _status?.Close();

        if (_uninstalling)
        {
            RemoveDataAndShowExe();
        }
    }

    private static void RemoveDataAndShowExe()
    {
        try
        {
            if (Directory.Exists(AppPaths.DataDirectory))
            {
                Directory.Delete(AppPaths.DataDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing useful to do: the folder is small and the user can delete it.
        }

        try
        {
            if (Environment.ProcessPath is { } exe)
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{exe}\"");
            }
        }
        catch (Exception)
        {
        }
    }

    // --------------------------------------------------------------- events

    private void OnTick()
    {
        var now = Environment.TickCount64;
        var gap = TimeSpan.FromMilliseconds(now - _lastTickMs);
        _lastTickMs = now;

        // The machine slept and Windows did not tell us: treat it like an absence.
        if (gap > MissedTimeThreshold && Settings.PauseOnLockOrSleep)
        {
            Engine.ReportMissedTime(gap);
        }

        _session.PollIdle();
        _reminders.ShowPrompt(Engine.Tick());

        _tray.Refresh();
        if (_status?.IsVisible == true)
        {
            _status.Refresh();
        }

        if (Engine.IsActive && now - _lastSaveMs > 60_000)
        {
            SaveState();
        }
    }

    private void OnEngineChanged(object? sender, EventArgs e)
    {
        SaveState();
        _tray.Refresh();
        if (_status?.IsVisible == true)
        {
            _status.Refresh();
        }

        _settingsWindow?.RefreshRoutineState();
    }

    private void OnAwayChanged(bool away, TimeSpan alreadyAway)
    {
        if (away)
        {
            _reminders.HideAll();
            _status?.HidePanel();
            Engine.BeginAway(alreadyAway);
        }
        else
        {
            Engine.EndAway();
        }

        _lastTickMs = Environment.TickCount64;
    }

    private void OnDisplaysChanged()
    {
        _status?.HidePanel();
        _reminders.OnDisplaysChanged();
    }

    private void OnThemeChanged()
    {
        ThemeManager.Apply();
        _tray.Refresh();
        _status?.Refresh(force: true);
    }

    private void ApplySessionSettings()
    {
        _session.PauseOnLockOrSleep = Settings.PauseOnLockOrSleep;
        _session.IdleThreshold = Settings.PauseWhenIdle ? Minutes(Settings.IdleMinutes) : null;
    }

    private TimeSpan PhaseDuration(Phase phase) => TestMode
        ? phase switch
        {
            Phase.Sitting => TimeSpan.FromSeconds(10),
            Phase.Standing => TimeSpan.FromSeconds(5),
            _ => TimeSpan.FromSeconds(3),
        }
        : TimeSpan.FromMinutes(Settings.MinutesFor(phase));
}

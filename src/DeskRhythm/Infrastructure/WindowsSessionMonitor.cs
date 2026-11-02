using System.Windows.Threading;
using Microsoft.Win32;

namespace DeskRhythm.Infrastructure;

/// <summary>
/// Turns Windows events into two simple signals for the app:
/// "the user is away / back" (lock, sleep, optional idle) and "the displays changed".
/// </summary>
internal sealed class WindowsSessionMonitor : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private AwayReason _reasons;

    public WindowsSessionMonitor(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    [Flags]
    private enum AwayReason
    {
        None = 0,
        Locked = 1,
        Sleeping = 2,
        Idle = 4,
    }

    /// <summary>Raised on the UI thread. Arguments: away now, and for how long the user has already been away.</summary>
    public event Action<bool, TimeSpan>? AwayChanged;

    public event Action? DisplaysChanged;

    public event Action? ThemeChanged;

    public bool PauseOnLockOrSleep { get; set; } = true;

    /// <summary>Null: idle detection off.</summary>
    public TimeSpan? IdleThreshold { get; set; }

    /// <summary>Call once a second from the UI timer. Reads only the idle duration.</summary>
    public void PollIdle()
    {
        var idleFlag = (_reasons & AwayReason.Idle) != 0;
        if (IdleThreshold is not { } threshold)
        {
            if (idleFlag)
            {
                SetReason(AwayReason.Idle, false, TimeSpan.Zero);
            }

            return;
        }

        var idle = NativeMethods.GetIdleTime();
        if (!idleFlag && idle >= threshold)
        {
            SetReason(AwayReason.Idle, true, idle);
        }
        else if (idleFlag && idle < threshold)
        {
            SetReason(AwayReason.Idle, false, TimeSpan.Zero);
        }
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                Post(() => SetLockOrSleep(AwayReason.Locked, true));
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
                Post(() => SetLockOrSleep(AwayReason.Locked, false));
                break;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            Post(() => SetLockOrSleep(AwayReason.Sleeping, true));
        }
        else if (e.Mode == PowerModes.Resume)
        {
            Post(() => SetLockOrSleep(AwayReason.Sleeping, false));
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Post(() => DisplaysChanged?.Invoke());

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
        {
            Post(() => ThemeChanged?.Invoke());
        }
    }

    private void SetLockOrSleep(AwayReason reason, bool on)
    {
        // With the setting off we still clear a flag, so the app can never stay stuck "away".
        if (on && !PauseOnLockOrSleep)
        {
            return;
        }

        SetReason(reason, on, TimeSpan.Zero);
    }

    private void SetReason(AwayReason reason, bool on, TimeSpan alreadyAway)
    {
        var wasAway = _reasons != AwayReason.None;
        _reasons = on ? _reasons | reason : _reasons & ~reason;
        var isAway = _reasons != AwayReason.None;

        if (wasAway != isAway)
        {
            AwayChanged?.Invoke(isAway, alreadyAway);
        }
    }

    // SystemEvents fire on their own thread. Everything in the app runs on the UI thread.
    private void Post(Action action) => _dispatcher.BeginInvoke(action);
}

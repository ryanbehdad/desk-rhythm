# Desk Rhythm

A quiet sit–stand–move reminder for Windows 11. It runs in the notification area (system tray). It shows a small reminder card on **every monitor** when it is time to change position.

Default routine:

| Phase    | Duration | Reminder at the end     | Button you press |
|----------|---------:|-------------------------|------------------|
| Sitting  | 30 min   | **Time to stand**       | I'm standing     |
| Standing | 15 min   | **Time to move**        | Start walk       |
| Moving   | 2 min    | **Ready to sit?**       | I'm sitting      |

The next phase starts **when you press the button**, not when the reminder appears.

### No standing desk? (for example at home)

Turn off **I have a standing desk** (tray menu, status panel or Settings). The routine then has no standing phase:

| Phase         | Duration | Reminder at the end     | Button you press |
|---------------|---------:|-------------------------|------------------|
| Sitting       | 40 min   | **Time for a break**    | Start break      |
| Walking break | 5 min    | **Ready to sit?**       | I'm sitting      |

The two routines keep their own durations, so you can switch between home and office without losing your settings. A change applies from your next change of position: the phase that runs now keeps its length, and a reminder that is on screen updates at once. (If Desk Rhythm restarts during that phase, the phase you resume gets the length of the new routine.)

Why a walk and not only "stand up"? In a small study at Columbia University (Diaz et al., 2023), a **5-minute walk every 30 minutes** was the only pattern that lowered both blood sugar after meals and blood pressure during 8 hours of sitting. Walks every 60 minutes did not lower blood sugar. If 40 minutes is too long for you, set the sitting time to 30 minutes.

> Desk Rhythm is a gentle movement prompt. It is not a health tracker, and the timings are not medical advice.

---

## Download

**[⬇ Download DeskRhythm.exe from the latest release](../../releases/latest)**. You do not need to compile anything.

1. On the release page, under **Assets**, click **`DeskRhythm.exe`**.
2. Move it to a permanent folder, for example `%LOCALAPPDATA%\Programs\DeskRhythm\`.
3. Double-click it. A card "Ready when you are" appears. Press **Start sitting**.
4. Optional: turn on **Start with Windows** on that card. Desk Rhythm then opens in the tray each time you sign in.

You do not need administrator rights, an installer, .NET, an account or an internet connection. The one exe has everything it needs (Windows 10/11, 64-bit).

**"Windows protected your PC"?** The exe is not code-signed, so SmartScreen can warn you. Click **More info → Run anyway** if you trust this source. On a work PC, ask your IT team before you run it. Each release also has a `DeskRhythm.exe.sha256` file, so you can check the download:

```powershell
Get-FileHash .\DeskRhythm.exe -Algorithm SHA256
```

## Uninstall

1. Right-click the tray icon → **Settings…** → **Uninstall…** → **Yes**.
   This turns off launch at sign-in, deletes your settings, closes the app and opens File Explorer at the exe.
2. Delete `DeskRhythm.exe`.

If the exe is already gone: in Task Manager → **Startup apps**, disable Desk Rhythm (or delete the value `DeskRhythm` in `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`), then delete the folder `%LOCALAPPDATA%\DeskRhythm`.

## Daily use

| You want to…                      | Do this |
|-----------------------------------|---------|
| See the time left                 | Point to the tray icon (tooltip), or click it to open the status panel. |
| Change position early             | Status panel → **I'm now…** → Sitting / Standing / Moving. Or right-click the tray icon → **Switch phase**. |
| Delay a reminder                  | **Snooze 5 min** on the card. **More** has 10 and 15 min, and Pause for 30 min / 1 hour / 2 hours. |
| Skip a break                      | Card → **More** → **Skip this standing break** / **Skip this walk** (no standing desk: **Skip this break**). A new sitting phase starts. |
| Hide a card without deciding      | **×**, Esc, or do nothing. The card hides after 30 s and comes back after 5 min (you can change this). |
| Work without a standing desk      | Right-click tray → turn off **I have a standing desk**. Or status panel → **No standing desk here**. When you come back after a long time away, the "Welcome back" card also has this under **More**. |
| Go into a meeting                 | Right-click tray → **Pause 1 hour** (or 30 min / 2 hours). |
| Present                           | Right-click tray → **Quiet mode (presenting)**. Turn it off when you finish. |
| Stop for the day                  | Right-click tray → **Stop routine**. |
| Start automatically with Windows  | Right-click tray → **Start with Windows** (a check mark shows it is on). The same switch is on the "Ready" card and in Settings. Turn it off the same way, or in Task Manager → Startup apps. |
| Close the app                     | Right-click tray → **Exit Desk Rhythm**. Closing a window does not exit the app. |

### Tray icon

- **Ring that fills**: a phase is running. The colour shows the phase (blue sitting, teal standing, amber moving).
- **Solid disc**: action due.
- **Two bars**: paused, quiet mode, away, or waiting for you.
- **Empty ring**: stopped.

### When you come back

- **Windows locked or asleep**: the timer stops. If you were away less than 5 minutes, it continues by itself. If you were away longer, one card asks **Resume** or **Restart cycle**. You never get a pile of old reminders.
- **App closed or PC restarted**: at the next start, the same card asks you. The app never assumes that you sat during that time.

## Settings

Right-click the tray icon → **Settings…**. All changes save and apply immediately.

| Setting | Default |
|---|---|
| Standing desk | On |
| Sitting / Standing / Moving (with a standing desk) | 30 / 15 / 2 min (ranges 5–120, 1–60, 1–15) |
| Sitting / Walking break (no standing desk) | 40 / 5 min (ranges 5–120, 1–15) |
| Show reminders on | All monitors (or: monitor with mouse, primary monitor) |
| Corner | Bottom right |
| Remind again after an ignored popup | 5 min |
| Snooze options | 5, 10, 15 min |
| Soft chime | Off |
| Pause when locked or asleep | On |
| Pause when keyboard and mouse are idle | Off (10 min when on) |
| Launch at Windows sign-in | Off |
| Quiet mode | Off |

**Test reminder on all monitors** shows a card on each display, labelled for example "Display 2 of 3 · 150 %".

Files are in `%LOCALAPPDATA%\DeskRhythm\`:

- `settings.json`: your settings
- `state.json`: the routine state, used to recover after a restart
- `error.log`: only if something goes wrong (max 1 MB)

To reset everything, exit the app and delete this folder.

## Build from source

You only need this if you want to change the code. Everyone else can use the [download](#download).

Requirements: Windows 10/11 x64 and the **.NET 10 SDK**.

If you do not have the SDK and do not have admin rights, install it for your user only:

```powershell
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
.\dotnet-install.ps1 -Channel 10.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"
```

Then double-click **`build.cmd`**. It:

1. finds the .NET 10 SDK (on PATH, in `%LOCALAPPDATA%\Microsoft\dotnet`, or in Program Files),
2. runs the unit tests,
3. writes one self-contained file: `dist\DeskRhythm.exe` (about 150 MB).

Manual commands:

```powershell
dotnet test tests\DeskRhythm.Core.Tests
dotnet publish src\DeskRhythm\DeskRhythm.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o dist
```

### Publish a release (maintainers)

The workflow [.github/workflows/build.yml](.github/workflows/build.yml) runs the tests and builds the exe on every push. To publish a downloadable release, do **one** of these:

- **On GitHub:** **Actions** → **Build** → **Run workflow** → type a version (for example `1.0.1`) → **Run workflow**.
- **With git:** `git tag v1.0.1` and then `git push origin v1.0.1`.

GitHub then makes the release `v1.0.1` with `DeskRhythm.exe` and its SHA-256 checksum attached. The exe is not stored in the repository: it is too large (GitHub refuses files over 100 MB), and it is rebuilt from the source for each release.

### Test mode

```
DeskRhythm.exe --test-mode
```

Phases last 10 s / 5 s / 3 s (no standing desk: 10 s sitting, 4 s break). Every other "minute" (snooze, pause, re-prompt, idle) counts as one second, and the time-out is 10 s. Test mode keeps its own state file (`state.test.json`) and shows "TEST MODE" in the status panel and the tooltip. It is only on when you give the flag.

## Project layout

```
src/DeskRhythm.Core/          No UI. Unit-tested.
  RoutineEngine.cs            The state machine: Sitting/Standing/Moving (or Sitting/Moving with no standing desk), pending, snoozed, paused, resync.
  Routine.cs                  Enums, clock interface, timings, snapshot.
  AppSettings.cs              Settings, defaults, validation.
  JsonFileStore.cs            Safe JSON read/write (temp file + replace).
src/DeskRhythm/               WPF app (.NET 10, net10.0-windows)
  App.xaml.cs                 Bootstrap: single instance, error logging.
  AppController.cs            Connects engine, Windows events and UI. Owns the 1-second UI timer.
  Reminders/                  ReminderCoordinator (one logical reminder), MonitorOverlayManager (one card per display), ReminderWindow.
  Tray/                       TrayHost (icon, tooltip, menu), TrayIconRenderer, StatusWindow.
  Settings/                   SettingsWindow.
  Infrastructure/             Displays, WindowPlacer (DPI-safe placement), WindowsSessionMonitor (lock/sleep/idle/display changes),
                              SingleInstance, StartupRegistration, NativeMethods, ErrorLog, Chime.
  Theming/                    Styles.xaml, ThemeManager (light/dark, phase colours), PhaseGlyph (pictograms).
tests/DeskRhythm.Core.Tests/  xUnit tests for the engine and settings.
tools/make_icon.py            Makes Assets/DeskRhythm.ico (only needed to change the icon).
docs/MANUAL_TEST_CHECKLIST.md Three-monitor manual test.
.github/workflows/build.yml   CI: test, build, and publish the exe to GitHub Releases.
build.cmd                     Local one-click test + build.
```

### How timing works

- The engine uses **deadlines on a monotonic clock** (`Environment.TickCount64`). It does not count ticks. The 1-second UI timer only asks "is anything due?" and refreshes the display.
- Changes to the Windows clock or time zone have no effect on reminders.
- Lock, sleep and (optional) idle freeze the countdown. If a sleep is not reported by Windows, a gap of more than 2 minutes between ticks is treated as an absence.
- The engine never asks for a second copy of a prompt while one is visible, so duplicates cannot stack.

### Multi-monitor and DPI

- The app is per-monitor DPI aware (PerMonitorV2, in `app.manifest`).
- Each card is created directly on its target display, so it starts with that display's DPI. Then it is measured in physical pixels and put in the corner of the display's **working area** (clear of the taskbar), with a 20 px margin.
- Negative coordinates (displays left of or above the primary) work.
- When displays are added, removed or changed, visible cards are rebuilt on the displays that exist.
- Cards are topmost, do not show in the taskbar or Alt+Tab, and **do not take keyboard focus** when they open.

### Resource use

Rendering is software-only. This keeps the graphics driver out of the process: about 30 MB private memory, compared with about 145 MB with GPU rendering. A few small cards do not need the GPU.

## Troubleshooting

| Problem | What to do |
|---|---|
| No tray icon | Windows can hide new icons. Click **^** on the taskbar, or Settings → Personalization → Taskbar → Other system tray icons → turn on Desk Rhythm. |
| Starting it again does nothing | One copy is already running. The second start opens the status panel of the first one. |
| No card over a full-screen game or video | Exclusive full-screen apps can draw over all windows. See Limitations. |
| "Windows protected your PC" or blocked by IT | The exe is not code-signed. Ask your IT team to approve or allow-list it. Do not bypass security controls. |
| Launch at sign-in does not work | Check Task Manager → Startup apps → Desk Rhythm is **Enabled**. Your IT policy may block startup apps. |
| Something is wrong | Look in `%LOCALAPPDATA%\DeskRhythm\error.log`. |

## Limitations

- Cards cannot show on the lock screen, the UAC prompt (secure desktop), or over **exclusive** full-screen apps. Windows does not allow this.
- Other topmost windows (for example some call windows) can be on top of a card.
- Windows **Do Not Disturb / Focus** does not hide these cards, because they are app windows and not Windows notifications. Use **Quiet mode** or **Pause** when you present.
- The exe is not signed. Some company policies (SmartScreen, AppLocker, WDAC) block unsigned apps until IT approves them.
- Idle detection reads only the time since the last input. It does not read keys or app content.
- No automatic presentation detection in v1. Use Quiet mode.

## Privacy

No telemetry, no network access, no accounts. Settings and state are small local JSON files.

## Licence

[MIT](LICENSE). Use, change and share it freely. There is no warranty.

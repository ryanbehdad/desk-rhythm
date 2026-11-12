using DeskRhythm.Core;
using DeskRhythm.Theming;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DeskRhythm.Tray;

/// <summary>The notification-area icon, its tooltip and its right-click menu.</summary>
internal sealed class TrayHost : IDisposable
{
    private readonly AppController _app;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private Drawing.Icon? _currentIcon;
    private string _iconKey = string.Empty;

    public TrayHost(AppController app)
    {
        _app = app;
        _menu = new Forms.ContextMenuStrip { ShowCheckMargin = true, ShowImageMargin = false };
        _menu.Opening += (_, e) =>
        {
            BuildMenu();
            e.Cancel = false;
        };

        _icon = new Forms.NotifyIcon
        {
            Text = "Desk Rhythm",
            ContextMenuStrip = _menu,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                _app.ToggleStatus();
            }
        };

        Refresh();
        _icon.Visible = true;
    }

    /// <summary>Update the tooltip and icon. Cheap: the icon is redrawn only when it looks different.</summary>
    public void Refresh()
    {
        var text = TooltipText();
        if (text.Length > 127)
        {
            text = text[..127];
        }

        if (_icon.Text != text)
        {
            _icon.Text = text;
        }

        var engine = _app.Engine;
        var (glyph, phase) = engine.State switch
        {
            RoutineState.Stopped => (TrayGlyph.Stopped, engine.Phase),
            RoutineState.Running => (TrayGlyph.Running, engine.Phase),
            RoutineState.ReminderPending or RoutineState.Snoozed => (TrayGlyph.Due, engine.NextPhase),
            _ => (TrayGlyph.Paused, engine.Phase),
        };

        var size = Math.Max(16, Forms.SystemInformation.SmallIconSize.Width);
        var step = (int)Math.Round(engine.Progress * 32);
        var key = $"{glyph}|{phase}|{step}|{size}|{ThemeManager.IsTaskbarDark}";
        if (key == _iconKey)
        {
            return;
        }

        _iconKey = key;
        var old = _currentIcon;
        _currentIcon = TrayIconRenderer.Render(size, glyph, ThemeManager.TrayAccent(phase), step / 32.0, ThemeManager.IsTaskbarDark);
        _icon.Icon = _currentIcon;
        old?.Dispose();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _currentIcon?.Dispose();
    }

    private string TooltipText()
    {
        var e = _app.Engine;
        var prefix = _app.TestMode ? "Desk Rhythm [test mode]" : "Desk Rhythm";
        var status = e.State switch
        {
            RoutineState.Stopped => "Not running. Click to start.",
            RoutineState.Running => $"{PhaseText.Name(e.Phase)}: {PhaseText.Clock(e.Remaining)} remaining",
            RoutineState.ReminderPending => $"Action due: {PhaseText.DueTitle(e.Phase, e.NextPhase).ToLowerInvariant()}",
            RoutineState.Snoozed => $"Snoozed: {PhaseText.DueTitle(e.Phase, e.NextPhase).ToLowerInvariant()} in {PhaseText.Clock(e.SnoozeRemaining ?? TimeSpan.Zero)}",
            RoutineState.NeedsResync => "Waiting for you: resume or restart",
            _ => e.PauseReason switch
            {
                PauseReason.Quiet => "Quiet mode: reminders hidden",
                PauseReason.Away => "Paused while you're away",
                _ when e.PauseRemaining is { } left => $"Paused until {PhaseText.At(left)}",
                _ => "Paused",
            },
        };

        return $"{prefix} — {status}";
    }

    private void BuildMenu()
    {
        var e = _app.Engine;
        _menu.Renderer = new TrayMenuRenderer(ThemeManager.IsDark);
        _menu.Items.Clear();

        var status = Item("Show status", _app.ShowStatus);
        status.Font = new Drawing.Font(status.Font, Drawing.FontStyle.Bold);
        _menu.Items.Add(status);
        _menu.Items.Add(new Forms.ToolStripSeparator());

        if (e.State == RoutineState.Stopped)
        {
            _menu.Items.Add(Item("Start sitting", _app.Start));
        }

        if (e.IsTransitionDue)
        {
            _menu.Items.Add(Item(PhaseText.AcknowledgeLabel(e.Phase, e.NextPhase), _app.Acknowledge));
            var snooze = _app.Minutes(_app.Settings.SnoozeMinutes[0]);
            _menu.Items.Add(Item($"Snooze {PhaseText.Short(snooze)}", () => _app.Snooze(snooze)));
        }

        if (e.State is RoutineState.Paused or RoutineState.NeedsResync)
        {
            _menu.Items.Add(Item("Resume", _app.Resume));
        }
        else
        {
            _menu.Items.Add(Item("Pause", () => _app.Pause(null), enabled: e.IsActive));
        }

        foreach (var preset in _app.PausePresets)
        {
            _menu.Items.Add(Item($"Pause {PhaseText.Short(preset)}", () => _app.Pause(preset), enabled: e.IsActive));
        }

        var quiet = Item("Quiet mode (presenting)", () => _app.SetQuietMode(e.PauseReason != PauseReason.Quiet), enabled: e.IsActive);
        quiet.Checked = e.State == RoutineState.Paused && e.PauseReason == PauseReason.Quiet;
        _menu.Items.Add(quiet);

        var switchMenu = new Forms.ToolStripMenuItem("Switch phase") { Padding = ItemPadding };
        foreach (var phase in _app.SelectablePhases)
        {
            var label = $"{PhaseText.SwitchLabel(phase)} ({PhaseText.Short(e.DurationOf(phase))})";
            var item = Item(label, () => _app.SwitchTo(phase));
            item.Checked = e.IsActive && e.Phase == phase;
            switchMenu.DropDownItems.Add(item);
        }

        switchMenu.DropDown.Renderer = _menu.Renderer;
        _menu.Items.Add(switchMenu);
        _menu.Items.Add(Item("Stop routine", _app.Stop, enabled: e.IsActive));

        var hasDesk = _app.Settings.HasStandingDesk;
        var desk = Item("I have a standing desk", () => _app.SetStandingDesk(!hasDesk));
        desk.Checked = hasDesk;
        desk.ToolTipText = "Off: sit, then take a short walking break (for example at home). Applies from your next change of position.";
        _menu.Items.Add(desk);

        _menu.Items.Add(new Forms.ToolStripSeparator());
        var launchAtSignIn = _app.LaunchAtSignIn;
        var startup = Item("Start with Windows", () => _app.SetLaunchAtSignIn(!launchAtSignIn));
        startup.Checked = launchAtSignIn;
        startup.ToolTipText = "Open Desk Rhythm in the tray when you sign in to Windows";
        _menu.Items.Add(startup);
        _menu.Items.Add(Item("Settings…", _app.ShowSettings));
        _menu.Items.Add(Item("Test reminder", _app.TestReminder));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(Item("Exit Desk Rhythm", _app.Exit));
    }

    private static readonly Forms.Padding ItemPadding = new(0, 3, 0, 3);

    private static Forms.ToolStripMenuItem Item(string text, Action onClick, bool enabled = true)
    {
        var item = new Forms.ToolStripMenuItem(text) { Enabled = enabled, Padding = ItemPadding };
        item.Click += (_, _) => onClick();
        return item;
    }
}

/// <summary>Flat light / dark colours for the WinForms tray menu, close to the Windows 11 look.</summary>
internal sealed class TrayMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    private readonly Drawing.Color _text;
    private readonly Drawing.Color _disabled;

    public TrayMenuRenderer(bool dark)
        : base(new Palette(dark))
    {
        RoundedEdges = false;
        _text = dark ? Drawing.Color.FromArgb(243, 243, 243) : Drawing.Color.FromArgb(27, 27, 27);
        _disabled = dark ? Drawing.Color.FromArgb(120, 120, 120) : Drawing.Color.FromArgb(160, 160, 160);
    }

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? _text : _disabled;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = _text;
        base.OnRenderArrow(e);
    }

    private sealed class Palette(bool dark) : Forms.ProfessionalColorTable
    {
        private readonly Drawing.Color _background = dark ? Drawing.Color.FromArgb(43, 43, 43) : Drawing.Color.FromArgb(249, 249, 249);
        private readonly Drawing.Color _hover = dark ? Drawing.Color.FromArgb(61, 61, 61) : Drawing.Color.FromArgb(234, 234, 234);
        private readonly Drawing.Color _border = dark ? Drawing.Color.FromArgb(70, 70, 70) : Drawing.Color.FromArgb(214, 214, 214);
        private readonly Drawing.Color _check = dark ? Drawing.Color.FromArgb(44, 58, 75) : Drawing.Color.FromArgb(228, 237, 247);

        public override Drawing.Color ToolStripDropDownBackground => _background;

        public override Drawing.Color ImageMarginGradientBegin => _background;

        public override Drawing.Color ImageMarginGradientMiddle => _background;

        public override Drawing.Color ImageMarginGradientEnd => _background;

        public override Drawing.Color MenuBorder => _border;

        public override Drawing.Color MenuItemBorder => _hover;

        public override Drawing.Color MenuItemSelected => _hover;

        public override Drawing.Color MenuItemSelectedGradientBegin => _hover;

        public override Drawing.Color MenuItemSelectedGradientEnd => _hover;

        public override Drawing.Color MenuItemPressedGradientBegin => _hover;

        public override Drawing.Color MenuItemPressedGradientEnd => _hover;

        public override Drawing.Color SeparatorDark => _border;

        public override Drawing.Color SeparatorLight => _background;

        public override Drawing.Color CheckBackground => _check;

        public override Drawing.Color CheckSelectedBackground => _check;

        public override Drawing.Color CheckPressedBackground => _check;

        public override Drawing.Color ButtonSelectedBorder => _border;
    }
}

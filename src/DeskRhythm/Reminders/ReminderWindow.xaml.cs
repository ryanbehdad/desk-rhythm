using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DeskRhythm.Core;
using DeskRhythm.Infrastructure;
using DeskRhythm.Theming;

namespace DeskRhythm.Reminders;

/// <summary>
/// One copy of a reminder on one display. It is topmost but never takes keyboard
/// focus when it opens, so typing in another app is not interrupted.
/// </summary>
public partial class ReminderWindow : Window
{
    // The window has a 14 px transparent shadow margin; 6 more puts the card 20 px from the edge.
    private const double EdgeMargin = 6;

    private readonly ReminderContent _content;
    private readonly DisplayInfo _display;
    private readonly PopupCorner _corner;
    private readonly int _displayNumber;
    private readonly int _displayCount;
    private bool _placed;
    private bool _closingFromOwner;
    private bool _syncingToggle;

    internal ReminderWindow(ReminderContent content, DisplayInfo display, int displayNumber, int displayCount, PopupCorner corner)
    {
        InitializeComponent();
        _content = content;
        _display = display;
        _corner = corner;
        _displayNumber = displayNumber;
        _displayCount = displayCount;

        ThemeManager.ApplyAccent(this, content.Accent);
        Glyph.Phase = content.Accent;
        CaptionText.Text = content.Caption;
        TitleText.Text = content.Title;
        MessageText.Text = content.Message;
        DetailText.Text = content.Detail ?? string.Empty;
        DetailText.Visibility = string.IsNullOrEmpty(content.Detail) ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(this, $"{content.Title}. {content.Message} {content.Detail}");

        BuildActions();
        BuildToggle();

        HideButton.Click += (_, _) => CloseRequested?.Invoke();
        MouseEnter += (_, _) => AttentionChanged?.Invoke(true);
        MouseLeave += (_, _) => AttentionChanged?.Invoke(false);
        PreviewKeyDown += OnPreviewKeyDown;
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);
        SizeChanged += (_, _) => KeepInCorner();
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(KeepInCorner);
    }

    internal event Action<ReminderAction>? ActionChosen;

    internal event Action? CloseRequested;

    /// <summary>The user flipped the card's switch. The argument is the new value.</summary>
    internal event Action<bool>? ToggleChanged;

    /// <summary>True while the pointer is over the card or its menu is open. The time-out waits.</summary>
    internal event Action<bool>? AttentionChanged;

    internal void ShowOnDisplay() =>
        WindowPlacer.ShowAt(this, _display, _corner, EdgeMargin, activate: false, placed: () =>
        {
            _placed = true;
            if (_content.ShowDisplayInfo)
            {
                var scale = (int)Math.Round(VisualTreeHelper.GetDpi(this).DpiScaleX * 100);
                var primary = _display.IsPrimary ? " · primary" : string.Empty;
                CaptionText.Text = $"Test · Display {_displayNumber} of {_displayCount} · {scale}%{primary}";
            }
        });

    internal void CloseFromOwner()
    {
        _closingFromOwner = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Alt+F4 must not advance the routine: treat it like "hide for now".
        if (!_closingFromOwner)
        {
            e.Cancel = true;
            CloseRequested?.Invoke();
        }

        base.OnClosing(e);
    }

    private void BuildActions()
    {
        foreach (var action in _content.Actions)
        {
            var button = new Button
            {
                Content = action.Label,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = action.ToolTip,
            };
            if (action.IsPrimary)
            {
                button.Style = (Style)FindResource("PrimaryButton");
            }

            button.Click += (_, _) => ActionChosen?.Invoke(action);
            ActionPanel.Children.Add(button);
        }

        if (_content.MoreActions.Count == 0)
        {
            MoreButton.Visibility = Visibility.Collapsed;
            return;
        }

        MoreButton.Click += (_, _) => OpenMoreMenu();
    }

    /// <summary>Show the real value of the switch (after a change on this or another display).</summary>
    internal void RefreshToggle()
    {
        if (_content.Toggle is not { } toggle)
        {
            return;
        }

        _syncingToggle = true;
        ToggleBox.IsChecked = toggle.IsOn();
        _syncingToggle = false;
    }

    private void BuildToggle()
    {
        if (_content.Toggle is not { } toggle)
        {
            return;
        }

        ToggleLabel.Text = toggle.Label;
        ToggleHint.Text = toggle.Hint;
        AutomationProperties.SetName(ToggleBox, toggle.Label);
        ToggleBox.Visibility = Visibility.Visible;
        RefreshToggle();

        ToggleBox.Checked += (_, _) => OnToggleClicked();
        ToggleBox.Unchecked += (_, _) => OnToggleClicked();
    }

    private void OnToggleClicked()
    {
        if (!_syncingToggle)
        {
            ToggleChanged?.Invoke(ToggleBox.IsChecked == true);
        }
    }

    private void OpenMoreMenu()
    {
        var menu = new ContextMenu
        {
            PlacementTarget = MoreButton,
            Placement = _corner is PopupCorner.BottomLeft or PopupCorner.BottomRight ? PlacementMode.Top : PlacementMode.Bottom,
        };

        foreach (var action in _content.MoreActions)
        {
            if (action is null)
            {
                menu.Items.Add(new Separator());
                continue;
            }

            var item = new MenuItem { Header = action.Label, ToolTip = action.ToolTip };
            item.Click += (_, _) => ActionChosen?.Invoke(action);
            menu.Items.Add(item);
        }

        menu.Opened += (_, _) => AttentionChanged?.Invoke(true);
        menu.Closed += (_, _) => AttentionChanged?.Invoke(false);
        menu.IsOpen = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseRequested?.Invoke();
        }
    }

    private void KeepInCorner()
    {
        if (_placed && IsVisible)
        {
            WindowPlacer.Place(this, _display, _corner, EdgeMargin);
        }
    }
}

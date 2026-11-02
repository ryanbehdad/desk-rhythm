using DeskRhythm.Core;
using DeskRhythm.Infrastructure;

namespace DeskRhythm.Reminders;

/// <summary>
/// Shows one reminder as mirrored windows on the selected displays and removes
/// all of them together. A click on any copy is reported once.
/// </summary>
internal sealed class MonitorOverlayManager
{
    private readonly List<ReminderWindow> _windows = [];
    private ReminderContent? _content;
    private MonitorMode _mode;
    private PopupCorner _corner;

    public event Action<ReminderAction>? ActionChosen;

    public event Action? CloseRequested;

    public event Action<bool>? AttentionChanged;

    public bool IsShowing => _windows.Count > 0;

    public void Show(ReminderContent content, MonitorMode mode, PopupCorner corner)
    {
        Hide();
        _content = content;
        _mode = mode;
        _corner = corner;

        // A test reminder always goes to every display, so each one can be checked.
        var displays = content.Kind == ReminderKind.Test ? Displays.All() : Displays.Select(mode);
        for (var i = 0; i < displays.Count; i++)
        {
            var window = new ReminderWindow(content, displays[i], i + 1, displays.Count, corner);
            window.ActionChosen += OnActionChosen;
            window.CloseRequested += OnCloseRequested;
            window.AttentionChanged += OnAttentionChanged;
            window.ToggleChanged += OnToggleChanged;
            _windows.Add(window);
            window.ShowOnDisplay();
        }
    }

    public void Hide()
    {
        foreach (var window in _windows)
        {
            window.ActionChosen -= OnActionChosen;
            window.CloseRequested -= OnCloseRequested;
            window.AttentionChanged -= OnAttentionChanged;
            window.ToggleChanged -= OnToggleChanged;
            window.CloseFromOwner();
        }

        _windows.Clear();
        _content = null;
    }

    /// <summary>Displays changed: rebuild the copies on the displays that exist now.</summary>
    public void Reshow()
    {
        if (_content is { } content)
        {
            Show(content, _mode, _corner);
        }
    }

    private void OnActionChosen(ReminderAction action) => ActionChosen?.Invoke(action);

    private void OnCloseRequested() => CloseRequested?.Invoke();

    private void OnAttentionChanged(bool attention) => AttentionChanged?.Invoke(attention);

    /// <summary>One switch changed: apply it once, then show the real value on every copy.</summary>
    private void OnToggleChanged(bool value)
    {
        _content?.Toggle?.Set(value);
        foreach (var window in _windows)
        {
            window.RefreshToggle();
        }
    }
}

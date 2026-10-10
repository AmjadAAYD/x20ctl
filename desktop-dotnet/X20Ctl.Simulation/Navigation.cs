namespace X20Ctl.Simulation;

public enum NavigationAction { None, Up, Down, Left, Right, Activate, Back, PreviousTab, NextTab }
public sealed class Navigation
{
    private Buttons previous;
    private NavigationAction direction;
    private double repeatAt;
    private string? source;
    private int slot;
    private bool needsBaseline;

    public NavigationAction Read(InputFrame frame, bool foreground, bool captureOwnsInput = false)
    {
        bool sourceChanged = source != null && (source != frame.SourceKey || slot != frame.Slot);
        source = frame.SourceKey; slot = frame.Slot;
        var fresh = frame.Buttons & ~previous;
        previous = frame.Buttons;
        if (!frame.Connected) { needsBaseline = true; direction = NavigationAction.None; return NavigationAction.None; }
        if (sourceChanged || needsBaseline) { needsBaseline = false; direction = NavigationAction.None; return NavigationAction.None; }
        if (!foreground || captureOwnsInput || !frame.Connected)
        {
            direction = NavigationAction.None;
            return NavigationAction.None;
        }
        foreach (var (button, action) in new[] { (Buttons.A, NavigationAction.Activate), (Buttons.Back, NavigationAction.Back),
            (Buttons.B, NavigationAction.Back), (Buttons.LB, NavigationAction.PreviousTab), (Buttons.RB, NavigationAction.NextTab) })
            if (fresh.HasFlag(button)) return action;
        var next = frame.Buttons.HasFlag(Buttons.Up) ? NavigationAction.Up :
            frame.Buttons.HasFlag(Buttons.Down) ? NavigationAction.Down :
            frame.Buttons.HasFlag(Buttons.Left) ? NavigationAction.Left :
            frame.Buttons.HasFlag(Buttons.Right) ? NavigationAction.Right : NavigationAction.None;
        if (next == NavigationAction.None)
        {
            double x = frame.LeftX, y = frame.LeftY;
            double threshold = direction == NavigationAction.None ? .6 : .4;
            if (Math.Max(Math.Abs(x), Math.Abs(y)) >= threshold)
                next = Math.Abs(x) >= Math.Abs(y) ? (x > 0 ? NavigationAction.Right : NavigationAction.Left) :
                    (y > 0 ? NavigationAction.Up : NavigationAction.Down);
        }
        if (next == NavigationAction.None) { direction = next; return next; }
        if (next != direction)
        {
            direction = next; repeatAt = frame.TimeMs + 350; return next;
        }
        if (frame.TimeMs < repeatAt) return NavigationAction.None;
        repeatAt = frame.TimeMs + 120;
        return next;
    }

    public void Reset() { previous = Buttons.None; direction = NavigationAction.None; repeatAt = 0; source = null; needsBaseline = false; }
}

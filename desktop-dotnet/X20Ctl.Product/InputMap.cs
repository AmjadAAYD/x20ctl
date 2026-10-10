using System.Windows.Input;
namespace X20Ctl.Product;

/// <summary>
/// The one controller scheme for the whole app (owner-approved 10 Oct 2026). Every button has a single
/// meaning everywhere: A activates what is highlighted, B goes back, LB/RB change tab, LT/RT change the
/// section inside a page, X is the page's main shortcut, Y its secondary action, Menu opens Settings,
/// View returns to the Controller Zone, Home brings X20CTL forward. Keyboard keys mirror the buttons,
/// and the footer hints are generated from this table so behaviour and hints cannot drift apart.
/// </summary>
public static class InputMap
{
    public const Key SectionPrevious = Key.OemOpenBrackets, SectionNext = Key.OemCloseBrackets;
    public const Key Settings = Key.F10, ControllerZone = Key.F9, Fullscreen = Key.F11;

    /// <summary>Controller button (as reported by the native engine, plus virtual LT/RT/LS_*) → key.</summary>
    public static readonly (string Button, Key Key)[] Controller =
    [
        ("A", Key.A), ("B", Key.B), ("X", Key.X), ("Y", Key.Y),
        ("LB", Key.PageUp), ("RB", Key.PageDown), ("LT", SectionPrevious), ("RT", SectionNext),
        ("START", Settings), ("SELECT", ControllerZone),
        ("DPAD_UP", Key.Up), ("DPAD_DOWN", Key.Down), ("DPAD_LEFT", Key.Left), ("DPAD_RIGHT", Key.Right),
        ("LS_UP", Key.Up), ("LS_DOWN", Key.Down), ("LS_LEFT", Key.Left), ("LS_RIGHT", Key.Right),
    ];

    /// <summary>The controller button a key stands for, for remap capture ("press the button you want"): the face
    /// buttons, bumpers, triggers, Menu/View and the D-pad. Null for keys that are not a controller button.</summary>
    public static string? ButtonFor(Key key) => Controller.Where(c => !c.Button.StartsWith("LS_")).Select(c => (c.Button, c.Key)).FirstOrDefault(c => c.Key == key).Button;

    /// <summary>What A, X, Y, LT/RT and B do on a page. Null means the button does nothing there (no hint is shown).</summary>
    public sealed record PageActions(string? A, string? X, string? Y, string? Triggers, string B = "Back");

    public static PageActions For(string page, string? focusAction = null, bool trying = false) => page switch
    {
        "Dashboard" => new(focusAction ?? "Select", "Configure", "Rescan games", "Scroll games", "Controller Zone"),
        "Buttons" => new(focusAction ?? "Select", trying ? "Stop Try it" : "Try it", "All assignments", "Front / Back"),
        "Curves" => new(focusAction ?? "Adjust", "Reset curve", "Next preset", "Channel"),
        "Macros" => new(focusAction ?? "Edit", "Add event", "Preview", "Slot"),
        "Vibration" => new(focusAction ?? "Select", "Reset", null, "Strength"),
        "Device" => new(focusAction ?? "Open", null, "My setups", null),
        "Tester" => new(null, null, null, null, "Hold to exit"),
        "Setups" => new(focusAction ?? "Open", null, "New setup", null),
        _ => new(focusAction ?? "Select", null, null, null)
    };
}

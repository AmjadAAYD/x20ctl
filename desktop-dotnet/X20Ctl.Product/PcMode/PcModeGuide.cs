using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace X20Ctl.Product;

/// <summary>
/// The short "what is this and how do I use it" card at the top of each PC mode tab (owner, 6 Nov 2026: it was confusing).
/// Plain words, numbered steps, and an example drawn with button chips. Nothing here changes a setting.
/// </summary>
public static class PcModeGuide
{
    sealed record Guide(string Title, string What, string[] Steps, (string[] From, string To)[] Examples, string? Note = null);

    static readonly Dictionary<string, Guide> Guides = new()
    {
        ["Status"] = new("What PC mode does",
            "PC mode sits between your controller and your game. X20CTL reads your real controller, changes what it sends (remaps, curves, turbo, combos), and shows the game a virtual Xbox controller instead.",
            ["Switch PC mode on with the switch below.", "Change your settings in Buttons, Curves, Macros and the tabs next to them.", "Start your game. It sees the virtual controller with your settings."],
            [(["Your controller"], "X20CTL changes it"), (["X20CTL"], "Virtual Xbox controller"), (["Virtual controller"], "Your game")],
            "It only works on this PC while X20CTL is open. Close X20CTL and your controller goes back to normal."),
        ["Games"] = new("Settings for one game",
            "Every game uses the same settings by default. Give a game its own settings when you want it to behave differently.",
            ["Start the game first, so it shows up in the list.", "Press \"Give a game its own settings\" and pick it.", "Anything you change now applies only to that game."],
            [(["Every game"], "your normal settings"), (["Elden Ring"], "its own settings")]),
        ["Turbo"] = new("Turbo: hold a button, it taps itself",
            "Pick how fast a button repeats while you hold it. Good for games where you mash a button.",
            ["Find the button in the list.", "Pick a rate next to it, for example 5 / s.", "In the game, hold that button. It taps itself at that speed."],
            [(["Hold A"], "A A A A A  (5 times a second)")],
            "Some online games count turbo as cheating."),
        ["Combos"] = new("Combos: press buttons a special way",
            "A combo watches for one button or two buttons pressed a certain way, and gives the game something else instead. The buttons themselves are not sent to the game.",
            ["Pick the button, and a second one if you want a pair.", "Pick how you press it: together, quick tap, long hold, double tap or on release.", "Pick what the game gets: another button, or a keyboard or mouse action.", "Press Add combo. It starts working at once."],
            [(["X", "B", "together"], "Y"), (["LB", "long hold"], "Keyboard: Escape"), (["A", "double tap"], "Mouse: left click")]),
        ["Test"] = new("Test: see what the game sees",
            "A live view of the virtual controller, next to what your real controller sends. Use it to check that your settings do what you expect.",
            ["Press buttons and move the sticks on your controller.", "\"What the game sees\" should change.", "If it stays empty, PC mode is off or no controller is connected."],
            []),
        ["Share"] = new("Share or reset your setup",
            "Save your PC mode settings to a file or a code, send them to a friend, or start again from nothing.",
            ["Save to a file or copy a code to give your setup away.", "Open a file or paste a code to use someone else's.", "Reset clears this player's settings. It cannot be undone."],
            []),
    };

    public static FrameworkElement Build(string section)
    {
        if (!Guides.TryGetValue(section, out var g)) return new Grid();
        var body = new StackPanel();
        body.Children.Add(Kit.Text(g.Title, 19, "DS.Text", FontWeights.SemiBold));
        body.Children.Add(Wrap(g.What, 15, "DS.TextSoft", new(0, 6, 0, 0)));
        int n = 1;
        foreach (var step in g.Steps) body.Children.Add(StepRow(n++, step));
        foreach (var (from, to) in g.Examples) body.Children.Add(ExampleRow(from, to));
        if (g.Note != null) body.Children.Add(Wrap(g.Note, 14, "DS.TextMuted", new(0, 12, 0, 0)));
        return new Border
        {
            Child = body, CornerRadius = new(16), Padding = new(22, 18, 22, 18), Margin = new(0, 16, 0, 8), BorderThickness = new(1),
            Background = DS.Brush("DS.GlassSubtle"), BorderBrush = DS.Brush("DS.GlassBorder"),
        };
    }

    static TextBlock Wrap(string text, double size, string brush, Thickness margin) =>
        Kit.Text(text, size, brush).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = margin; });

    static FrameworkElement StepRow(int number, string text)
    {
        var badge = new Border
        {
            Width = 26, Height = 26, CornerRadius = new(13), Background = DS.Brush("DS.AccentFill"), VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 0, 12, 0),
            Child = Kit.Text(number.ToString(), 13.5, "DS.Text", FontWeights.Bold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center),
        };
        var row = new DockPanel { Margin = new(0, 10, 0, 0) };
        DockPanel.SetDock(badge, Dock.Left); row.Children.Add(badge);
        row.Children.Add(Wrap(text, 15, "DS.Text", new(0, 2, 0, 0)));
        return row;
    }

    static FrameworkElement ExampleRow(string[] from, string to)
    {
        var row = new WrapPanel { Margin = new(0, 14, 0, 0) };
        row.Children.Add(Kit.Text("Example", 13, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 14, 0)));
        for (int i = 0; i < from.Length; i++)
        {
            if (i > 0 && from.Length > 1 && !IsWay(from[i])) row.Children.Add(Kit.Text("+", 15, "DS.TextSoft").Also(t => t.Margin = new(6, 0, 6, 0)));
            row.Children.Add(Chip(from[i], IsWay(from[i])));
        }
        row.Children.Add(Kit.Text("→", 18, "DS.AccentHi", FontWeights.Bold).Also(t => t.Margin = new(12, 0, 12, 0)));
        row.Children.Add(Chip(to, false, highlight: true));
        return row;
    }

    static bool IsWay(string s) => s is "together" or "long hold" or "double tap" or "quick tap" or "on release";

    static FrameworkElement Chip(string text, bool soft, bool highlight = false) => new Border
    {
        CornerRadius = new(10), Padding = new(12, 5, 12, 6), BorderThickness = new(1), Margin = new(0, 2, 0, 2),
        Background = highlight ? DS.Brush("DS.AccentFill") : DS.Brush("DS.GlassHover"), BorderBrush = DS.Brush("DS.GlassBorder"),
        Child = Kit.Text(text, 14.5, soft ? "DS.TextSoft" : "DS.Text", soft ? FontWeights.Normal : FontWeights.SemiBold),
    };
}

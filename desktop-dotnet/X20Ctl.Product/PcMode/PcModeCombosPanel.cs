using System.IO;
using System.Windows;
using System.Windows.Controls;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>
/// Front-button combos for PC mode (rework, 6 Nov 2026): "X + B", "Home + A", or one button, with a press type, and the pad
/// button the game gets. Saves straight into the selected profile, beside turbo; Buttons, Curves and Macros keep what is saved here.
/// </summary>
public sealed class PcModeCombosPanel : StackPanel
{
    static readonly (string Id, string Label)[] Digital =
    [
        ("A", "A"), ("B", "B"), ("X", "X"), ("Y", "Y"), ("LB", "Left bumper"), ("RB", "Right bumper"), ("L3", "Left stick click"), ("R3", "Right stick click"),
        ("DPAD_UP", "D-pad up"), ("DPAD_DOWN", "D-pad down"), ("DPAD_LEFT", "D-pad left"), ("DPAD_RIGHT", "D-pad right"),
        ("SELECT", "View / Back"), ("START", "Menu / Start"), ("HOME", "Home / Guide"),
    ];
    /// <summary>Keyboard and mouse outputs, from the Mouse tab's own action list.</summary>
    static readonly (string Id, string Label)[] Actions = Enum.GetValues<X20Ctl.Zone.Mouse.MouseAction>()
        .Where(a => a != X20Ctl.Zone.Mouse.MouseAction.None && a != X20Ctl.Zone.Mouse.MouseAction.Precision)
        .Select(a => (ComboRule.ActionPrefix + a, "Keyboard / mouse: " + System.Text.RegularExpressions.Regex.Replace(a.ToString(), "(?<=[a-z])(?=[A-Z])", " "))).ToArray();
    static readonly (string Id, string Label)[] Outputs = Digital.Concat(Actions).ToArray();
    static readonly (PressKind Kind, string Label)[] Kinds =
    [
        (PressKind.Press, "Held together"), (PressKind.Tap, "Quick tap"), (PressKind.Hold, "Long hold"), (PressKind.DoubleTap, "Double tap"), (PressKind.Release, "On release"),
    ];

    readonly PcModeStore store; readonly PcModeService service; readonly ProductPreferences? preferences; readonly Action<string> say;
    readonly StackPanel list = new();
    List<ComboRule> working = new(); bool dirty; readonly Button applyButton;
    readonly ComboBox first, second, kind, output; readonly CheckBox waitForChord;

    public PcModeCombosPanel(PcModeStore store, PcModeService service, ProductPreferences? preferences, Action<string> say)
    {
        this.store = store; this.service = service; this.preferences = preferences; this.say = say; Margin = new(0, 12, 0, 0);
        ComboBox Box(IEnumerable<string> items, int width) => new() { MinHeight = 38, Width = width, FontSize = 14.5, ItemsSource = items.ToList(), SelectedIndex = 0, Margin = new(0, 0, 10, 8) };
        first = Box(Digital.Select(d => d.Label), 150);
        second = Box(new[] { "(no second button)" }.Concat(Digital.Select(d => d.Label)), 170);
        kind = Box(Kinds.Select(k => k.Label), 140);
        output = Box(new[] { "Nothing (just hide the buttons)" }.Concat(Outputs.Select(d => d.Label)), 280);
        waitForChord = new CheckBox { Content = Kit.Text("Wait to see if it is a combo (adds a short delay to the single button)", 14.5, "DS.Text"), Margin = new(0, 2, 0, 12) };
        var add = new Button { Style = DS.Style("ActionButton"), Content = Kit.Text("Add combo", 15, "DS.Text", FontWeights.SemiBold), Height = 42, MinHeight = 0, Padding = new(20, 0, 20, 0), HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += (_, _) => Add();
        Children.Add(Kit.Text("Press these buttons, in this way, and the game gets this instead. The buttons themselves are not sent to the game.", 14.5, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, 0, 0, 12); }));
        Children.Add(list);
        StackPanel Field(string label, ComboBox box) => new() { Margin = new(0, 0, 4, 0), Children = { Kit.Text(label, 13, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(2, 0, 0, 4)), box } };
        Children.Add(new WrapPanel { Children = { Field("1. Button", first), Field("2. Plus another button (optional)", second), Field("3. How you press it", kind), Field("4. The game gets", output) } });
        Children.Add(waitForChord);
        Children.Add(add);
        applyButton = new Button { Style = DS.Style("ActionButton"), Content = Kit.Text("Apply combos", 15, "DS.Text", FontWeights.SemiBold), Height = 42, MinHeight = 0, Padding = new(20, 0, 20, 0), HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 12, 0, 0), IsEnabled = false };
        applyButton.Click += (_, _) => ApplyNow();
        Children.Add(applyButton);
        Reload();
    }

    /// <summary>Redraws the list from the selected profile (call after the selected profile changes).</summary>
    public void Reload()
    {
        list.Children.Clear();
        if (!dirty) working = store.Get(PcModePage.StoreKey).Combos.ToList();
        var profile = new PcModeProfile { Combos = working };
        if (profile.Combos.Count == 0) list.Children.Add(Kit.Text("No combos yet.", 14.5, "DS.TextMuted").Also(t => t.Margin = new(0, 0, 0, 10)));
        foreach (var rule in profile.Combos)
        {
            var remove = new Button { Style = DS.Style("ActionButton"), Content = Kit.Text("Remove", 13.5, "DS.Text"), Height = 34, MinHeight = 0, Padding = new(14, 0, 14, 0), Margin = new(14, 0, 0, 0) };
            string id = rule.Id; remove.Click += (_, _) => Remove(id);
            list.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 6), Children = { Kit.Text(Describe(rule), 15, "DS.Text"), remove } });
        }
    }

    static string Label(string id) => Outputs.FirstOrDefault(d => d.Id == id).Label ?? id;

    internal static string Describe(ComboRule rule) =>
        string.Join(" + ", rule.Buttons.Select(Label)) + " · " + Kinds.First(k => k.Kind == rule.Kind).Label + " → " + (rule.Output == "NONE" ? "nothing" : Label(rule.Output));

    void Add()
    {
        var buttons = new List<string> { Digital[first.SelectedIndex].Id };
        if (second.SelectedIndex > 0) buttons.Add(Digital[second.SelectedIndex - 1].Id);
        if (buttons.Distinct().Count() != buttons.Count) { say("Pick two different buttons."); return; }
        var rule = new ComboRule
        {
            Id = Guid.NewGuid().ToString("N")[..8], Buttons = buttons.ToArray(), Kind = Kinds[kind.SelectedIndex].Kind,
            Mode = waitForChord.IsChecked == true ? ChordMode.WaitForChord : ChordMode.Instant,
            Output = output.SelectedIndex == 0 ? "NONE" : Outputs[output.SelectedIndex - 1].Id,
        };
        working.Add(rule); Stage("Combo added to the list. Press Apply combos to start using it.");
    }

    void Remove(string id) { working.RemoveAll(c => c.Id == id); Stage("Combo removed from the list. Press Apply combos to save."); }

    void Stage(string message) { dirty = true; applyButton.IsEnabled = true; say(message); Reload(); }

    void ApplyNow()
    {
        var profile = store.Get(PcModePage.StoreKey);
        profile.Combos = working.ToList();
        try { profile.Validate(); }
        catch (InvalidDataException error) { say("Not saved: " + error.Message); return; }
        if (!profile.IsNeutral && !PcModePage.MaySave(preferences)) { say("Read the PC mode notice on the Status tab and press \"I understand\" first."); return; }
        try { store.Save(PcModePage.StoreKey, profile); service.Reload(); say("Combos applied. They work in games now."); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { say("Couldn't save: " + error.Message); return; }
        dirty = false; applyButton.IsEnabled = false; Reload();
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Pick a control on the pad, then pick what it sends. The showcase below holds every output a control can send.</summary>
internal sealed class ButtonsPage : UserControl
{
    static readonly string[] Groups = ["All", "Face", "D-pad", "Shoulders", "Sticks", "System"];
    static readonly Dictionary<string, string[]> Members = new()
    {
        ["Face"] = ["A", "B", "X", "Y"], ["D-pad"] = ["DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT"], ["Shoulders"] = ["LB", "RB", "LT", "RT"], ["Sticks"] = ["L3", "R3"], ["System"] = ["SELECT", "START"]
    };

    readonly StudioState state; string? selected; bool rear, listing; string group = "All";
    GamepadReader? tester; readonly TextBlock testLine = T.Label("", 14, T.AccentHi);
    public ButtonsPage(StudioState state)
    {
        this.state = state; Build();
        DetachedFromVisualTree += (_, _) => StopTest();
    }

    void StopTest() { tester?.Dispose(); tester = null; }

    /// <summary>Press anything on the controller and see what the game would get, through the mapping as it stands now.</summary>
    void ToggleTest()
    {
        if (tester != null) { StopTest(); testLine.Text = ""; Build(); return; }
        var r = new GamepadReader(); r.Start();
        if (!r.Connected) { r.Dispose(); testLine.Text = "No controller to test with."; Build(); return; }
        tester = r; testLine.Text = "Press a button on the controller.";
        r.Frame += f =>
        {
            var lines = f.Down.Select(b => state.Buttons.Target(b) is var t && t != b ? Names.Of(b) + " → " + Names.Long(t) : Names.Of(b)).ToList();
            testLine.Text = lines.Count == 0 ? "Press a button on the controller." : string.Join("   ", lines);
        };
        Build();
    }

    void Build()
    {
        // the pad and its mapping take the height the showcase leaves; on a short window the page scrolls
        var page = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new Thickness(36, 6, 36, 18) };
        var draft = Parts.Draft(state, () => { selected = null; Build(); }); draft.Margin = new Thickness(0, 0, 0, 14); page.Children.Add(draft);

        var top = new Grid { ColumnDefinitions = new("1.12*,1*"), MinHeight = 320 }; Grid.SetRow(top, 1);
        top.Children.Add(Visualizer());
        var side = Mapping(); side.Margin = new Thickness(18, 0, 0, 0); Grid.SetColumn(side, 1); top.Children.Add(side);
        page.Children.Add(top);

        var show = Showcase(); show.Margin = new Thickness(0, 18, 0, 0); Grid.SetRow(show, 2); page.Children.Add(show);
        Content = new FillScroll(page, 640);
    }

    // ---- the pad ------------------------------------------------------------------------------------------------

    Control Visualizer()
    {
        var head = new Grid { ColumnDefinitions = new("*,Auto") };
        head.Children.Add(Parts.Heading("Button Visualizer", "Select a control on the controller to remap it."));
        var seg = Segmented.Make(["Front", "Back"], rear ? "Back" : "Front", v => { rear = v == "Back"; Build(); }); seg.VerticalAlignment = VerticalAlignment.Top; Grid.SetColumn(seg, 1); head.Children.Add(seg);

        var art = new ControllerArt(state.Model.Id, rear) { Margin = new Thickness(0, 6, 0, 0) };
        if (!rear) foreach (var (name, p) in state.Model.FrontButtons) if (ButtonDraft.Sources.Contains(name)) art.Children.Add(Spot(name, p));
        var g = new Grid { RowDefinitions = new("Auto,*") }; g.Children.Add(head); Grid.SetRow(art, 1); g.Children.Add(art);
        return T.Card(g, 20);
    }

    Control Spot(string name, ControlPoint p)
    {
        bool on = selected == name, changed = state.Buttons.Target(name) != name;
        var ring = new Border
        {
            Width = on ? 40 : 34, Height = on ? 40 : 34, CornerRadius = new CornerRadius(20), Cursor = new Cursor(StandardCursorType.Hand), Background = on ? T.B("#CC2A62C8") : changed ? T.B("#992A62C8") : T.B("#01000000"),
            BorderBrush = on ? T.B("#CFE3FF") : changed ? T.B("#7DB6FF") : T.B("#14FFFFFF"), BorderThickness = new Thickness(on ? 2.4 : changed ? 1.6 : 1),
            BoxShadow = on ? new BoxShadows(new BoxShadow { Blur = 18, Color = Color.Parse("#993D8BFF") }) : default
        };
        if (name is "LB" or "RB" or "LT" or "RT") ring.Width = ring.Height = on ? 34 : 30;
        string key = name;
        ring.OnTap(() => { selected = key; listing = false; Build(); });
        ring.PointerEntered += (_, _) => { if (selected != key && !changed) ring.BorderBrush = T.B("#7DB6FF"); }; ring.PointerExited += (_, _) => { if (selected != key && !changed) ring.BorderBrush = T.B("#14FFFFFF"); };
        if (changed) ring.Child = Parts.Center(T.Label(Names.Of(state.Buttons.Target(name)), 12, T.Text, FontWeight.SemiBold));
        ControllerArt.SetAnchor(ring, new Point(p.X, p.Y));
        return ring;
    }

    // ---- the mapping ---------------------------------------------------------------------------------------------

    Control Mapping()
    {
        var col = new StackPanel { Spacing = 12 };
        if (listing) { col.Children.Add(Assignments()); return T.Card(new ScrollViewer { Content = col }, 22); }
        if (selected == null)
        {
            col.Children.Add(T.Label("Select a control", 24, null, FontWeight.SemiBold));
            col.Children.Add(T.Label("Click a button on the controller, then choose what it should send.", 14, T.Soft));
        }
        else
        {
            string from = selected, to = state.Buttons.Target(from);
            col.Children.Add(T.Label(Names.Of(from), 26, null, FontWeight.SemiBold));
            var ends = new Grid { ColumnDefinitions = new("*,Auto,*"), Margin = new Thickness(0, 6, 0, 0) };
            ends.Children.Add(End("YOU PRESS", from, Names.Long(from), false));
            var arrow = T.Label("→", 26, T.AccentHi); arrow.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(arrow, 1); ends.Children.Add(arrow);
            var game = End("GAME GETS", to, Names.Long(to), true); Grid.SetColumn(game, 2); ends.Children.Add(game);
            var foot = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new Thickness(0, 6, 0, 0) };
            var cur = T.Label("Current mapping: " + Names.Of(from) + " → " + Names.Of(to), 15, null, FontWeight.Medium); cur.VerticalAlignment = VerticalAlignment.Center; foot.Children.Add(cur);
            var restore = new Pill("Restore " + Names.Of(from) + " → " + Names.Of(from), Pill.Kinds.Ghost, () => { state.Buttons.Reset(from); Build(); }) { Enabled = to != from }; Grid.SetColumn(restore, 1); foot.Children.Add(restore);
            col.Children.Add(new Border { Background = T.B("#4D0B1430"), BorderBrush = T.B("#334F7FD0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20, 14), Child = T.Stack(10, ends, foot) });
            col.Children.Add(T.Label(Phone.Active ? "DRAFT · Kept on this phone. Controller memory is unchanged." : "PC MODE · Processed by X20CTL on this PC. Controller memory is unchanged.", 12.5, T.Soft));
            col.Children.Add(T.Label("Assign " + Names.Of(from) + " to:", 14, T.Soft));
            col.Children.Add(Segmented.Make(Groups, group, v => { group = v; Build(); }, 13));
            var wrap = new WrapPanel();
            foreach (string t in Targets()) { string target = t; var chip = new Pill(Names.Of(target), to == target ? Pill.Kinds.Primary : Pill.Kinds.Ghost, () => { state.Buttons.Set(from, target); Build(); }) { Padding = new Thickness(16, 8), Margin = new Thickness(0, 0, 8, 8) }; wrap.Children.Add(chip); }
            col.Children.Add(wrap);
        }
        var actions = new Grid { ColumnDefinitions = new("*,*,Auto"), Margin = new Thickness(0, 6, 0, 0) };
        var test = new Pill(tester == null ? "▷  Test mapping" : "■  Stop test", tester == null ? Pill.Kinds.Ghost : Pill.Kinds.Light, ToggleTest) { Padding = new Thickness(0, 12), Margin = new Thickness(0, 0, 10, 0) };
        var all = new Pill("▦  All assignments", Pill.Kinds.Ghost, () => { listing = true; Build(); }) { Padding = new Thickness(0, 12), Margin = new Thickness(0, 0, 10, 0) }; Grid.SetColumn(all, 1);
        var more = new Pill("···", Pill.Kinds.Ghost, () => { state.Buttons.ResetAll(); selected = null; Build(); }) { Padding = new Thickness(18, 12) }; Grid.SetColumn(more, 2);
        actions.Children.Add(test); actions.Children.Add(all); actions.Children.Add(more);
        if (testLine.Text.Length > 0) { (testLine.Parent as Panel)?.Children.Remove(testLine); col.Children.Add(testLine); }
        col.Children.Add(actions);
        return T.Card(new ScrollViewer { Content = col, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, 22);
    }

    IEnumerable<string> Targets() => group == "All" ? ButtonDraft.Targets.Append("NONE") : Members[group];

    Control End(string caption, string key, string name, bool game)
    {
        var cap = Parts.Cap(Names.Of(key), 62, Names.FaceColour(key), current: game && key == state.Buttons.Target(selected ?? key) && key != selected);
        if (game && key != selected) cap = Parts.Cap(Names.Of(key), 62, Names.FaceColour(key), current: true);
        return Parts.Center(T.Stack(6, Parts.Center(T.Label(caption, 11, T.Muted, FontWeight.SemiBold)), Parts.Center(cap), Parts.Center(T.Label(name, 13.5, T.Soft)))) is var c ? c : cap;
    }

    Control Assignments()
    {
        var col = new StackPanel { Spacing = 10 };
        var head = new Grid { ColumnDefinitions = new("*,Auto") }; head.Children.Add(Parts.Heading("All assignments", state.Buttons.UnsentChanges == 0 ? "Nothing is remapped." : state.Buttons.UnsentChanges + " changed"));
        var back = new Pill("Done", Pill.Kinds.Primary, () => { listing = false; Build(); }); Grid.SetColumn(back, 1); head.Children.Add(back); col.Children.Add(head);
        foreach (var (from, to) in state.Buttons.Changes.OrderBy(c => c.Key))
        {
            string f = from; var row = new Grid { ColumnDefinitions = new("*,Auto") };
            var t = T.Label(Names.Of(f) + "  →  " + Names.Of(to), 16); t.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(t);
            var r = new Pill("Restore", Pill.Kinds.Ghost, () => { state.Buttons.Reset(f); Build(); }) { Padding = new Thickness(14, 6) }; Grid.SetColumn(r, 1); row.Children.Add(r);
            col.Children.Add(new Border { Background = T.B("#240B1430"), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8), Child = row });
        }
        return col;
    }

    // ---- the showcase --------------------------------------------------------------------------------------------

    Control Showcase()
    {
        string? to = selected == null ? null : state.Buttons.Target(selected);
        var head = new Grid { ColumnDefinitions = new("*,Auto") }; head.Children.Add(T.Label("Buttons showcase", 19, null, FontWeight.SemiBold));
        var hint = T.Label(selected == null ? "Select a control first" : "Assign " + Names.Of(selected) + " to any output", 13, T.Soft); hint.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(hint, 1); head.Children.Add(hint);

        Control Tilebox(string title, Control body, double minWidth = 0)
        {
            var dot = new Border { Width = 5, Height = 5, CornerRadius = new CornerRadius(3), Background = T.AccentHi, VerticalAlignment = VerticalAlignment.Center };
            var cap = T.Line(8, dot, Parts.Caps(title)); cap.HorizontalAlignment = HorizontalAlignment.Center;
            var inner = T.Stack(10, cap, Parts.Center(body));
            return new Border { Background = T.B("#400B1430"), BorderBrush = T.B("#12FFFFFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 12), MinWidth = minWidth, Child = inner, Margin = new Thickness(0, 0, 10, 0) };
        }

        Control Output(string key, Func<bool, Control> make, bool enabled = true)
        {
            bool current = to == key; var c = make(current);
            c.Cursor = new Cursor(StandardCursorType.Hand);
            c.OnTap(() => { if (selected == null) return; if (key == "DEFAULT") state.Buttons.Reset(selected); else state.Buttons.Set(selected, key); Build(); });
            c.PointerEntered += (_, _) => c.Opacity = .85; c.PointerExited += (_, _) => c.Opacity = 1;
            return c;
        }
        Control Face(string k) => Output(k, cur => Parts.Cap(k, 46, Names.FaceColour(k), cur, font: 17));

        var diamond = new Canvas { Width = 150, Height = 134 };
        void At(Control c, double x, double y) { Canvas.SetLeft(c, x); Canvas.SetTop(c, y); diamond.Children.Add(c); }
        At(Face("Y"), 52, 0); At(Face("X"), 6, 44); At(Face("B"), 98, 44); At(Face("A"), 52, 88);

        var pad = new Grid { Width = 140, Height = 140, RowDefinitions = new("*,*,*"), ColumnDefinitions = new("*,*,*") };
        void Dir(string k, int r, int c) { var key = Output(k, cur => Parts.Key(Names.Of(k), 42, 42, cur, 18)); Grid.SetRow(key, r); Grid.SetColumn(key, c); key.HorizontalAlignment = HorizontalAlignment.Center; key.VerticalAlignment = VerticalAlignment.Center; pad.Children.Add(key); }
        Dir("DPAD_UP", 0, 1); Dir("DPAD_LEFT", 1, 0); Dir("DPAD_RIGHT", 1, 2); Dir("DPAD_DOWN", 2, 1);
        var hub = new Ellipse { Width = 12, Height = 12, Fill = T.B("#1B2B48"), Stroke = T.B("#33FFFFFF") }; Grid.SetRow(hub, 1); Grid.SetColumn(hub, 1); pad.Children.Add(hub);

        var shoulders = new Grid { RowDefinitions = new("Auto,Auto"), ColumnDefinitions = new("Auto,Auto") };
        void Sh(string k, int r, int c) { var key = Output(k, cur => Parts.Key(k, 96, 46, cur)); key.Margin = new Thickness(c == 0 ? 0 : 8, r == 0 ? 0 : 8, 0, 0); Grid.SetRow(key, r); Grid.SetColumn(key, c); shoulders.Children.Add(key); }
        Sh("LT", 0, 0); Sh("RT", 0, 1); Sh("LB", 1, 0); Sh("RB", 1, 1);

        Control Stick(string k, string side) => T.Stack(6, Output(k, cur => Parts.Cap(k, 62, null, cur, font: 18)), Parts.Center(T.Label(side, 12, T.Muted)));
        var sticks = T.Line(14, Stick("L3", "Left"), Stick("R3", "Right"));
        var system = T.Line(10, Output("SELECT", cur => Parts.Key("View", 78, 44, cur)), Output("START", cur => Parts.Key("Menu", 78, 44, cur)));
        var special = T.Stack(10, Output("DEFAULT", cur => Parts.Key("Default", 170, 44, selected != null && to == selected, icon: "↺")), Output("NONE", cur => Parts.Key("Disabled", 170, 44, cur, icon: "✕")));

        var tiles = new Grid { ColumnDefinitions = new("1.15*,1.05*,1.3*,1*,0.95*,0.95*"), Margin = new Thickness(0, 12, 0, 0) };
        var list = new[] { Tilebox("FACE", diamond), Tilebox("D-PAD", pad), Tilebox("SHOULDERS", shoulders), Tilebox("STICKS", sticks), Tilebox("SYSTEM", system), Tilebox("SPECIAL", special) };
        for (int i = 0; i < list.Length; i++) { Grid.SetColumn(list[i], i); tiles.Children.Add(list[i]); }
        ((Border)list[^1]).Margin = new Thickness(0);
        return T.Card(T.Stack(0, head, tiles), 20);
    }
}

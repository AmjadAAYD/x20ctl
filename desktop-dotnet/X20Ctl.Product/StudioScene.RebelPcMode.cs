using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product;

/// <summary>The Rebel PC mode page (owner board pcmode.png): the heist map, where the route from the pad to the game is planned in white
/// and lit red once it runs; the settings as a cascade of rows, and the chosen row's detail on pinned paper. Everything is PcModePage's own.</summary>
public partial class StudioScene
{
    private Grid? rebelPc; private ContentControl? rebelPcPaper; private PcModePage? rebelPcPage; private string rebelPcRow = "";
    private PcModePage? RebelPcSource => ManagementBodyHost.Children.OfType<ScrollViewer>().FirstOrDefault()?.Content as PcModePage;

    private static UIElement RebelMapPin(double x, double y, bool lit)
    {
        string d = string.Format(CultureInfo.InvariantCulture, "M{0} {1}C{2} {3} {4} {5} {4} {6}A18 18 0 0 1 {7} {6}C{7} {5} {8} {3} {0} {1}Z", x, y, x - 16, y - 22, x - 18, y - 30, y - 38, x + 18, x + 16);
        var g = Geometry.Parse(d); var c = new Canvas { IsHitTestVisible = false };
        c.Children.Add(new Path { Data = g, Fill = Ink.B(Colors.Black), RenderTransform = new TranslateTransform(4, 5) });
        c.Children.Add(new Path { Data = g, Fill = Ink.B(lit ? Ink.Red : Ink.C("#1c1a1d")), Stroke = Ink.B(Ink.White), StrokeThickness = 2.4 });
        c.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = Ink.B(lit ? Ink.White : Ink.Red) }.Also(e => { Canvas.SetLeft(e, x - 6); Canvas.SetTop(e, y - 44); }));
        return c;
    }
    /// <summary>The PC mode switch (.tg): a skewed track, red with the knob right when on.</summary>
    private static FrameworkElement RebelToggle(bool on, Func<bool> lit)
    {
        var knob = new Rectangle { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(on ? 36 : 4, 0, 0, 0) };
        var track = new Border { Width = 56, Height = 24, BorderThickness = new(1.5), Child = knob, RenderTransform = new SkewTransform(-14, 0) };
        void Paint() { bool l = lit(); track.Background = Ink.B(on ? Ink.Red : l ? Ink.C("#d9d2d4") : Ink.C("#2a2629")); track.BorderBrush = Ink.B(on ? Ink.White : l ? Ink.Black : Ink.A(Ink.White, .35)); knob.Fill = Ink.B(on ? Ink.White : l ? Ink.Black : Ink.C("#6d6569")); }
        track.Tag = (Action)Paint; Paint(); return track;
    }
    private static Button RebelPrimary(string text, Action click, string tag, bool paper = true)
    {
        var key = new Border { Width = 28, Height = 28, CornerRadius = new(14), Background = Ink.B(Ink.White), Margin = new(0, 0, 10, 0), Child = Ink.Text("A", 14, Ink.B(Ink.Red)).Also(t => { t.RenderTransform = null; t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }) };
        var label = Ink.Text(text, 17, Ink.B(Colors.White)); label.RenderTransform = null; label.VerticalAlignment = VerticalAlignment.Center;
        var face = new Border { Height = 46, Padding = new(24, 0, 24, 0), Background = Ink.B(Ink.Red), BorderBrush = Ink.B(paper ? Ink.Black : Ink.White), BorderThickness = new(2), RenderTransformOrigin = new(.5, .5), RenderTransform = new SkewTransform(-12, 0),
            Effect = Ink.Shadow(6, 6, 0, paper ? Ink.Black : Colors.Black, 1), Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { key, label } } };
        var b = new Button { Content = face, Tag = tag, FocusVisualStyle = null, Cursor = Cursors.Hand, Template = RebelBare(), HorizontalAlignment = HorizontalAlignment.Left };
        void Paint() { bool f = b.IsKeyboardFocused || b.IsMouseOver; face.Effect = Ink.Shadow(f ? 8 : 6, f ? 8 : 6, 0, f ? Ink.White : Colors.Black, 1); face.BorderBrush = Ink.B(f ? Ink.White : paper ? Ink.Black : Ink.White); }
        b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); b.Click += (_, _) => click(); Paint();
        return b;
    }
    private static TextBlock RebelBody(string text, double size = 15, string color = "#2e2629") => Ink.Text(text, size, Ink.B(Ink.C(color)), false).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.LineHeight = size * 1.5; });

    private void RebelPcModePage()
    {
        var pc = RebelPcSource; var g = rebelPc!; g.Children.Clear();
        if (pc == null) { g.Children.Add(Place(Ink.Text("PC mode isn't available in this window.", 18, Ink.B(Ink.C("#cfc7c9")), false), 268, 200)); return; }
        if (!ReferenceEquals(pc, rebelPcPage)) { rebelPcPage = pc; pc.Watch(() => { if (rebelPage == "pcmode" && ReferenceEquals(rebelPcPage, RebelPcSource)) RebelLater(); }); rebelPcRow = pc.NoticeShown ? "notice" : "switch"; }
        string? focused = (Keyboard.FocusedElement as FrameworkElement) is { Tag: string t } fe && g.IsAncestorOf(fe) ? t : null;

        // the map: streets, the planned route in white, a pin at each stop, the stops on paper
        double x0 = 262, y0 = 172, w = 1290, h = 180; var map = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        map.Children.Add(new Rectangle { Width = w, Height = h, Fill = Ink.B(Ink.C("#121012")), Stroke = Ink.B(Ink.A(Ink.White, .3)), StrokeThickness = 1.5 }.Also(r => { Canvas.SetLeft(r, x0); Canvas.SetTop(r, y0); }));
        var inner = new Canvas { Width = 1600, Height = 900, Clip = new RectangleGeometry(new Rect(x0, y0, w, h)) }; map.Children.Add(inner);
        for (double x = x0 + 40; x < x0 + w; x += 40) inner.Children.Add(new Line { X1 = x, Y1 = y0, X2 = x, Y2 = y0 + h, Stroke = Ink.B(Ink.A(Ink.White, .05)) });
        for (double y = y0 + 30; y < y0 + h; y += 30) inner.Children.Add(new Line { X1 = x0, Y1 = y, X2 = x0 + w, Y2 = y, Stroke = Ink.B(Ink.A(Ink.White, .05)) });
        foreach (var (a, b, c, d) in new[] { (300, 352, 520, 172), (620, 352, 760, 172), (900, 172, 1100, 352), (1180, 352, 1300, 172), (262, 300, 1552, 250), (1400, 172, 1520, 352) })
            inner.Children.Add(new Line { X1 = a, Y1 = b, X2 = c, Y2 = d, Stroke = Ink.B(Ink.C("#262326")), StrokeThickness = 10 });
        double[] xs = [400, 730, 1060, 1390], ys = [250, 226, 262, 234]; bool live = pc.RouteActive;
        inner.Children.Add(new Polyline { Points = new PointCollection(xs.Zip(ys, (x, y) => new Point(x, y))), Stroke = Ink.B(live ? Ink.Red : Ink.White), StrokeThickness = 3, StrokeDashArray = live ? null : new DoubleCollection { 4, 3 } });
        for (int i = 0; i < 4; i++) inner.Children.Add(RebelMapPin(xs[i], ys[i] - 6, live));
        g.Children.Add(map);
        string[] stops = [$"Physical XInput {PcModePage.Player + 1}", "PC profile", "Virtual Xbox", "Game"];
        for (int i = 0; i < 4; i++)
        {
            var label = Ink.Text(stops[i], 16, Ink.B(Ink.Black)); label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center;
            var slab = new Slab { Width = 200, Height = 40, Seed = (int)xs[i], Fill = Ink.B(Ink.Paper), Rot = 2, IsHitTestVisible = false }; slab.Children.Add(label);
            g.Children.Add(Place(slab, xs[i] - 100, ys[i] + 6));
        }
        var lines = pc.Pipeline.Split('\n'); string sub = string.Join(" · ", lines.Skip(1));
        g.Children.Add(Place(Ink.Text(sub, 14, Ink.B(Ink.C("#9d9598")), false), 268, 362));

        string turbo = pc.TurboCount == 0 ? "No turbo" : $"{pc.TurboCount} button{(pc.TurboCount == 1 ? "" : "s")}";
        string profile = pc.ProfileChoices.FirstOrDefault(c => c.Id == PcModePage.ProfileKey).Label is { Length: > 0 } l ? l.Split("  ·  ")[0] : "Every game";
        var list = new StackPanel { Width = 620 }; int n = 0;
        RowSlab Row(string key, string label, string? subText, FrameworkElement? right, Action? click)
        {
            var row = new RowSlab(n, 56) { Margin = new(n * 3, 0, 0, 6), Tag = "pc:" + key };
            if (subText == null) row.Left.Children.Add(row.Tint(Ink.Text(label, 19), Ink.White, Ink.Black));
            else row.Left.Children.Add(new StackPanel { Children = { row.Tint(Ink.Text(label, 19), Ink.White, Ink.Black), row.Tint(Ink.Text(subText, 13, null, false), Ink.Grey, Ink.C("#5a5155")).Also(s => { s.HorizontalAlignment = HorizontalAlignment.Left; s.Margin = new(0, 2, 0, 0); s.TextTrimming = TextTrimming.CharacterEllipsis; s.MaxWidth = 470; }) } });
            if (right != null) row.Right.Children.Add(right);
            row.GotKeyboardFocus += (_, _) => { row.On = true; if (rebelPcRow != key) { rebelPcRow = key; RebelPcPaper(pc); } }; row.LostKeyboardFocus += (_, _) => row.On = false;
            if (click != null) row.Click += (_, _) => click();
            list.Children.Add(row); n++; return row;
        }
        RowSlab? switchRow = null; switchRow = Row("switch", "PC mode", pc.StatusDetail.Length > 0 ? pc.StatusDetail : "PC mode is " + pc.StatusText.ToLowerInvariant() + ".", RebelToggle(pc.IsOn, () => switchRow?.On == true), pc.Toggle);
        Row("profile", "Settings for", null, RebelDropdown(profile), () => RebelCycleProfile(pc, 1));
        Row("turbo", "Turbo bindings", null, Ink.Text(turbo, 15, Ink.B(Ink.C("#c9c1c3")), false, FontWeights.SemiBold), null).Also(r => ((TextBlock)r.Right.Children[0]).Also(t => r.Tint(t, Ink.C("#c9c1c3"), Ink.Black)));
        Row("games", "Give a game its own settings", null, Glyphs.Make(Glyphs.Chevron, 12, Ink.B(Ink.A(Ink.White, .6))), null);
        Row("share", "Import / export PC profile", null, Glyphs.Make(Glyphs.Chevron, 12, Ink.B(Ink.A(Ink.White, .6))), null);
        Row("reset", $"Reset Player {PcModePage.Player + 1} profile", null, Glyphs.Make(Glyphs.Chevron, 12, Ink.B(Ink.A(Ink.White, .6))), null);
        Row("notice", "Before PC mode starts", null, Glyphs.Make(Glyphs.Chevron, 12, Ink.B(Ink.A(Ink.White, .6))), null);
        if (switchRow.Right.Children[0] is Border tg && tg.Tag is Action repaint) { switchRow.GotKeyboardFocus += (_, _) => repaint(); switchRow.LostKeyboardFocus += (_, _) => repaint(); }
        g.Children.Add(Place(list, 262, 398));

        rebelPcPaper = new ContentControl { Focusable = false }; g.Children.Add(Place(rebelPcPaper, 930, 398)); RebelPcPaper(pc);
        if (pc.SavedText.Length > 0)
            g.Children.Add(Place(new StackPanel { Orientation = Orientation.Horizontal, Children = { new Canvas { Width = 16, Height = 18, Children = { Star.Make(8, 9, 6) } }, Ink.Text(pc.SavedText, 14, Ink.B(Ink.C("#9d9598")), false).Also(t => { t.Margin = new(10, 0, 0, 0); t.MaxWidth = 900; t.TextTrimming = TextTrimming.CharacterEllipsis; }) } }, 262, 846));

        string want = focused ?? "pc:" + rebelPcRow;
        Dispatcher.BeginInvoke(() => (FindTagged(g, want) ?? FindTagged(g, "pc:switch"))?.Focus(), DispatcherPriority.Input);
    }
    private static FrameworkElement RebelDropdown(string text) => new Border
    {
        Height = 34, Padding = new(14, 0, 14, 0), Background = Ink.B(Ink.Black), BorderBrush = Ink.B(Ink.A(Ink.White, .55)), BorderThickness = new(1.5), RenderTransform = new SkewTransform(-10, 0),
        Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { Ink.Text(text, 15, Ink.B(Ink.White), false, FontWeights.SemiBold), Glyphs.Make("", 10, Ink.B(Ink.Hot)).Also(i => i.Margin = new(12, 0, 0, 0)) } }
    };
    private static void RebelCycleProfile(PcModePage pc, int step)
    {
        var all = pc.ProfileChoices; if (all.Count < 2) return; int i = all.ToList().FindIndex(c => c.Id == PcModePage.ProfileKey);
        pc.PickProfile(all[((i < 0 ? 0 : i) + step + all.Count) % all.Count].Id);
    }
    private void RebelPcPaper(PcModePage pc)
    {
        if (rebelPcPaper == null) return;
        var root = new Grid { Width = 616, Height = 430, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(1) };
        root.Children.Add(new TornPaper { Seed = 13 });
        var body = new StackPanel { Margin = new(32, 26, 32, 20) }; root.Children.Add(body);
        var pin = new Ellipse { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(299, 1, 0, 0), Effect = Ink.Shadow(3, 6, 4, Colors.Black, .6),
            Fill = new RadialGradientBrush { GradientOrigin = new(.35, .3), Center = new(.35, .3), RadiusX = .65, RadiusY = .65, GradientStops = { new(Ink.C("#ff9aa4"), 0), new(Ink.Red, .45), new(Ink.C("#4a030b"), 1) } } };
        root.Children.Add(pin);
        void Title(string t) => body.Children.Add(Ink.Text(t, 30, Ink.B(Ink.Black)));
        void Gap(double px) => body.Children.Add(new Border { Height = px });
        switch (rebelPcRow)
        {
            case "switch":
                Title("PC mode");
                body.Children.Add(Ink.Text(pc.StatusText, 21, Ink.B(pc.IsOn ? Ink.Cut : Ink.Black)).Also(t => t.Margin = new(0, 10, 0, 0)));
                if (pc.StatusDetail.Length > 0) body.Children.Add(RebelBody(pc.StatusDetail).Also(t => t.Margin = new(0, 4, 0, 0)));
                if (pc.SetupNote.Length > 0) body.Children.Add(RebelBody(pc.SetupNote, 13.5, "#6b6064").Also(t => t.Margin = new(0, 6, 0, 0)));
                Gap(16); body.Children.Add(RebelPrimary(pc.IsOn ? "Switch off" : "Switch on", pc.Toggle, "pcp:switch"));
                if (!PcModeSetup.Ready) { Gap(12); body.Children.Add(RebelBt("Set up PC mode", null, async () => await pc.SetUp(), "pcp:setup", 15, 40, true)); }
                Gap(22); body.Children.Add(Ink.Text("Where you change the settings", 21, Ink.B(Ink.Black)));
                body.Children.Add(RebelBody("Right here, in this controller's tabs. While PC mode is on, Buttons, Curves (with Advanced) and Macros apply on this PC, for any controller. Turbo is below.").Also(t => t.Margin = new(0, 6, 0, 0)));
                Gap(12); body.Children.Add(RebelBt("Edit buttons", null, ShowButtons, "pcp:edit", 15, 40, true).Also(b => b.HorizontalAlignment = HorizontalAlignment.Left));
                break;
            case "profile":
                Title("Settings for");
                body.Children.Add(RebelBody($"Player {PcModePage.Player + 1} profile · Physical XInput index {PcModePage.Player + 1}. This is an input index, not verified device identity; it can change after reconnecting. Game-specific profiles activate when that game is in front.").Also(t => t.Margin = new(0, 8, 0, 0)));
                var picks = new WrapPanel { Margin = new(0, 16, 0, 0) };
                foreach (var (id, label) in pc.ProfileChoices) { string key = id; picks.Children.Add(RebelBt(label.Split("  ·  ")[0], null, () => pc.PickProfile(key), "pcp:p:" + id, 14, 34, true, id == PcModePage.ProfileKey).Also(b => b.Margin = new(0, 0, 8, 8))); }
                body.Children.Add(picks);
                break;
            case "turbo":
                Title("Turbo bindings");
                body.Children.Add(RebelBody("Holding a button with turbo repeats it. Press a button to step its rate.", 14, "#6b6064").Also(t => t.Margin = new(0, 4, 0, 10)));
                var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
                foreach (var (id, label, rate) in pc.TurboChoices)
                {
                    string key = id; var cell = new DockPanel { Margin = new(0, 0, 10, 6) };
                    cell.Children.Add(RebelBt(rate == "No turbo" ? "Off" : rate, null, () => pc.CycleTurbo(key, 1), "pcp:t:" + id, 12.5, 26, true, rate != "No turbo").Also(b => { b.Margin = new(0); b.Width = 64; DockPanel.SetDock(b, Dock.Right); }));
                    cell.Children.Add(Ink.Text(label, 13, Ink.B(Ink.C("#151316")), false).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.TextTrimming = TextTrimming.CharacterEllipsis; }));
                    grid.Children.Add(cell);
                }
                body.Children.Add(grid);
                break;
            case "games":
                Title("Give a game its own settings");
                body.Children.Add(RebelBody("Start the game first, then come back (Home on the controller brings X20CTL forward). It's listed first.").Also(t => t.Margin = new(0, 8, 0, 0)));
                Gap(14);
                if (pc.Picking)
                {
                    var apps = new WrapPanel();
                    foreach (var (id, label) in pc.AppChoices.Take(8)) { string key = id; apps.Children.Add(RebelBt(label.Split("  ·  ")[0], null, () => pc.AddGameById(key), "pcp:a:" + id, 14, 34, true).Also(b => b.Margin = new(0, 0, 8, 8))); }
                    apps.Children.Add(RebelBt("Cancel", null, pc.CancelPicker, "pcp:cancel", 14, 34, true).Also(b => b.Margin = new(0, 0, 8, 8))); body.Children.Add(apps);
                }
                else body.Children.Add(RebelPrimary("Find open games", pc.FindGames, "pcp:find"));
                if (pc.IsGame) { Gap(12); body.Children.Add(RebelBt("Remove this game", null, pc.RemoveThisGame, "pcp:remove", 15, 40, true).Also(b => b.HorizontalAlignment = HorizontalAlignment.Left)); }
                break;
            case "share":
                Title("Import / export PC profile");
                body.Children.Add(RebelBody("Share these settings as a file or a code, or bring someone else's in. What comes in replaces the settings being edited.").Also(t => t.Margin = new(0, 8, 0, 0)));
                var share = new WrapPanel { Margin = new(0, 16, 0, 0) };
                foreach (var (label, act) in new (string, Action)[] { ("Save to a file", pc.SaveFile), ("Open a file", pc.OpenFile), ("Copy as a code", pc.CopyAsCode), ("Paste a code", pc.PasteAsCode) })
                    share.Children.Add(RebelBt(label, null, act, "pcp:s:" + label, 15, 40, true).Also(b => b.Margin = new(0, 0, 10, 10)));
                body.Children.Add(share);
                break;
            case "reset":
                Title($"Reset Player {PcModePage.Player + 1} profile");
                body.Children.Add(RebelBody("Reset the selected player's current PC profile? This clears its buttons, curves, stick tools, macros and turbo. Other player profiles and controller hardware are unchanged.").Also(t => t.Margin = new(0, 8, 0, 0)));
                Gap(18); body.Children.Add(RebelPrimary("Reset selected profile", pc.ResetProfile, "pcp:reset"));
                break;
            default:
                Title("Before PC mode starts");
                for (int i = 0; i < PcModePage.NoticeLines.Length; i++)
                {
                    string line = PcModePage.NoticeLines[i]; line = line[(line.IndexOf(". ", StringComparison.Ordinal) + 2)..];
                    var badge = new Border { Width = 30, Height = 30, CornerRadius = new(15), Background = Ink.B(Ink.Red), VerticalAlignment = VerticalAlignment.Top, Child = Ink.Text((i + 1).ToString(), 16, Ink.B(Colors.White)).Also(t => { t.RenderTransform = null; t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }) };
                    var row = new DockPanel { Margin = new(0, 14, 0, 0) }; DockPanel.SetDock(badge, Dock.Left); row.Children.Add(badge); row.Children.Add(RebelBody(line).Also(t => t.Margin = new(16, 0, 0, 0))); body.Children.Add(row);
                }
                Gap(18); body.Children.Add(RebelPrimary("I understand", pc.Accept, "pcp:accept"));
                break;
        }
        rebelPcPaper.Content = root;
    }
    private bool RebelPcKey(KeyEventArgs e)
    {
        if (rebelPage != "pcmode" || rebelPcPage == null || e.Key is not (Key.Left or Key.Right) || Keyboard.FocusedElement is not FrameworkElement { Tag: "pc:profile" }) return false;
        if (!e.IsRepeat) RebelCycleProfile(rebelPcPage, e.Key == Key.Right ? 1 : -1); e.Handled = true; return true;
    }
}

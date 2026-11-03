using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product;

/// <summary>The Rebel Tools pages (owner boards hub.png, about.png, tools.png): a pinboard of paper tiles, the About case file and the themes wall.
/// Every action is the page's own button or switch; these pages only lay them out again.</summary>
public sealed partial class ProductSettings
{
    private Grid? rebelRoot, rebelBody, rebelFrameGrid; private ContentControl? rebelTitle, rebelLead, rebelHints; private readonly List<PageSlab> rebelNav = new();
    private string rebelView = "";
    /// <summary>Set by the host: opens a Studio page from the page cascade.</summary>
    public Action<string>? RebelNavigate { get; set; }
    internal bool RebelReturnToStudio { get; set; }

    private static Canvas RPut(UIElement e, double x, double y) { var c = new Canvas(); Canvas.SetLeft(e, x); Canvas.SetTop(e, y); c.Children.Add(e); c.IsHitTestVisible = e.IsHitTestVisible; return c; }
    private static T At<T>(T e, double x, double y) where T : FrameworkElement { e.HorizontalAlignment = HorizontalAlignment.Left; e.VerticalAlignment = VerticalAlignment.Top; e.Margin = new(x, y, 0, 0); return e; }
    private static ControlTemplate Bare() { var t = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; t.Seal(); return t; }
    private static T Tweak<T>(T v, Action<T> a) { a(v); return v; }

    private void BuildRebel()
    {
        pageGrid!.Visibility = Visibility.Collapsed;
        rebelRoot = new Grid { Background = Ink.B(Ink.Black) }; Children.Add(rebelRoot);
        var back = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        back.Children.Add(new Rectangle { Width = 1600, Height = 900, Fill = new RadialGradientBrush { Center = new(1, 0), GradientOrigin = new(1, 0), RadiusX = .7, RadiusY = .6, GradientStops = { new(Ink.A(Ink.Red, .14), 0), new(Colors.Transparent, .6) } } });
        rebelRoot.Children.Add(new Viewbox { Child = back, Stretch = Stretch.Fill });
        var stage = new Grid { Width = 1600, Height = 900 }; rebelRoot.Children.Add(new Viewbox { Child = stage, Stretch = Stretch.Uniform });
        rebelBody = new Grid(); rebelFrameGrid = new Grid();
        stage.Children.Add(rebelFrameGrid); stage.Children.Add(rebelBody);
        var logo = Pieces.Logo(110); rebelFrameGrid.Children.Add(At(logo, 26, 20));
        for (int i = 0; i < StudioScene.RebelPages.Length; i++)
        {
            var (name, icon, key) = StudioScene.RebelPages[i];
            var slab = new PageSlab(name, icon, 62, 52, i + 3, -5 + i % 3, new Thickness(0, -4, -152, -4), false, 21, 22, 22, 22) { ShowName = false, Tag = key, On = key == "tools" };
            slab.Click += (_, _) => { if (key != "tools") RebelNavigate?.Invoke(key); };
            rebelNav.Add(slab); rebelFrameGrid.Children.Add(At(slab, 22 + i * 4, 128 + i * 77));
        }
        rebelTitle = At(new ContentControl { IsHitTestVisible = false }, 262, 24); rebelLead = At(new ContentControl { IsHitTestVisible = false }, 268, 114);
        rebelFrameGrid.Children.Add(rebelTitle); rebelFrameGrid.Children.Add(rebelLead);
        rebelHints = new ContentControl { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 836, 40, 0) }; stage.Children.Add(rebelHints);
        var changed = (Action)(() => { if (rebelRoot.IsVisible && rebelView == "hub") RebelBuild(); });
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock)).AddValueChanged(updateStatus, (_, _) => changed());
        DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(UIElement)).AddValueChanged(updateGo, (_, _) => changed());
        autoUpdate.Checked += (_, _) => changed(); autoUpdate.Unchecked += (_, _) => changed();
        RebelSync();
    }
    /// <summary>Shows each Tools page in its Rebel composition while retaining the native page state.</summary>
    private void RebelSync()
    {
        if (rebelRoot == null) return;
        rebelRoot.Visibility = Visibility.Visible; pageGrid!.Visibility = Visibility.Collapsed;
        rebelView = pcMode.Visibility == Visibility.Visible ? "pcmode" : feedback.Visibility == Visibility.Visible ? "feedback" : about.Visibility == Visibility.Visible ? "about" : themes.Visibility == Visibility.Visible ? "themes" : "hub"; RebelBuild();
    }
    private void RebelFocus(string tag) => Dispatcher.BeginInvoke(() => FindTag(rebelBody!, tag)?.Focus(), DispatcherPriority.Input);
    private static FrameworkElement? FindTag(DependencyObject root, string tag)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { Tag: string t } fe && t == tag && fe.Focusable) return fe;
            if (FindTag(child, tag) is { } found) return found;
        }
        return null;
    }
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    private static TextBlock Body(string text, double size, string color, bool wrap = true) => Ink.Text(text, size, Ink.B(Ink.C(color)), false).Also(t => { t.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap; t.LineHeight = size * 1.45; });
    /// <summary>A skewed button: black with a white edge, white when lit; red when it is the main one.</summary>
    private static Button RBt(string text, string tag, Action click, bool red = false, string? key = null, double size = 15, double h = 42)
    {
        var label = Ink.Text(text, size); label.RenderTransform = null; label.VerticalAlignment = VerticalAlignment.Center;
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (key != null) row.Children.Add(new Border { Width = 26, Height = 26, CornerRadius = new(13), Background = Ink.B(Colors.White), Margin = new(0, 0, 10, 0), Child = Ink.Text(key, 13, Ink.B(Ink.Red)).Also(t => { t.RenderTransform = null; t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }) });
        row.Children.Add(label);
        var face = new Border { Height = h, Padding = new(20, 0, 20, 0), Child = row, BorderThickness = new(1.8), RenderTransformOrigin = new(.5, .5), RenderTransform = new SkewTransform(-12, 0) };
        var b = new Button { Content = face, Tag = tag, Template = Bare(), FocusVisualStyle = null, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left };
        void Paint()
        {
            bool f = b.IsKeyboardFocused || b.IsMouseOver;
            face.Background = Ink.B(f ? Ink.White : red ? Ink.Red : Ink.Black); face.BorderBrush = Ink.B(f ? Colors.Transparent : Ink.White); label.Foreground = Ink.B(f ? Ink.Black : Colors.White);
            face.Effect = f ? Ink.Shadow(5, 5, 0, Ink.Red, 1) : red ? Ink.Shadow(5, 5, 0, Colors.Black, 1) : null;
        }
        b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); b.Click += (_, _) => click(); Paint(); return b;
    }
    private static FrameworkElement RPin(double x, double y)
    {
        return At(new Ellipse { Width = 17, Height = 17, IsHitTestVisible = false, Effect = Ink.Shadow(2, 5, 4, Colors.Black, .6),
            Fill = new RadialGradientBrush { GradientOrigin = new(.35, .3), Center = new(.35, .3), RadiusX = .65, RadiusY = .65, GradientStops = { new(Ink.C("#ff9aa4"), 0), new(Ink.Red, .45), new(Ink.C("#4a030b"), 1) } } }, x, y);
    }

    private void RebelBuild()
    {
        var body = rebelBody!; body.Children.Clear();
        bool about = rebelView is "about" or "pcmode"; rebelFrameGrid!.Visibility = about ? Visibility.Collapsed : Visibility.Visible;
        rebelTitle!.Content = about ? null : Tweak(Ransom.Make(rebelView == "feedback" ? "Send feedback" : rebelView == "themes" ? "Themes" : "Tools", 58), e => { e.RenderTransform = new RotateTransform(-2); e.RenderTransformOrigin = new(0, .5); });
        rebelLead!.Content = rebelView == "themes" ? RLead("Changes how X20CTL looks. Your controllers and drafts stay as they are.") : rebelView == "hub" ? RLead("Help, extras and everything about X20CTL") : null;
        rebelHints!.Content = rebelView switch { "pcmode" => null, "hub" => Parts.Hints(("A", "Open"), ("B", "Back")), "themes" => Parts.Hints(("A", "Apply"), ("B", "Back")), _ => Parts.Hints(("A", "Open"), ("B", "Back")) };
        string? focused = Keyboard.FocusedElement is FrameworkElement { Tag: string t } fe && rebelBody.IsAncestorOf(fe) ? t : null;
        switch (rebelView) { case "pcmode": RebelStandalonePc(body); break; case "feedback":
                var form = FeedbackForm; if (form.Parent is ContentControl content) content.Content = null; else if (form.Parent is Panel panel) panel.Children.Remove(form);
                body.Children.Add(form); break;
            case "about": RebelAbout(body); break; case "themes": RebelThemes(body); break; default: RebelHub(body); break; }
        RebelFocus(focused ?? (rebelView == "themes" ? "th:" + ChosenTheme : rebelView == "hub" ? "t:pcmode" : "ab:back"));
    }
    private static FrameworkElement RLead(string text) => new Border { Width = 900, BorderBrush = Ink.B(Ink.Red), BorderThickness = new(4, 0, 0, 0), Padding = new(16, 0, 0, 0), Margin = new(-2, -2, 0, 0), Child = Ink.Text(text, 16, Ink.B(Ink.C("#cfc7c9")), false) };

    // ---- the pinboard
    private void RebelHub(Grid g)
    {
        g.Children.Add(At(new Border { Width = 1262, Height = 660, Background = Ink.B(Ink.C("#100b0e")), BorderBrush = Ink.B(Ink.A(Ink.White, .08)), BorderThickness = new(1.4), IsHitTestVisible = false }, 298, 158));
        var head = new StackPanel { IsHitTestVisible = false }; head.Children.Add(Ink.Text("Controller not working?", 25)); head.Children.Add(Ink.Text("Read scan, then Write scan, then send both reports online together.", 15, Ink.B(Ink.C("#9d9598")), false).Also(x => x.Margin = new(0, 6, 0, 0)));
        g.Children.Add(At(RBt(RebelReturnToStudio ? "Back to Studio" : "Controller Zone", "rebel:back", () => Click(Back)), 1330, 92));
        g.Children.Add(At(head, 322, 178));
        var string1 = new Polyline { Points = new PointCollection { new(900, 196), new(1000, 214), new(1120, 208), new(1250, 218), new(1340, 196) }, Stroke = Ink.B(Ink.Red), StrokeThickness = 2, IsHitTestVisible = false };
        g.Children.Add(string1);
        var steps = new (string text, double x, double y, double rot, bool red)[] { ("1  Read scan", 830, 204, -3, true), ("2  Write scan", 1052, 222, 1.5, false), ("3  Send online", 1274, 206, -2, false) };
        foreach (var (text, x, y, rot, red) in steps)
        {
            var tag = new Border { Width = 156, Height = 46, Background = Ink.B(red ? Ink.Red : Ink.White), Child = Ink.Text(text, 15, Ink.B(red ? Colors.White : Ink.Black)).Also(l => { l.RenderTransform = null; l.HorizontalAlignment = HorizontalAlignment.Center; l.VerticalAlignment = VerticalAlignment.Center; }), RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(rot), Effect = Ink.Shadow(3, 5, 6, Colors.Black, .5) };
            var b = new Button { Content = tag, Tag = "t:check" + (red ? "" : x.ToString()), Template = Bare(), FocusVisualStyle = null, Cursor = Cursors.Hand }; b.Click += (_, _) => Click(Check);
            g.Children.Add(At(b, x, y)); g.Children.Add(RPin(x + 70, y - 24));
        }
        var tiles = new (string tag, string title, string detail, string glyph, string big, Button src)[]
        {
            ("t:pcmode", "PC mode", "Remaps, turbo and macros for any pad.", Glyphs.PcMode, "", PcMode), ("t:write", "Write scan", "What an app writes. 20 to 40 min.", "", "", WriteScan),
            ("t:replay", "Replay Intro", "Watch the X20CTL opening again.", "", "", Replay), ("t:about", "About", "What X20CTL is, who made it, and credits.", "", "", About),
            ("t:support", "Support X20CTL", "Help keep it going on Ko-fi.", "", "", Support), ("t:star", "Star on GitHub", "AmjadAAYD/x20ctl", "", "", Star),
            ("t:feedback", "Send feedback", "Report a bug, ask for a feature or tell us about your controller.", "", "", Feedback),
        };
        int slot = 0;
        foreach (var (tag, title, detail, glyph, big, src) in tiles)
        {
            if (src.Visibility == Visibility.Collapsed) continue;
            RebelTile(g, slot++, title, tag, glyph, big, () => { var s = new StackPanel(); s.Children.Add(Body(detail, 14.5, "#2e2629")); return s; }, () => Click(src));
        }
        // updates: its own tile, with the switch and Check now
        RebelTile(g, 7, "Keep X20CTL up to date", "t:update", "", "", () =>
        {
            var s = new StackPanel(); s.Children.Add(Body(updateStatus.Text, 13.5, "#2e2629"));
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 10, 0, 0) };
            row.Children.Add(RBtSwitch("Auto", "t:auto", autoUpdate, true).Also(b => b.Margin = new(0, 0, 8, 0)));
            row.Children.Add(RBt("Check now", "t:checknow", () => Click(updateCheck), true, null, 13.5, 32).Also(b => { b.IsEnabled = updateCheck.IsEnabled; b.Margin = new(0, 0, 10, 0); }));
            if (updateGo.Visibility == Visibility.Visible) row.Children.Add(RBt(updateGo.Content is StackPanel { Children.Count: > 1 } sp && sp.Children[1] is TextBlock tb ? tb.Text : "Update now", "t:updatego", () => Click(updateGo), false, null, 13.5, 32));
            s.Children.Add(row); return s;
        }, () => Click(updateCheck));
        var themesBt = RBt("Themes", "t:themes", () => Click(Themes), false, null, 13.5, 34); g.Children.Add(At(themesBt, 1440, 270));
    }
    private void RebelTile(Grid g, int slot, string title, string tag, string glyph, string big, Func<FrameworkElement> content, Action click, bool? switchOn = null)
    {
        double x = 324 + slot % 4 * 306, y = 316 + slot / 4 * 246, rot = new[] { -.6, .5, -.4, .7, .4, -.5, .3, -.3 }[slot];
        var root = new Grid { Width = 280, Height = 206, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(rot) };
        var paper = new TornPaper { Seed = 30 + slot }; root.Children.Add(paper);
        var bar = new Border { Height = 50, VerticalAlignment = VerticalAlignment.Top, Background = Ink.B(Ink.Black), Margin = new(0, 2, 0, 0) };
        var barRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        barRow.Children.Add(Glyphs.Make(glyph, 17, Ink.B(Colors.White))); barRow.Children.Add(Ink.Text(title, 17, Ink.B(Colors.White)).Also(t => { t.Margin = new(10, 0, 0, 0); })); bar.Child = barRow;
        if (switchOn is bool on) { var track = new Border { Width = 46, Height = 20, BorderThickness = new(1.4), Background = Ink.B(on ? Ink.Red : Ink.C("#2a2629")), BorderBrush = Ink.B(on ? Colors.White : Ink.A(Ink.White, .4)), HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 0, 14, 0), RenderTransform = new SkewTransform(-14, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = new Rectangle { Width = 12, Height = 12, Fill = Ink.B(Colors.White), HorizontalAlignment = HorizontalAlignment.Left, Margin = new(on ? 28 : 3, 0, 0, 0) } }; var bg = new Grid(); bg.Children.Add(bar); bg.Children.Add(track); bg.Height = 52; bg.VerticalAlignment = VerticalAlignment.Top; root.Children.Add(bg); } else root.Children.Add(bar);
        var inner = content(); inner.Margin = new(18, 66, 18, 12); inner.VerticalAlignment = VerticalAlignment.Top; root.Children.Add(inner);
        root.Children.Add(Glyphs.Make(big, 54, Ink.B(Ink.A(Ink.Red, .22))).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Right; t.VerticalAlignment = VerticalAlignment.Bottom; t.Margin = new(0, 0, 18, 12); t.IsHitTestVisible = false; }));
        var pin = RPin(131, -2); root.Children.Add(pin);
        var b = new Button { Content = root, Tag = tag, Template = Bare(), FocusVisualStyle = null, Cursor = Cursors.Hand, Width = 280, Height = 206 };
        // a tile that holds buttons itself is not a button over them: those stay their own focus stops
        void Lit() { bool f = b.IsKeyboardFocused || b.IsMouseOver; paper.UnderSheet = f ? Ink.B(Ink.Red) : null; paper.InvalidateVisual(); bar.Background = Ink.B(f ? Ink.Red : Ink.Black); }
        b.GotKeyboardFocus += (_, _) => Lit(); b.LostKeyboardFocus += (_, _) => Lit(); b.MouseEnter += (_, _) => Lit(); b.MouseLeave += (_, _) => Lit(); b.Click += (_, _) => click();
        g.Children.Add(At(b, x, y));
    }

    // ---- About
    private void RebelAbout(Grid g)
    {
        var rays = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        for (int i = 0; i < 16; i++)
        {
            double a0 = i * Math.PI / 8, a1 = a0 + Math.PI / 16; double cx = 330, cy = 430, R = 2200;
            rays.Children.Add(new Polygon { Points = new PointCollection { new(cx, cy), new(cx + R * Math.Cos(a0), cy + R * Math.Sin(a0)), new(cx + R * Math.Cos(a1), cy + R * Math.Sin(a1)) }, Fill = Ink.B(Ink.A(Ink.Red, i % 2 == 0 ? .1 : .05)) });
        }
        g.Children.Add(rays);
        var sheet = new Grid { Width = 556, Height = 620, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(1.6) };
        sheet.Children.Add(new TornPaper { Seed = 5, UnderSheet = Ink.B(Ink.Red) });
        var col = new StackPanel { Margin = new(30, 40, 30, 20) }; sheet.Children.Add(col);
        col.Children.Add(Ransom.Make("X20CTL", 56).Also(r => r.HorizontalAlignment = HorizontalAlignment.Center));
        var box = new Grid { Height = 350, Margin = new(0, 24, 0, 0), Background = Ink.B(Ink.Black), ClipToBounds = true };
        box.Children.Add(Dots.Field(0, 0, 500, 350, Ink.A(Ink.Red, .45), 12, 1.8)); var logo = Pieces.Logo(420); logo.HorizontalAlignment = HorizontalAlignment.Center; logo.VerticalAlignment = VerticalAlignment.Center; box.Children.Add(logo);
        col.Children.Add(box);
        string version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "preview";
        col.Children.Add(Ink.Text($"Native Controller Studio · {X20Ctl.Zone.ReleaseVersions.Display(version)}", 18, Ink.B(Ink.Black)).Also(t => { t.Margin = new(0, 16, 0, 0); t.HorizontalAlignment = HorizontalAlignment.Center; t.RenderTransform = new RotateTransform(-2); }));
        g.Children.Add(At(sheet, 84, 110));
        foreach (var (x, y, rot) in new[] { (60d, 120d, -32d), (500d, 90d, 28d) }) g.Children.Add(At(new Border { Width = 130, Height = 34, Background = Ink.B(Ink.A(Ink.Red, .8)), RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(rot), IsHitTestVisible = false }, x, y));
        g.Children.Add(At(Ransom.Make("About", 54).Also(r => { r.RenderTransform = new RotateTransform(-2); r.IsHitTestVisible = false; }), 700, 112));
        var text = new StackPanel { Width = 840 };
        text.Children.Add(Body("X20CTL is an independent, open-source controller studio for EasySMX pads. Remap buttons, shape stick and trigger response, build back-paddle macros, set vibration, and check that a controller works, all on your own PC. There is no account, and nothing leaves your PC unless you choose to send a report or feedback.", 15.5, "#e6dfe1").Also(t => { t.RenderTransform = Ink.Slant; t.RenderTransformOrigin = new(0, .5); }));
        text.Children.Add(Body("Every change stays a local draft until it can be sent to a connected, verified controller. X20CTL never writes to a controller it cannot read back.", 15.5, "#e6dfe1").Also(t => { t.Margin = new(0, 12, 0, 0); t.RenderTransform = Ink.Slant; t.RenderTransformOrigin = new(0, .5); }));
        var facts = new[] { ("Made by", "Amjad AAYD, who designs and builds X20CTL."), ("Thanks", "Special thanks to Kiin from Team EasySMX for sending me a free X20 Pro controller."), ("Inspiration", "The console-style look of X20CTL was inspired by ApexSenseBridge by ReynArts. Thank you for showing how good a controller app can feel.") };
        for (int i = 0; i < facts.Length; i++)
        {
            var row = new Grid { MinHeight = 44, Margin = new(i * 4, i == 0 ? 20 : 8, 0, 0), Background = Ink.B(Ink.Panel) }; row.ColumnDefinitions.Add(new() { Width = new(150) }); row.ColumnDefinitions.Add(new());
            row.Children.Add(Ink.Label(facts[i].Item1, 11.5, Ink.B(Ink.Hot)).Also(l => { l.Margin = new(20, 0, 0, 0); l.VerticalAlignment = VerticalAlignment.Center; }));
            var v = Body(facts[i].Item2, 15, "#e6dfe1").Also(t => { t.Margin = new(0, 9, 14, 9); t.VerticalAlignment = VerticalAlignment.Center; t.RenderTransform = Ink.Slant; }); Grid.SetColumn(v, 1); row.Children.Add(v); text.Children.Add(row);
        }
        var links = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 24, 0, 0) };
        links.Children.Add(RBt("Back to Tools", "ab:back", () => ShowAbout(false), true, "B").Also(b => b.Margin = new(0, 0, 12, 0)));
        links.Children.Add(RBt("X20CTL on GitHub", "ab:repo", () => Try(() => ExternalLinks.Open(ExternalLinks.Repository))).Also(b => b.Margin = new(0, 0, 12, 0)));
        links.Children.Add(RBt("ApexSenseBridge by ReynArts", "ab:apex", () => Try(() => ExternalLinks.Open(ExternalLinks.Inspiration))));
        text.Children.Add(links);
        text.Children.Add(Body("Credits · Desktop-control layout inspired by Gopher360 — Tylemagne, GPL-3.0. See the distributed open-source license notices.", 12.5, "#8d8588").Also(t => { t.Margin = new(0, 18, 0, 0); t.RenderTransform = Ink.Slant; t.RenderTransformOrigin = new(0, .5); }));
        text.Children.Add(Body("X20CTL is not affiliated with, endorsed by or sponsored by EasySMX. EasySMX and its product names belong to their owners.", 12.5, "#8d8588").Also(t => { t.Margin = new(0, 6, 0, 0); t.RenderTransform = Ink.Slant; t.RenderTransformOrigin = new(0, .5); }));
        g.Children.Add(At(text, 710, 208));
    }

    private StudioScene.RebelPcPresenter? standaloneRebelPc;
    private void RebelStandalonePc(Grid body)
    {
        if (pcMode.Children.OfType<ScrollViewer>().FirstOrDefault() is not { Content: PcModePage source } native) return;
        if (standaloneRebelPc?.Source != source) standaloneRebelPc = new(source);
        RebelToolFrame.Detach(standaloneRebelPc);
        var board = RebelToolFrame.Create("RebelToolsPcMode", "PC mode", "Software processing on this PC · controller hardware stays unchanged", () => ShowPcMode(false), key => RebelNavigate?.Invoke(key), 58, "pcmode");
        board.Children.Add(standaloneRebelPc); Panel.SetZIndex(standaloneRebelPc, 1);
        // Keep the native page loaded so its service subscription and saved-state refresh remain active.
        if (pcMode.Parent is Panel parent) parent.Children.Remove(pcMode);
        native.Visibility = Visibility.Hidden; pcMode.Width = 1; pcMode.Height = 1; pcMode.IsHitTestVisible = false; body.Children.Add(pcMode);
        body.Children.Add(new Viewbox { Child = board, Stretch = Stretch.Uniform }); standaloneRebelPc.Rebuild();
    }

    // ---- Themes
    private void RebelThemes(Grid g)
    {
        string chosen = ChosenTheme; var status = Ink.Text("", 14, Ink.B(Ink.Hot), false, FontWeights.SemiBold);
        var next = ThemeCatalog.Find(chosen);
        status.Text = next == ThemeCatalog.Current ? "" : next.Name + " starts the next time X20CTL opens.";
        g.Children.Add(At(status, 268, 150));
        int i = 0;
        foreach (var theme in ThemeCatalog.All)
        {
            double x = 266 + i % 5 * 258, y = 188 + i / 5 * 236; bool now = theme.Id == ThemeCatalog.Current.Id, picked = theme.Id == chosen;
            var card = new Grid { Width = 244, Height = 206 }; var tilt = new[] { -1.2, .8, -.6, 1, -.4 }[i % 5];
            var frame = new Grid { Width = 244, Height = 138, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(tilt), Effect = Ink.Shadow(3, 6, 8, Colors.Black, .6), VerticalAlignment = VerticalAlignment.Top };
            frame.Children.Add(new Border { Background = Ink.B(picked ? Colors.White : Ink.C("#b9b2b4")) });
            var thumbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", theme.Id, "thumb.png");
            var pic = new Image { Margin = new(picked ? 4 : 5), Stretch = Stretch.UniformToFill, Opacity = picked ? 1 : .8 }; if (System.IO.File.Exists(thumbPath)) pic.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(thumbPath));
            frame.Children.Add(pic); frame.Children.Add(RPin(114, -6));
            card.Children.Add(frame);
            var name = Ink.Text(theme.Name, 21, Ink.B(picked ? Colors.White : Ink.C("#9d9598"))); name.Margin = new(12, 146, 0, 0); name.VerticalAlignment = VerticalAlignment.Top; name.HorizontalAlignment = HorizontalAlignment.Left; card.Children.Add(name);
            if (now || picked) { var tag = Pieces.Tag(now ? "In use" : "Next"); tag.HorizontalAlignment = HorizontalAlignment.Right; tag.VerticalAlignment = VerticalAlignment.Top; tag.Margin = new(0, 148, 14, 0); card.Children.Add(tag); }
            string id = theme.Id; var b = new Button { Content = card, Tag = "th:" + id, Template = Bare(), FocusVisualStyle = null, Cursor = Cursors.Hand, IsEnabled = preferences != null, Width = 244, Height = 206 };
            void Lit() { frame.Effect = b.IsKeyboardFocused || b.IsMouseOver ? Ink.Shadow(5, 6, 0, Ink.Red, 1) : Ink.Shadow(3, 6, 8, Colors.Black, .6); }
            b.GotKeyboardFocus += (_, _) => Lit(); b.LostKeyboardFocus += (_, _) => Lit(); b.MouseEnter += (_, _) => Lit(); b.MouseLeave += (_, _) => Lit();
            b.Click += (_, _) => { try { preferences?.SetTheme(id); RebelBuild(); } catch (Exception error) when (error is System.IO.IOException or InvalidOperationException or UnauthorizedAccessException) { status.Text = "Couldn't save this setting: " + error.Message; } };
            g.Children.Add(At(b, x, y)); i++;
        }
        // the opening intro, and restarting into a chosen theme
        var bar = new Border { Width = 1240, Height = 104, Background = Ink.B(Ink.C("#121012")), BorderBrush = Ink.B(Ink.A(Ink.White, .22)), BorderThickness = new(1.4), RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-.4) };
        var inner = new Grid { Margin = new(28, 0, 28, 0) }; bar.Child = inner;
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left }; left.Children.Add(Ink.Text("Opening intro", 20)); left.Children.Add(Ink.Text(introStatus.Text, 14, Ink.B(Ink.C("#9d9598")), false).Also(t => t.Margin = new(0, 3, 0, 0))); inner.Children.Add(left);
        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Button Sw(CheckBox box, string label, string tag) => Tweak(RBtSwitch(label, tag, box), b => { });
        if (next != ThemeCatalog.Current && !ReviewSandbox.Active) right.Children.Add(RBt("Restart now", "th:restart", RequestThemeRestart, true, "A").Also(b => b.Margin = new(0, 0, 24, 0)));
        right.Children.Add(Sw(introLaunch, "At launch", "th:launch")); if (introSound.IsVisible || IntroView.HasIntroScore) right.Children.Add(Sw(introSound, "Sound", "th:sound"));
        inner.Children.Add(right); g.Children.Add(At(bar, 300, 672));
    }
    /// <summary>A switch with its name, as the board draws it: a skewed track, red when on. It flips the page's own box, which saves the setting.</summary>
    private Button RBtSwitch(string label, string tag, CheckBox box, bool onPaper = false)
    {
        bool on = box.IsChecked == true;
        var track = new Border { Width = 58, Height = 24, BorderThickness = new(1.5), Background = Ink.B(on ? Ink.Red : Ink.C("#2a2629")), BorderBrush = Ink.B(on ? Colors.White : Ink.A(Ink.White, .4)), RenderTransform = new SkewTransform(-14, 0),
            Child = new Rectangle { Width = 16, Height = 16, Fill = Ink.B(Colors.White), HorizontalAlignment = HorizontalAlignment.Left, Margin = new(on ? 36 : 4, 0, 0, 0) } };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(12, 6, 12, 6) }; row.Children.Add(track); row.Children.Add((onPaper ? Ink.Text(label, 15, Ink.B(Ink.Black)) : Ink.Text(label, 17)).Also(t => { t.Margin = new(16, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; }));
        var face = new Border { Child = row, BorderThickness = new(1.4), BorderBrush = Brushes.Transparent, IsEnabled = box.IsEnabled };
        var b = new Button { Content = face, Tag = tag, Template = Bare(), FocusVisualStyle = null, Cursor = Cursors.Hand, IsEnabled = box.IsEnabled, Margin = new(12, 0, 0, 0) };
        void Lit() { face.BorderBrush = b.IsKeyboardFocused || b.IsMouseOver ? Ink.B(Ink.A(Ink.White, .7)) : Brushes.Transparent; }
        b.GotKeyboardFocus += (_, _) => Lit(); b.LostKeyboardFocus += (_, _) => Lit(); b.MouseEnter += (_, _) => Lit(); b.MouseLeave += (_, _) => Lit();
        b.Click += (_, _) => { box.IsChecked = box.IsChecked != true; RebelBuild(); };
        return b;
    }
}

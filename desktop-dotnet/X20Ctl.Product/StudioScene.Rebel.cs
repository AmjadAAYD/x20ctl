using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>The Rebel theme's Studio (owner boards dashboard.png, buttons.png and the Studio sheets): the pages step down the left as a
/// cascade of slabs, titles are ransom notes, the Dashboard throws the pad onto a red slash with the pages as slabs along the floor, and
/// Buttons puts the chosen control in a crosshair with the outputs as small cards. Every action is the Studio's own.</summary>
public partial class StudioScene
{
    internal bool IsRebel { get; private set; }
    /// <summary>Tools from the page cascade: the host opens its Tools page.</summary>
    public event Action? ToolsRequested;
    private Grid? rebelBack, rebelFront, rebelDash, rebelButtons, rebelFrame; private ContentControl? rebelTitle, rebelLead, rebelHints;
    private readonly List<PageSlab> rebelNav = new(); private SlabButton? rebelApply; private string rebelPage = ""; private bool rebelQueued, rebelAll;
    internal static readonly (string name, string icon, string key)[] RebelPages =
    [("Buttons", Glyphs.Buttons, "buttons"), ("Curves", Glyphs.Curves, "curves"), ("Macros", Glyphs.Macros, "macros"), ("Vibration", Glyphs.Vibration, "vibration"), ("PC mode", Glyphs.PcMode, "pcmode"),
     ("Device", Glyphs.Device, "device"), ("Mouse", Glyphs.Mouse, "mouse"), ("Setups", Glyphs.Setups, "setups"), ("Tools", Glyphs.Tools, "tools")];
    private static readonly (string name, string[] keys)[] RebelGroups =
    [("Face", ["A", "B", "X", "Y"]), ("Shoulders", ["LB", "RB", "LT", "RT"]), ("Sticks", ["L3", "R3", "LSTICK_ANALOG", "RSTICK_ANALOG"]), ("System", ["SELECT", "START", "HOME", "CAPTURE", "TURBO"]), ("D-pad", ["DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT"])];

    private static void RPut(FrameworkElement e, double x, double y) { e.HorizontalAlignment = HorizontalAlignment.Left; e.VerticalAlignment = VerticalAlignment.Top; e.Margin = new(x, y, 0, 0); }
    private static Polygon RPoly(Brush fill, params double[] xy) => new() { Fill = fill, Points = new PointCollection(Enumerable.Range(0, xy.Length / 2).Select(i => new Point(xy[2 * i], xy[2 * i + 1]))), IsHitTestVisible = false };
    private static Polygon RStripe(double x0, double y0, double x1, double y1, double w, Color fill, double op = 1) => RPoly(Ink.B(Ink.A(fill, op)), x0, y0, x0 + w, y0, x1 + w, y1, x1, y1);
    private static Brush RRadial(double cx, double cy, double rx, double ry, Color color, double stop)
    {
        var b = new RadialGradientBrush { Center = new(cx, cy), GradientOrigin = new(cx, cy), RadiusX = rx, RadiusY = ry }; b.GradientStops.Add(new(color, 0)); b.GradientStops.Add(new(Ink.A(color, 0), stop)); return b;
    }
    private static LinearGradientBrush RFade(double cssDeg, double from, double to)
    {   // a CSS linear-gradient angle (0 = to top, clockwise) as start and end points in the box
        double a = cssDeg * Math.PI / 180, dx = Math.Sin(a) / 2, dy = -Math.Cos(a) / 2;
        return new LinearGradientBrush { StartPoint = new(.5 - dx, .5 - dy), EndPoint = new(.5 + dx, .5 + dy), GradientStops = { new(Colors.Black, from), new(Colors.Transparent, to) } };
    }

    /// <summary>Called once, right after the Studio's own parts exist.</summary>
    private void BuildRebelStudio()
    {
        if (ThemeCatalog.Current.Id != "rebel") return;
        IsRebel = true; StudioBackdrop.Visibility = Visibility.Collapsed;
        rebelBack = new Grid { Width = 1600, Height = 900, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        rebelFront = new Grid { Width = 1600, Height = 900, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        StudioShell.Children.Insert(StudioShell.Children.IndexOf(RootLayout), rebelBack); StudioShell.Children.Add(rebelFront);
        // the header's own controls (agent, Controller Zone, clock, window buttons) stay, small, in the corner
        if (HeaderGrid.Children.Count > 1 && HeaderGrid.Children[1] is Panel corner)
        {
            HeaderGrid.Children.Remove(corner); Grid.SetColumn(corner, 0); corner.HorizontalAlignment = HorizontalAlignment.Right; corner.VerticalAlignment = VerticalAlignment.Top; corner.Margin = new(0, 2, 8, 0);
            var tools = new SlabButton("Tools", 76, 28, 412, -2, 14, Ink.White) { Tag = "rebel:chrome:tools" };
            tools.Click += (_, _) => ToolsRequested?.Invoke(); corner.Children.Insert(0, tools);
            if (AgentTitleHost.Parent is Panel agentParent) { agentParent.Children.Remove(AgentTitleHost); corner.Children.Insert(1, AgentTitleHost); }
            rebelFront.Children.Add(RebelChrome.Fit(corner, 590));
        }
        RootLayout.RowDefinitions[0].Height = new(0); RootLayout.RowDefinitions[1].Height = new(0); NavigationLayer.Visibility = Visibility.Collapsed; StudioFooter.Visibility = Visibility.Collapsed; RootLayout.RowDefinitions[5].Height = new(0);
        RootLayout.Margin = new(262, 150, 30, 70);
        // the frame: the logo, the page cascade, the ransom title and its lead
        rebelFrame = new Grid(); var logo = Pieces.Logo(110); RPut(logo, 26, 20); rebelFrame.Children.Add(logo);
        for (int i = 0; i < RebelPages.Length; i++)
        {
            var (name, icon, key) = RebelPages[i];
            var slab = new PageSlab(name, icon, 62, 52, i + 3, -5 + i % 3, new Thickness(0, -4, -152, -4), false, 21, 22, 22, 22) { ShowName = false, Tag = key };
            slab.Click += (_, _) => RebelGo(key); RPut(slab, 22 + i * 4, 128 + i * 77); rebelNav.Add(slab); rebelFrame.Children.Add(slab);
        }
        rebelTitle = new ContentControl { IsHitTestVisible = false }; RPut(rebelTitle, 262, 24); rebelFrame.Children.Add(rebelTitle);
        rebelLead = new ContentControl { IsHitTestVisible = false }; RPut(rebelLead, 268, 114); rebelFrame.Children.Add(rebelLead);
        rebelFront.Children.Add(rebelFrame);
        rebelDash = new Grid { Visibility = Visibility.Collapsed }; rebelButtons = new Grid { Visibility = Visibility.Collapsed }; rebelCurves = new Grid { Visibility = Visibility.Collapsed }; rebelMacros = new Grid { Visibility = Visibility.Collapsed }; rebelVibration = new Grid { Visibility = Visibility.Collapsed }; rebelPc = new Grid { Visibility = Visibility.Collapsed }; rebelDevice = new Grid { Visibility = Visibility.Collapsed }; rebelMouse = new Grid { Visibility = Visibility.Collapsed }; rebelSetups = new Grid { Visibility = Visibility.Collapsed }; rebelTester = new Grid { Visibility = Visibility.Collapsed }; rebelDiag = new Grid { Visibility = Visibility.Collapsed };
        rebelFront.Children.Add(rebelDash); rebelFront.Children.Add(rebelButtons); rebelFront.Children.Add(rebelCurves); rebelFront.Children.Add(rebelMacros); rebelFront.Children.Add(rebelVibration); rebelFront.Children.Add(rebelPc); rebelFront.Children.Add(rebelDevice); rebelFront.Children.Add(rebelMouse); rebelFront.Children.Add(rebelSetups); rebelFront.Children.Add(rebelTester); rebelFront.Children.Add(rebelDiag);
        rebelHints = new ContentControl { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 836, 40, 0) }; rebelFront.Children.Add(rebelHints);
        rebelApply = new SlabButton("Apply Changes", 220, 46, 141, -2, 18, Ink.White, true); RPut(rebelApply, 270, 834); rebelApply.Click += (_, _) => { if (Apply.IsEnabled) Apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); };
        rebelFront.Children.Add(rebelApply);
        LayoutUpdated += (_, _) => { if (rebelApply != null) rebelApply.Visibility = IsRebel && rebelPage != "dashboard" && Apply.IsVisible && Apply.IsEnabled ? Visibility.Visible : Visibility.Collapsed; };
        RebelLater();
    }
    private void RebelLater() { if (!IsRebel || rebelQueued) return; rebelQueued = true; Dispatcher.BeginInvoke(() => { rebelQueued = false; RebelShow(); }, DispatcherPriority.Loaded); }
    internal void RebelGoPage(string key) => RebelGo(key);
    private void RebelGo(string key)
    {
        switch (key)
        {
            case "buttons": ShowButtons(); break; case "curves": ShowCurves(true); break; case "macros": ShowMacros(true); break; case "vibration": ShowVibration(true); break;
            case "pcmode": ShowManagement("PcMode"); break; case "device": ShowManagement("Device"); break; case "mouse": ShowManagement("Mouse"); break; case "setups": ShowManagement("Setups"); break;
            case "tools": ToolsRequested?.Invoke(); break;
        }
    }
    private string RebelCurrent => ManagementPage switch
    {
        "Dashboard" => "dashboard", "PcMode" => "pcmode", "Device" => "device", "Screen" => "screen", "Diagnostics" => "diagnostics", "Tester" => "tester", "Mouse" => "mouse", "Setups" => "setups",
        _ => IsCurves ? "curves" : IsMacros ? "macros" : IsVibration ? "vibration" : "buttons"
    };
    /// <summary>Lays the page shown out as its board.</summary>
    private void RebelShow()
    {
        if (!IsRebel) return;
        string page = RebelCurrent; bool changed = page != rebelPage; rebelPage = page;
        bool dash = page == "dashboard", buttons = page == "buttons", curves = page == "curves", macros = page == "macros" && Macros != null, vibration = page == "vibration" && Vibration != null, pcmode = page == "pcmode", device = page == "device" && Device != null, mouse = page == "mouse" && Mouse != null, setups = page == "setups" && Setups != null, tester = page == "tester" && Tester != null, diag = page == "diagnostics" && Diagnostics != null;
        RootLayout.Visibility = dash || curves || macros || vibration || pcmode || device || mouse || setups || tester || diag || buttons && !AssignmentsVisible ? Visibility.Hidden : Visibility.Visible; rebelCurves!.Visibility = curves ? Visibility.Visible : Visibility.Collapsed; rebelMacros!.Visibility = macros ? Visibility.Visible : Visibility.Collapsed; rebelVibration!.Visibility = vibration ? Visibility.Visible : Visibility.Collapsed; rebelPc!.Visibility = pcmode ? Visibility.Visible : Visibility.Collapsed; rebelDevice!.Visibility = device ? Visibility.Visible : Visibility.Collapsed; rebelMouse!.Visibility = mouse ? Visibility.Visible : Visibility.Collapsed; rebelSetups!.Visibility = setups ? Visibility.Visible : Visibility.Collapsed; rebelTester!.Visibility = tester ? Visibility.Visible : Visibility.Collapsed; rebelDiag!.Visibility = diag ? Visibility.Visible : Visibility.Collapsed;
        rebelFrame!.Visibility = dash ? Visibility.Collapsed : Visibility.Visible; rebelDash!.Visibility = dash ? Visibility.Visible : Visibility.Collapsed; rebelButtons!.Visibility = buttons ? Visibility.Visible : Visibility.Collapsed;
        string nav = page switch { "diagnostics" or "tester" or "screen" => "device", _ => page };
        foreach (var slab in rebelNav) slab.On = (string)slab.Tag == nav;
        if (changed) RebelBackdrop(page);
        rebelTitle!.Content = new Viewbox { MaxWidth = 700, MaxHeight = 86, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Child = Ransom.Make(page switch
        {
            "buttons" => "Buttons", "curves" => "Curves", "macros" => "Macros", "vibration" => "Vibration", "pcmode" => "PC mode", "device" => "Device", "screen" => "Screen", "diagnostics" => "Diagnostics",
            "mouse" => "Controller as mouse", "setups" => "My setups", "tester" => "Tester", _ => "Studio"
        }, page == "mouse" ? 51 : 58) };
        ((FrameworkElement)rebelTitle.Content).RenderTransform = new RotateTransform(-2); ((FrameworkElement)rebelTitle.Content).RenderTransformOrigin = new(0, .5);
        bool draft = page is "buttons" or "curves" or "macros" or "vibration";
        rebelLead!.Content = macros && Macros!.Draft.PcMode ? RebelBanner("Macros · PC mode · Applies on this PC, to games, for any controller. PC mode can't see a back paddle, so pick the button that plays each slot.", 860)
            : diag ? RebelBanner("Roll each stick slowly around its edge a few times, squeeze both triggers all the way and press every button. X20CTL only listens; nothing is sent to the controller. All controller buttons are captured for testing. Use Esc or the mouse to leave.", 1180)
            : tester ? RebelBanner("Working Draft · " + (Tester!.SourceText.StartsWith("Gameplay input unavailable") ? "No input" : "Input detected") + " · Gameplay tester", 700)
            : setups ? (Setups!.Healthy ? null : RebelBanner(Setups.LibraryText, 900))
            : mouse ? RebelBanner($"Player {Player + 1} · {Model.Name}", 600)
            : device ? RebelBanner("Model selected manually. Connect the controller to check its device details.", 900)
            : pcmode ? RebelBanner("Route gameplay input through your saved PC profile to a virtual Xbox controller. Nothing is written to hardware.", 1000)
            : vibration ? (Vibration!.Draft.CanEdit ? RebelLeadLine("Linked grip strength", "Local draft") : RebelLeadLine("Visual preview only", "Native settings unavailable"))
            : draft ? RebelLeadLine("Local draft", Draft.PcMode ? "Applies on this PC." : "Not written to controller memory.") : null;
        rebelHints!.Content = page switch
        {
            "dashboard" => Parts.Hints(("A", "Select"), ("B", "Controller Zone"), ("X", "Configure"), ("Y", "Rescan games")),
            "buttons" => Parts.Hints(("Pad", "Select control"), ("Esc", "Back")),
            "curves" => Parts.Hints(("A", "Adjust"), ("X", "Reset"), ("B", "Back")),
            "pcmode" => Parts.Hints(("A", "Select"), ("B", "Back"), ("LB/RB", "Tabs")),
            "mouse" => Parts.Hints(("Esc", "Back")),
            "tester" => Parts.Hints(("B", "Hold to exit")),
            "diagnostics" => Parts.Hints(("Esc", "Exit capture")),
            "setups" => Parts.Hints(("A", "Open"), ("B", "Back"), ("Y", "New setup"), ("LB/RB", "Tabs")),
            "device" => Parts.Hints(("A", "Open"), ("B", "Back"), ("LB/RB", "Tabs")),
            "vibration" => Parts.Hints(("A", "Select"), ("B", "Back"), ("X", "Reset"), ("LT/RT", "Strength"), ("LB/RB", "Tabs")),
            "macros" => Parts.Hints(("A", "Edit"), ("B", "Back"), ("X", "Add event"), ("Y", "Preview"), ("LT/RT", "Slot"), ("LB/RB", "Tabs")),
            _ => Parts.Hints(("A", "Select"), ("B", "Back"))
        };
        if (dash) { if (changed || rebelDash.Children.Count == 0) RebelDashboard(); }
        else if (curves) RebelCurvesPage();
        else if (macros) RebelMacrosPage();
        else if (vibration) RebelVibrationPage();
        else if (pcmode) RebelPcModePage();
        else if (device) RebelDevicePage();
        else if (mouse) RebelMousePage();
        else if (setups) RebelSetupsPage();
        else if (tester) RebelTesterPage();
        else if (diag) RebelDiagnosticsPage();
        else if (buttons) { if (SelectedControl.Length == 0 || !layout.Controls.Any(control => control.Key == SelectedControl)) SelectControl("A"); RebelButtonsPage(changed); }
    }
    /// <summary>A lead paragraph behind a red rule (.lead).</summary>
    private static FrameworkElement RebelBanner(string text, double width)
    {
        var t = Ink.Text(text, 16, Ink.B(Ink.C("#cfc7c9")), false); t.TextWrapping = TextWrapping.Wrap; t.LineHeight = 23;
        return new Border { Width = width, BorderBrush = Ink.B(Ink.Red), BorderThickness = new(4, 0, 0, 0), Padding = new(16, 0, 0, 0), Margin = new(-2, -2, 0, 0), Child = t };
    }
    private static FrameworkElement RebelLeadLine(string tag, string lead)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(Pieces.Tag(tag)); var t = Ink.Text(lead, 16, Ink.B(Ink.C("#cfc7c9")), false); t.Margin = new(14, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(t); return row;
    }
    private void RebelBackdrop(string page)
    {
        var c = new Canvas { Width = 1600, Height = 900 }; rebelBack!.Children.Clear(); rebelBack.Children.Add(c);
        c.Children.Add(new Rectangle { Width = 1600, Height = 900, Fill = Ink.B(Ink.Black) });
        c.Children.Add(new Rectangle { Width = 1600, Height = 900, Fill = RRadial(1, 0, .7, .6, Ink.A(Ink.Red, .14), .6) });
        c.Children.Add(new Rectangle { Width = 1600, Height = 900, Fill = RRadial(0, 1, .6, .5, Ink.A(Ink.Dark, .22), .7) });
        switch (page)
        {
            case "dashboard":
                c.Children.Add(RStripe(1180, -40, 600, 940, 330, Ink.Red)); c.Children.Add(RStripe(1546, -40, 966, 940, 22, Ink.White, .92)); c.Children.Add(RStripe(1112, -40, 532, 940, 12, Ink.Black));
                c.Children.Add(Dots.Field(560, 0, 1040, 900, Ink.A(Ink.Black, .55), 13, 2.8, RFade(205, .08, .55)));
                c.Children.Add(Star.Make(1150, 360, 290, 14, .5, Ink.Dark, Ink.Black));
                var r = new Random(4); for (int i = 0; i < 3; i++) c.Children.Add(Star.Make(560 + r.NextDouble() * 140, 180 + r.NextDouble() * 150, 7 + r.NextDouble() * 8, (r.NextDouble() - .5) * 50, .3 + r.NextDouble() * .4));
                break;
            case "buttons":
                c.Children.Add(RStripe(1250, -40, 990, 940, 250, Ink.Red)); c.Children.Add(Dots.Field(900, 0, 700, 900, Ink.A(Ink.Black, .5), 12, 2.6, RFade(200, .1, .6)));
                break;
            case "curves":
                c.Children.Add(RPoly(Ink.B(Ink.Red), 960, 770, 1600, 610, 1600, 900, 880, 900)); c.Children.Add(RPoly(Ink.B(Ink.White), 952, 760, 1600, 600, 1600, 606, 950, 767));
                c.Children.Add(Dots.Field(860, 600, 740, 300, Ink.A(Ink.Black, .5), 12, 2.6, RFade(0, 0, .8)));
                break;
            default:
                c.Children.Add(RStripe(1360, -40, 1180, 940, 120, Ink.C("#120709")));
                break;
        }
    }
    private FrameworkElement RebelStatus()
    {
        var clock = new HeaderClock { FontFamily = Ink.Heavy, FontSize = 17, Foreground = Ink.B(Ink.Hot), Margin = new(14, 0, 0, 0), MinWidth = 0, RenderTransform = Ink.Slant, RenderTransformOrigin = new(.5, .5) };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { Ink.Text($"Player {Player + 1}", 17), clock } };
        return new Border
        {
            Background = Ink.B(Ink.Black), BorderBrush = Ink.B(Ink.A(Ink.White, .3)), BorderThickness = new(1.5), Padding = new(20, 8, 20, 8), Effect = Ink.Shadow(6, 6, 0, Ink.Red, 1), Child = row,
            RenderTransformOrigin = new(.5, .5), RenderTransform = new TransformGroup { Children = { new SkewTransform(-10, 0), new RotateTransform(-2) } },
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 34, 42, 0), IsHitTestVisible = false
        };
    }
    private string RebelFinish => PadColours.Catalog.For(Model.Id).FirstOrDefault(f => f.Id == PadColours.For(Player, Model.Id))?.Name ?? "";
    private void RebelDashboard()
    {
        var d = rebelDash!; d.Children.Clear(); Dashboard ??= CreateDashboard();
        var logo = Pieces.Logo(104); RPut(logo, 36, 22); d.Children.Add(logo);
        var word = Ransom.Make("X20CTL", 40, 9); RPut(word, 152, 40); d.Children.Add(word);
        d.Children.Add(RebelStatus());
        var who = new StackPanel(); who.Children.Add(Ink.Label($"Player {Player + 1}"));
        var name = Ink.Text(Model.Name, 58); name.Margin = new(0, 6, 0, 0); name.HorizontalAlignment = HorizontalAlignment.Left; who.Children.Add(name);
        string finish = RebelFinish; if (finish.Length > 0) { var f = Ransom.Make(finish, 34, 3); f.Margin = new(0, 12, 0, 0); f.HorizontalAlignment = HorizontalAlignment.Left; who.Children.Add(f); }
        RPut(who, 64, 168); d.Children.Add(who);
        var facts = new (string label, TextBlock source)[] { ("Current setup", Dashboard.RebelSetup), ("Connection", Dashboard.RebelConnection), ("Battery", Dashboard.RebelBattery), ("Changes", Dashboard.RebelChanges) };
        for (int i = 0; i < facts.Length; i++)
        {
            var slab = new Slab { Width = 390, Height = 54, Seed = 40 + i, Fill = Ink.B(Ink.Panel), Line = Ink.B(Ink.A(Ink.White, .26)), LineWidth = 1.4, Rot = -2 };
            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(22, 0, 0, 0) }; col.Children.Add(Ink.Label(facts[i].label, 10.5));
            var value = Ink.Text("", 18); value.Margin = new(0, 2, 0, 0); value.HorizontalAlignment = HorizontalAlignment.Left; value.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text") { Source = facts[i].source }); col.Children.Add(value);
            slab.Children.Add(col); RPut(slab, 64 + i * 18, 372 + i * 64); d.Children.Add(slab);
        }
        var pad = Pieces.Pad(Model.Id, PadColours.For(Player, Model.Id), 766); pad.RenderTransformOrigin = new(.5, .5); pad.RenderTransform = new RotateTransform(-8); pad.Effect = Ink.Shadow(18, 20, 0, Ink.Black, 1);
        RPut(pad, 1075 - 383, 369 - 255); d.Children.Add(pad);
        for (int i = 0; i < RebelPages.Length; i++)
        {
            var (n, icon, key) = RebelPages[i];
            var tab = new PageSlab(n, icon, 146, 70, 60 + i, -6, new Thickness(-10, -16, -16, -2), true, 20, 26, 16, 22) { Tag = key };
            tab.GotKeyboardFocus += (_, _) => { foreach (var other in d.Children.OfType<PageSlab>()) other.On = ReferenceEquals(other, tab); };
            tab.Click += (_, _) => RebelGo(key); RPut(tab, 96 + i * 158, 734 - i * 9); d.Children.Add(tab); if (i == 0) tab.On = true;
        }
        var lb = Parts.Key("LB"); ((FrameworkElement)lb).HorizontalAlignment = HorizontalAlignment.Left; ((FrameworkElement)lb).VerticalAlignment = VerticalAlignment.Top; ((FrameworkElement)lb).Margin = new(40, 760, 0, 0); d.Children.Add(lb);
        var rb = Parts.Key("RB"); ((FrameworkElement)rb).HorizontalAlignment = HorizontalAlignment.Left; ((FrameworkElement)rb).VerticalAlignment = VerticalAlignment.Top; ((FrameworkElement)rb).Margin = new(1536, 676, 0, 0); d.Children.Add(rb);
        Dispatcher.BeginInvoke(() => d.Children.OfType<PageSlab>().FirstOrDefault()?.Focus(), DispatcherPriority.Input);
    }
    private (string name, string[] keys)[] RebelAvailableGroups()
    {
        var groups = RebelGroups.Select(group => (group.name, keys: group.keys.Where(key => layout.Controls.Any(control => control.Key == key)).ToArray())).Where(group => group.keys.Length > 0).ToList();
        var paddles = layout.Controls.Where(control => control.Role == "macro").Select(control => control.Key).Distinct().ToArray();
        if (paddles.Length > 0) groups.Add(("Paddles", paddles));
        return groups.ToArray();
    }
    private static string RebelGlyph(string key) => key switch { "LSTICK_ANALOG" => "L axis", "RSTICK_ANALOG" => "R axis", "NONE" => "Off", _ => Glyph(key) };
    private void RebelButtonsPage(bool fresh)
    {
        var b = rebelButtons!; string? focused = (Keyboard.FocusedElement as FrameworkElement) is { Tag: string t } fe && b.IsAncestorOf(fe) ? t : null;
        b.Children.Clear();
        var groups = RebelAvailableGroups(); string group = groups.FirstOrDefault(g => g.keys.Contains(SelectedControl)).name ?? "Face", sel = SelectedControl, target = Draft.Target(sel); bool editable = Draft.CanEdit(sel);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (name, keys) in groups) { var chip = new ChipTab(name) { On = name == group && !rebelAll, Tag = "group:" + name }; chip.Click += (_, _) => { rebelAll = false; SelectControl(keys[0]); }; tabs.Children.Add(chip); }
        RPut(tabs, 930, 40); b.Children.Add(tabs);
        // the rows: each control of the group, what it sends, and whether that is changed
        var list = new StackPanel { Width = 560 }; int i = 0;
        var keysShown = rebelAll ? ButtonDraft.Sources.Where(k => Draft.Target(k) != k).ToArray() : groups.First(g => g.name == group).keys;
        foreach (var key in keysShown)
        {
            bool on = key == sel; var row = new RowSlab(i, 70) { Margin = new(i * 5, 0, 0, 9), Tag = "row:" + key, On = on };
            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(Parts.Key(RebelGlyph(key), on)); var sends = row.Tint(Ink.Text(Draft.CanEdit(key) ? "sends" : "Not remappable", 16, null, false), Ink.A(Ink.White, .7), Ink.A(Ink.Black, .7)); sends.Margin = new(16, 0, 16, 0); sends.VerticalAlignment = VerticalAlignment.Center; left.Children.Add(sends);
            if (Draft.CanEdit(key)) left.Children.Add(Parts.Key(RebelGlyph(Draft.Target(key)))); row.Left.Children.Add(left);
            if (Draft.Target(key) != key) row.Right.Children.Add(row.Tint(Ink.Text("Modified", 15, null, false, FontWeights.SemiBold), Ink.C("#c9c1c3"), Ink.Black));
            string k = key; row.Click += (_, _) => { if (!OpenMacroSlot(k)) SelectControl(k); }; row.GotKeyboardFocus += (_, _) => { if (SelectedControl != k) SelectControl(k); };
            list.Children.Add(row); i++;
        }
        if (rebelAll && keysShown.Length == 0) { var none = new RowSlab(0, 70) { Tag = "row:none" }; none.Left.Children.Add(none.Tint(Ink.Text("No changed assignments", 19), Ink.White, Ink.Black)); list.Children.Add(none); i++; }
        foreach (var (g, text, act) in new (string, string, Action)[] { (Glyphs.Reset, "Reset this control", () => { Draft.Reset(SelectedControl); RefreshEditor(); }), (Glyphs.Test, "Test mapping", StartTry) })
        {
            var row = new RowSlab(i, 64) { Margin = new(i * 5, 0, 0, 9), Tag = "act:" + text, IsEnabled = text != "Reset this control" || editable };
            if (!row.IsEnabled) row.Opacity = .45;
            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; left.Children.Add(Glyphs.Make(g, 16, Ink.B(Ink.Hot)));
            var label = row.Tint(Ink.Text(text, 18), Ink.White, Ink.Black); label.Margin = new(16, 0, 0, 0); left.Children.Add(label); row.Left.Children.Add(left);
            var chev = Glyphs.Make(Glyphs.Chevron, 12, Ink.B(Ink.A(Ink.White, .5))); row.Right.Children.Add(chev);
            row.Click += (_, _) => act(); list.Children.Add(row); i++;
        }
        RPut(list, 262, 200); b.Children.Add(list);
        // the pad, with the chosen control in the crosshair
        double px = 920, py = 141, pw = 600, ph = 400;
        var pad = Pieces.Pad(Model.Id, PadColours.For(Player, Model.Id), pw); pad.Effect = Ink.Shadow(14, 16, 0, Ink.Black, 1); RPut(pad, px, py); b.Children.Add(pad);
        var region = layout.Controls.FirstOrDefault(r => r.Key == sel && r.View == "front");
        if (region != null)
        {
            double ax = px + (region.Bounds.X + region.Bounds.Width / 2) * pw, ay = py + (region.Bounds.Y + region.Bounds.Height / 2) * ph, top = 164;
            var g = new Canvas { IsHitTestVisible = false, Width = 1600, Height = 900 };
            g.Children.Add(new Polyline { Points = new PointCollection([new(ax - 34, ay - 34), new(ax - 70, top), new(ax - 150, top)]), Stroke = Ink.B(Ink.White), StrokeThickness = 2.5 });
            void Ring(double r, Brush stroke, double t, DoubleCollection? dash = null) { var e = new Ellipse { Width = r * 2, Height = r * 2, Stroke = stroke, StrokeThickness = t, StrokeDashArray = dash }; Canvas.SetLeft(e, ax - r); Canvas.SetTop(e, ay - r); g.Children.Add(e); }
            Ring(34, Ink.B(Ink.Red), 5); Ring(46, Ink.B(Ink.White), 1.5, new DoubleCollection([4 / 1.5, 6 / 1.5]));
            foreach (double a in new[] { 0, Math.PI / 2, Math.PI, 1.5 * Math.PI }) g.Children.Add(new Line { X1 = ax + 26 * Math.Cos(a), Y1 = ay + 26 * Math.Sin(a), X2 = ax + 48 * Math.Cos(a), Y2 = ay + 48 * Math.Sin(a), Stroke = Ink.B(Ink.White), StrokeThickness = 3 });
            var diamond = new Rectangle { Width = 18, Height = 18, Stroke = Ink.B(Ink.White), StrokeThickness = 2, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(45) }; Canvas.SetLeft(diamond, ax - 9); Canvas.SetTop(diamond, ay - 9); g.Children.Add(diamond);
            b.Children.Add(g);
            var say = Ransom.Make(editable ? $"{RebelGlyph(sel)} sends {RebelGlyph(target)}" : RebelGlyph(sel), 32, 4); RPut(say, Math.Max(930, ax - 410), top - 46); b.Children.Add(say);
        }
        var output = Ink.Label(editable ? "Output" : "Output · read-only"); RPut(output, 960, 560); b.Children.Add(output);
        double[] tilt = [-6, 4, -3, 5, -5];
        var outputs = Draft.PcMode ? ButtonDraft.Targets.Append("NONE").ToArray() : ButtonDraft.Targets;
        var outputGrid = new Canvas { Width = 600, Height = Math.Ceiling(outputs.Length / 5d) * 104 + 12 };
        for (int o = 0; o < outputs.Length; o++)
        {
            string key = outputs[o]; bool on = key == target;
            var face = new Slab { Seed = 110 + o, Fill = Ink.B(on ? Ink.White : Ink.Panel), Line = on ? null : Ink.B(Ink.A(Ink.White, .26)), LineWidth = 1.6, Under = on ? Ink.B(Ink.Red) : null };
            var t2 = Ink.Text(key switch { "L3" => "LS", "R3" => "RS", "DPAD_UP" => "↑", "DPAD_DOWN" => "↓", "DPAD_LEFT" => "←", "DPAD_RIGHT" => "→", "SELECT" => "View", "START" => "Menu", "NONE" => "Off", _ => key }, key is "SELECT" or "START" or "NONE" ? 17 : 28, Ink.B(on ? Ink.Black : Ink.White)); t2.HorizontalAlignment = HorizontalAlignment.Center; t2.VerticalAlignment = VerticalAlignment.Center; face.Children.Add(t2);
            var cell = new Button { Width = 74, Height = 84, Content = face, Tag = "out:" + key, FocusVisualStyle = null, IsEnabled = editable, Opacity = editable ? 1 : .35, Cursor = Cursors.Hand, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(tilt[o % 5]) };
            var tpl = new ControlTemplate(typeof(Button)); tpl.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)); cell.Template = tpl;
            cell.GotKeyboardFocus += (_, _) => { if (!on) { face.Line = Ink.B(Ink.White); face.LineWidth = 2.5; face.InvalidateVisual(); } };
            cell.LostKeyboardFocus += (_, _) => { if (!on) { face.Line = Ink.B(Ink.A(Ink.White, .26)); face.LineWidth = 1.6; face.InvalidateVisual(); } };
            System.Windows.Automation.AutomationProperties.SetName(cell, $"Send {Label(key)}");
            cell.Click += (_, _) => SetDraftTarget(key);
            RPut(cell, o % 5 * 116, o / 5 * 104 + (o / 5 >= 2 ? 12 : 0)); outputGrid.Children.Add(cell);
        }
        var outputScroll = new ScrollViewer { Content = outputGrid, Width = 600, Height = 208, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        RPut(outputScroll, 960, 598); b.Children.Add(outputScroll);
        string want = focused is { } focusTag && focusTag.StartsWith("row:") && focusTag != "row:" + sel
            ? "row:" + sel : focused ?? (fresh ? "row:" + sel : "");
        if (want.Length > 0) Dispatcher.BeginInvoke(() => FindTagged(b, want)?.Focus(), DispatcherPriority.Input);
    }
    private static FrameworkElement? FindTagged(DependencyObject root, string tag)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child == null) continue;
            if (child is FrameworkElement { Tag: string t } fe && t == tag && fe.Focusable) return fe;
            if (FindTagged(child, tag) is { } found) return found;
        }
        return null;
    }
    /// <summary>The Rebel pages' own keys; true when handled.</summary>
    private bool RebelKey(KeyEventArgs e)
    {
        if (!IsRebel) return false;
        if (RebelCurveKey(e) || RebelVibrationKey(e) || RebelPcKey(e) || RebelMouseKey(e)) return true;
        if (e.IsRepeat) return false;
        if (rebelPage == "setups" && Setups != null)
        {
            if (Keyboard.FocusedElement is TextBox || Setups.DeletePending || Setups.RenameActive) return false;
            if (e.Key is Key.Escape or Key.B) { BackRequested?.Invoke(); e.Handled = true; return true; }
            if (e.Key == Key.Y) { Setups.Click("new"); e.Handled = true; return true; }
        }
        if (rebelPage == "buttons" && e.Key == Key.Y && !AssignmentsVisible) { rebelAll = !rebelAll; RebelButtonsPage(false); e.Handled = true; return true; }
        if (rebelPage is "buttons" or "dashboard" && e.Key is Key.Escape or Key.B) { BackRequested?.Invoke(); e.Handled = true; return true; }
        if (rebelPage == "macros" && Macros != null)
        {
            var tag = (Keyboard.FocusedElement as FrameworkElement)?.Tag as string ?? "";
            if (e.Key == Key.Y) { RebelMacroPreview(); e.Handled = true; return true; }
            if (e.Key is Key.Left or Key.Right && tag == "mac:trigger") { RebelCycleTrigger(e.Key == Key.Right ? 1 : -1); e.Handled = true; return true; }
            if (e.Key is Key.Escape or Key.B)
            {   // B steps out of the editor to the chosen panel, then leaves
                if (!tag.StartsWith("ev:") && rebelMacros!.IsKeyboardFocusWithin) FindTagged(rebelMacros, Macros.SelectedIndex >= 0 ? "ev:" + Macros.SelectedIndex : "ev:add")?.Focus(); else BackRequested?.Invoke();
                e.Handled = true; return true;
            }
        }
        if (rebelPage is "curves" or "vibration" or "pcmode" or "device" or "mouse" && e.Key is Key.Escape or Key.B) { BackRequested?.Invoke(); e.Handled = true; return true; }
        if (rebelPage == "dashboard" && e.Key == Key.Y) { Dashboard?.Rescan(); e.Handled = true; return true; }
        if (rebelPage == "dashboard" && e.Key == Key.X) { ShowButtons(); e.Handled = true; return true; }
        return false;
    }
}

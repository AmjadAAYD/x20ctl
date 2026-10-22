using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>One controller's Studio: header, the tab strip, the page, and the hint row that changes with the page.</summary>
internal sealed class StudioView : UserControl
{
    static readonly string[] AllTabs = ["Dashboard", "Buttons", "Curves", "Macros", "Vibration", "PC mode", "Device", "Mouse", "Setups", "Tester", "Diagnostics"];
    // a phone is not a PC: no virtual pad to drive and no pointer to steer
    static readonly string[] PhoneTabs = AllTabs.Where(t => t is not ("PC mode" or "Mouse")).ToArray();
    public static string[] Tabs => Phone.Active ? PhoneTabs : AllTabs;
    readonly StudioState state; readonly Dictionary<string, Control> pages = new();
    readonly ContentControl host = new(); readonly StackPanel strip = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    readonly Border footerHost = new();
    ScrollViewer? scroller;
    // the tab strip: each tab's label and the blank line under it, and the one white underline that slides between them
    readonly Dictionary<string, (Border Tab, TextBlock Label, Border Line)> tabs = new();
    readonly Canvas overlay = new() { IsHitTestVisible = false };
    readonly Border underline = new() { Height = 2, Background = T.Text, CornerRadius = new CornerRadius(1) };
    Action stopUnderline = () => { };
    string current = "Dashboard";
    public Control Header { get; }
    public Control Footer => footerHost;

    public StudioView(StudioState state, int slot, Action back, TextBlock clock, Bitmap? mark)
    {
        this.state = state;
        Header = BuildHeader(state, back, clock, mark);
        var lb = new Pill("‹  LB", Pill.Kinds.Ghost, () => Step(-1), 12) { Padding = new Thickness(14, 5) };
        var rb = new Pill("RB  ›", Pill.Kinds.Ghost, () => Step(1), 12) { Padding = new Thickness(14, 5) };
        var tabRow = new Grid { Margin = Phone.Active ? new Thickness(14, 2, 14, 2) : new Thickness(36, 12, 36, 6), ColumnDefinitions = new("Auto,*,Auto") };
        lb.IsVisible = rb.IsVisible = !Phone.Active;
        tabRow.Children.Add(lb);
        foreach (string t in Tabs) strip.Children.Add(Tab(t));
        overlay.Children.Add(underline);
        var scroll = new ScrollViewer { Content = new Grid { Children = { strip, overlay } }, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Margin = new Thickness(18, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(scroll, 1); tabRow.Children.Add(scroll); Grid.SetColumn(rb, 2); tabRow.Children.Add(rb);
        var layout = new DockPanel(); DockPanel.SetDock(tabRow, Dock.Top); layout.Children.Add(tabRow);
        // a phone shows the top of each tab and scrolls to the rest with a swipe; the tab keeps its desktop height
        if (Phone.Active) layout.Children.Add(scroller = new ScrollViewer { Content = host, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        else layout.Children.Add(host);
        if (Phone.Active) host.MinHeight = 600;
        Content = layout; if (PcModeHub.Shared.AutoFor(state.Model.Id)) current = "PC mode"; Show(current);
        // the underline finds its first tab once the strip is laid out
        strip.SizeChanged += (_, _) => { if (double.IsNaN(Canvas.GetLeft(underline))) Underline(false); };
        scroll.SizeChanged += (_, e) => Fit(e.NewSize.Width);
    }

    Control Tab(string name)
    {
        var label = T.Label(name, 15, T.Soft); label.TextWrapping = TextWrapping.NoWrap;
        var line = new Border { Height = 2, Margin = new Thickness(2, 5, 2, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
        var tab = new Border { Padding = new Thickness(18, Phone.Active ? 10 : 6), Cursor = new Cursor(StandardCursorType.Hand), Background = Brushes.Transparent, Child = T.Stack(0, label, line) };
        tab.PointerEntered += (_, _) => { if (name != current) label.Foreground = T.Text; };
        tab.PointerExited += (_, _) => { if (name != current) label.Foreground = T.Soft; };
        if (Phone.Active) tab.Tapped += (_, _) => Show(name); else tab.PointerPressed += (_, _) => Show(name);
        tabs[name] = (tab, label, line); return tab;
    }

    /// <summary>On a narrow window the tabs close up, so every one of them still shows; the current one is scrolled to
    /// if they still don't fit.</summary>
    void Fit(double room)
    {
        double wide = tabs.Values.Sum(t => t.Label.DesiredSize.Width + 36 + 4) - 4;
        bool tight = wide > room, changed = tight != (strip.Spacing == 0); strip.Spacing = tight ? 0 : 4;
        if (changed) foreach (var t in tabs.Values) t.Tab.Padding = new Thickness(tight ? 11 : 18, Phone.Active ? 10 : 6);
        Dispatcher.UIThread.Post(() => { if (changed) Underline(false); tabs[current].Tab.BringIntoView(); }, DispatcherPriority.Loaded);
    }

    /// <summary>Moves the underline under the current tab: gliding there (260 ms), or straight there before the strip is shown.</summary>
    void Underline(bool glide)
    {
        if (!tabs.TryGetValue(current, out var tab) || tab.Line.Bounds.Width <= 0 || tab.Line.TranslatePoint(default, overlay) is not { } to) return;
        double x0 = Canvas.GetLeft(underline), w0 = underline.Width, x1 = to.X, w1 = tab.Line.Bounds.Width; Canvas.SetTop(underline, to.Y);
        stopUnderline();
        if (!glide || double.IsNaN(x0) || double.IsNaN(w0)) { Canvas.SetLeft(underline, x1); underline.Width = w1; return; }
        stopUnderline = Motion.Run(260, t => { double e = Motion.CubicOut(t); Canvas.SetLeft(underline, Motion.Lerp(x0, x1, e)); underline.Width = Motion.Lerp(w0, w1, e); });
    }

    static Control BuildHeader(StudioState state, Action back, TextBlock clock, Bitmap? mark)
    {
        Control logo = mark == null ? new Border() : new Image { Source = mark, Width = 26, Height = 26 };
        var left = T.Line(12, logo, T.Label("X20CTL", 22, null, FontWeight.Bold)); left.VerticalAlignment = VerticalAlignment.Center;
        var pillCells = Enumerable.Range(0, 4).Select(_ => (Control)new Border { Width = 5, Height = 12, Background = T.B("#4A5A7C"), CornerRadius = new CornerRadius(1.5), Margin = new Thickness(1, 0) }).ToArray();
        var batt = new Border
        {
            Background = T.B("#66101A33"), BorderBrush = T.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(30), Padding = new Thickness(18, 8), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Child = T.Line(10, new Border { BorderBrush = T.Soft, BorderThickness = new Thickness(1.4), CornerRadius = new CornerRadius(4), Padding = new Thickness(2), Child = T.Line(0, pillCells) }, T.Label("Battery", 14, T.Soft), T.Label("—", 14, T.Text))
        };
        var shortName = state.Model.Name.Replace("EasySMX ", "");
        var device = T.Line(8, T.Label("🎮", 14, T.AccentHi), T.Label(shortName, 14, null, FontWeight.Medium), T.Label("· Offline", 13.5, T.Warn), T.Label("···", 16, T.Soft)); device.VerticalAlignment = VerticalAlignment.Center;
        var tail = T.Line(18, device, new Pill("← Controller Zone", Pill.Kinds.Flat, back) { Padding = new Thickness(12, 7) }, clock); tail.VerticalAlignment = VerticalAlignment.Center;
        var bar = new Grid { Margin = Phone.Active ? new Thickness(24, 6, 24, 0) : new Thickness(34, 12, 26, 0), Height = 46, ColumnDefinitions = new("*,Auto,*") };
        bar.Children.Add(left); Grid.SetColumn(batt, 1); bar.Children.Add(batt); tail.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(tail, 2); bar.Children.Add(tail);
        return bar;
    }

    public void HandleKey(KeyEventArgs e, bool down = true)
    {
        if (host.Content is IKeyPage page) { page.OnKey(e, down); if (e.Handled) return; }
        if (!down) return;
        if (e.Key == Key.PageUp || e.Key == Key.OemOpenBrackets) Step(-1);
        else if (e.Key == Key.PageDown || e.Key == Key.OemCloseBrackets) Step(1);
    }
    void Step(int d) => Show(Tabs[(Array.IndexOf(Tabs, current) + d + Tabs.Length) % Tabs.Length]);
    public void ShowTab(string name) => Show(Tabs.Contains(name) ? name : "Dashboard");

    Control Page(string name) => name switch
    {
        "Buttons" => new ButtonsPage(state),
        "Curves" => new CurvesPage(state),
        "Macros" => new MacrosPage(state),
        "Vibration" => new VibrationPage(state),
        "Device" => new DevicePage(state),
        "PC mode" => new PcModePage(state, ShowTab),
        "Mouse" => new MousePage(state),
        "Setups" => new SetupsPage(state),
        "Tester" => new TesterPage(state),
        "Diagnostics" => new DiagnosticsPage(state),
        _ => new DashboardPage(state, ShowTab)
    };

    /// <summary>Opens a tab: its page slides in from the side it lies on, and the underline glides to it.</summary>
    void Show(string name)
    {
        int from = Array.IndexOf(Tabs, current), to = Array.IndexOf(Tabs, name);
        bool moved = host.Content != null && name != current;
        current = name;
        // pages that summarise or follow the drafts are rebuilt each time they open
        if (!pages.TryGetValue(name, out var page) || name is "Dashboard" or "Setups" or "Device" or "PC mode") pages[name] = page = Page(name);
        host.Content = page;
        if (moved && scroller != null) scroller.Offset = default;
        if (moved) Motion.SlideIn(page, to > from ? 28 : -28);
        foreach (var (n, (_, label, _)) in tabs) { bool on = n == current; label.Foreground = on ? T.Text : T.Soft; label.FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal; }
        // the new label's width settles in this layout pass; the underline moves after it
        Dispatcher.UIThread.Post(() => { Underline(moved); tabs[current].Tab.BringIntoView(); }, DispatcherPriority.Loaded);
        footerHost.Child = Footing(name);
    }

    string PcHeadline() => PcModeHub.Shared.Status.Length > 0 ? "PC mode · " + PcModeHub.Shared.Status : PcModeHub.Shared.NoticeSeen ? "PC mode · Apply Changes saves these settings" : "PC mode · Open the PC mode tab and read the notice to start applying this";

    Control Footing(string name)
    {
        var (headline, hints) = name switch
        {
            "Dashboard" => (null, Hints.Row(("A", "Select"), ("B", "Controller Zone"), ("X", "Configure"), ("Y", "Rescan games"), ("LT/RT", "Scroll games"), ("LB/RB", "Tabs"))),
            "Buttons" => (PcHeadline(), Hints.Row(("A", "Select"), ("B", "Back"), ("X", "Test mapping"), ("LB/RB", "Tabs"))),
            "Curves" => (PcHeadline(), Hints.Row(("A", "Adjust"), ("B", "Back"), ("X", "Reset curve"), ("Y", "Next preset"), ("LT/RT", "Channel"), ("LB/RB", "Tabs"))),
            "Macros" => (PcHeadline(), Hints.Row(("A", "Edit"), ("B", "Back"), ("X", "Add event"), ("Y", "Preview"), ("LT/RT", "Slot"), ("LB/RB", "Tabs"))),
            "PC mode" => (PcModeHub.Shared.Enabled ? "PC mode is on · Buttons, Curves and Macros apply on this PC" : "PC mode is off · Your controller goes straight to the game", Hints.Row(("A", "Select"), ("B", "Back"), ("LB/RB", "Tabs"))),
            "Vibration" => ("Vibration preview · No local changes", Hints.Row(("A", "Select"), ("B", "Back"), ("X", "Reset"), ("LT/RT", "Strength"), ("LB/RB", "Tabs"))),
            _ => (null, Hints.Row(("A", "Select"), ("B", "Back"), ("LB/RB", "Tabs")))
        };
        var inner = new StackPanel { Spacing = 10 };
        if (headline != null) inner.Children.Add(T.Label(headline, 18, null, FontWeight.Medium));
        inner.Children.Add(hints);
        var apply = new Pill("Apply Changes", Pill.Kinds.Primary) { Padding = new Thickness(26, 12) };
        apply.Click += () => { PcModeHub.Shared.Save(state); Show(current); };
        // PC mode (and its Apply Changes) is for a PC: a phone shows no footer at all
        if (Phone.Active) return new Border();
        var row = new Grid { ColumnDefinitions = new("*,Auto") }; row.Children.Add(inner);
        if (name is "Buttons" or "Curves" or "Macros") { apply.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(apply, 1); row.Children.Add(apply); }
        return new Border { BorderBrush = T.Hairline, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(30, 12, 30, 14), Child = row, Background = T.B("#33040814") };
    }
}

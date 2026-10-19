using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Shapes;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>One controller's Studio: header, the tab strip, the page, and the hint row that changes with the page.</summary>
internal sealed class StudioView : UserControl
{
    public static readonly string[] Tabs = ["Dashboard", "Buttons", "Curves", "Macros", "Vibration", "PC mode", "Device", "Mouse", "Setups", "Tester", "Diagnostics"];
    readonly StudioState state; readonly Dictionary<string, Control> pages = new();
    readonly ContentControl host = new(); readonly StackPanel strip = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    readonly Border footerHost = new();
    string current = "Dashboard";
    public Control Header { get; }
    public Control Footer => footerHost;

    public StudioView(StudioState state, int slot, Action back, TextBlock clock, Bitmap? mark)
    {
        this.state = state;
        Header = BuildHeader(state, back, clock, mark);
        var lb = new Pill("‹  LB", Pill.Kinds.Ghost, () => Step(-1), 12) { Padding = new Thickness(14, 5) };
        var rb = new Pill("RB  ›", Pill.Kinds.Ghost, () => Step(1), 12) { Padding = new Thickness(14, 5) };
        var tabRow = new Grid { Margin = new Thickness(36, 12, 36, 6), ColumnDefinitions = new("Auto,*,Auto") };
        tabRow.Children.Add(lb);
        var scroll = new ScrollViewer { Content = strip, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Margin = new Thickness(18, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(scroll, 1); tabRow.Children.Add(scroll); Grid.SetColumn(rb, 2); tabRow.Children.Add(rb);
        var layout = new DockPanel(); DockPanel.SetDock(tabRow, Dock.Top); layout.Children.Add(tabRow); layout.Children.Add(host);
        Content = layout; if (PcModeHub.Shared.AutoFor(state.Model.Id)) current = "PC mode"; Show(current);
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
        var bar = new Grid { Margin = new Thickness(34, 12, 26, 0), Height = 46, ColumnDefinitions = new("*,Auto,*") };
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
    public void ShowTab(string name) => Show(name);

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

    void Show(string name)
    {
        current = name;
        // pages that summarise or follow the drafts are rebuilt each time they open
        if (!pages.TryGetValue(name, out var page) || name is "Dashboard" or "Setups" or "Device" or "PC mode") pages[name] = page = Page(name);
        host.Content = page;
        strip.Children.Clear();
        foreach (string t in Tabs)
        {
            string n = t; bool on = n == current;
            var label = T.Label(n, 15, on ? T.Text : T.Soft, on ? FontWeight.SemiBold : FontWeight.Normal); label.TextWrapping = TextWrapping.NoWrap;
            var underline = new Border { Height = 2, Background = on ? T.Text : Brushes.Transparent, CornerRadius = new CornerRadius(1), Margin = new Thickness(2, 5, 2, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            var tab = new Border { Padding = new Thickness(18, 6), Cursor = new Cursor(StandardCursorType.Hand), Background = Brushes.Transparent, Child = T.Stack(0, label, underline) };
            tab.PointerPressed += (_, _) => Show(n); strip.Children.Add(tab);
        }
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
        var row = new Grid { ColumnDefinitions = new("*,Auto") }; row.Children.Add(inner);
        if (name is "Buttons" or "Curves" or "Macros") { apply.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(apply, 1); row.Children.Add(apply); }
        return new Border { BorderBrush = T.Hairline, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(30, 12, 30, 14), Child = row, Background = T.B("#33040814") };
    }
}

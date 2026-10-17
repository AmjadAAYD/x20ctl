using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>One controller's Studio: a header, the tabs down the left, and the page beside them.</summary>
internal sealed class StudioView : UserControl
{
    readonly StudioState state;
    readonly ContentControl host = new();
    readonly StackPanel tabs = new() { Spacing = 6, Width = 170 };
    readonly Dictionary<string, Control> pages = new();
    string current = "Dashboard";

    static readonly string[] Order = ["Dashboard", "Buttons", "Curves", "Macros", "Vibration", "Device"];

    public StudioView(ControllerModel model, Action back)
    {
        state = new StudioState(model);
        var backButton = Ui.Chip("‹  Controller Zone", false, back);
        var title = Ui.Label(model.Name, 22, null, FontWeight.SemiBold); title.VerticalAlignment = VerticalAlignment.Center; title.Margin = new Thickness(16, 0, 0, 0);
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 18), Children = { backButton, title } };
        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        DockPanel.SetDock(tabs, Dock.Left); root.Children.Add(tabs);
        host.Margin = new Thickness(18, 0, 0, 0); root.Children.Add(host);
        Content = root; Show(current);
    }

    public void ShowTab(string name) => Show(name);

    void Show(string name)
    {
        current = name;
        if (!pages.TryGetValue(name, out var page))
            pages[name] = page = name switch
            {
                "Buttons" => new ButtonsPage(state), "Curves" => new CurvesPage(state), "Macros" => new MacrosPage(state),
                "Vibration" => new VibrationPage(state), "Device" => new DevicePage(state), _ => new DashboardPage(state)
            };
        // the dashboard summarises the drafts, so it is rebuilt every time it is opened
        if (name == "Dashboard") pages[name] = page = new DashboardPage(state);
        host.Content = page;
        tabs.Children.Clear();
        foreach (string t in Order)
        {
            string n = t; var chip = Ui.Chip(n, n == current, () => Show(n));
            chip.HorizontalAlignment = HorizontalAlignment.Stretch; chip.HorizontalContentAlignment = HorizontalAlignment.Left; tabs.Children.Add(chip);
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

/// <summary>
/// The PC Mode entry (rework plan, 6 Nov 2026): names the scanned controller, shows the virtual pad type (a choice only when
/// the driver offers more than one), and shows the ban-risk warning before PC Mode is switched on. Enabling itself stays with
/// PcModePage.SwitchFromAgent so the driver check and the saved choice are the same as the switch on the PC mode page.
/// Drawn with the app's own dark surface, fonts and buttons.
/// </summary>
public static class PcModeGate
{
    /// <summary>True when the owner accepted. Cancel, Esc or closing the window all mean no.</summary>
    public static bool Ask(Window? owner, string controllerName, out PcModeEntry.VirtualType type)
    {
        var chosen = PcModeEntry.Chosen;
        FrameworkElement typeRow;
        if (PcModeEntry.Offered.Count > 1)
        {
            var types = new ComboBox { ItemsSource = PcModeEntry.Offered.Select(PcModeEntry.DisplayName).ToList(), SelectedIndex = Math.Max(0, PcModeEntry.Offered.ToList().IndexOf(chosen)), MinHeight = 38, FontSize = 15, Margin = new(0, 8, 0, 0) };
            types.SelectionChanged += (_, _) => { if (types.SelectedIndex >= 0) chosen = PcModeEntry.Offered[types.SelectedIndex]; };
            typeRow = new StackPanel { Margin = new(0, 18, 0, 0), Children = { Kit.Text("Virtual controller type", 15, "DS.TextSoft"), types } };
        }
        else typeRow = Kit.Text("Games will see a virtual " + PcModeEntry.DisplayName(chosen) + " controller.", 15.5, "DS.Text").Also(t => t.Margin = new(0, 18, 0, 0));

        var accept = new CheckBox { Margin = new(0, 18, 0, 0), VerticalContentAlignment = VerticalAlignment.Center, Content = Kit.Text("I understand and accept the risk", 15, "DS.Text") };
        var go = new Button { Style = DS.Style("ActionButton"), Content = Kit.Text("Enable PC Mode", 15, "DS.Text", FontWeights.SemiBold), IsEnabled = false, IsDefault = true, Height = 44, MinWidth = 150, Padding = new(18, 0, 18, 0), Margin = new(0, 0, 12, 0) };
        var cancel = new Button { Style = DS.Style("ActionButton"), Content = Kit.Text("Cancel", 15, "DS.Text"), IsCancel = true, Height = 44, MinWidth = 100, Padding = new(18, 0, 18, 0) };
        accept.Checked += (_, _) => go.IsEnabled = true; accept.Unchecked += (_, _) => go.IsEnabled = false;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 22, 0, 0), Children = { go, cancel } };
        var warning = Kit.Text(PcModeEntry.Warning, 15, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, 18, 0, 0); });
        var body = new StackPanel { Margin = new(26, 22, 26, 24), Children =
        {
            Kit.Text("PC Mode", 28, "DS.Text", FontWeights.Bold),
            Kit.Text("Controller: " + controllerName, 15, "DS.TextSoft").Also(t => t.Margin = new(0, 6, 0, 0)),
            typeRow, warning, accept, buttons,
        } };
        var window = new Window
        {
            Title = "PC Mode", Content = body, Width = 500, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            Background = DS.Brush("DS.Deep"), Foreground = DS.Brush("DS.Text"), FontFamily = DS.Body,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner, Owner = owner, ShowInTaskbar = false,
        };
        bool ok = false;
        go.Click += (_, _) => { ok = true; window.Close(); };
        window.ShowDialog();
        type = chosen;
        return ok;
    }
}

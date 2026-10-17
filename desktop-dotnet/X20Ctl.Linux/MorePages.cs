using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>What the app knows about the connected controller, and what it supports on this model.</summary>
internal sealed class DevicePage : UserControl
{
    public DevicePage(StudioState s)
    {
        var o = new DeviceOverview(s.Model.Id, 1);
        var tiles = new Grid { ColumnDefinitions = new("*,*,*"), RowDefinitions = new("Auto,Auto") };
        void Tile(string l, string v, int r, int c) { var t = Parts.Tile(l, v); t.Margin = new Thickness(c == 0 ? 0 : 6, r == 0 ? 0 : 10, c == 2 ? 0 : 6, 0); Grid.SetRow(t, r); Grid.SetColumn(t, c); tiles.Children.Add(t); }
        Tile("Battery", o.BatteryPercent is int p ? p + "%" : "Not reported", 0, 0); Tile("Firmware", o.Firmware ?? "Not reported", 0, 1); Tile("Hardware revision", o.HardwareRevision ?? "Not reported", 0, 2);
        Tile("Transport", o.Transport ?? "Not reported", 1, 0); Tile("Gameplay connection", o.GameplayStatus, 1, 1); Tile("Configuration connection", o.ConfigurationStatus, 1, 2);
        var support = T.Stack(6, T.Label("Support on this model", 18, null, FontWeight.SemiBold), T.Label(s.Model.Support + " · " + s.Model.Summary, 14, T.Soft));
        var art = new ControllerArt(s.Model.Id) { Height = 260 };
        var grid = new Grid { ColumnDefinitions = new("1*,1.4*"), Margin = new Thickness(36, 8, 36, 18) };
        grid.Children.Add(T.Card(art, 16));
        var right = T.Card(T.Stack(20, T.Stack(4, T.Label("Device", 28, null, FontWeight.SemiBold), T.Label(s.Model.Name + " · Offline", 14.5, T.Soft)), tiles, support), 26); right.Margin = new Thickness(18, 0, 0, 0); Grid.SetColumn(right, 1); grid.Children.Add(right);
        Content = new ScrollViewer { Content = grid };
    }
}

internal sealed class StubPage : UserControl
{
    public StubPage(string title, string text)
    {
        Content = new Grid { Margin = new Thickness(36, 8, 36, 18), Children = { T.Card(T.Stack(8, T.Label(title, 28, null, FontWeight.SemiBold), T.Label(text, 15, T.Soft)), 28) } };
    }
}
internal sealed class MousePage : StubPageBase { public MousePage(StudioState s) : base("Mouse", "Coming next.") { } }
internal class StubPageBase : UserControl { public StubPageBase(string title, string text) { Content = new StubPage(title, text); } }

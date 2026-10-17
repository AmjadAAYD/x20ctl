using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>The controller, its draft and what is waiting to be applied.</summary>
internal sealed class DashboardPage : UserControl
{
    public DashboardPage(StudioState s)
    {
        var art = new Image { Source = Ui.Art(s.Model.Id, "front-full"), Stretch = Stretch.Uniform, MaxHeight = 420 };
        var facts = Ui.Column(
            Ui.Label(s.Model.Name, 22, null, FontWeight.SemiBold),
            Ui.Label(s.Model.Support, 12, Ui.Accent, FontWeight.SemiBold),
            Ui.Label(s.Model.Summary, 13, Ui.Quiet),
            Ui.Label("Draft", 12, Ui.Quiet),
            Ui.Label(s.Buttons.UnsentChanges + " button changes · " + s.Curves.ModifiedChannels + " curves · " + s.Macros.ModifiedSlots + " macros", 14));
        var grid = new Grid { ColumnDefinitions = new("*,Auto") };
        grid.Children.Add(Ui.Panel(art, 10));
        var side = Ui.Panel(facts); side.Margin = new Thickness(14, 0, 0, 0); side.Width = 340; Grid.SetColumn(side, 1); grid.Children.Add(side);
        Content = grid;
    }
}

/// <summary>Motor strength, as a preview: nothing is written to the controller.</summary>
internal sealed class VibrationPage : UserControl
{
    public VibrationPage(StudioState s)
    {
        var value = Ui.Label(s.VibrationPreview + "%", 40, null, FontWeight.SemiBold);
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = s.VibrationPreview, Width = 420, HorizontalAlignment = HorizontalAlignment.Left };
        slider.PropertyChanged += (_, e) => { if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty) { s.VibrationPreview = (int)Math.Round(slider.Value); value.Text = s.VibrationPreview + "%"; } };
        var note = s.Model.Id is "x20_pro" or "dune" ? "This pad has trigger motors as well as two grip motors." : "This pad has two grip motors.";
        Content = Ui.Panel(Ui.Column(Ui.Label("Vibration", 18, null, FontWeight.SemiBold), value, slider, Ui.Label(note, 13, Ui.Quiet), Ui.Label("A preview of your setting, not a reading from the motors.", 12, Ui.Quiet)));
    }
}

/// <summary>What the app knows about the connected controller. Nothing connects on Linux yet, so this says so.</summary>
internal sealed class DevicePage : UserControl
{
    public DevicePage(StudioState s)
    {
        var o = new DeviceOverview(s.Model.Id, 1);
        Control Line(string k, string v) { var g = new Grid { ColumnDefinitions = new("200,*") }; g.Children.Add(Ui.Label(k, 13, Ui.Quiet)); var t = Ui.Label(v, 14); Grid.SetColumn(t, 1); g.Children.Add(t); return g; }
        Content = Ui.Panel(Ui.Column(Ui.Label("Device", 18, null, FontWeight.SemiBold),
            Line("Gameplay", o.GameplayStatus), Line("Configuration", o.ConfigurationStatus),
            Line("Battery", o.BatteryPercent is int pct ? pct + "%" : "Not reported"),
            Line("Firmware", o.Firmware ?? "Not reported"), Line("Transport", o.Transport ?? "Not reported")));
    }
}

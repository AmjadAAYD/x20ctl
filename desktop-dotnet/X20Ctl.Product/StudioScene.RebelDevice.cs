using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product;

/// <summary>The Rebel Device page (owner board device.png): a case file on the pad. A clipped photo and the model's name on torn paper,
/// the facts typed up on the right. Every value is the Device workbench's own.</summary>
public partial class StudioScene
{
    private Grid? rebelDevice; private bool rebelDeviceWired;

    private void RebelDevicePage()
    {
        var w = Device; if (w == null) return;
        if (!rebelDeviceWired) { rebelDeviceWired = true; w.Watch(() => { if (rebelPage == "device") RebelLater(); }); }
        var g = rebelDevice!; string? focused = (Keyboard.FocusedElement as FrameworkElement) is { Tag: string t } fe && g.IsAncestorOf(fe) ? t : null;
        g.Children.Clear();
        string shortName = Model.Name.Replace("EasySMX ", ""), finish = RebelFinish;

        // the case file: photo, name, stamp
        var file = new Grid { Width = 610, Height = 360, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-1.4) };
        file.Children.Add(new TornPaper { Seed = 14 });
        var photo = new Grid { Width = 270, Height = 250, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-5), Effect = Ink.Shadow(6, 10, 16, Colors.Black, .35) };
        photo.Children.Add(new Border { Background = Ink.B(Ink.White) });
        var frame = new Grid { Width = 246, Height = 180, Margin = new(12, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, ClipToBounds = true, Background = Ink.B(Ink.Black) };
        frame.Children.Add(Dots.Field(0, 0, 246, 180, Ink.A(Ink.Red, .45), 9, 1.9));
        var pad = Pieces.Pad(Model.Id, PadColours.For(Player, Model.Id), 262); RPut(pad, -8, 4); frame.Children.Add(pad);
        photo.Children.Add(frame);
        var cap = Ink.Text(finish.Length > 0 ? $"{shortName} · {finish}" : shortName, 19, Ink.B(Ink.Black)); cap.Margin = new(0, 204, 0, 0); cap.HorizontalAlignment = HorizontalAlignment.Center; cap.VerticalAlignment = VerticalAlignment.Top; photo.Children.Add(cap);
        RPut(photo, 24, 30); file.Children.Add(photo);
        var info = new StackPanel { Width = 250 }; info.Children.Add(Ink.Label($"Player {Player + 1}", 13, Ink.B(Ink.Red)));
        info.Children.Add(Ink.Text(Model.Name, 30, Ink.B(Ink.Black)).Also(t => t.Margin = new(0, 6, 0, 0)));
        var assigned = Ink.Text("ASSIGNED CONTROLLER", 12, Ink.B(Ink.Black), false, FontWeights.Bold);
        info.Children.Add(new Border { Margin = new(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Padding = new(12, 5, 12, 5), BorderBrush = Ink.B(Ink.Black), BorderThickness = new(1.6), RenderTransform = Ink.Slant, Child = assigned });
        RPut(info, 330, 44); file.Children.Add(info);
        var stamp = new Canvas { Width = 170, Height = 170, Opacity = .85, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-14), IsHitTestVisible = false };
        var dark = Ink.C("#c40f27");
        stamp.Children.Add(new Ellipse { Width = 148, Height = 148, Stroke = Ink.B(dark), StrokeThickness = 6, StrokeDashArray = new(new[] { 10d, .83, 5, .67 }) }.Also(e => { Canvas.SetLeft(e, 11); Canvas.SetTop(e, 11); }));
        stamp.Children.Add(new Ellipse { Width = 120, Height = 120, Stroke = Ink.B(dark), StrokeThickness = 2 }.Also(e => { Canvas.SetLeft(e, 25); Canvas.SetTop(e, 25); }));
        stamp.Children.Add(Star.Make(85, 85, 34, 0, 1, dark, Ink.Paper));
        RPut(stamp, 390, 150); file.Children.Add(stamp);
        // the paper clip
        var clip = new Canvas { Width = 40, Height = 80, IsHitTestVisible = false, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(8) };
        var wire = Ink.B(Ink.C("#bdb6b8"));
        clip.Children.Add(new Path { Data = Geometry.Parse("M12,52 L12,12 A9,9 0 0 1 30,12 L30,60 A12,12 0 0 1 6,60 L6,20"), Stroke = wire, StrokeThickness = 3.4, StrokeLineJoin = PenLineJoin.Round, Effect = Ink.Shadow(2, 3, 3, Colors.Black, .5) });
        RPut(clip, 138, -18); file.Children.Add(clip);
        g.Children.Add(Place(file, 262, 168));

        // what the page says, and the way on
        var below = new StackPanel { Width = 580 };
        below.Children.Add(Ink.Text("Controller input and configuration connection are checked separately.", 16, Ink.B(Ink.C("#cfc7c9")), false).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.RenderTransform = Ink.Slant; t.RenderTransformOrigin = new(0, .5); }));
        below.Children.Add(RebelPrimary("Open Diagnostics", w.OpenDiagnostics, "dev:diag", false).Also(b => { b.Margin = new(0, 18, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Left; }));
        below.Children.Add(Ink.Text(ModelProfiles.For(Model.Id).SupportsLocalDrafts ? "Local drafts available. Native configuration writes remain locked." : "Layout and artwork available. Native configuration is unavailable for this model.", 13, Ink.B(Ink.C("#9d9598")), false)
            .Also(t => { t.Margin = new(0, 14, 0, 0); t.TextWrapping = TextWrapping.Wrap; t.RenderTransform = Ink.Slant; t.RenderTransformOrigin = new(0, .5); }));
        g.Children.Add(Place(below, 296, 584));

        // the facts
        var rows = new (string label, string value, string tag)[] { ("Connection", w.ConnectionText, "dev:conn"), ("Battery", w.BatteryText, "dev:battery"), ("Firmware", w.FirmwareText, "dev:firmware"), ("Hardware revision", w.RevisionText, "dev:revision"), ("Transport", w.TransportText, "dev:transport") };
        var list = new StackPanel { Width = 600 }; list.Children.Add(Ink.Label("Device", 12, Ink.B(Ink.Red)).Also(l => l.Margin = new(6, 0, 0, 8)));
        for (int i = 0; i < rows.Length; i++)
        {
            var (label, value, tag) = rows[i]; var row = new RowSlab(i, 42) { Margin = new(i * 4, 0, 0, 7), Tag = tag };
            row.Left.Children.Add(row.Tint(Ink.Text(label, 17), Ink.White, Ink.Black));
            var v = row.Tint(Ink.Text(value, 15, null, false, FontWeights.SemiBold), Ink.C("#c9c1c3"), Ink.Black); v.RenderTransform = Ink.Slant; row.Right.Children.Add(v);
            row.GotKeyboardFocus += (_, _) => row.On = true; row.LostKeyboardFocus += (_, _) => row.On = false;
            if (tag == "dev:battery" && w.CanRefreshBattery) { row.Click += (_, _) => w.RefreshBattery(); row.ToolTip = "Click to refresh battery and firmware over Bluetooth"; }
            list.Children.Add(row);
        }
        g.Children.Add(Place(list, 930, 176));
        var profile = ModelProfiles.For(Model.Id);
        var caps = new (string label, string value)[] { ("Sticks", profile.Sticks), ("Triggers", profile.Triggers), ("Rear controls", profile.Paddles.Length == 0 ? "None listed" : string.Join(" · ", profile.Paddles)), ("Motors", profile.Motors), ("Motion", profile.Motion) };
        var capList = new StackPanel { Width = 600 }; capList.Children.Add(Ink.Label("Model capabilities", 12, Ink.B(Ink.Red)).Also(l => l.Margin = new(6, 0, 0, 8)));
        for (int i = 0; i < caps.Length; i++)
        {
            var row = new Border { Height = 42, Margin = new(i * 4, 0, 0, 7), Background = Ink.B(Ink.Panel) };
            var inner = new Grid { Margin = new(26, 0, 20, 0) };
            inner.Children.Add(Ink.Text(caps[i].label, 15, Ink.B(Ink.C("#a39b9e")), false).Also(t => { t.RenderTransform = Ink.Slant; t.VerticalAlignment = VerticalAlignment.Center; }));
            inner.Children.Add(Ink.Text(caps[i].value, 16, Ink.B(Ink.White)).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Right; t.VerticalAlignment = VerticalAlignment.Center; }));
            row.Child = inner; capList.Children.Add(row);
        }
        g.Children.Add(Place(capList, 930, 490));

        var note = new StackPanel { Orientation = Orientation.Horizontal }; note.Children.Add(new Canvas { Width = 22, Height = 22, Children = { Star.Make(11, 11, 8, 0, 1) } });
        note.Children.Add(Ink.Text("Assigned model · Gameplay and configuration remain separate", 14, Ink.B(Ink.C("#9d9598")), false).Also(t => { t.Margin = new(6, 0, 0, 0); t.RenderTransform = Ink.Slant; }));
        g.Children.Add(Place(note, 262, 842));

        string want = focused ?? "dev:conn";
        Dispatcher.BeginInvoke(() => FindTagged(g, want)?.Focus(), DispatcherPriority.Input);
    }
}

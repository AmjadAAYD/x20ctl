using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private CurveChannel? hybridCurveChannel;
    private Polyline? hybridCurveLine;
    private bool hybridCurveWired;
    private void HybridCurvesPage()
    {
        if (hybridCurves == null) return;
        if (!hybridCurveWired)
        {
            hybridCurveWired = true;
            Curves.Changed += HybridCurveGraph;
            Curves.ChannelSelected += _ => { hybridCurveChannel = null; HybridLater(); };
        }
        if (hybridCurveChannel == Curves.Channel && hybridCurves.Children.Count > 0) { HybridCurveGraph(); return; }
        hybridCurveChannel = Curves.Channel; hybridCurves.Children.Clear();
        var board = new Canvas { Width = 1600, Height = 848, ClipToBounds = true, Background = HBrush("#17151c") }; hybridCurves.Children.Add(board);
        HAt(board, new Rectangle { Width = 156, Height = 848, Fill = HBrush("#cbb5e4") }, 0, 0);
        HAt(board, new Rectangle { Width = 140, Height = 848, Fill = HBrush("#29262f") }, 1460, 0);
        HAt(board, new Rectangle { Width = 3, Height = 640, Fill = HBrush("#52dcff") }, 846, 108);
        HAt(board, HText("Curves", 38, "#f5f1fa", FontWeights.ExtraBold), 216, 14);
        HAt(board, HText(Draft.PcMode ? "LOCAL DRAFT · Applies on this PC." : "LOCAL DRAFT · Not written to controller memory.", 14, "#bdb4c8", FontWeights.SemiBold), 218, 64);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++) { var (label, key) = pages[i]; var nav = HybridButton(label, "hybrid:nav:" + key, key == "curves", 120, 50); nav.Click += (_, _) => HybridGo(key); HAt(board, nav, 18, 88 + i * 67); }
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var channel in Enum.GetValues<CurveChannel>())
        {
            var tab = HybridButton(CurveWorkbench.Name(channel), "hybrid:curve:channel:" + channel, channel == Curves.Channel, 155, 38); tab.Margin = new(0, 0, 5, 0); tab.Click += (_, _) => Curves.SelectChannel(channel); tabs.Children.Add(tab);
        }
        HAt(board, tabs, 770, 5);
        HAt(board, HText(CurveWorkbench.Name(Curves.Channel), 24, "#f5f1fa", FontWeights.Bold), 222, 108);
        var graph = new Canvas { Width = 300, Height = 200, Background = HBrush("#201d25"), IsHitTestVisible = false };
        for (int i = 1; i < 10; i++) { graph.Children.Add(new Line { X1 = i * 30, X2 = i * 30, Y1 = 0, Y2 = 200, Stroke = HBrush("#39333f") }); graph.Children.Add(new Line { X1 = 0, X2 = 300, Y1 = i * 20, Y2 = i * 20, Stroke = HBrush("#39333f") }); }
        hybridCurveLine = new Polyline { Stroke = HBrush("#d5bff6"), StrokeThickness = 4 }; graph.Children.Add(hybridCurveLine); HAt(board, graph, 340, 152); HybridCurveGraph();
        var rows = new StackPanel { Width = 560 };
        foreach (var (label, source) in RebelCurveRows)
        {
            var row = new Grid { Height = 58, Background = HBrush("#25212c"), Margin = new(0, 0, 0, 8) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            var title = HText(label, 16, "#f5f1fa", FontWeights.SemiBold); title.VerticalAlignment = VerticalAlignment.Center; title.Margin = new(15, 0, 0, 0); row.Children.Add(title);
            var slider = new Slider { Minimum = source.Minimum, Maximum = source.Maximum, TickFrequency = (source as Slider)?.TickFrequency ?? 1, IsSnapToTickEnabled = true, IsEnabled = Curves.Draft.CanEdit, Tag = "hybrid:curve:" + label, VerticalAlignment = VerticalAlignment.Center, Margin = new(8, 0, 8, 0) };
            slider.SetBinding(RangeBase.ValueProperty, new Binding("Value") { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); Grid.SetColumn(slider, 1); row.Children.Add(slider);
            var value = HText("", 15, "#ddd4e6", FontWeights.SemiBold); value.VerticalAlignment = VerticalAlignment.Center; value.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = source, StringFormat = "{0:0}%" }); Grid.SetColumn(value, 2); row.Children.Add(value); rows.Children.Add(row);
        }
        HAt(board, rows, 222, 390);
        var other = Curves.Channel switch { CurveChannel.LeftStick => CurveChannel.RightStick, CurveChannel.RightStick => CurveChannel.LeftStick, CurveChannel.LeftTrigger => CurveChannel.RightTrigger, _ => CurveChannel.LeftTrigger };
        HAt(board, HText(CurveWorkbench.Name(other), 24, "#a9a1b4", FontWeights.Bold), 898, 108);
        var otherButton = HybridButton("Edit " + CurveWorkbench.Name(other), "hybrid:curve:other", false, 250, 44); otherButton.Click += (_, _) => Curves.SelectChannel(other); HAt(board, otherButton, 898, 158);
        var preset = HybridButton("Next response preset", "hybrid:curve:preset", false, 250, 44); preset.IsEnabled = Curves.Draft.CanEdit; preset.Click += (_, _) => { string current = Curves.Draft[Curves.Channel].Preset; int index = Array.IndexOf(RebelPresets, current); Curves.SetPreset(RebelPresets[(index + 1 + RebelPresets.Length) % RebelPresets.Length]); }; HAt(board, preset, 898, 225);
        var reset = HybridButton("Reset this curve", "hybrid:curve:reset", false, 250, 44); reset.IsEnabled = Curves.Draft.CanEdit; reset.Click += (_, _) => Curves.Reset(); HAt(board, reset, 898, 281);
        var advanced = HybridButton("Advanced", "hybrid:curve:advanced", false, 250, 44); advanced.Click += (_, _) => OpenStickTools(); HAt(board, advanced, 898, 337);
        HAt(board, HText("Use the sliders to adjust the selected input.", 15, "#b9afc4", FontWeights.Normal), 898, 405);
    }
    private void HybridCurveGraph()
    {
        if (hybridCurveLine == null || !IsCurves) return;
        hybridCurveLine.Points = new PointCollection(Enumerable.Range(0, 101).Select(i => new Point(i * 3, 200 - 200 * Math.Clamp(Curves.Output(i / 100d), 0, 1))));
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private VibrationWorkbench? hybridVibrationWorkbench;
    private void HybridVibrationPage()
    {
        var vibration = Vibration; if (hybridVibration == null || vibration == null) return;
        if (ReferenceEquals(hybridVibrationWorkbench, vibration) && hybridVibration.Children.Count > 0) return;
        hybridVibrationWorkbench = vibration; hybridVibration.Children.Clear();
        var board = new Canvas { Width = 1600, Height = 848, Background = HBrush("#17151c"), ClipToBounds = true }; hybridVibration.Children.Add(board);
        HAt(board, new Rectangle { Width = 156, Height = 848, Fill = HBrush("#cbb5e4") }, 0, 0);
        HAt(board, new Rectangle { Width = 140, Height = 848, Fill = HBrush("#29262f") }, 1460, 0);
        HAt(board, HText("Vibration", 38, "#f5f1fa", FontWeights.ExtraBold), 216, 14);
        HAt(board, HText(vibration.Draft.CanEdit ? "Linked grip strength · Local draft" : "Visual preview only · Native settings unavailable", 14, "#bdb4c8", FontWeights.SemiBold), 218, 64);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++) { var (label, key) = pages[i]; var nav = HybridButton(label, "hybrid:nav:" + key, key == "vibration", 120, 50); nav.Click += (_, _) => HybridGo(key); HAt(board, nav, 18, 88 + i * 67); }
        HAt(board, new Rectangle { Width = 4, Height = 470, Fill = HBrush("#52dcff"), Opacity = .8 }, 518, 98);
        HAt(board, HText("LINKED GRIPS", 13, "#9feaff", FontWeights.Bold), 554, 110);
        if (vibration.Visualizer.Parent is Viewbox old) old.Child = null;
        else if (vibration.Visualizer.Parent is Panel panel) panel.Children.Remove(vibration.Visualizer);
        var visual = new Viewbox { Width = 560, Height = 420, Stretch = Stretch.Uniform, Child = vibration.Visualizer, IsHitTestVisible = false }; HAt(board, visual, 240, 158);
        HAt(board, HText("One strength is linked across the grips.", 14, "#bdb4c8", FontWeights.Normal), 280, 590);
        HAt(board, HText("VIBRATION PREVIEW", 14, "#bdb4c8", FontWeights.Bold), 900, 118);
        var value = HText("", 100, "#f5f1fa", FontWeights.ExtraBold); value.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = vibration.Strength, StringFormat = "{0:0}%" }); HAt(board, value, 900, 146);
        var slider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 484, IsEnabled = vibration.Draft.CanEdit, Tag = "hybrid:vibration:strength" };
        slider.SetBinding(RangeBase.ValueProperty, new Binding("Value") { Source = vibration.Strength, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); HAt(board, slider, 900, 310);
        var presets = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (int amount in new[] { 0, 25, 50, 75, 100 }) { var b = HybridButton(amount == 0 ? "Off" : amount + "%", "hybrid:vibration:" + amount, false, 84, 38); b.IsEnabled = vibration.Draft.CanEdit; b.Margin = new(0, 0, 10, 0); b.Click += (_, _) => vibration.Set(amount); presets.Children.Add(b); } HAt(board, presets, 900, 355);
        HAt(board, HText("Preview region", 17, "#f5f1fa", FontWeights.SemiBold), 900, 430);
        var region = new ComboBox { Width = 220, Height = 40, ItemsSource = new[] { "All", "Both grips", "Left grip", "Right grip" }, SelectedItem = vibration.PreviewRegion, Tag = "hybrid:vibration:region" };
        region.SelectionChanged += (_, _) => { if (region.SelectedItem is string selected) vibration.SelectRegion(selected); }; HAt(board, region, 1140, 420);
        var test = HybridButton("Test on controller", "hybrid:vibration:test", false, 240, 42); test.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = vibration.Test }); test.Click += (_, _) => vibration.Test.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); HAt(board, test, 900, 490);
        var status = HText("", 14, "#bdb4c8", FontWeights.Normal); status.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = vibration.Readback }); HAt(board, status, 900, 548);
        HAt(board, HText("Preview animation · No motor telemetry is generated.", 13, "#bdb4c8", FontWeights.Normal), 900, 585);
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Zone.Mouse;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private MouseWorkbench? hybridMouseSource;
    private string hybridMouseTab = "assign", hybridMouseGroup = "";
    private void HybridMousePage()
    {
        var m = Mouse; if (hybridMouse == null || m == null) return;
        if (ReferenceEquals(hybridMouseSource, m) && hybridMouse.Children.Count > 0) return;
        hybridMouseSource = m; hybridMouse.Children.Clear();
        var board = new Canvas { Width = 1600, Height = 848, Background = HBrush("#17151c"), ClipToBounds = true }; hybridMouse.Children.Add(board);
        HAt(board, new Rectangle { Width = 156, Height = 848, Fill = HBrush("#cbb5e4") }, 0, 0); HAt(board, new Rectangle { Width = 140, Height = 848, Fill = HBrush("#29262f") }, 1460, 0); HAt(board, new Rectangle { Width = 3, Height = 684, Fill = HBrush("#52dcff") }, 846, 94);
        HAt(board, HText("Controller as mouse", 38, "#f5f1fa", FontWeights.ExtraBold), 216, 14); HAt(board, HText("Saved on this PC · Changes apply immediately.", 14, "#bdb4c8", FontWeights.SemiBold), 218, 64);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++) { var (label, key) = pages[i]; var nav = HybridButton(label, "hybrid:nav:" + key, key == "mouse", 120, 50); nav.Click += (_, _) => HybridGo(key); HAt(board, nav, 18, 88 + i * 67); }
        var enabled = new CheckBox { Content = "Controller as mouse", Foreground = HBrush("#f5f1fa"), FontSize = 17, FontWeight = FontWeights.SemiBold, Tag = "hybrid:mouse:switch" };
        enabled.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsChecked") { Source = m.Switch, Mode = BindingMode.TwoWay }); enabled.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = m.Switch }); HAt(board, enabled, 216, 102);
        HAt(board, HText("Desktop input source", 16, "#f5f1fa", FontWeights.SemiBold), 216, 155); HAt(board, HybridCombo(m.SourceBox, "hybrid:mouse:source", 260), 504, 145);
        HAt(board, HText("BUTTON MAPPINGS · " + m.MappedText, 13, "#bdb4c8", FontWeights.Bold), 216, 205);
        var mapping = new WrapPanel { Width = 588, ItemWidth = 294, ItemHeight = 48 };
        foreach (string key in m.Inputs)
        {
            var row = HybridButton(m.LabelFor(key) + "  ·  " + m.ActionFor(key), "hybrid:mouse:input:" + key, key == m.SelectedKey, 282, 42); row.HorizontalContentAlignment = HorizontalAlignment.Left; row.Padding = new(12, 0, 0, 0);
            row.Click += (_, _) => { m.Select(key); hybridMouse.Children.Clear(); HybridMousePage(); }; mapping.Children.Add(row);
        }
        var mappingScroll = new ScrollViewer { Content = mapping, Width = 588, Height = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; HAt(board, mappingScroll, 216, 235);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (label, key) in new[] { ("Assign", "assign"), ("Pointer", "pointer"), ("Scroll", "scroll") }) { var tab = HybridButton(label, "hybrid:mouse:tab:" + key, hybridMouseTab == key, 120, 38); tab.Margin = new(0, 0, 6, 0); tab.Click += (_, _) => { hybridMouseTab = key; hybridMouse.Children.Clear(); HybridMousePage(); }; tabs.Children.Add(tab); } HAt(board, tabs, 892, 95);
        var detail = new StackPanel { Width = 492 }; var scroll = new ScrollViewer { Width = 492, Height = 620, Content = detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; HAt(board, scroll, 892, 145);
        void Caption(string text) { var label = HText(text, 16, "#f5f1fa", FontWeights.SemiBold); label.Margin = new(0, 12, 0, 6); detail.Children.Add(label); }
        void SliderRow(string name, Slider source) { Caption(name); var slider = new Slider { Minimum = source.Minimum, Maximum = source.Maximum, TickFrequency = source.TickFrequency, IsSnapToTickEnabled = source.IsSnapToTickEnabled, Tag = "hybrid:mouse:slider:" + name }; slider.SetBinding(RangeBase.ValueProperty, new Binding("Value") { Source = source, Mode = BindingMode.TwoWay }); slider.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = source }); detail.Children.Add(slider); }
        void Check(string name, CheckBox source) { var check = new CheckBox { Content = name, Foreground = HBrush("#f5f1fa"), FontSize = 15, Margin = new(0, 10, 0, 0) }; check.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsChecked") { Source = source, Mode = BindingMode.TwoWay }); check.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = source }); detail.Children.Add(check); }
        void Choice(string name, ComboBox source) { Caption(name); detail.Children.Add(HybridCombo(source, "hybrid:mouse:choice:" + name, 360)); }
        if (hybridMouseTab == "assign")
        {
            detail.Children.Add(HText("Assign input · " + m.LabelFor(m.SelectedKey), 23, "#f5f1fa", FontWeights.Bold));
            if (!m.Groups.Any(g => g.Group == hybridMouseGroup)) hybridMouseGroup = m.Groups.FirstOrDefault(g => g.Actions.Contains(m.CurrentAction)).Group ?? "MOUSE";
            foreach (var (group, actions) in m.Groups)
            {
                var heading = HybridButton(group, "hybrid:mouse:group:" + group, group == hybridMouseGroup, 450, 35); heading.Margin = new(0, 6, 0, 0); heading.Click += (_, _) => { hybridMouseGroup = group; hybridMouse.Children.Clear(); HybridMousePage(); }; detail.Children.Add(heading);
                if (group != hybridMouseGroup) continue;
                foreach (var action in actions) { var choice = HybridButton(MouseWorkbench.Friendly(action), "hybrid:mouse:action:" + (int)action, action == m.CurrentAction, 450, 35); choice.Margin = new(0, 3, 0, 0); choice.Click += (_, _) => { m.Assign(action); hybridMouse.Children.Clear(); HybridMousePage(); }; detail.Children.Add(choice); }
            }
            foreach (string name in MouseWorkbench.PresetNames) { var preset = HybridButton(name, "hybrid:mouse:preset:" + name, false, 450, 35); preset.Margin = new(0, 4, 0, 0); preset.Click += (_, _) => { m.ApplyPreset(name); hybridMouse.Children.Clear(); HybridMousePage(); }; detail.Children.Add(preset); }
            var reset = HybridButton("Reset Mouse Mode", "hybrid:mouse:reset", false, 450, 35); reset.Click += (_, _) => { m.ResetMode(); hybridMouse.Children.Clear(); HybridMousePage(); }; detail.Children.Add(reset);
        }
        else if (hybridMouseTab == "pointer")
        {
            detail.Children.Add(HText("Pointer", 23, "#f5f1fa", FontWeights.Bold)); Choice("Pointer source", m.PointerBox); SliderRow("Speed", m.SpeedSlider); SliderRow("Acceleration", m.CurveSlider); SliderRow("Dead zone", m.DeadzoneSlider);
            var test = HybridButton(m.Testing ? "Stop pointer test" : "Test pointer", "hybrid:mouse:test", false, 250, 40); test.Click += (_, _) => { m.TestPointer(); test.Content = m.Testing ? "Stop pointer test" : "Test pointer"; }; detail.Children.Add(test);
        }
        else
        {
            detail.Children.Add(HText("Scroll and precision", 23, "#f5f1fa", FontWeights.Bold)); Check("Scrolling", m.ScrollsBox); SliderRow("Scroll speed", m.ScrollSlider); SliderRow("Precision speed", m.PrecisionSlider); Check("Horizontal scroll", m.HorizontalBox); Check("Invert vertical", m.InvertVBox); Check("Invert horizontal", m.InvertHBox); Check("Quick toggle", m.ComboToggle); Choice("Shortcut first button", m.Chord1Box); Choice("Shortcut second button", m.Chord2Box); Choice("Hold for", m.HoldBox);
        }
    }
    private static ComboBox HybridCombo(ComboBox source, string tag, double width)
    {
        var combo = new ComboBox { ItemsSource = source.Items, DisplayMemberPath = source.DisplayMemberPath, SelectedValuePath = source.SelectedValuePath, Width = width, Height = 40, Tag = tag, HorizontalAlignment = HorizontalAlignment.Left };
        combo.SetBinding(Selector.SelectedIndexProperty, new Binding("SelectedIndex") { Source = source, Mode = BindingMode.TwoWay }); combo.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = source }); return combo;
    }
}

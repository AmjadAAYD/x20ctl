using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hybrid;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public sealed partial class StickToolsPanel
{
    private void BuildHybridAdvanced(Action close)
    {
        if (Child is not FrameworkElement original) return;
        Child = null; CornerRadius = new(0); BorderThickness = new(0); Padding = new(0); Width = 1600; Height = 900; MaxWidth = double.PositiveInfinity; Background = Brushes.Transparent;
        var board = HybridToolFrame.Create("HybridAdvanced", "Advanced stick tools", "PC-side processing for accessible analog sticks. Settings save locally; nothing is written to the controller.", close, key => RebelNavigate?.Invoke(key), "curves");
        foreach (var (_, row) in rows)
        {
            System.Windows.Automation.AutomationProperties.SetName(row.Inner, "Inner dead zone");
            System.Windows.Automation.AutomationProperties.SetName(row.Outer, "Full output at");
            System.Windows.Automation.AutomationProperties.SetName(row.Anti, "Minimum output");
            System.Windows.Automation.AutomationProperties.SetName(row.Max, "Maximum output");
            System.Windows.Automation.AutomationProperties.SetName(row.Raw, "Bypass X20CTL processing");
            System.Windows.Automation.AutomationProperties.SetName(row.Square, "Square gate");
            System.Windows.Automation.AutomationProperties.SetName(row.Ratio, "Vertical sensitivity");
        }
        var settings = new StackPanel { Width = 588, Tag = "hybrid:advanced:settings" };
        HybridToolFrame.At(board, settings, 216, 150);
        HybridToolFrame.At(board, new Rectangle { Width = 2, Height = 670, Fill = HybridToolFrame.Color("#52dcff") }, 846, 150);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        HybridToolFrame.At(board, tabs, 1100, 45);
        var graph = new Viewbox { Width = 486, Height = 220, Stretch = Stretch.Uniform, Tag = "hybrid:advanced:graph" };
        HybridToolFrame.At(board, graph, 898, 160);
        original.Width = 486; original.Height = 420;
        var details = new ScrollViewer { Width = 486, Height = 420, Content = original, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Tag = "hybrid:advanced:details" };
        HybridToolFrame.At(board, details, 898, 400);
        Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        Populate();

        void Populate()
        {
            settings.Children.Clear();
            tabs.Children.Clear();
            foreach (var (name, channel) in Sticks)
            {
                var button = HybridToolFrame.Button(name, "hybrid:advanced:" + channel, 138, 42, channel == selectedStick);
                tabs.Children.Add(button);
                button.Click += (_, _) =>
                {
                    SelectStick(channel); Populate();
                    tabs.Children.OfType<Button>().FirstOrDefault(b => b.Tag as string == "hybrid:advanced:" + channel)?.Focus();
                };
            }
            graph.Child = null;
            var selectedGraph = charts[selectedStick]; HybridToolFrame.Detach(selectedGraph); graph.Child = selectedGraph;
            var row = rows[selectedStick];
            AddSlider("Inner dead zone", row.Inner, "Stops small movements around the centre.");
            AddSlider("Full output at", row.Outer, "Set where the stick reaches full output.");
            AddSlider("Minimum output", row.Anti, "Helps small movements register in games.");
            AddSlider("Maximum output", row.Max, "Limit the output at full stick movement.");
            AddSlider("Vertical sensitivity", row.Ratio, "Adjust vertical movement relative to horizontal.");
            AddToggle("Square gate", row.Square);
            AddToggle("Bypass X20CTL processing", row.Raw);
            void AddSlider(string title, Slider source, string hint)
            {
                var panel = Card();
                var label = HybridToolFrame.Text(title, 16, "#f4f1f8", FontWeights.Bold); panel.Children.Add(label);
                var description = HybridToolFrame.Text(hint, 12, "#bdb4c8", FontWeights.Normal); panel.Children.Add(description);
                var controls = new DockPanel { Margin = new(0, 4, 0, 0) };
                var value = HybridToolFrame.Text("", 14, "#d8c6ed", FontWeights.Bold); value.Width = 64; DockPanel.SetDock(value, Dock.Right); controls.Children.Add(value);
                value.SetBinding(TextBlock.TextProperty, new Binding(nameof(Slider.Value)) { Source = source, StringFormat = "{0:0.0}%" });
                var slider = new Slider { Minimum = source.Minimum, Maximum = source.Maximum, TickFrequency = source.TickFrequency, IsSnapToTickEnabled = source.IsSnapToTickEnabled, SmallChange = source.SmallChange, LargeChange = source.LargeChange, Tag = "hybrid:advanced:value:" + title };
                slider.SetBinding(Slider.ValueProperty, new Binding(nameof(Slider.Value)) { Source = source, Mode = BindingMode.TwoWay });
                slider.SetBinding(IsEnabledProperty, new Binding(nameof(IsEnabled)) { Source = row.Tuning });
                controls.Children.Add(slider); panel.Children.Add(controls); settings.Children.Add(panel);
            }
            void AddToggle(string title, CheckBox source)
            {
                var panel = Card(); var toggle = new CheckBox { Content = title, Foreground = HybridToolFrame.Color("#f4f1f8"), FontSize = 15, Tag = "hybrid:advanced:toggle:" + title };
                toggle.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(CheckBox.IsChecked)) { Source = source, Mode = BindingMode.TwoWay });
                if (!ReferenceEquals(source, row.Raw)) toggle.SetBinding(IsEnabledProperty, new Binding(nameof(IsEnabled)) { Source = row.Tuning });
                panel.Children.Add(toggle); settings.Children.Add(panel);
            }
            StackPanel Card()
            {
                var panel = new StackPanel { Margin = new(0, 0, 0, 8) };
                return panel;
            }
        }
    }
}


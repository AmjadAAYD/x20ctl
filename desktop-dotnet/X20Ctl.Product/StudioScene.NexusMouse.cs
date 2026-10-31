using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NexusShowMouse()
    {
        if (Mouse is not { } mouse) return;
        nexusPageHost!.Children.Clear();

        // 1. Background gradient
        var bg = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0.5, 0), EndPoint = new(0.5, 1),
                GradientStops = { new(NexusKit.C("#1a1c20"), 0.0), new(NexusKit.House, 1.0) }
            }
        };
        nexusPageHost.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: Player,
            model: Model.Name,
            status: "Offline",
            onZone: () => BackRequested?.Invoke(),
            onSetups: () => ShowManagement("Setups"),
            onPcMode: () => ShowManagement("PcMode"),
            onTools: () => ToolsRequested?.Invoke()
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusPageHost.Children.Add(topBar);

        // 3. Title & Lead
        var title = new TextBlock
        {
            Text = "Mouse",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 138);
        nexusPageHost.Children.Add(title);

        var lead = new TextBlock
        {
            Text = $"Player 1 · {Model.Name}",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        nexusPageHost.Children.Add(lead);

        // 4. Left column: the real mouse-mode settings
        var rowPanel = new StackPanel { Width = 820 };
        var enabled = new CheckBox { Tag = "theme:mouse:switch", Foreground = NexusKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center };
        enabled.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = mouse.Switch, Mode = BindingMode.TwoWay });
        rowPanel.Children.Add(NexusKit.RowButton("Controller as mouse", null, enabled, focused: false, width: 800));
        foreach (var (name, source) in new[] { ("Pointer speed", mouse.SpeedSlider), ("Response curve", mouse.CurveSlider), ("Scroll speed", mouse.ScrollSlider), ("Dead zone", mouse.DeadzoneSlider) })
        {
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var slider = ThemeSlider(source, "theme:mouse:" + name, 210, true, NexusKit.SliderTrackBrush); slider.TickFrequency = source.TickFrequency;
            var value = new TextBlock { Width = 56, FontFamily = NexusKit.Body, FontSize = 16, Foreground = NexusKit.SubBrush, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            value.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = source, StringFormat = "{0:0.##}" });
            right.Children.Add(slider); right.Children.Add(value);
            rowPanel.Children.Add(NexusKit.RowButton(name, null, right, focused: false, width: 800));
        }
        foreach (var (label, source) in new[] { ("Input source", mouse.SourceBox), ("Pointer stick", mouse.PointerBox), ("Shortcut first", mouse.Chord1Box), ("Shortcut second", mouse.Chord2Box), ("Hold behavior", mouse.HoldBox) })
        {
            var choice = new ComboBox { Width = 260, Height = 36, DisplayMemberPath = source.DisplayMemberPath, SelectedValuePath = source.SelectedValuePath, Tag = "theme:mouse:" + label };
            choice.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("ItemsSource") { Source = source });
            choice.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedIndexProperty, new Binding("SelectedIndex") { Source = source, Mode = BindingMode.TwoWay });
            rowPanel.Children.Add(NexusKit.RowButton(label, null, choice, focused: false, width: 800));
        }
        foreach (var (label, source) in new[] { ("Scroll", mouse.ScrollsBox), ("Shortcut toggle", mouse.ComboToggle), ("Horizontal scroll", mouse.HorizontalBox), ("Invert vertical", mouse.InvertVBox), ("Invert horizontal", mouse.InvertHBox) })
        {
            var toggle = new CheckBox { Tag = "theme:mouse:" + label, VerticalAlignment = VerticalAlignment.Center };
            toggle.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = source, Mode = BindingMode.TwoWay });
            rowPanel.Children.Add(NexusKit.RowButton(label, null, toggle, focused: false, width: 800));
        }
        var scroll = new ScrollViewer { Width = 836, Height = 580, Content = rowPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Canvas.SetLeft(scroll, 68);
        Canvas.SetTop(scroll, 236);
        nexusPageHost.Children.Add(scroll);

        // 5. Right side: every mappable input and the live state
        var sideStack = new StackPanel { Width = 612 };
        var card = new Border { Width = 612, Padding = new Thickness(28, 26, 28, 26), CornerRadius = new CornerRadius(8), Background = NexusKit.PanelBrush };
        var cardStack = new StackPanel();
        cardStack.Children.Add(new TextBlock { Text = "Assign control", FontFamily = NexusKit.Display, FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = NexusKit.WhiteBrush });
        cardStack.Children.Add(new TextBlock { Text = "Pick an input, then the desktop action it performs when mouse control is active.", FontFamily = NexusKit.Body, FontSize = 15, Foreground = NexusKit.SubBrush, Margin = new Thickness(0, 4, 0, 16), TextWrapping = TextWrapping.Wrap });
        var inputs = new WrapPanel();
        foreach (string key in mouse.Inputs)
        {
            string k = key;
            var action = new Button { Content = new TextBlock { Text = mouse.LabelFor(k), Foreground = NexusKit.WhiteBrush, FontFamily = NexusKit.Body, FontSize = 15 }, Width = 130, Height = 44, Margin = new Thickness(0, 0, 8, 8), Tag = "theme:mouse:input:" + k, Background = NexusKit.BtnBrush, BorderThickness = new Thickness(0) };
            action.Click += (_, _) =>
            {
                mouse.PickKey(k);
                var menu = new ContextMenu { PlacementTarget = action };
                foreach (var group in mouse.Groups)
                {
                    var section = new MenuItem { Header = group.Group };
                    foreach (var choice in group.Actions) { var item = new MenuItem { Header = choice.ToString(), Tag = "theme:mouse:assign:" + choice }; item.Click += (_, _) => mouse.Assign(choice); section.Items.Add(item); }
                    menu.Items.Add(section);
                }
                action.ContextMenu = menu; if (!ReviewSandbox.Active) menu.IsOpen = true;
            };
            inputs.Children.Add(action);
        }
        cardStack.Children.Add(inputs);
        var status = new TextBlock { FontFamily = NexusKit.Body, FontSize = 16, Foreground = NexusKit.WhiteBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
        void UpdateState() { status.Text = mouse.StatusText + " · " + mouse.MappedText + " · " + mouse.SavedText; }
        mouse.Watch(UpdateState); UpdateState();
        cardStack.Children.Add(status);
        var resetBtn = new Button { Content = new TextBlock { Text = "Reset mouse settings", Foreground = NexusKit.WhiteBrush, FontFamily = NexusKit.Body, FontSize = 16 }, Height = 48, Margin = new Thickness(0, 16, 0, 0), Tag = "theme:mouse:reset", Background = NexusKit.BtnBrush, BorderThickness = new Thickness(0) };
        resetBtn.Click += (_, _) => mouse.ResetMode();
        cardStack.Children.Add(resetBtn);
        card.Child = cardStack;
        sideStack.Children.Add(card);
        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 250);
        nexusPageHost.Children.Add(sideStack);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Select"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }
}

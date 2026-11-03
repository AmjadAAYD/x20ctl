using System.Windows.Data;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private FrameworkElement? duskMouseView;
    private void DuskShowMouse()
    {
        if (Mouse is not {} mouse) return;
        duskPageHost!.Children.Clear();
        if (duskMouseView != null) { duskPageHost.Children.Add(duskMouseView); return; }

        var hints = new[]
        {
            ("X", "DEFAULTS"),
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Left list (w: 580)
        var leftStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };

        leftStack.Children.Add(new TextBlock
        {
            Text = "MOUSE EMULATION",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        leftStack.Children.Add(new TextBlock
        {
            Text = "Map joystick deflection to high-precision desktop cursor movement.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var enabled = new CheckBox { Content = "Controller as mouse", Foreground = DuskKit.WhiteBrush, Tag = "theme:mouse:switch", Margin = new(16,8,0,18) };
        enabled.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = mouse.Switch, Mode = BindingMode.TwoWay }); leftStack.Children.Add(enabled);
        foreach (var (name, source) in new[] { ("Pointer speed", mouse.SpeedSlider), ("Response curve", mouse.CurveSlider), ("Scroll speed", mouse.ScrollSlider), ("Dead zone", mouse.DeadzoneSlider), ("Precision", mouse.PrecisionSlider) })
        {
            var row = new Grid { Height = 56 }; row.ColumnDefinitions.Add(new() { Width = new GridLength(260) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(220) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(84) });
            row.Children.Add(new TextBlock { Text = name, FontFamily = DuskKit.Display, FontSize = 20, Foreground = DuskKit.LightBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new(16,0,0,0) });
            var slider = ThemeSlider(source, "theme:mouse:" + name, 220, true); slider.TickFrequency = source.TickFrequency; Grid.SetColumn(slider,1); row.Children.Add(slider);
            var value = new TextBlock { Foreground = DuskKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right }; value.SetBinding(TextBlock.TextProperty,new Binding("Value") { Source = source, StringFormat = "{0:0.##}" }); Grid.SetColumn(value,2); row.Children.Add(value); leftStack.Children.Add(row);
        }
        foreach (var (label, source) in new[] { ("Input source", mouse.SourceBox), ("Pointer stick", mouse.PointerBox), ("Shortcut first", mouse.Chord1Box), ("Shortcut second", mouse.Chord2Box), ("Hold behavior", mouse.HoldBox) })
        {
            var row = new DockPanel { Margin = new(16,8,0,0) }; row.Children.Add(new TextBlock { Text = label, Width = 220, Foreground = DuskKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
            var choice = new ComboBox { Width = 260, Height = 36, DisplayMemberPath = source.DisplayMemberPath, SelectedValuePath = source.SelectedValuePath, Tag = "theme:mouse:" + label };
            choice.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("ItemsSource") { Source = source }); choice.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedIndexProperty, new Binding("SelectedIndex") { Source = source, Mode = BindingMode.TwoWay }); row.Children.Add(choice); leftStack.Children.Add(row);
        }
        foreach (var (label, source) in new[] { ("Scroll", mouse.ScrollsBox), ("Shortcut toggle", mouse.ComboToggle), ("Horizontal scroll", mouse.HorizontalBox), ("Invert vertical", mouse.InvertVBox), ("Invert horizontal", mouse.InvertHBox) })
        {
            var toggle = new CheckBox { Content = label, Foreground = DuskKit.LightBrush, Margin = new(16,10,0,0), Tag = "theme:mouse:" + label }; toggle.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = source, Mode = BindingMode.TwoWay }); leftStack.Children.Add(toggle);
        }
        foreach (string preset in X20Ctl.Product.MouseWorkbench.PresetNames) leftStack.Children.Add(DuskMacroAction("Preset · " + preset, "mouse:preset:" + preset, () => mouse.ApplyPreset(preset)));

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        var settingsScroll = new ScrollViewer { Width = 590, Height = 580, Content = leftStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Canvas.SetLeft(settingsScroll,0); Canvas.SetTop(settingsScroll,0); contentCanvas.Children.Add(settingsScroll);

        // Right side: 8-way directional mapping grid
        var rightStack = new StackPanel { Width = 380, Orientation = Orientation.Vertical };

        var mapCard = new Border
        {
            Width = 360,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.SlateItemBrush,
            Padding = new Thickness(22)
        };
        var mapStack = new StackPanel { Orientation = Orientation.Vertical };
        mapStack.Children.Add(new TextBlock
        {
            Text = "DIRECTIONAL MAPPING",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var grid8 = new WrapPanel();
        foreach (string key in mouse.Inputs)
        {
            var action = DuskMacroAction(mouse.LabelFor(key), "mouse:input:" + key, () => { });
            action.Click += (_, _) =>
            {
                mouse.PickKey(key); var menu = new ContextMenu { PlacementTarget = action };
                foreach (var group in mouse.Groups)
                {
                    var section = new MenuItem { Header = group.Group };
                    foreach (var choice in group.Actions) { var item = new MenuItem { Header = choice.ToString(), Tag = "theme:mouse:assign:" + choice }; item.Click += (_, _) => mouse.Assign(choice); section.Items.Add(item); } menu.Items.Add(section);
                }
                action.ContextMenu = menu; if (!ReviewSandbox.Active) menu.IsOpen = true;
            };
            grid8.Children.Add(action);
        }
        mapStack.Children.Add(grid8);
        mapCard.Child = mapStack;
        rightStack.Children.Add(mapCard);

        // Detail card
        var detailCard = new Border
        {
            Background = DuskKit.SlateItemBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            Width = 360
        };
        var dStack = new StackPanel { Orientation = Orientation.Vertical };
        dStack.Children.Add(new TextBlock
        {
            Text = "TRACKING BEHAVIOR",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        dStack.Children.Add(new TextBlock
        {
            Text = mouse.StatusText,
            FontFamily = DuskKit.Display,
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        dStack.Children.Add(new TextBlock
        {
            Text = mouse.MappedText,
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        void UpdateState() { ((TextBlock)dStack.Children[1]).Text = mouse.StatusText; ((TextBlock)dStack.Children[2]).Text = mouse.MappedText + " · " + mouse.SavedText; }
        mouse.Watch(UpdateState); UpdateState();
        dStack.Children.Add(DuskMacroAction("Reset mouse settings", "mouse:reset", mouse.ResetMode));
        detailCard.Child = dStack;
        rightStack.Children.Add(detailCard);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "mouse",
            title: "Mouse Emulation",
            lead: "Map joystick deflection to high-precision cursor movement and desktop navigation.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskMouseView = shell; duskPageHost.Children.Add(shell);
    }
}

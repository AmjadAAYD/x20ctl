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
    private PcModePage? duskPcSource;
    private FrameworkElement? duskPcView;
    private void DuskShowPcMode()
    {
        var original = ManagementBodyHost.Children.OfType<ScrollViewer>().FirstOrDefault();
        var source = original?.Content as PcModePage;
        duskPageHost!.Children.Clear();
        if (source == null && duskPcView != null) { duskPageHost.Children.Add(duskPcView); return; }
        if (source == null) return;
        duskPcSource = source; original!.Content = null;

        var hints = new[]
        {
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Left list (w: 580)
        var leftStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };

        leftStack.Children.Add(new TextBlock
        {
            Text = "PROTOCOL & ROUTING",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        leftStack.Children.Add(new TextBlock
        {
            Text = "Software processing on this PC. Hardware configuration stays unchanged.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var state = new TextBlock { Foreground = DuskKit.WhiteBrush, FontFamily = DuskKit.Display, FontSize = 22, TextWrapping = TextWrapping.Wrap, Margin = new(16,10,0,14) }; state.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = source.StatusLabel }); leftStack.Children.Add(state);
        var toggle = new CheckBox { Content = "PC mode", Foreground = DuskKit.WhiteBrush, FontFamily = DuskKit.Display, FontSize = 20, Margin = new(16,8,0,18), Tag = "theme:pc:switch" }; toggle.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = source.Switch, Mode = BindingMode.TwoWay }); leftStack.Children.Add(toggle);
        leftStack.Children.Add(new TextBlock { Text = "Settings for", Foreground = DuskKit.LightBrush, Margin = new(16,0,0,8) });
        var profile = new ComboBox { DisplayMemberPath = "Label", SelectedValuePath = "Id", Width = 520, Height = 42, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(16,0,0,18), Tag = "theme:pc:profile" };
        profile.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("ItemsSource") { Source = source.ProfileList }); profile.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new Binding("SelectedValue") { Source = source.ProfileList, Mode = BindingMode.TwoWay }); leftStack.Children.Add(profile);
        foreach (string page in new[] { "Buttons", "Curves", "Macros" }) leftStack.Children.Add(DuskMacroAction("Edit " + page, "pc:edit:" + page, () => DuskSelectPage(page)));
        var more = DuskMacroAction("More PC settings", "pc:more", () => { }); leftStack.Children.Add(more);
        var details = new ScrollViewer { Width = 590, Height = 520, Content = source, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed, Tag = "theme:pc:details" }; Canvas.SetLeft(details,0); Canvas.SetTop(details,0); contentCanvas.Children.Add(details);
        var overview = DuskMacroAction("Back to PC overview", "pc:overview", () => { }); overview.Visibility = Visibility.Collapsed; Canvas.SetLeft(overview,0); Canvas.SetTop(overview,535); contentCanvas.Children.Add(overview);
        void ShowDetails(bool show) { leftStack.Visibility = show ? Visibility.Collapsed : Visibility.Visible; details.Visibility = show ? Visibility.Visible : Visibility.Collapsed; overview.Visibility = show ? Visibility.Visible : Visibility.Collapsed; }
        more.Click += (_, _) => ShowDetails(true); overview.Click += (_, _) => { if (!source.NoticeShown) ShowDetails(false); };
        source.Watch(() => { overview.IsEnabled = !source.NoticeShown; if (source.NoticeShown) { ShowDetails(true); Dispatcher.BeginInvoke(source.RevealNotice, System.Windows.Threading.DispatcherPriority.Loaded); } });
        if (source.NoticeShown) { ShowDetails(true); Dispatcher.BeginInvoke(source.RevealNotice, System.Windows.Threading.DispatcherPriority.Loaded); }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        contentCanvas.Children.Add(leftStack);

        // Right side: Signal routing diagram + details
        var rightStack = new StackPanel { Width = 380, Orientation = Orientation.Vertical };

        var routeCard = new Border
        {
            Width = 360,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.SlateItemBrush,
            BorderBrush = DuskKit.B("rgba(26,159,255,0.35)"),
            BorderThickness = new Thickness(1, 2, 1, 1),
            Padding = new Thickness(20),
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.2 }
        };
        var routeStack = new StackPanel { Orientation = Orientation.Vertical };

        var rHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        rHead.Children.Add(new TextBlock
        {
            Text = "SIGNAL PIPELINE",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush
        });
        var pipeChip = new Border
        {
            Padding = new Thickness(8, 2, 8, 2),
            CornerRadius = new CornerRadius(3),
            Background = DuskKit.B("rgba(26,159,255,0.18)"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.4)"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(12, 0, 0, 0),
            Child = new TextBlock
            {
                Text = "SOFTWARE ROUTE",
                FontFamily = DuskKit.Display,
                FontSize = 10,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DuskKit.CyanNeonBrush
            }
        };
        rHead.Children.Add(pipeChip);
        routeStack.Children.Add(rHead);

        // Cybernetic signal nodes
        var nodes = new (string Name, string Sub, string Icon, Color DotCol, Color BorderCol)[]
        {
            ("Physical Hardware", "The controller as Windows sees it", "\uE7FC", DuskKit.GreenOnline, DuskKit.C("#3d4450")),
            ("ViGEmBus Emulation", "Virtual controller bus", "\uE777", DuskKit.CyanNeon, DuskKit.CyanNeon),
            ("Virtual XInput Pad", "DirectX / Xbox 360 Compatible", "\uE977", DuskKit.GreenOnline, DuskKit.GreenOnline)
        };

        for (int i = 0; i < nodes.Length; i++)
        {
            var (nName, nSub, nIcon, nDot, nBorder) = nodes[i];
            var nodeCard = new Border
            {
                Padding = new Thickness(14, 10, 14, 10),
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.B("#141920"),
                BorderBrush = DuskKit.B(nBorder),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { Color = nBorder, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.3 }
            };
            var nGrid = new Grid();
            nGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            nGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icBox = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.B("rgba(255,255,255,0.06)"),
                Margin = new Thickness(0, 0, 12, 0),
                Child = new TextBlock
                {
                    Text = nIcon,
                    FontFamily = DuskKit.Icons,
                    FontSize = 16,
                    Foreground = DuskKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(icBox, 0);
            nGrid.Children.Add(icBox);

            var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock { Text = nName, FontFamily = DuskKit.Display, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = DuskKit.WhiteBrush });
            textStack.Children.Add(new TextBlock { Text = nSub, FontFamily = DuskKit.Body, FontSize = 11, Foreground = DuskKit.MuteBrush, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(textStack, 1);
            nGrid.Children.Add(textStack);

            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = DuskKit.B(nDot),
                Effect = new DropShadowEffect { Color = nDot, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9 },
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dot, 2);
            nGrid.Children.Add(dot);

            nodeCard.Child = nGrid;
            routeStack.Children.Add(nodeCard);

            if (i < nodes.Length - 1)
            {
                var connector = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
                connector.Children.Add(new TextBlock
                {
                    Text = "↓  LOCAL PC PROCESSING  ↓",
                    FontFamily = DuskKit.Display,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = DuskKit.CyanNeonBrush
                });
                routeStack.Children.Add(connector);
            }
        }
        routeCard.Child = routeStack;
        rightStack.Children.Add(routeCard);

        // Driver card with lime green specular border
        var driverCard = new Border
        {
            Background = DuskKit.SlateItemBrush,
            BorderBrush = DuskKit.GreenOnlineBrush,
            BorderThickness = new Thickness(3, 1, 1, 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            Width = 360,
            Effect = new DropShadowEffect { Color = DuskKit.GreenOnline, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.2 }
        };
        var dStack = new StackPanel { Orientation = Orientation.Vertical };
        dStack.Children.Add(new TextBlock
        {
            Text = "DRIVER STATUS",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        dStack.Children.Add(new TextBlock
        {
            Text = source.StatusText,
            FontFamily = DuskKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.GreenOnlineBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        dStack.Children.Add(new TextBlock
        {
            Text = source.StatusDetail,
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        ((TextBlock)dStack.Children[1]).SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = source.StatusLabel });
        ((TextBlock)dStack.Children[2]).SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = source.StatusDetailLabel });
        driverCard.Child = dStack;
        rightStack.Children.Add(driverCard);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "pcmode",
            title: "PC Mode & Routing",
            lead: "Software remapping, response curves, macros and per-game profiles.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskPcView = shell; duskPageHost.Children.Add(shell);
    }
}

using X20Ctl.Zone;
using System.Windows.Data;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private CurveChannel? duskRenderedCurve;
    private FrameworkElement? duskCurveView;
    private void DuskShowCurves()
    {
        duskPageHost!.Children.Clear();
        if (duskRenderedCurve == Curves.Channel && duskCurveView != null) { duskPageHost.Children.Add(duskCurveView); return; }
        duskRenderedCurve = Curves.Channel;

        var hints = new[]
        {
            ("X", "RESET"),
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Subtabs across top
        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        var tabs = new[] { "LEFT STICK", "RIGHT STICK", "LT", "RT" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var channel = Enum.GetValues<CurveChannel>()[i];
            bool active = channel == Curves.Channel;
            var tab = new Border
            {
                Padding = new Thickness(18, 8, 18, 8),
                CornerRadius = new CornerRadius(18),
                Background = active ? DuskKit.SlateTabOnBrush : Brushes.Transparent,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            tab.Child = new TextBlock
            {
                Text = tabs[i],
                FontFamily = DuskKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush
            };
            tabsStack.Children.Add(ThemeAction(tab, "theme:curve:channel:" + channel, () => Curves.SelectChannel(channel)));
        }

        // Left list (w: 580)
        var leftStack = new StackPanel { Width = 720, Orientation = Orientation.Vertical };
        leftStack.Children.Add(tabsStack);

        leftStack.Children.Add(new TextBlock
        {
            Text = CurveWorkbench.Name(Curves.Channel).ToUpperInvariant(),
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        leftStack.Children.Add(new TextBlock
        {
            Text = "LOCAL DRAFT · Not written to controller memory.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        foreach (var (name, source) in RebelCurveRows)
        {
            var row = new Grid { Height = 60 };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(260) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(380) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(84) });
            row.Children.Add(new TextBlock { Text = name, FontFamily = DuskKit.Display, FontSize = 20, Foreground = DuskKit.LightBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new(16,0,0,0) });
            var slider = ThemeSlider(source, "theme:curve:" + name, 380, Curves.Draft.CanEdit); Grid.SetColumn(slider, 1); row.Children.Add(slider);
            var value = new TextBlock { FontFamily = DuskKit.Display, FontSize = 18, Foreground = DuskKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
            value.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = source, StringFormat = "{0:0}%" }); Grid.SetColumn(value, 2); row.Children.Add(value);
            leftStack.Children.Add(row);
        }
        foreach (var (text, tag, action) in new (string, string, Action)[] { ("Next response preset", "preset", () => { int index = Array.IndexOf(RebelPresets, Curves.Draft[Curves.Channel].Preset); Curves.SetPreset(RebelPresets[(index + 1) % RebelPresets.Length]); }), ("Reset curve", "reset", Curves.Reset), ("Advanced", "advanced", OpenStickTools) })
        {
            var face = new Border { Height = 46, Margin = new(0,8,0,0), Background = DuskKit.SlateItemBrush, Child = new TextBlock { Text = text, FontFamily = DuskKit.Display, FontSize = 18, Foreground = DuskKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new(16,0,0,0) } };
            leftStack.Children.Add(ThemeAction(face, "theme:curve:" + tag, action, tag == "advanced" || Curves.Draft.CanEdit));
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        contentCanvas.Children.Add(leftStack);

        // Right side: Graph + details
        var rightStack = new StackPanel { Width = 300, Orientation = Orientation.Vertical };

        var graphBorder = new Border
        {
            Width = 300,
            Height = 300,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.B("#141920"),
            BorderBrush = DuskKit.B("#232a33"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        graphBorder.Child = new Viewbox { Stretch = Stretch.Uniform, Child = Curves.TakeThemeGraph() };
        rightStack.Children.Add(graphBorder);

        // Details card with cyan specular border
        var detailCard = new Border
        {
            Background = DuskKit.SlateItemBrush,
            BorderBrush = DuskKit.CyanNeonBrush,
            BorderThickness = new Thickness(3, 1, 1, 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            Width = 300,
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.2 }
        };
        var detailStack = new StackPanel { Orientation = Orientation.Vertical };
        detailStack.Children.Add(new TextBlock
        {
            Text = "RESPONSE CURVE",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = CurveWorkbench.Name(Curves.Channel),
            FontFamily = DuskKit.Display,
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Edit the response using the graph handles or sliders. Changes stay in the current draft.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        detailCard.Child = detailStack;


        Canvas.SetLeft(rightStack, 788);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "curves",
            title: "Stick & Trigger Curves",
            lead: "Tune analog deadzones, response curve linearity, and anti-deadzone.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskCurveView = shell; duskPageHost.Children.Add(shell);
    }
}

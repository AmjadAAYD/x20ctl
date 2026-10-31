using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool voltCurvesWired;
    private TextBlock? voltPresetCaption;

    private UIElement BuildVoltCurves()
    {
        if (!voltCurvesWired)
        {
            voltCurvesWired = true;
            Curves.Changed += () =>
            {
                string preset = Curves.Draft[Curves.Channel].Preset;
                if (voltPresetCaption != null) voltPresetCaption.Text = "Preset: " + char.ToUpper(preset[0]) + preset[1..];
            };
            Curves.ChannelSelected += _ => VoltLater();
        }

        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Yellow wave backdrop
        root.Children.Add(VoltKit.WaveBackdrop(16));

        // 2. Top bar
        root.Children.Add(VoltKit.TopBar());

        // 3. Left side menu
        var pages = new (string Key, string Label, string Icon)[]
        {
            ("buttons", "Buttons", "\uE765"),
            ("curves", "Curves", "\uE9D9"),
            ("macros", "Macros", "\uE7C3"),
            ("vibration", "Vibration", "\uE8F1"),
            ("pcmode", "PC Mode", "\uE7F4"),
            ("device", "Device", "\uE770"),
            ("diagnostics", "Diagnostics", "\uE9E9"),
            ("mouse", "Mouse", "\uE962"),
            ("setups", "Setups", "\uE71D"),
            ("tester", "Tester", "\uE7FC"),
            ("tools", "Tools", "\uE713")
        };
        var menu = VoltKit.SideMenu(pages, "curves", VoltSelectPage);
        Canvas.SetLeft(menu, 0);
        Canvas.SetTop(menu, 98);
        root.Children.Add(menu);

        // 4. Tabs at (540, 110)
        var tabRow = new StackPanel { Orientation = Orientation.Horizontal };
        var channelDefs = new (string Name, CurveChannel Ch)[]
        {
            ("Left stick", CurveChannel.LeftStick),
            ("Right stick", CurveChannel.RightStick),
            ("Left trigger", CurveChannel.LeftTrigger),
            ("Right trigger", CurveChannel.RightTrigger)
        };
        foreach (var (tName, ch) in channelDefs)
        {
            bool active = ch == Curves.Channel;
            var tab = new Border
            {
                Padding = new Thickness(20, 9, 20, 9),
                CornerRadius = new CornerRadius(12),
                Background = active ? VoltKit.AmberHotBrush : VoltKit.B("#1d1d1d"),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = tName,
                    FontFamily = VoltKit.Display,
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = active ? VoltKit.B("#111111") : VoltKit.WhiteBrush
                }
            };
            tabRow.Children.Add(ThemeAction(tab, "theme:curve:channel:" + ch, () =>
            {
                Curves.SelectChannel(ch);
                VoltShow();
            }));
        }
        Canvas.SetLeft(tabRow, 540);
        Canvas.SetTop(tabRow, 110);
        root.Children.Add(tabRow);

        // 5. Main Card at (540, 186, w: 1020, h: 570)
        var card = new Border
        {
            Width = 1020,
            Height = 570,
            Background = VoltKit.CardDarkBrush,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(40, 34, 40, 34),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 60, ShadowDepth = 26, Opacity = 0.45 }
        };

        var cardCanvas = new Canvas { Width = 940, Height = 502 };

        // Headers
        var hdrStack = new StackPanel { Orientation = Orientation.Vertical };
        hdrStack.Children.Add(new TextBlock
        {
            Text = $"{CurveWorkbench.Name(Curves.Channel)} response",
            FontFamily = VoltKit.Display,
            FontSize = 30,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.WhiteBrush
        });
        hdrStack.Children.Add(new TextBlock
        {
            Text = "LOCAL DRAFT · Not written to controller memory.",
            FontFamily = VoltKit.Body,
            FontSize = 16,
            Foreground = VoltKit.MuteBrush,
            Margin = new Thickness(0, 6, 0, 20)
        });
        Canvas.SetLeft(hdrStack, 0);
        Canvas.SetTop(hdrStack, 0);
        cardCanvas.Children.Add(hdrStack);

        // Sliders on left (width: 480)
        var sliderStack = new StackPanel { Orientation = Orientation.Vertical, Width = 480 };

        foreach (var (fName, source) in RebelCurveRows)
        {
            var row = new Border
            {
                Height = 64,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var rStack = new Grid { VerticalAlignment = VerticalAlignment.Center };
            rStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            rStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            var lbl = new TextBlock
            {
                Text = fName,
                FontFamily = VoltKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);
            rStack.Children.Add(lbl);

            var slider = ThemeSlider(source, "theme:curve:" + fName, 220, Curves.Draft.CanEdit);
            slider.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(slider, 1);
            rStack.Children.Add(slider);

            var valBlock = new TextBlock
            {
                FontFamily = VoltKit.Display,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.AmberHotBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            valBlock.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = source, StringFormat = "{0:0}%" });
            Grid.SetColumn(valBlock, 2);
            rStack.Children.Add(valBlock);

            row.Child = rStack;
            sliderStack.Children.Add(row);
        }

        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 12, 0, 0) };
        string preset = Curves.Draft[Curves.Channel].Preset;
        var presetBtn = new Border
        {
            Padding = new Thickness(14, 7, 14, 7),
            Background = VoltKit.B("#262626"),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock
            {
                Text = "Preset: " + char.ToUpper(preset[0]) + preset[1..],
                FontFamily = VoltKit.Display,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush
            }
        };
        voltPresetCaption = (TextBlock)presetBtn.Child;
        actionsRow.Children.Add(ThemeAction(presetBtn, "theme:curve:preset", () =>
        {
            int index = Array.IndexOf(RebelPresets, Curves.Draft[Curves.Channel].Preset);
            Curves.SetPreset(RebelPresets[(index + 1) % RebelPresets.Length]);
            VoltShow();
        }, Curves.Draft.CanEdit));

        var resetBtn = new Border
        {
            Padding = new Thickness(14, 7, 14, 7),
            Background = VoltKit.B("#262626"),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock
            {
                Text = "Reset",
                FontFamily = VoltKit.Display,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush
            }
        };
        actionsRow.Children.Add(ThemeAction(resetBtn, "theme:curve:reset", () =>
        {
            Curves.Reset();
            VoltShow();
        }, Curves.Draft.CanEdit));

        var advBtn = new Border
        {
            Padding = new Thickness(14, 7, 14, 7),
            Background = VoltKit.B("#262626"),
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                Text = "Advanced",
                FontFamily = VoltKit.Display,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush
            }
        };
        actionsRow.Children.Add(ThemeAction(advBtn, "theme:curve:advanced", OpenStickTools));
        sliderStack.Children.Add(actionsRow);

        Canvas.SetLeft(sliderStack, 0);
        Canvas.SetTop(sliderStack, 84);
        cardCanvas.Children.Add(sliderStack);

        // 6. Response Curve Graph on right at (510, 60, w: 400, h: 380)
        double gw = 400, gh = 380;
        var graphBox = new Border
        {
            Width = gw,
            Height = gh,
            Background = VoltKit.B("#0b0b0b"),
            CornerRadius = new CornerRadius(14),
            ClipToBounds = true,
            BorderBrush = VoltKit.B("#222222"),
            BorderThickness = new Thickness(1)
        };
        graphBox.Child = new Viewbox { Stretch = Stretch.Uniform, Child = Curves.TakeThemeGraph() };
        Canvas.SetLeft(graphBox, 500);
        Canvas.SetTop(graphBox, 60);
        cardCanvas.Children.Add(graphBox);

        card.Child = cardCanvas;
        Canvas.SetLeft(card, 540);
        Canvas.SetTop(card, 186);
        root.Children.Add(card);

        // 7. Dots + Bottom Bar
        var dots = VoltKit.CarouselDots(2, 4);
        Canvas.SetLeft(dots, 760);
        Canvas.SetTop(dots, 812);
        root.Children.Add(dots);

        var bar = VoltKit.BottomBar(new[] { ("X", "RESET CURVE"), ("A", "SELECT"), ("B", "BACK") });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 844);
        root.Children.Add(bar);

        return root;
    }
}

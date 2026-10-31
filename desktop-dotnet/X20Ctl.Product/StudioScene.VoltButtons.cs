using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private UIElement BuildVoltButtons()
    {
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
        var menu = VoltKit.SideMenu(pages, "buttons", VoltSelectPage);
        Canvas.SetLeft(menu, 0);
        Canvas.SetTop(menu, 98);
        root.Children.Add(menu);

        // 4. Large Controller Artwork at (540, 240, w: 620)
        var floorGlow = VoltKit.PedestalGlow(650, 160, VoltKit.AmberHot);
        Canvas.SetLeft(floorGlow, 520);
        Canvas.SetTop(floorGlow, 420);
        root.Children.Add(floorGlow);

        var padArt = new Image { Width = 620, Source = ControllerArt.Front(Model.Id, PadColours.For(Player, Model.Id)), Stretch = Stretch.Uniform };
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect { Color = VoltKit.C("#3c2800"), BlurRadius = 40, ShadowDepth = 30, Opacity = 0.5 };
            Canvas.SetLeft(padArt, 540);
            Canvas.SetTop(padArt, 240);
            root.Children.Add(padArt);
        }

        // Highlight ring on A button with radiant bloom
        var aRing = new Border
        {
            Width = 64,
            Height = 64,
            CornerRadius = new CornerRadius(32),
            BorderBrush = VoltKit.AmberHotBrush,
            BorderThickness = new Thickness(5),
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 20, ShadowDepth = 0, Opacity = 1.0 }
        };
        if (TryControlCenter(SelectedControl, out var ringFx, out var ringFy))
        {
            Canvas.SetLeft(aRing, 540 + ringFx * 620 - 32);
            Canvas.SetTop(aRing, 240 + ringFy * 620 * 2 / 3 - 32);
            root.Children.Add(aRing);
        }

        // 5. Category Pill top-right at (1200, 118)
        var catPill = new Border
        {
            Padding = new Thickness(28, 12, 28, 12),
            CornerRadius = new CornerRadius(16),
            Background = VoltKit.B("#1b1b1b"),
            Child = new TextBlock
            {
                Text = "\uE76B  Face Buttons  \uE76C",
                FontFamily = VoltKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush
            }
        };
        Canvas.SetLeft(catPill, 1200);
        Canvas.SetTop(catPill, 118);
        var category = ThemeAction(catPill, "theme:all-inputs", () => { }); category.Click += (_, _) => ThemeInputMenu(category); root.Children.Add(category);

        // 6. Remap Dialog Box at (1200, 186, w: 340)
        var remapDlg = new Border
        {
            Width = 340,
            Background = VoltKit.CardDarkBrush,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 60, ShadowDepth = 26, Opacity = 0.5 }
        };
        var remapStack = new StackPanel { Orientation = Orientation.Vertical };

        var remaps = ThemeMappingKeys().Select(key => (key, Draft.Target(key), key == SelectedControl)).ToArray();

        foreach (var (fromKey, toKey, active) in remaps)
        {
            var row = new Border
            {
                Height = 56,
                CornerRadius = new CornerRadius(12),
                Background = active ? VoltKit.AmberHotBrush : Brushes.Transparent,
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(16, 0, 16, 0),
                Cursor = Cursors.Hand
            };
            var rowStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            rowStack.Children.Add(new TextBlock
            {
                Text = fromKey,
                FontFamily = VoltKit.Display,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = active ? VoltKit.B("#111111") : VoltKit.WhiteBrush
            });
            rowStack.Children.Add(new TextBlock
            {
                Text = "  ▸  ",
                FontFamily = VoltKit.Display,
                FontSize = 20,
                Foreground = active ? VoltKit.B("#111111") : VoltKit.MuteBrush
            });
            rowStack.Children.Add(new TextBlock
            {
                Text = toKey,
                FontFamily = VoltKit.Display,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = active ? VoltKit.B("#111111") : VoltKit.AmberHotBrush
            });
            row.Child = rowStack;
            remapStack.Children.Add(ThemeMappingAction(row, fromKey));
        }
        remapDlg.Child = new ScrollViewer { MaxHeight = 440, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = remapStack };
        Canvas.SetLeft(remapDlg, 1200);
        Canvas.SetTop(remapDlg, 186);
        root.Children.Add(remapDlg);

        // 7. Local Draft Toast at (540, 720, w: 680)
        var toast = new Border
        {
            Width = 680,
            Height = 72,
            CornerRadius = new CornerRadius(4),
            Background = VoltKit.B("#1c1c1c"),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, ShadowDepth = 6, Opacity = 0.4 }
        };
        var tStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var icon = new TextBlock
        {
            Text = "\uE7C3",
            FontFamily = VoltKit.Icons,
            FontSize = 22,
            Foreground = VoltKit.AmberHotBrush,
            Margin = new Thickness(24, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        tStack.Children.Add(icon);
        var tTxt = new TextBlock
        {
            Text = "LOCAL DRAFT · Not written to controller memory.",
            FontFamily = VoltKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        tStack.Children.Add(tTxt);
        toast.Child = tStack;
        Canvas.SetLeft(toast, 540);
        Canvas.SetTop(toast, 720);
        root.Children.Add(toast);

        // 8. Dots + Bottom Bar
        var dots = VoltKit.CarouselDots(1, 4);
        Canvas.SetLeft(dots, 760);
        Canvas.SetTop(dots, 812);
        root.Children.Add(dots);

        var bar = VoltKit.BottomBar(new[] { ("Y", "ALL BUTTONS"), ("A", "SELECT"), ("B", "BACK") });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 844);
        root.Children.Add(bar);

        return root;
    }
}

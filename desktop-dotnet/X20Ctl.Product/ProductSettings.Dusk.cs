using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;

namespace X20Ctl.Product;

public sealed partial class ProductSettings
{
    internal FrameworkElement? duskRoot;
    private string duskView = "hub";

    private void BuildDusk()
    {
        if (pageGrid != null) pageGrid.Visibility = Visibility.Collapsed;
        hub.Visibility = Visibility.Collapsed;
        about.Visibility = Visibility.Collapsed;
        feedback.Visibility = Visibility.Collapsed;
        pcMode.Visibility = Visibility.Collapsed;
        themes.Visibility = Visibility.Collapsed;
        footer.Visibility = Visibility.Collapsed;

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Background = DuskKit.SlateBgBrush };
        duskRoot = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        Children.Add(duskRoot);

        DuskSync();
    }

    internal void DuskSync()
    {
        if (duskRoot is not Viewbox box || box.Child is not Canvas board) return;
        board.Children.Clear();

        duskView = pcMode.Visibility == Visibility.Visible ? "pcmode" :
                   feedback.Visibility == Visibility.Visible ? "feedback" :
                   about.Visibility == Visibility.Visible ? "about" :
                   themes.Visibility == Visibility.Visible ? "themes" : "hub";

        switch (duskView)
        {
            case "about":
                RenderDuskAbout(board);
                break;
            case "themes":
                RenderDuskThemes(board);
                break;
            case "feedback":
                RenderDuskFeedback(board);
                break;
            case "pcmode":
                RenderDuskPcMode(board);
                break;
            case "hub":
            default:
                RenderDuskHub(board);
                break;
        }
    }

    private void RenderDuskHub(Canvas board)
    {
        var hints = new[] { ("A", "Open"), ("B", "Back") };
        var content = new Canvas { Width = 1040, Height = 580 };

        // 1. Feature card at (0, 0, w: 520, h: 280)
        var featCard = new Border
        {
            Width = 520,
            Height = 280,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = { new(DuskKit.C("#2a3442"), 0.0), new(DuskKit.C("#141a22"), 1.0) }
            },
            BorderBrush = DuskKit.WhiteBrush,
            BorderThickness = new Thickness(2),
            Effect = new DropShadowEffect { Color = Colors.White, BlurRadius = 24, ShadowDepth = 0, Opacity = 0.25 }
        };

        var fCanvas = new Canvas { Width = 520, Height = 280 };

        // Ambient cyan pedestal pool under controller
        var pedestal = new Ellipse
        {
            Width = 240,
            Height = 44,
            Fill = new RadialGradientBrush(DuskKit.CyanNeon, Colors.Transparent),
            Opacity = 0.35,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(pedestal, 260);
        Canvas.SetTop(pedestal, 200);
        fCanvas.Children.Add(pedestal);

        var padArt = DuskKit.ControllerArt("x05pro", 300);
        if (padArt != null)
        {
            padArt.Opacity = 0.85;
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 10, Opacity = 0.5 };
            Canvas.SetLeft(padArt, 240);
            Canvas.SetTop(padArt, 20);
            fCanvas.Children.Add(padArt);
        }

        // Tactical HUD micro-brackets & top highlight
        fCanvas.Children.Add(DuskKit.CornerBrackets(520, 280, DuskKit.CyanNeon, 18, 1.5));
        fCanvas.Children.Add(new Rectangle { Width = 520, Height = 2, Fill = DuskKit.CyanNeonBrush, IsHitTestVisible = false });

        var hBadge = new Border
        {
            Padding = new Thickness(8, 3, 8, 3),
            CornerRadius = new CornerRadius(3),
            Background = DuskKit.B("rgba(14,18,24,0.85)"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.4)"),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = "DIAGNOSTICS",
                FontFamily = DuskKit.Display,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.CyanNeonBrush
            }
        };
        Canvas.SetRight(hBadge, 16);
        Canvas.SetTop(hBadge, 14);
        fCanvas.Children.Add(hBadge);

        var fText = new StackPanel { Orientation = Orientation.Vertical, Width = 260 };
        fText.Children.Add(new TextBlock
        {
            Text = "Sticks & Triggers",
            FontFamily = DuskKit.Display,
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            TextWrapping = TextWrapping.Wrap
        });
        fText.Children.Add(new TextBlock
        {
            Text = "Live readings in Diagnostics.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        Canvas.SetLeft(fText, 24);
        Canvas.SetTop(fText, 24);
        fCanvas.Children.Add(fText);

        var chips = new StackPanel { Orientation = Orientation.Horizontal };
        var chip1 = new Border
        {
            Padding = new Thickness(14, 6, 14, 6),
            CornerRadius = new CornerRadius(14),
            Background = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock { Text = "Sticks", FontFamily = DuskKit.Display, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = DuskKit.SlateBgBrush }
        };
        var chip2 = new Border
        {
            Padding = new Thickness(14, 6, 14, 6),
            CornerRadius = new CornerRadius(14),
            Background = DuskKit.SlateTabOnBrush,
            Child = new TextBlock { Text = "Triggers", FontFamily = DuskKit.Display, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = DuskKit.WhiteBrush }
        };
        chips.Children.Add(chip1);
        chips.Children.Add(chip2);
        Canvas.SetLeft(chips, 24);
        Canvas.SetBottom(chips, 20);
        fCanvas.Children.Add(chips);

        featCard.Child = fCanvas;
        Canvas.SetLeft(featCard, 0);
        Canvas.SetTop(featCard, 0);
        content.Children.Add(featCard);

        // 2. Grid of tool tiles (w: 230, h: 84) at x = 560
        var toolTiles = new (string Icon, string Name, string Sub, Action OnClick)[]
        {
            ("\uE790", "Themes", "Console looks & intros", () => ShowThemes(true)),
            ("\uE977", "PC Mode", "Routing & drivers", () => ShowPcMode(true)),
            ("\uE946", "About", "Version & licenses", () => ShowAbout(true)),
            ("\uE939", "Feedback", "Bug reports & logs", () => ShowFeedback(true))
        };

        for (int i = 0; i < toolTiles.Length; i++)
        {
            var (icon, name, sub, onClick) = toolTiles[i];
            int col = i % 2;
            int row = i / 2;

            var tile = new Border
            {
                Width = 230,
                Height = 84,
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Background = DuskKit.SlateItemBrush,
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 10, ShadowDepth = 3, Opacity = 0.3 }
            };

            var tCanvas = new Canvas { Width = 230, Height = 84 };

            var tGrid = new Grid { Width = 206, Height = 84 };
            tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icContainer = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.B("#121720"),
                BorderBrush = DuskKit.SlateTabOnBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = icon,
                    FontFamily = DuskKit.Icons,
                    FontSize = 22,
                    Foreground = DuskKit.CyanNeonBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(icContainer, 0);
            tGrid.Children.Add(icContainer);

            var tStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            tStack.Children.Add(new TextBlock
            {
                Text = name,
                FontFamily = DuskKit.Display,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush
            });
            tStack.Children.Add(new TextBlock
            {
                Text = sub,
                FontFamily = DuskKit.Body,
                FontSize = 12,
                Foreground = DuskKit.MuteBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(tStack, 1);
            tGrid.Children.Add(tStack);

            Canvas.SetLeft(tGrid, 12);
            tCanvas.Children.Add(tGrid);

            // Subtle console corner brackets
            tCanvas.Children.Add(DuskKit.CornerBrackets(230, 84, DuskKit.SlateTabOn, 10, 1.0));

            tile.Child = tCanvas;
            tile.MouseLeftButtonUp += (_, _) => onClick();

            Canvas.SetLeft(tile, 560 + col * 246);
            Canvas.SetTop(tile, row * 98);
            content.Children.Add(tile);
        }

        // 3. Lower section: Updates & Intro
        var upHdr = new TextBlock
        {
            Text = "UPDATES AND OPENING INTRO",
            FontFamily = DuskKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush
        };
        Canvas.SetLeft(upHdr, 0);
        Canvas.SetTop(upHdr, 320);
        content.Children.Add(upHdr);

        var upStack = new StackPanel { Width = 1040, Orientation = Orientation.Vertical };
        var upRows = new (string Title, string Sub, string Val)[]
        {
            ("Check for application updates", $"Current version: {Scanner.ScanFlow.AppVersion}", "Check now"),
            ("Play console opening intro", "Plays silent opening cinematic film at startup", "Enabled")
        };

        foreach (var (tTitle, tSub, tVal) in upRows)
        {
            var uRow = new Border
            {
                Height = 60,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = DuskKit.B("rgba(255,255,255,.07)"),
                Padding = new Thickness(16, 0, 16, 0)
            };
            var uGrid = new Grid();
            uGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            uGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            lStack.Children.Add(new TextBlock { Text = tTitle, FontFamily = DuskKit.Display, FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = DuskKit.LightBrush });
            lStack.Children.Add(new TextBlock { Text = tSub, FontFamily = DuskKit.Body, FontSize = 13, Foreground = DuskKit.MuteBrush, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(lStack, 0);
            uGrid.Children.Add(lStack);

            var rBtn = new Border
            {
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.SlateTabOnBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            rBtn.Child = new TextBlock { Text = tVal, FontFamily = DuskKit.Display, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = DuskKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(rBtn, 1);
            uGrid.Children.Add(rBtn);

            uRow.Child = uGrid;
            upStack.Children.Add(uRow);
        }

        Canvas.SetLeft(upStack, 0);
        Canvas.SetTop(upStack, 350);
        content.Children.Add(upStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "tools",
            title: "Tools & Utilities",
            lead: "Diagnostic scanners, theme customization, firmware tools and system status.",
            content: content,
            hints: hints,
            onNav: key => ThemeNavigate?.Invoke(key)
        );
        board.Children.Add(shell);
    }

    private void RenderDuskThemes(Canvas board)
    {
        RenderCatalogThemes(board);
    }

    private void RenderDuskAbout(Canvas board)
    {
        var hints = new[] { ("B", "Back") };
        var content = new Canvas { Width = 1040, Height = 580 };

        var aboutCard = new Border
        {
            Width = 600,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.SlateItemBrush,
            Padding = new Thickness(26)
        };
        var aStack = new StackPanel { Orientation = Orientation.Vertical };
        aStack.Children.Add(new TextBlock { Text = "X20CTL — Console Controller Studio", FontFamily = DuskKit.Display, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = DuskKit.WhiteBrush });
        aStack.Children.Add(new TextBlock { Text = $"Native Controller Studio · {Updater.Shown}", FontFamily = DuskKit.Display, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = DuskKit.GreenOnlineBrush, Margin = new Thickness(0, 4, 0, 16) });
        aStack.Children.Add(new TextBlock
        {
            Text = "An independent, open-source controller studio for EasySMX pads. Changes stay local drafts. Controller configuration writes remain locked until verified.\n\nMade by Amjad AAYD.\n\nSpecial thanks to Kiin from Team EasySMX for providing an X20 Pro.\n\nConsole design inspired by ApexSenseBridge by ReynArts. Desktop-control layout inspired by Gopher360 by Tylemagne (GPL-3.0); see the distributed license notices.\n\nX20CTL is not affiliated with or endorsed by EasySMX.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22
        });
        var links = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) };
        foreach (var (label, url) in new[] { ("X20CTL on GitHub", ExternalLinks.Repository), ("ApexSenseBridge by ReynArts", ExternalLinks.Inspiration) })
        {
            var link = Pill("", label); link.Margin = new Thickness(0, 0, 10, 10);
            link.Click += (_, _) => Try(() => ExternalLinks.Open(url)); links.Children.Add(link);
        }
        aStack.Children.Add(links);
        aboutCard.Child = new ScrollViewer { Content = aStack, MaxHeight = 640, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Canvas.SetLeft(aboutCard, 0);
        Canvas.SetTop(aboutCard, 0);
        content.Children.Add(aboutCard);

        var shell = DuskKit.SettingsShell(
            activeKey: "tools",
            title: "About X20CTL",
            lead: "Open-source license information, hardware architecture, and runtime details.",
            content: content,
            hints: hints,
            onNav: key => ThemeNavigate?.Invoke(key)
        );
        board.Children.Add(shell);
    }

    private void RenderDuskFeedback(Canvas board)
    {
        var hints = new[] { ("A", "Send"), ("B", "Back") };
        var content = new Canvas { Width = 1040, Height = 580 };

        MountFeedbackForm(content, 0, 0, 1040, 580);

        var shell = DuskKit.SettingsShell(
            activeKey: "tools",
            title: "User Feedback",
            lead: "Report issues, suggest feature improvements, and upload hardware diagnostics.",
            content: content,
            hints: hints,
            onNav: key => ThemeNavigate?.Invoke(key)
        );
        board.Children.Add(shell);
    }

    private void RenderDuskPcMode(Canvas board)
    {
        var hints = new[] { ("A", "Save"), ("B", "Back") };
        var content = new Canvas { Width = 1040, Height = 580 };

        var shell = DuskKit.SettingsShell(
            activeKey: "tools",
            title: "PC Mode Configuration",
            lead: "Configure USB virtual routing and ViGEmBus controller emulation.",
            content: content,
            hints: hints,
            onNav: key => ThemeNavigate?.Invoke(key)
        );
        board.Children.Add(shell);
    }
}

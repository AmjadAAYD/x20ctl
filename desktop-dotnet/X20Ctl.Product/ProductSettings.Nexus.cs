using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product;

public sealed partial class ProductSettings
{
    internal FrameworkElement? nexusRoot;
    public Action<string>? NexusNavigate { get; set; }
    private string nexusView = "hub";

    private void BuildNexus()
    {
        if (pageGrid != null) pageGrid.Visibility = Visibility.Collapsed;
        hub.Visibility = Visibility.Collapsed;
        about.Visibility = Visibility.Collapsed;
        feedback.Visibility = Visibility.Collapsed;
        pcMode.Visibility = Visibility.Collapsed;
        themes.Visibility = Visibility.Collapsed;
        footer.Visibility = Visibility.Collapsed;

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        nexusRoot = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        Children.Add(nexusRoot);

        var changed = (Action)(() => { if (nexusRoot.IsVisible && nexusView == "hub") NexusSync(); });
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock)).AddValueChanged(updateStatus, (_, _) => changed());
        DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(UIElement)).AddValueChanged(updateGo, (_, _) => changed());
        autoUpdate.Checked += (_, _) => changed();
        autoUpdate.Unchecked += (_, _) => changed();

        NexusSync();
    }

    internal void NexusSync()
    {
        if (nexusRoot is not Viewbox box || box.Child is not Canvas board) return;
        board.Children.Clear();

        nexusView = pcMode.Visibility == Visibility.Visible ? "pcmode" :
                    feedback.Visibility == Visibility.Visible ? "feedback" :
                    about.Visibility == Visibility.Visible ? "about" :
                    themes.Visibility == Visibility.Visible ? "themes" : "hub";

        switch (nexusView)
        {
            case "about":
                RenderNexusAbout(board);
                break;
            case "pcmode":
                RenderNexusPcMode(board);
                break;
            case "themes":
                RenderNexusThemes(board);
                break;
            case "feedback":
                BuildNexusFeedbackContent(board);
                break;
            case "hub":
            default:
                RenderNexusHub(board);
                break;
        }
    }

    private void RenderNexusHub(Canvas board)
    {
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
        board.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: 0,
            model: "EasySMX X20 Pro",
            status: "Offline",
            onZone: () => NexusNavigate?.Invoke("Zone"),
            onSetups: () => NexusNavigate?.Invoke("Setups"),
            onPcMode: () => ShowPcMode(true),
            onTools: null
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        board.Children.Add(topBar);

        // 3. Title & Subtitle
        var title = new TextBlock
        {
            Text = "Tools",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 138);
        board.Children.Add(title);

        var lead = new TextBlock
        {
            Text = "Help, extras and everything about X20CTL",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        board.Children.Add(lead);

        // 4. Hero Tile at (68, 240, w=600, h=320)
        var heroGrid = new Grid
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(1, 1),
                GradientStops = { new(NexusKit.C("#3a4250"), 0.0), new(NexusKit.C("#17191c"), 1.0) }
            }
        };
        var heroArt = NexusKit.ControllerArt("x20", 300);
        if (heroArt != null)
        {
            heroArt.RenderTransformOrigin = new Point(0.5, 0.5);
            heroArt.RenderTransform = new RotateTransform(-8);
            heroArt.Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 24, Direction = 270, Opacity = 0.6, Color = Colors.Black };
            heroArt.HorizontalAlignment = HorizontalAlignment.Right;
            heroArt.VerticalAlignment = VerticalAlignment.Center;
            heroArt.Margin = new Thickness(0, 0, 20, 0);
            heroGrid.Children.Add(heroArt);
        }

        var hTextStack = new StackPanel { Margin = new Thickness(26, 28, 0, 0), Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
        hTextStack.Children.Add(new TextBlock
        {
            Text = "Controller not working?",
            FontFamily = NexusKit.Display,
            FontSize = 30,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush,
            TextWrapping = TextWrapping.Wrap
        });
        hTextStack.Children.Add(new TextBlock
        {
            Text = "Read scan, then Write scan, then send both reports online together.",
            FontFamily = NexusKit.Body,
            FontSize = 16,
            Foreground = NexusKit.SubBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0)
        });
        heroGrid.Children.Add(hTextStack);

        var hChips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(26, 0, 0, 24), VerticalAlignment = VerticalAlignment.Bottom };
        var chips = new[] { "1 Read scan", "2 Write scan", "3 Send online" };
        for (int i = 0; i < chips.Length; i++)
        {
            var ch = new Border
            {
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0),
                CornerRadius = new CornerRadius(4),
                Background = (i == 0) ? NexusKit.WhiteBrush : NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = chips[i],
                    FontFamily = NexusKit.Body,
                    FontSize = 14,
                    FontWeight = (i == 0) ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = (i == 0) ? NexusKit.B("#111111") : NexusKit.WhiteBrush
                }
            };
            hChips.Children.Add(ch);
        }
        heroGrid.Children.Add(hChips);

        var heroTile = NexusKit.Tile(600, 320, heroGrid, null, focused: true, onClick: () => Check.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));
        Canvas.SetLeft(heroTile, 68);
        Canvas.SetTop(heroTile, 240);
        board.Children.Add(heroTile);

        // 5. 6 Tool Tiles at (688 + (i%3)*214, 240 + (i//3)*166, w=200, h=154)
        var cols = new[] { "#1f3a3a", "#2a2440", "#3a2a1f", "#1f2a3a", "#3a1f2a", "#24272c" };
        var toolTiles = new (string Icon, string Name, Action Act)[]
        {
            ("\uE9A6", "PC mode", () => ShowPcMode(true)),
            ("\uE8EA", "Write scan", () => WriteScan.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE768", "Replay Intro", () => Replay.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE946", "About", () => ShowAbout(true)),
            ("\uEB51", "Support X20CTL", () => Support.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE734", "Star on GitHub", () => Support.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)))
        };

        for (int i = 0; i < toolTiles.Length; i++)
        {
            var (ic, nm, act) = toolTiles[i];
            double tx = 688 + (i % 3) * 214;
            double ty = 240 + (i / 3) * 166;

            var tGrid = new Grid
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new(0, 0), EndPoint = new(1, 1),
                    GradientStops = { new(NexusKit.C(cols[i]), 0.0), new(NexusKit.C("#1b1c1f"), 1.0) }
                }
            };
            var icText = new TextBlock
            {
                Text = ic,
                FontFamily = NexusKit.Icons,
                FontSize = 34,
                Foreground = NexusKit.WhiteBrush,
                Margin = new Thickness(18, 18, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            tGrid.Children.Add(icText);

            var tile = NexusKit.Tile(200, 154, tGrid, nm, focused: false, onClick: act);
            Canvas.SetLeft(tile, tx);
            Canvas.SetTop(tile, ty);
            board.Children.Add(tile);
        }

        // 6. Right Tile at (1330, 240, w=202, h=320): Send feedback
        var fbGrid = new Grid
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(1, 1),
                GradientStops = { new(NexusKit.C("#1f3a3a"), 0.0), new(NexusKit.C("#162426"), 1.0) }
            }
        };
        fbGrid.Children.Add(new TextBlock
        {
            Text = "\uED15", // Feedback icon
            FontFamily = NexusKit.Icons,
            FontSize = 34,
            Foreground = NexusKit.WhiteBrush,
            Margin = new Thickness(18, 18, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        });
        fbGrid.Children.Add(new TextBlock
        {
            Text = "Report a bug, ask for a feature or tell us about your controller.",
            FontFamily = NexusKit.Body,
            FontSize = 14,
            Foreground = NexusKit.SubBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(18, 120, 16, 0),
            VerticalAlignment = VerticalAlignment.Top
        });
        var fbTile = NexusKit.Tile(202, 320, fbGrid, "Send feedback", focused: false, onClick: () => ShowFeedback(true));
        Canvas.SetLeft(fbTile, 1330);
        Canvas.SetTop(fbTile, 240);
        board.Children.Add(fbTile);

        // 7. Bottom rows at (68, 590, w=1464)
        var bRows = new StackPanel { Width = 1464 };

        // Update row
        var upBtn = NexusKit.TheatricalButton("Check now", primary: false, height: 44, fontSize: 14);
        var rUpdate = NexusKit.RowButton(
            "Keep X20CTL up to date",
            $"On: each launch checks GitHub for a newer version and installs it. You have {Scanner.ScanFlow.AppVersion}.",
            upBtn,
            focused: false,
            width: 1464
        );
        bRows.Children.Add(rUpdate);

        // Intro options row
        var tStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        tStack.Children.Add(new TextBlock { Text = "At launch ", FontFamily = NexusKit.Body, FontSize = 16, Foreground = NexusKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        tStack.Children.Add(NexusKit.Toggle(true));
        tStack.Children.Add(new TextBlock { Text = "   Sound ", FontFamily = NexusKit.Body, FontSize = 16, Foreground = NexusKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        tStack.Children.Add(NexusKit.Toggle(true));

        var rIntro = NexusKit.RowButton(
            "Opening intro",
            "Esc skips it, M mutes it.",
            tStack,
            focused: false,
            width: 1464
        );
        bRows.Children.Add(rIntro);

        Canvas.SetLeft(bRows, 68);
        Canvas.SetTop(bRows, 590);
        board.Children.Add(bRows);

        // 8. Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Open"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        board.Children.Add(hints);
    }

    private void RenderNexusThemes(Canvas board)
    {
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
        board.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: 0,
            model: "EasySMX X20 Pro",
            status: "Offline",
            onZone: () => NexusNavigate?.Invoke("Zone"),
            onSetups: () => NexusNavigate?.Invoke("Setups"),
            onPcMode: () => ShowPcMode(true),
            onTools: () => ShowThemes(false)
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        board.Children.Add(topBar);

        // 3. Title & Tabs
        var title = new TextBlock
        {
            Text = "Customize your home",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 140);
        board.Children.Add(title);

        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (t, on) in new[] { ("Themes", true), ("Motion", false), ("Background", false) })
        {
            var tabBtn = new Border
            {
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 0, 10, 0),
                CornerRadius = new CornerRadius(4),
                Background = on ? NexusKit.WhiteBrush : NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = t,
                    FontFamily = NexusKit.Body,
                    FontSize = 16,
                    FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = on ? NexusKit.B("#111111") : NexusKit.WhiteBrush
                }
            };
            tabsStack.Children.Add(tabBtn);
        }
        Canvas.SetLeft(tabsStack, 68);
        Canvas.SetTop(tabsStack, 200);
        board.Children.Add(tabsStack);

        // 4. 10 Theme Tiles (284x160) at (68 + (i%4)*300, 270 + (i//4)*196)
        var themesList = new (string Key, string Name)[]
        {
            ("default", "Classic Steam"),
            ("pulse", "Pulse"),
            ("spotlight", "Spotlight"),
            ("nexus", "Nexus"),
            ("rebel", "Rebel"),
            ("dusk", "Dusk"),
            ("volt", "Volt"),
            ("yozakura", "Yozakura"),
            ("tide", "Tide")
        };

        for (int i = 0; i < themesList.Length; i++)
        {
            var th = themesList[i];
            double tx = 68 + (i % 4) * 300;
            double ty = 270 + (i / 4) * 196;
            bool on = (th.Key == "nexus");

            var thumb = NexusKit.ThemeThumb(th.Key, 284, 160);
            var tile = NexusKit.Tile(284, 160, thumb, th.Name, focused: on);
            Canvas.SetLeft(tile, tx);
            Canvas.SetTop(tile, ty);
            board.Children.Add(tile);
        }

        // 5. Right Details Panel at (1290, 270, w=244, h=552)
        var sidePanel = new Border
        {
            Width = 244, Height = 552,
            Padding = new Thickness(24, 26, 24, 26),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var sStack = new StackPanel();
        sStack.Children.Add(new TextBlock
        {
            Text = "Nexus",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        sStack.Children.Add(new TextBlock
        {
            Text = "Key art across the top, tiles below, green focus.",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 30)
        });

        var applyBtn = NexusKit.TheatricalButton("Apply", primary: true, height: 48, fontSize: 16);
        sStack.Children.Add(applyBtn);

        sStack.Children.Add(new TextBlock
        {
            Text = "Your controllers and drafts stay as they are.",
            FontFamily = NexusKit.Body,
            FontSize = 14,
            Foreground = NexusKit.SubBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 20, 0, 0)
        });

        sidePanel.Child = sStack;
        Canvas.SetLeft(sidePanel, 1290);
        Canvas.SetTop(sidePanel, 270);
        board.Children.Add(sidePanel);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Apply"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        board.Children.Add(hints);
    }

    private void RenderNexusPcMode(Canvas board)
    {
        board.Children.Add(new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0.5, 0), EndPoint = new(0.5, 1),
                GradientStops = { new(NexusKit.C("#1a1c20"), 0.0), new(NexusKit.House, 1.0) }
            }
        });
        var topBar = NexusKit.TopBar(
            player: 0,
            model: "EasySMX X20 Pro",
            status: "Offline",
            onZone: () => NexusNavigate?.Invoke("Zone"),
            onSetups: () => NexusNavigate?.Invoke("Setups"),
            onPcMode: null,
            onTools: () => ShowPcMode(false)
        );
        Canvas.SetLeft(topBar, 62); Canvas.SetTop(topBar, 46);
        board.Children.Add(topBar);

        var title = new TextBlock { Text = "PC mode", FontFamily = NexusKit.Display, FontSize = 38, FontWeight = FontWeights.SemiBold, Foreground = NexusKit.WhiteBrush };
        Canvas.SetLeft(title, 68); Canvas.SetTop(title, 140);
        board.Children.Add(title);

        // The real PC mode page, not a drawn copy
        if (buildPcMode != null)
        {
            var scroll = new ScrollViewer
            {
                Width = 1464, Height = 600, Content = buildPcMode(),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Canvas.SetLeft(scroll, 68); Canvas.SetTop(scroll, 210);
            board.Children.Add(scroll);
        }

        var hints = NexusKit.HintsBar(new[] { ("B", "Back") });
        Canvas.SetRight(hints, 62); Canvas.SetTop(hints, 856);
        board.Children.Add(hints);
    }

    private void RenderNexusAbout(Canvas board)
    {
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
        board.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: 0,
            model: "EasySMX X20 Pro",
            status: "Offline",
            onZone: () => NexusNavigate?.Invoke("Zone"),
            onSetups: () => NexusNavigate?.Invoke("Setups"),
            onPcMode: () => ShowPcMode(true),
            onTools: () => ShowAbout(false)
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        board.Children.Add(topBar);

        // 3. Title & Subtitle
        var title = new TextBlock
        {
            Text = "About",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 138);
        board.Children.Add(title);

        var lead = new TextBlock
        {
            Text = $"Native Controller Studio · {Scanner.ScanFlow.AppVersion}",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        board.Children.Add(lead);

        // 4. Left Column at (68, 250, w=820)
        var rowPanel = new StackPanel { Width = 820 };

        var r1 = NexusKit.RowButton("Made By", "Amjad AAYD, who designs and builds X20CTL.", null, focused: true, width: 820);
        rowPanel.Children.Add(r1);

        var r2 = NexusKit.RowButton("Thanks", "Special thanks to Kiin from Team EasySMX for sending me a free X20 Pro controller.", null, focused: false, width: 820);
        rowPanel.Children.Add(r2);

        var r3 = NexusKit.RowButton("Inspiration", "The console-style look of X20CTL was inspired by ApexSenseBridge by ReynArts.", null, focused: false, width: 820);
        rowPanel.Children.Add(r3);

        var r4 = NexusKit.RowButton("X20CTL on GitHub", "AmjadAAYD/x20ctl", null, focused: false, width: 820);
        rowPanel.Children.Add(r4);

        var r5 = NexusKit.RowButton("ApexSenseBridge by ReynArts", null, null, focused: false, width: 820);
        rowPanel.Children.Add(r5);

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 250);
        board.Children.Add(rowPanel);

        // 5. Right Details Side at (920, 250, w=612)
        var sideStack = new StackPanel { Width = 612 };

        var card = new Border
        {
            Width = 612,
            Padding = new Thickness(28, 26, 28, 26),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var cardStack = new StackPanel();
        cardStack.Children.Add(new TextBlock
        {
            Text = "X20CTL",
            FontFamily = NexusKit.Display,
            FontSize = 32,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = $"Native Controller Studio · {Scanner.ScanFlow.AppVersion}",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 4, 0, 16)
        });

        cardStack.Children.Add(new TextBlock
        {
            Text = "X20CTL is an independent, open-source controller studio for EasySMX pads. Remap buttons, shape stick and trigger response, build back-paddle macros, set vibration, and check that a controller works, all on your own PC. There is no account, and nothing leaves your PC unless you choose to send a report or feedback.\n\nEvery change stays a local draft until it can be sent to a connected, verified controller. X20CTL never writes to a controller it cannot read back.",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.78)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        cardStack.Children.Add(new TextBlock
        {
            Text = "Credits · Desktop-control layout inspired by Gopher360 — Tylemagne, GPL-3.0.",
            FontFamily = NexusKit.Body,
            FontSize = 13,
            Foreground = NexusKit.SubBrush
        });

        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 250);
        board.Children.Add(sideStack);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Open"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        board.Children.Add(hints);
    }

    private void BuildNexusFeedbackContent(Canvas board)
    {
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
        board.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: 0,
            model: "EasySMX X20 Pro",
            status: "Offline",
            onZone: () => NexusNavigate?.Invoke("Zone"),
            onSetups: () => NexusNavigate?.Invoke("Setups"),
            onPcMode: () => ShowPcMode(true),
            onTools: () => ShowFeedback(false)
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        board.Children.Add(topBar);

        // 3. Title & Lead
        var title = new TextBlock
        {
            Text = "Send feedback",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 138);
        board.Children.Add(title);

        var lead = new TextBlock
        {
            Text = "Tell X20CTL what's broken, what you'd like, or how your controller behaves. It goes straight to the X20CTL inbox.",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        board.Children.Add(lead);

        // 4. Left Column at (68, 250, w=820)
        var rowPanel = new StackPanel { Width = 820 };

        var r1 = NexusKit.RowButton("What is it about?", null, NexusKit.Dropdown("Something's broken"), focused: false, width: 820);
        rowPanel.Children.Add(r1);

        var r2 = NexusKit.RowButton("Controller (optional)", null, NexusKit.Dropdown("Not chosen"), focused: false, width: 820);
        rowPanel.Children.Add(r2);

        var r3 = NexusKit.RowButton("Connected by (optional)", null, NexusKit.Dropdown("Not chosen"), focused: false, width: 820);
        rowPanel.Children.Add(r3);

        // Focused: Your message
        var r4 = NexusKit.RowButton("Your message", "65 / 4000", null, focused: true, width: 820);
        rowPanel.Children.Add(r4);

        var sendBtn = NexusKit.TheatricalButton("Send to X20CTL", primary: true, height: 48, fontSize: 16);
        rowPanel.Children.Add(sendBtn);

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 250);
        board.Children.Add(rowPanel);

        // 5. Right Details Side at (920, 250, w=612)
        var sideStack = new StackPanel { Width = 612 };

        var card = new Border
        {
            Width = 612,
            Padding = new Thickness(28, 26, 28, 26),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var cardStack = new StackPanel();
        cardStack.Children.Add(new TextBlock
        {
            Text = "Your message",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });

        // Quote box with green ring
        var quoteBox = new Border
        {
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 12, 0, 12),
            CornerRadius = new CornerRadius(4),
            Background = NexusKit.InputBrush,
            BorderBrush = NexusKit.GreenBrush,
            BorderThickness = new Thickness(3),
            Child = new TextBlock
            {
                Text = "The right trigger on my X20 Pro reads about 5% before I touch it.",
                FontFamily = NexusKit.Body,
                FontSize = 18,
                Foreground = NexusKit.WhiteBrush,
                TextWrapping = TextWrapping.Wrap
            }
        };
        cardStack.Children.Add(quoteBox);

        cardStack.Children.Add(new TextBlock
        {
            Text = "65 / 4000",
            FontFamily = NexusKit.Display,
            FontSize = 46,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 16)
        });

        cardStack.Children.Add(new TextBlock
        {
            Text = "What's sent: your message, what it's about, the controller and connection if you pick them, the X20CTL version and the Windows version. No account, files or personal details.",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            TextWrapping = TextWrapping.Wrap
        });

        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 250);
        board.Children.Add(sideStack);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Select"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        board.Children.Add(hints);
    }
}

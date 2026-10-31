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
using X20Ctl.Product.Volt;

namespace X20Ctl.Product;

public sealed partial class ProductSettings
{
    internal FrameworkElement? voltRoot;
    private string voltView = "hub";

    private void BuildVolt()
    {
        if (pageGrid != null) pageGrid.Visibility = Visibility.Collapsed;
        hub.Visibility = Visibility.Collapsed;
        about.Visibility = Visibility.Collapsed;
        feedback.Visibility = Visibility.Collapsed;
        pcMode.Visibility = Visibility.Collapsed;
        themes.Visibility = Visibility.Collapsed;
        footer.Visibility = Visibility.Collapsed;

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Background = VoltKit.BlackSkyBrush };
        voltRoot = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        Children.Add(voltRoot);

        VoltSync();
    }

    internal void VoltSync()
    {
        if (voltRoot is not Viewbox box || box.Child is not Canvas board) return;
        board.Children.Clear();

        voltView = pcMode.Visibility == Visibility.Visible ? "pcmode" :
                   feedback.Visibility == Visibility.Visible ? "feedback" :
                   about.Visibility == Visibility.Visible ? "about" :
                   themes.Visibility == Visibility.Visible ? "themes" : "hub";

        switch (voltView)
        {
            case "about":
                RenderVoltAbout(board);
                break;
            case "themes":
                RenderVoltThemes(board);
                break;
            case "feedback":
                RenderVoltFeedback(board);
                break;
            case "pcmode":
                RenderVoltPcMode(board);
                break;
            case "hub":
            default:
                RenderVoltHub(board);
                break;
        }
    }

    /// <summary>
    /// Tools Hub for Volt: Steam Deck tools library with top banner card and library of square capsule tiles.
    /// Strictly implements t_volt.py:327 hub().
    /// </summary>
    private void RenderVoltHub(Canvas board)
    {
        // 1. Soft blurred yellow wave backdrop
        board.Children.Add(VoltKit.WaveBackdrop(16));

        // 2. Top bar
        board.Children.Add(VoltKit.TopBar());

        // 3. Left side menu (with "tools" active)
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
        var menu = VoltKit.SideMenu(pages, "tools", key =>
        {
            if (key != "tools")
            {
                if (!StepBack()) Back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        });
        Canvas.SetLeft(menu, 0);
        Canvas.SetTop(menu, 98);
        board.Children.Add(menu);

        // 4. Feature Banner Card at (540, 110, w: 1010, h: 250)
        var featCard = new Border
        {
            Width = 1010,
            Height = 250,
            CornerRadius = new CornerRadius(14),
            ClipToBounds = true,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0.8),
                GradientStops =
                {
                    new(VoltKit.C("#151515"), 0.0),
                    new(VoltKit.C("#1c1c1c"), 0.4),
                    new(VoltKit.C("#3a2a00"), 1.0)
                }
            },
            BorderBrush = VoltKit.AmberHotBrush,
            BorderThickness = new Thickness(2),
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 30, ShadowDepth = 0, Opacity = 0.35 }
        };

        var fCanvas = new Canvas { Width = 1010, Height = 250 };

        var padArt = VoltKit.ControllerArt("dune", 420);
        if (padArt != null)
        {
            padArt.RenderTransformOrigin = new Point(0.5, 0.5);
            padArt.RenderTransform = new RotateTransform(-10);
            padArt.Opacity = 0.95;
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 30, ShadowDepth = 15, Opacity = 0.6 };
            Canvas.SetLeft(padArt, 600);
            Canvas.SetTop(padArt, -40);
            fCanvas.Children.Add(padArt);
        }

        var fStack = new StackPanel { Orientation = Orientation.Vertical, Width = 560 };
        fStack.Children.Add(new TextBlock
        {
            Text = "TOOLS & UTILITIES",
            FontFamily = VoltKit.Display,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.AmberHotBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });
        fStack.Children.Add(new TextBlock
        {
            Text = "Controller check",
            FontFamily = VoltKit.Display,
            FontSize = 36,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });
        fStack.Children.Add(new TextBlock
        {
            Text = "Read scan records what Windows sees. Write scan is a guided test and writes nothing.",
            FontFamily = VoltKit.Body,
            FontSize = 17,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 24
        });
        Canvas.SetLeft(fStack, 34);
        Canvas.SetTop(fStack, 28);
        fCanvas.Children.Add(fStack);

        // Quick feature chips at bottom left of banner
        var chipStack = new StackPanel { Orientation = Orientation.Horizontal };
        var chips = System.Array.Empty<(string, bool)>();
        foreach (var (cName, active) in chips)
        {
            var chip = new Border
            {
                Padding = new Thickness(16, 7, 16, 7),
                CornerRadius = new CornerRadius(12),
                Background = active ? VoltKit.AmberHotBrush : VoltKit.B("#252525"),
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = cName,
                    FontFamily = VoltKit.Display,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = active ? VoltKit.B("#111111") : VoltKit.WhiteBrush
                }
            };
            chipStack.Children.Add(chip);
        }
        Canvas.SetLeft(chipStack, 34);
        Canvas.SetBottom(chipStack, 24);
        fCanvas.Children.Add(chipStack);

        featCard.Child = fCanvas;
        Canvas.SetLeft(featCard, 540);
        Canvas.SetTop(featCard, 110);
        board.Children.Add(featCard);

        // 5. Library Tiles (8 tiles, 242x156 at 540 + col*258, 388 + row*172)
        var tiles = new (string Icon, string Name, string Sub, Action OnClick)[]
        {
            ("\uE7FC", "Hardware Check", "Read scan", () => Check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))),
            ("\uE7F4", "PC Mode", "ViGEmBus routing", () => ShowPcMode(true)),
            ("\uE771", "Themes", "Console themes catalog", () => ShowThemes(true)),
            ("\uE946", "About", "Version & credits", () => ShowAbout(true)),
            ("\uE939", "Feedback", "Send feedback", () => ShowFeedback(true)),
            ("\uE710", "Replay Intro", "Cinematic intro", () => Replay.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))),
            ("\uE8EA", "Write Scan", "Guided test", () => WriteScan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))),
            ("\uE895", "Updates", "Check latest release", () => { })
        };

        for (int i = 0; i < tiles.Length; i++)
        {
            var (icon, name, sub, onClick) = tiles[i];
            int col = i % 4;
            int row = i / 4;

            var tile = new Border
            {
                Width = 242,
                Height = 156,
                CornerRadius = new CornerRadius(12),
                Background = VoltKit.B("#151515"),
                Padding = new Thickness(20),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect { Color = VoltKit.C("rgba(60,40,0,.35)"), BlurRadius = 24, ShadowDepth = 10, Opacity = 0.4 }
            };

            var tCanvas = new Canvas { Width = 202, Height = 116 };

            var iconBlock = new TextBlock
            {
                Text = icon,
                FontFamily = VoltKit.Icons,
                FontSize = 34,
                Foreground = VoltKit.WhiteBrush
            };
            Canvas.SetLeft(iconBlock, 0);
            Canvas.SetTop(iconBlock, 0);
            tCanvas.Children.Add(iconBlock);

            var titleBlock = new TextBlock
            {
                Text = name,
                FontFamily = VoltKit.Display,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush
            };
            Canvas.SetLeft(titleBlock, 0);
            Canvas.SetTop(titleBlock, 58);
            tCanvas.Children.Add(titleBlock);

            var subBlock = new TextBlock
            {
                Text = sub,
                FontFamily = VoltKit.Body,
                FontSize = 13,
                Foreground = VoltKit.MuteBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = 200
            };
            Canvas.SetLeft(subBlock, 0);
            Canvas.SetTop(subBlock, 84);
            tCanvas.Children.Add(subBlock);

            tile.Child = tCanvas;
            tile.MouseLeftButtonUp += (_, _) => onClick();

            Canvas.SetLeft(tile, 540 + col * 258);
            Canvas.SetTop(tile, 388 + row * 172);
            board.Children.Add(tile);
        }

        // 6. Carousel dots & Bottom hint bar
        var dots = VoltKit.CarouselDots(3, 4);
        Canvas.SetLeft(dots, 800 - 40);
        Canvas.SetTop(dots, 812);
        board.Children.Add(dots);

        var bar = VoltKit.BottomBar(new[] { ("A", "OPEN"), ("B", "BACK") });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 854);
        board.Children.Add(bar);
    }

    /// <summary>
    /// Themes page for Volt: Centered title pill, theme selection list dialog with solid orange active pill,
    /// and theme preview toast notification at bottom.
    /// Strictly implements t_volt.py:173 tools(thumbs).
    /// </summary>
    private void RenderVoltThemes(Canvas board)
    {
        // 1. Soft blurred yellow wave backdrop
        board.Children.Add(VoltKit.WaveBackdrop(22));

        // 2. Dimmed overlay
        var overlay = new Rectangle { Width = 1600, Height = 900, Fill = VoltKit.B("rgba(0,0,0,.65)") };
        board.Children.Add(overlay);

        // 3. Top bar (right: false)
        board.Children.Add(VoltKit.TopBar(right: false));

        // 4. Centered Title pill: [Themes] at (800 - 80, 72)
        var tp = new Border
        {
            Padding = new Thickness(36, 12, 36, 12),
            CornerRadius = new CornerRadius(16),
            Background = VoltKit.B("#1b1b1b"),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 8, Opacity = 0.5 },
            Child = new TextBlock
            {
                Text = "Themes",
                FontFamily = VoltKit.Display,
                FontSize = 30,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        Canvas.SetLeft(tp, 700);
        Canvas.SetTop(tp, 72);
        board.Children.Add(tp);

        // 5. Themes selection dialog list at (530, 150, w: 540)
        var dlg = new Border
        {
            Width = 540,
            Background = VoltKit.CardDarkBrush,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 60, ShadowDepth = 26, Opacity = 0.5 }
        };

        var dStack = new StackPanel { Orientation = Orientation.Vertical };
        var themesList = new (string Id, string Name)[]
        {
            ("default", "Default"),
            ("volt", "Volt"),
            ("dusk", "Dusk"),
            ("hybrid", "Hybrid"),
            ("pulse", "Pulse"),
            ("spotlight", "Spotlight"),
            ("nexus", "Nexus"),
            ("rebel", "Rebel"),
            ("tide", "Tide")
        };

        foreach (var (tId, tName) in themesList)
        {
            bool isCurrent = tId == "volt";
            var item = new Border
            {
                Height = 56,
                CornerRadius = new CornerRadius(12),
                Background = isCurrent ? VoltKit.AmberHotBrush : Brushes.Transparent,
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = Cursors.Hand
            };
            item.Child = new TextBlock
            {
                Text = tName,
                FontFamily = VoltKit.Display,
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Foreground = isCurrent ? VoltKit.B("#111111") : VoltKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            string capturedId = tId;
            item.MouseLeftButtonUp += (_, _) =>
            {
                var theme = ThemeCatalog.Find(capturedId);
                if (theme != null) ThemeCatalog.Apply(theme);
                if (capturedId != "volt")
                {
                    ThemeCatalog.Restart();
                }
            };
            dStack.Children.Add(item);
        }

        dlg.Child = dStack;
        Canvas.SetLeft(dlg, 530);
        Canvas.SetTop(dlg, 140);
        board.Children.Add(dlg);

        // 6. Toast at (550, 744, w: 500, h: 72)
        var toast = new Border
        {
            Width = 500,
            Height = 72,
            Background = VoltKit.B("#1c1c1c"),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 8, Opacity = 0.5 }
        };
        var tGrid = new Grid();
        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(128) });
        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Thumbnail image
        var thumbImg = new Border
        {
            Width = 128,
            Height = 72,
            Background = VoltKit.AmberHotBrush,
            Child = new TextBlock
            {
                Text = "VOLT",
                FontFamily = VoltKit.LogoFont,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.B("#111111"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(thumbImg, 0);
        tGrid.Children.Add(thumbImg);

        var tInfo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 0, 0) };
        tInfo.Children.Add(new TextBlock
        {
            Text = "\uE771",
            FontFamily = VoltKit.Icons,
            FontSize = 22,
            Foreground = VoltKit.AmberHotBrush,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        tInfo.Children.Add(new TextBlock
        {
            Text = "Volt · In use",
            FontFamily = VoltKit.Display,
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(tInfo, 1);
        tGrid.Children.Add(tInfo);

        toast.Child = tGrid;
        Canvas.SetLeft(toast, 550);
        Canvas.SetTop(toast, 744);
        board.Children.Add(toast);

        // 7. Chevrons and bottom hint bar
        var leftChev = VoltKit.Chevron(false, () => { if (!StepBack()) Back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); });
        Canvas.SetLeft(leftChev, 28);
        Canvas.SetTop(leftChev, 452);
        board.Children.Add(leftChev);

        var rightChev = VoltKit.Chevron(true);
        Canvas.SetLeft(rightChev, 1518);
        Canvas.SetTop(rightChev, 452);
        board.Children.Add(rightChev);

        var bar = VoltKit.BottomBar(new[] { ("A", "APPLY"), ("B", "BACK") });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 854);
        board.Children.Add(bar);
    }

    /// <summary>
    /// About page for Volt: Two-pane shell with author credits and release telemetry.
    /// Strictly implements models.py:about.
    /// </summary>
    private void RenderVoltAbout(Canvas board)
    {
        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Left List: People and Project (width: 520)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        leftStack.Children.Add(new TextBlock
        {
            Text = "PROJECT CONTRIBUTORS",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var people = new (string Name, string Role, bool Active)[]
        {
            ("Amjad AAYD", "Lead Designer & Software Engineer", true),
            ("EasySMX Community", "Hardware Testing & Telemetry Verification", false),
            ("ViGEm Project", "Virtual Gamepad Emulation Bus Architecture", false),
            ("Open Source Contributors", "Gamepad Protocol Research & Documentation", false)
        };

        foreach (var (pName, pRole, active) in people)
        {
            var row = new Border
            {
                MinHeight = 62,
                Background = active ? VoltKit.B("#2a2a2a") : Brushes.Transparent,
                BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                BorderThickness = new Thickness(active ? 2 : 0, active ? 2 : 0, active ? 2 : 0, active ? 2 : 1),
                CornerRadius = active ? new CornerRadius(12) : new CornerRadius(0),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 0, 2),
                Cursor = Cursors.Hand
            };

            var lCol = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            lCol.Children.Add(new TextBlock
            {
                Text = pName,
                FontFamily = VoltKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.WhiteBrush : VoltKit.LightBrush
            });
            lCol.Children.Add(new TextBlock
            {
                Text = pRole,
                FontFamily = VoltKit.Body,
                FontSize = 14,
                Foreground = VoltKit.MuteBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });

            row.Child = lCol;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 10);
        content.Children.Add(leftStack);

        // 2. Right Details Card: X20CTL Software Info
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };

        var card = new Border
        {
            Width = 410,
            Background = VoltKit.SheetDarkBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24, 20, 24, 20)
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };

        cStack.Children.Add(new TextBlock
        {
            Text = "X20CTL",
            FontFamily = VoltKit.LogoFont,
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.AmberHotBrush
        });

        cStack.Children.Add(new TextBlock
        {
            Text = $"Native Controller Studio · {Updater.Shown}",
            FontFamily = VoltKit.Body,
            FontSize = 15,
            Foreground = VoltKit.MuteBrush,
            Margin = new Thickness(0, 2, 0, 16)
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "X20CTL is an independent, open-source gamepad studio for EasySMX controllers. Remap buttons, tune curves, record macros and test input without an account.",
            FontFamily = VoltKit.Body,
            FontSize = 15,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var checkBtn = VoltKit.OrangeActionButton("Check for Updates", 362, 54);
        cStack.Children.Add(checkBtn);

        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        var shell = VoltKit.TwoPaneShell("tools", "About X20CTL", "Independent, open-source gamepad suite for EasySMX hardware.", content, _ =>
        {
            if (!StepBack()) Back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        board.Children.Add(shell);
    }

    /// <summary>
    /// Feedback page for Volt: Send user feedback, suggestions, or diagnostic telemetry.
    /// Strictly implements models.py:feedback.
    /// </summary>
    private void RenderVoltFeedback(Canvas board)
    {
        var content = new Canvas { Width = 970, Height = 560 };

        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        leftStack.Children.Add(new TextBlock
        {
            Text = "FEEDBACK DETAILS",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var fbRows = new (string Label, string CtlType, string CtlVal)[]
        {
            ("Topic Category", "dd", "General Experience"),
            ("Controller Model", "dd", "Not selected"),
            ("Connection Type", "dd", "Not selected")
        };

        foreach (var (lbl, ctlType, ctlVal) in fbRows)
        {
            var row = new Border
            {
                MinHeight = 56,
                Background = Brushes.Transparent,
                BorderBrush = VoltKit.B("#222222"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 6, 16, 6)
            };
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = lbl,
                FontFamily = VoltKit.Display,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var dd = new Border
            {
                MinWidth = 200,
                Height = 40,
                CornerRadius = new CornerRadius(8),
                Background = VoltKit.B("#262626"),
                Padding = new Thickness(14, 0, 14, 0),
                Child = new Grid
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = ctlVal,
                            FontFamily = VoltKit.Display,
                            FontSize = 15,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = VoltKit.WhiteBrush,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "\uE70D",
                            FontFamily = VoltKit.Icons,
                            FontSize = 11,
                            Foreground = VoltKit.MuteBrush,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            Grid.SetColumn(dd, 1);
            grid.Children.Add(dd);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 10);
        content.Children.Add(leftStack);

        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };
        var card = new Border
        {
            Width = 410,
            Background = VoltKit.SheetDarkBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24, 20, 24, 20)
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };

        cStack.Children.Add(new TextBlock
        {
            Text = "YOUR MESSAGE",
            FontFamily = VoltKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "Send Feedback",
            FontFamily = VoltKit.Display,
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var msgBox = new Border
        {
            Height = 120,
            Background = VoltKit.B("#121212"),
            BorderBrush = VoltKit.B("#333333"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12)
        };
        msgBox.Child = new TextBlock
        {
            Text = "Type your feedback, bug description, or feature suggestion here...",
            FontFamily = VoltKit.Body,
            FontSize = 14,
            Foreground = VoltKit.MuteBrush
        };
        cStack.Children.Add(msgBox);

        var sendBtn = VoltKit.OrangeActionButton("Send Feedback", 362, 54);
        sendBtn.Margin = new Thickness(0, 16, 0, 0);
        cStack.Children.Add(sendBtn);

        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        var shell = VoltKit.TwoPaneShell("tools", "Send Feedback", "Diagnostic telemetry and user impressions help improve hardware support.", content, _ =>
        {
            if (!StepBack()) Back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        board.Children.Add(shell);
    }

    private void RenderVoltPcMode(Canvas board)
    {
        var content = new Canvas { Width = 970, Height = 560 };

        // Left List: Mode options
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };
        leftStack.Children.Add(new TextBlock
        {
            Text = "PC MODE CONFIGURATION",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var pcRows = new (string Label, string Val, bool Active)[]
        {
            ("Virtual Controller Emulation", "ViGEmBus", false),
            ("Target Protocol", "Xbox 360 Controller", false),
            ("Input Cloaking", "HidHide", false)
        };

        foreach (var (lbl, val, active) in pcRows)
        {
            var row = new Border
            {
                MinHeight = 56,
                Background = active ? VoltKit.B("#2a2a2a") : Brushes.Transparent,
                BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                BorderThickness = new Thickness(active ? 2 : 0, active ? 2 : 0, active ? 2 : 0, active ? 2 : 1),
                CornerRadius = active ? new CornerRadius(12) : new CornerRadius(0),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 0, 2)
            };
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = lbl,
                FontFamily = VoltKit.Display,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.WhiteBrush : VoltKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var valBlock = new TextBlock
            {
                Text = val,
                FontFamily = VoltKit.Display,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.AmberHotBrush : VoltKit.B("#bdbdbd"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(valBlock);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 10);
        content.Children.Add(leftStack);

        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };
        var card = new Border
        {
            Width = 410,
            Background = VoltKit.SheetDarkBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24, 20, 24, 20)
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };

        cStack.Children.Add(new TextBlock
        {
            Text = "VIRTUAL BUS STATUS",
            FontFamily = VoltKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "ViGEmBus Online",
            FontFamily = VoltKit.Display,
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "Virtual Xbox 360 controller peripheral is created and recognized by Windows Game Controller subsystem with full vibration and analog support.",
            FontFamily = VoltKit.Body,
            FontSize = 15,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var applyBtn = VoltKit.OrangeActionButton("Restart PC Mode", 362, 54);
        cStack.Children.Add(applyBtn);

        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        var shell = VoltKit.TwoPaneShell("tools", "PC Mode", "Protocol routing, ViGEm virtual controller emulation, and system input mapping.", content, _ =>
        {
            if (!StepBack()) Back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        board.Children.Add(shell);
    }
}

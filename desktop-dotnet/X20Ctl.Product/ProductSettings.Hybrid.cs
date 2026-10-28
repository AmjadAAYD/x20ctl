using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Hybrid;

namespace X20Ctl.Product;

public sealed partial class ProductSettings
{
    internal FrameworkElement? hybridRoot;
    private string hybridView = "hub";

    private void BuildHybrid()
    {
        if (pageGrid != null) pageGrid.Visibility = Visibility.Collapsed;
        hub.Visibility = Visibility.Collapsed;
        about.Visibility = Visibility.Collapsed;
        feedback.Visibility = Visibility.Collapsed;
        pcMode.Visibility = Visibility.Collapsed;
        themes.Visibility = Visibility.Collapsed;
        footer.Visibility = Visibility.Collapsed;

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        hybridRoot = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        Children.Add(hybridRoot);

        HybridSync();
    }

    internal void HybridSync()
    {
        if (hybridRoot is not Viewbox box || box.Child is not Canvas board) return;
        board.Children.Clear();

        hybridView = pcMode.Visibility == Visibility.Visible ? "pcmode" :
                     feedback.Visibility == Visibility.Visible ? "feedback" :
                     about.Visibility == Visibility.Visible ? "about" :
                     themes.Visibility == Visibility.Visible ? "themes" : "hub";

        switch (hybridView)
        {
            case "about":
                RenderHybridAbout(board);
                break;
            case "themes":
                RenderHybridThemes(board);
                break;
            case "feedback":
                RenderHybridFeedback(board);
                break;
            case "pcmode":
                RenderHybridPcMode(board);
                break;
            case "hub":
            default:
                RenderHybridHub(board);
                break;
        }
    }

    private void RenderHybridHub(Canvas board)
    {
        var hints = new[] { ("A", "Open"), ("B", "Back") };
        board.Children.Add(HybridKit.HandheldRails("tools", hints, "Tools & Utilities", "Diagnostic scanners, theme customization, firmware tools and user feedback.", _ => { }));

        // Banner at (216, 150, w: 1168, h: 176)
        var banner = new Border
        {
            Width = 1168,
            Height = 176,
            CornerRadius = new CornerRadius(26),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(28, 20, 28, 20)
        };
        var bGrid = new Grid();
        bGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var bHeader = new StackPanel { Orientation = Orientation.Vertical };
        bHeader.Children.Add(new TextBlock
        {
            Text = "Controller Verification & Scanners",
            FontFamily = HybridKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.ExtraBold,
            Foreground = HybridKit.WhiteBrush
        });
        bHeader.Children.Add(new TextBlock
        {
            Text = "Diagnose controller handshake, button matrices, Hall-effect variance and official app packets.",
            FontFamily = HybridKit.Body,
            FontSize = 14,
            Foreground = HybridKit.MuteBrush,
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetRow(bHeader, 0);
        bGrid.Children.Add(bHeader);

        // Snap block chain in banner
        var chainPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var chainSteps = new (string Num, string Title, Action Act)[]
        {
            ("1", "Read Scan", () => Check.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("2", "Write Scan", () => WriteScan.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("3", "Online Submit", () => { })
        };

        for (int i = 0; i < chainSteps.Length; i++)
        {
            var (num, text, act) = chainSteps[i];
            var stepBorder = new Border
            {
                Width = 260,
                Height = 52,
                CornerRadius = new CornerRadius(14),
                Background = i == 0 ? HybridKit.LavenderBrush : HybridKit.B("#2a2632"),
                Margin = new Thickness(0, 0, 14, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(14, 0, 14, 0),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new Border
                        {
                            Width = 24,
                            Height = 24,
                            CornerRadius = new CornerRadius(12),
                            Background = i == 0 ? HybridKit.InkBrush : HybridKit.B("#3d3846"),
                            Margin = new Thickness(0, 0, 10, 0),
                            Child = new TextBlock
                            {
                                Text = num,
                                FontFamily = HybridKit.Display,
                                FontSize = 12,
                                FontWeight = FontWeights.Bold,
                                Foreground = i == 0 ? Brushes.White : HybridKit.B("#c2bccd"),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        },
                        new TextBlock
                        {
                            Text = text,
                            FontFamily = HybridKit.Display,
                            FontSize = 15,
                            FontWeight = FontWeights.Bold,
                            Foreground = i == 0 ? HybridKit.InkBrush : HybridKit.WhiteBrush,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            stepBorder.MouseLeftButtonUp += (_, _) => act();
            chainPanel.Children.Add(stepBorder);
        }
        Grid.SetRow(chainPanel, 1);
        bGrid.Children.Add(chainPanel);

        banner.Child = bGrid;
        Canvas.SetLeft(banner, 216);
        Canvas.SetTop(banner, 150);
        board.Children.Add(banner);

        // 8 Tiles Grid at (216 + (i%4)*298, 356 + (i//4)*236, w: 282, h: 220)
        var tiles = new (string Icon, string Title, string Desc, Action Act)[]
        {
            ("\uE790", "Themes", "Change the console visual style and opening video.", () => ShowThemes(true)),
            ("\uE7FC", "PC Mode", "DirectInput & Switch communication mode settings.", () => ShowPcMode(true)),
            ("\uE715", "Feedback", "Report an issue, propose a feature or send logs.", () => ShowFeedback(true)),
            ("\uE946", "About X20CTL", "Version, native runtime, license and contributors.", () => ShowAbout(true)),
            ("\uE768", "Replay Intro", "Watch the console boot opening animation again.", () => Replay.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE735", "Star on GitHub", "Star the repository on GitHub at AmjadAAYD/x20ctl.", () => Star.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE74E", "Check Updates", "Check GitHub releases for application updates.", async () => await CheckForUpdate()),
            ("\uE772", "Support X20CTL", "Support development on Ko-fi.", () => Support.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)))
        };

        for (int i = 0; i < tiles.Length; i++)
        {
            var (ico, tTitle, desc, act) = tiles[i];
            double tx = 216 + (i % 4) * 298;
            double ty = 356 + (i / 4) * 236;

            var tCard = new Border
            {
                Width = 282,
                Height = 220,
                CornerRadius = new CornerRadius(22),
                Background = HybridKit.B("#1f1c25"),
                BorderBrush = HybridKit.B("#2e2a36"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(22),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var tStack = new StackPanel { Orientation = Orientation.Vertical };

            var iconCircle = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(24),
                Background = HybridKit.LavenderBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 16),
                Child = new TextBlock
                {
                    Text = ico,
                    FontFamily = HybridKit.Icons,
                    FontSize = 22,
                    Foreground = HybridKit.InkBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            tStack.Children.Add(iconCircle);

            tStack.Children.Add(new TextBlock
            {
                Text = tTitle,
                FontFamily = HybridKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.ExtraBold,
                Foreground = HybridKit.WhiteBrush
            });

            tStack.Children.Add(new TextBlock
            {
                Text = desc,
                FontFamily = HybridKit.Body,
                FontSize = 13,
                Foreground = HybridKit.MuteBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            });

            tCard.Child = tStack;
            tCard.MouseLeftButtonUp += (_, _) => act();

            Canvas.SetLeft(tCard, tx);
            Canvas.SetTop(tCard, ty);
            board.Children.Add(tCard);
        }
    }

    private void RenderHybridThemes(Canvas board)
    {
        var hints = new[] { ("A", "Apply"), ("B", "Back") };
        board.Children.Add(HybridKit.HandheldRails("tools", hints, "Themes", "Select the visual theme and console personality for X20CTL.", _ => { }));

        // Stand cards carousel along table at y = 690
        board.Children.Add(HybridKit.TabletopStage(690));

        var themeList = ThemeCatalog.All.ToList();
        double w = 240;
        double h = 340;
        double gap = 24;
        double x0 = 800 - (themeList.Count * w + (themeList.Count - 1) * gap) / 2;
        if (x0 < 216) x0 = 216;

        for (int i = 0; i < themeList.Count; i++)
        {
            var t = themeList[i];
            bool active = t.Id == ThemeCatalog.Current.Id;
            double x = x0 + i * (w + gap);
            double y = 690 - h + 16;

            var cardInner = new Canvas { Width = w, Height = h, ClipToBounds = true };

            // Preview background
            var bg = new Rectangle
            {
                Width = w,
                Height = h,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = { new(HybridKit.C("#2c2834"), 0.0), new(HybridKit.C("#1c1922"), 1.0) }
                }
            };
            cardInner.Children.Add(bg);

            // Thumbnail image if available
            string thumbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", t.Id, "thumb.png");
            if (System.IO.File.Exists(thumbPath))
            {
                var thumb = new Image
                {
                    Width = w - 24,
                    Height = 180,
                    Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(thumbPath))),
                    Stretch = Stretch.UniformToFill
                };
                Canvas.SetLeft(thumb, 12);
                Canvas.SetTop(thumb, 12);
                cardInner.Children.Add(thumb);
            }

            // Theme details
            var metaStack = new StackPanel { Orientation = Orientation.Vertical, Width = w - 24 };
            metaStack.Children.Add(new TextBlock
            {
                Text = t.Name,
                FontFamily = HybridKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.ExtraBold,
                Foreground = HybridKit.WhiteBrush
            });
            metaStack.Children.Add(new TextBlock
            {
                Text = active ? "Current Active Theme" : "Console Experience",
                FontFamily = HybridKit.Body,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? HybridKit.CyanBrush : HybridKit.MuteBrush,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Canvas.SetLeft(metaStack, 14);
            Canvas.SetTop(metaStack, 210);
            cardInner.Children.Add(metaStack);

            var standCard = HybridKit.StandCard(x, y, w, h, cardInner, on: active, onClick: () =>
            {
                preferences?.SetTheme(t.Id);
                RequestThemeRestart();
            });
            board.Children.Add(standCard);
        }
    }

    private void RenderHybridAbout(Canvas board)
    {
        // Docked mode background halves
        board.Children.Add(HybridKit.Halves(700, 460));
        board.Children.Add(HybridKit.Brand(44, 30, true));
        board.Children.Add(HybridKit.TopRight(false, 0));

        // Back link
        var back = new StackPanel { Orientation = Orientation.Horizontal, Cursor = System.Windows.Input.Cursors.Hand };
        back.Children.Add(new TextBlock { Text = "\uE72B", FontFamily = HybridKit.Icons, FontSize = 14, Foreground = HybridKit.B("#5c4075"), Margin = new Thickness(0, 0, 8, 0) });
        back.Children.Add(new TextBlock { Text = "Tools / About", FontFamily = HybridKit.Body, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = HybridKit.B("#5c4075") });
        back.MouseLeftButtonUp += (_, _) => ShowAbout(false);
        Canvas.SetLeft(back, 64);
        Canvas.SetTop(back, 44);
        board.Children.Add(back);

        // Left info
        var leftStack = new StackPanel { Width = 560, Orientation = Orientation.Vertical };
        leftStack.Children.Add(new TextBlock
        {
            Text = "ABOUT X20CTL",
            FontFamily = HybridKit.Body,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.B("#5c4075")
        });
        leftStack.Children.Add(new TextBlock
        {
            Text = "v2.4.0",
            FontFamily = HybridKit.Display,
            FontSize = 100,
            FontWeight = FontWeights.Black,
            Foreground = HybridKit.InkBrush,
            Margin = new Thickness(0, -10, 0, 4)
        });
        leftStack.Children.Add(new TextBlock
        {
            Text = "Universal Gamepad Configuration, Telemetry & Diagnostics",
            FontFamily = HybridKit.Display,
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });
        leftStack.Children.Add(new TextBlock
        {
            Text = "Built with Microsoft .NET 8 WPF, C# 12, native DirectInput / XInput report pipelines, and zero-latency Hall-sensor calibration telemetry.",
            FontFamily = HybridKit.Body,
            FontSize = 15,
            Foreground = HybridKit.B("#4b3b5e"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 24)
        });

        // Authors pill
        var authPill = new Border
        {
            Padding = new Thickness(16, 10, 16, 10),
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.FromArgb(0x2b, 92, 64, 117)),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = "Engineered by Amjad & Antigravity Autonomous Systems",
                FontFamily = HybridKit.Body,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = HybridKit.InkBrush
            }
        };
        leftStack.Children.Add(authPill);

        Canvas.SetLeft(leftStack, 64);
        Canvas.SetTop(leftStack, 130);
        board.Children.Add(leftStack);

        // Right details card at (760, 130, w: 760, h: 680)
        var rightCard = new Border
        {
            Width = 760,
            Height = 680,
            CornerRadius = new CornerRadius(28),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(36),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 10, Opacity = 0.4 }
        };
        var rStack = new StackPanel { Orientation = Orientation.Vertical };
        rStack.Children.Add(new TextBlock
        {
            Text = "Open Source Acknowledgements & Licenses",
            FontFamily = HybridKit.Display,
            FontSize = 22,
            FontWeight = FontWeights.ExtraBold,
            Foreground = HybridKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 18)
        });

        var packages = new (string Name, string License, string Desc)[]
        {
            ("XInput.Wrapper", "MIT License", "High precision 1000 Hz XInput telemetry hooks."),
            ("SharpDX.DirectInput", "MIT License", "Raw DirectInput enumeration and report polling."),
            ("SkiaSharp", "MIT License", "Hardware-accelerated vector rendering and image pipelines."),
            ("System.Text.Json", "MIT License", "High performance zero-allocation JSON parser.")
        };

        foreach (var (pName, pLic, pDesc) in packages)
        {
            var pBorder = new Border
            {
                Height = 68,
                CornerRadius = new CornerRadius(12),
                Background = HybridKit.B("#282430"),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(18, 10, 18, 10)
            };
            var pGrid = new Grid();
            pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pText = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            pText.Children.Add(new TextBlock { Text = pName, FontFamily = HybridKit.Display, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = HybridKit.WhiteBrush });
            pText.Children.Add(new TextBlock { Text = pDesc, FontFamily = HybridKit.Body, FontSize = 12, Foreground = HybridKit.MuteBrush, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(pText, 0);
            pGrid.Children.Add(pText);

            var licBadge = new Border
            {
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                CornerRadius = new CornerRadius(13),
                Background = HybridKit.B("#383244"),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = pLic, FontFamily = HybridKit.Body, FontSize = 12, Foreground = HybridKit.CyanBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            Grid.SetColumn(licBadge, 1);
            pGrid.Children.Add(licBadge);

            pBorder.Child = pGrid;
            rStack.Children.Add(pBorder);
        }

        rightCard.Child = rStack;
        Canvas.SetLeft(rightCard, 760);
        Canvas.SetTop(rightCard, 130);
        board.Children.Add(rightCard);
    }

    private void RenderHybridFeedback(Canvas board)
    {
        var hints = new[] { ("A", "Select / Send"), ("B", "Back") };
        board.Children.Add(HybridKit.HandheldRails("tools", hints, "User Feedback", "Send telemetry logs, report bugs or request features directly to the team.", _ => { }));

        // Left form at (216, 166, w: 588)
        var leftForm = new StackPanel { Width = 588, Orientation = Orientation.Vertical };
        leftForm.Children.Add(new TextBlock { Text = "FEEDBACK CATEGORY", FontFamily = HybridKit.Display, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = HybridKit.B("#8e809f"), Margin = new Thickness(0, 0, 0, 12) });

        var catGrid = new UniformGrid { Columns = 2, Rows = 2, Margin = new Thickness(0, 0, 0, 24) };
        var cats = new (string Name, bool Act)[]
        {
            ("Bug Report", true),
            ("New Feature", false),
            ("Hardware Support", false),
            ("Other Feedback", false)
        };
        foreach (var (cName, act) in cats)
        {
            var cBorder = new Border
            {
                Height = 48,
                CornerRadius = new CornerRadius(14),
                Background = act ? HybridKit.LavenderBrush : HybridKit.B("#1f1c25"),
                Margin = new Thickness(4),
                Child = new TextBlock
                {
                    Text = cName,
                    FontFamily = HybridKit.Body,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = act ? HybridKit.InkBrush : HybridKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            catGrid.Children.Add(cBorder);
        }
        leftForm.Children.Add(catGrid);

        leftForm.Children.Add(new TextBlock { Text = "CONTROLLER MODEL", FontFamily = HybridKit.Display, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = HybridKit.B("#8e809f"), Margin = new Thickness(0, 0, 0, 10) });
        var modelDropdown = new Border
        {
            Height = 50,
            CornerRadius = new CornerRadius(14),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(18, 0, 18, 0),
            Child = new Grid
            {
                Children =
                {
                    new TextBlock { Text = "EasySMX D10 (Connected)", FontFamily = HybridKit.Body, FontSize = 14, Foreground = HybridKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "\uE70D", FontFamily = HybridKit.Icons, FontSize = 12, Foreground = HybridKit.CyanBrush, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        leftForm.Children.Add(modelDropdown);

        Canvas.SetLeft(leftForm, 216);
        Canvas.SetTop(leftForm, 166);
        board.Children.Add(leftForm);

        // Seam between form and message
        board.Children.Add(HybridKit.Seam(846, 166, 830, 480, true, 0.8));

        // Right message card at (892, 166, w: 492, h: 500)
        var msgCard = new Border
        {
            Width = 492,
            Height = 500,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(26)
        };
        var mStack = new StackPanel { Orientation = Orientation.Vertical };
        mStack.Children.Add(new TextBlock { Text = "MESSAGE DETAILS", FontFamily = HybridKit.Display, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = HybridKit.B("#8e809f"), Margin = new Thickness(0, 0, 0, 12) });

        var textBox = new Border
        {
            Height = 280,
            CornerRadius = new CornerRadius(16),
            Background = HybridKit.B("#fbf8ff"),
            BorderBrush = HybridKit.CyanBrush,
            BorderThickness = new Thickness(2),
            Padding = new Thickness(18),
            Child = new TextBlock
            {
                Text = "Describe the observed behavior or request...",
                FontFamily = HybridKit.Body,
                FontSize = 15,
                Foreground = HybridKit.B("#6d5786")
            }
        };
        mStack.Children.Add(textBox);

        // Submit button
        var btnSend = new Border
        {
            Height = 50,
            CornerRadius = new CornerRadius(25),
            Background = HybridKit.LavenderBrush,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0, 20, 0, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "\uE725", FontFamily = HybridKit.Icons, FontSize = 16, Foreground = HybridKit.InkBrush, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "Send Feedback Report", FontFamily = HybridKit.Body, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = HybridKit.InkBrush, VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        mStack.Children.Add(btnSend);

        msgCard.Child = mStack;
        Canvas.SetLeft(msgCard, 892);
        Canvas.SetTop(msgCard, 166);
        board.Children.Add(msgCard);
    }

    private void RenderHybridPcMode(Canvas board)
    {
        var hints = new[] { ("A", "Switch Mode"), ("B", "Back") };
        board.Children.Add(HybridKit.HandheldRails("tools", hints, "PC Mode Configuration", "Driver communication protocol settings for Windows gaming and emulation.", _ => { }));
    }
}

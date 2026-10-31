using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowCurves()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Cinematic Sea Background
        board.Children.Add(TideKit.SeaBackground(3, "CURVE STUDIO"));

        var channelDefs = new (string Name, CurveChannel Ch, string Icon)[]
        {
            ("LEFT STICK", CurveChannel.LeftStick, "\uE9D9"),
            ("RIGHT STICK", CurveChannel.RightStick, "\uE9D9"),
            ("LEFT TRIGGER", CurveChannel.LeftTrigger, "\uE765"),
            ("RIGHT TRIGGER", CurveChannel.RightTrigger, "\uE765")
        };

        var curChannel = Curves.Channel;
        var curSetting = Curves.Draft[curChannel];

        // 2. Top Header Bar: Logo, Title, Channel Ribbon, Actions
        var topBar = new Canvas { Width = 1520, Height = 64 };
        Canvas.SetLeft(topBar, 40);
        Canvas.SetTop(topBar, 20);

        // Title
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var moonIcon = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = new RadialGradientBrush
            {
                Center = new Point(0.4, 0.4),
                GradientStops =
                {
                    new(Colors.White, 0.0),
                    new(Color.FromRgb(0x9f, 0xf6, 0xff), 0.7),
                    new(Color.FromRgb(0x3a, 0xa5, 0xff), 1.0)
                }
            },
            Margin = new Thickness(0, 0, 12, 0),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x9f, 0xe0, 0xff), BlurRadius = 14, ShadowDepth = 0 }
        };
        titleStack.Children.Add(moonIcon);
        titleStack.Children.Add(new TextBlock
        {
            Text = "TIDE // ANALOG RESPONSE STUDIO",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 20,
            Foreground = TideKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        topBar.Children.Add(titleStack);
        Canvas.SetLeft(titleStack, 0);
        Canvas.SetTop(titleStack, 14);

        // Channel Ribbon
        var chanPanel = new Border
        {
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(Color.FromArgb(0xbb, 0x06, 0x14, 0x38)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x9f, 0xf6, 0xff)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 3, 4, 3)
        };
        var chanStack = new StackPanel { Orientation = Orientation.Horizontal };

        foreach (var (cName, ch, icon) in channelDefs)
        {
            bool isChActive = (ch == curChannel);
            var chBtn = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                Background = isChActive
                    ? new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1),
                        GradientStops =
                        {
                            new(Color.FromRgb(0x18, 0x5a, 0xe0), 0.0),
                            new(Color.FromRgb(0x0c, 0x32, 0x96), 1.0)
                        }
                    }
                    : Brushes.Transparent,
                BorderBrush = isChActive ? TideKit.CyanFoamBrush : Brushes.Transparent,
                BorderThickness = new Thickness(isChActive ? 1 : 0)
            };

            var chLabel = new TextBlock
            {
                Text = cName,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                Foreground = isChActive ? TideKit.WhiteBrush : new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8))
            };
            chBtn.Child = chLabel;

            CurveChannel targetCh = ch;
            chBtn.MouseLeftButtonUp += (_, _) =>
            {
                Curves.SelectChannel(targetCh);
                TideShowCurves();
            };

            chanStack.Children.Add(chBtn);
        }
        chanPanel.Child = chanStack;
        topBar.Children.Add(chanPanel);
        Canvas.SetLeft(chanPanel, 520);
        Canvas.SetTop(chanPanel, 10);

        // Right Actions: Dashboard return
        var backBtn = TideKit.OceanButton("DASHBOARD", icon: "\uE72B", onClick: () => TideShowDashboard());
        topBar.Children.Add(backBtn);
        Canvas.SetRight(backBtn, 0);
        Canvas.SetTop(backBtn, 10);

        board.Children.Add(topBar);

        // 3. Center-Left Stage — Interactive Oscilloscope Radar
        var oscCard = TideKit.OceanGlassCard(w: 580, h: 680);
        Canvas.SetLeft(oscCard, 60);
        Canvas.SetTop(oscCard, 100);

        var oscStack = new StackPanel();

        // Oscilloscope Header
        var oscHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        oscHead.Children.Add(new TextBlock
        {
            Text = "\uE9D9",
            FontFamily = TideKit.Icons,
            FontSize = 18,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        oscHead.Children.Add(new TextBlock
        {
            Text = "RESPONSE OSCILLOSCOPE",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = TideKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        oscStack.Children.Add(oscHead);

        // Oscilloscope Screen (540x480)
        var scopeBorder = new Border
        {
            Width = 540,
            Height = 480,
            CornerRadius = new CornerRadius(12),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new(Color.FromArgb(0xf0, 0x02, 0x08, 0x1c), 0.0),
                    new(Color.FromArgb(0xf8, 0x01, 0x04, 0x10), 1.0)
                }
            },
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xaa, 0x9f, 0xf6, 0xff)),
            BorderThickness = new Thickness(1.5),
            ClipToBounds = true
        };

        var scopeCanvas = new Canvas { Width = 540, Height = 480 };

        // Grid Lines
        for (int step = 1; step <= 4; step++)
        {
            double gx = step * (540.0 / 5.0);
            double gy = step * (480.0 / 5.0);

            var vLine = new Line
            {
                X1 = gx, Y1 = 0, X2 = gx, Y2 = 480,
                Stroke = new SolidColorBrush(Color.FromArgb(0x28, 0x9f, 0xf6, 0xff)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            scopeCanvas.Children.Add(vLine);

            var hLine = new Line
            {
                X1 = 0, Y1 = gy, X2 = 540, Y2 = gy,
                Stroke = new SolidColorBrush(Color.FromArgb(0x28, 0x9f, 0xf6, 0xff)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            scopeCanvas.Children.Add(hLine);
        }

        // Inner Deadzone Zone Shading
        double dzWidth = 540 * (curSetting.Inner / 100.0);
        if (dzWidth > 0)
        {
            var dzRect = new Rectangle
            {
                Width = dzWidth,
                Height = 480,
                Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xff, 0x24, 0x42))
            };
            Canvas.SetLeft(dzRect, 0);
            Canvas.SetTop(dzRect, 0);
            scopeCanvas.Children.Add(dzRect);

            var dzLabel = new TextBlock
            {
                Text = $"DEADZONE ({curSetting.Inner}%)",
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = TideKit.HarpoonRedBrush,
                RenderTransform = new RotateTransform(-90)
            };
            Canvas.SetLeft(dzLabel, dzWidth - 12);
            Canvas.SetTop(dzLabel, 100);
            scopeCanvas.Children.Add(dzLabel);
        }

        // Response Curve Path
        var curveGeometry = new PathGeometry();
        var curveFigure = new PathFigure { StartPoint = new Point(dzWidth, 480) };

        double pMidX = (540 + dzWidth) / 2.0;
        double pMidY = curSetting.Preset switch
        {
            "quick" => 360,
            "slow" => 160,
            "smooth" => 200,
            "fine" => 140,
            _ => 240
        };

        curveFigure.Segments.Add(new BezierSegment(
            new Point(dzWidth + 80, 480 - (480 - pMidY) * 0.4),
            new Point(pMidX, pMidY),
            new Point(540, 20),
            true
        ));
        curveGeometry.Figures.Add(curveFigure);

        var curvePath = new System.Windows.Shapes.Path
        {
            Data = curveGeometry,
            Stroke = TideKit.CyanFoamBrush,
            StrokeThickness = 3.5,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x9f, 0xe0, 0xff),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.95
            }
        };
        scopeCanvas.Children.Add(curvePath);

        // Tracking dot on curve
        var dot = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = TideKit.HarpoonRedBrush,
            Effect = new DropShadowEffect { Color = TideKit.HarpoonRed, BlurRadius = 12, ShadowDepth = 0 }
        };
        Canvas.SetLeft(dot, pMidX - 7);
        Canvas.SetTop(dot, pMidY - 7);
        scopeCanvas.Children.Add(dot);
        TideKit.AnimatePulse(dot, 0.8, 1.3, 1.4);

        scopeBorder.Child = scopeCanvas;
        oscStack.Children.Add(scopeBorder);

        // Status Readout below scope
        var scopeFoot = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        scopeFoot.Children.Add(new TextBlock
        {
            Text = $"INPUT DEFLECTION: 0% ▸ OUTPUT RESPONSE: 0% · PRESET: {curSetting.Preset.ToUpperInvariant()}",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = TideKit.CyanFoamBrush
        });
        oscStack.Children.Add(scopeFoot);

        oscCard.Child = oscStack;
        board.Children.Add(oscCard);

        // 4. Center-Right Stage — Curve Tuning Deck
        var deckCard = TideKit.OceanGlassCard(w: 860, h: 680);
        Canvas.SetLeft(deckCard, 680);
        Canvas.SetTop(deckCard, 100);

        var deckStack = new StackPanel();

        // Channel Plaque
        var plaque = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new(Color.FromArgb(0xcc, 0x18, 0x5a, 0xe0), 0.0),
                    new(Color.FromArgb(0xee, 0x0c, 0x32, 0x96), 1.0)
                }
            },
            BorderBrush = TideKit.CyanFoamBrush,
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(18, 12, 18, 12),
            Margin = new Thickness(0, 0, 0, 20),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x1a, 0x5a, 0xd8), BlurRadius = 18, ShadowDepth = 4 }
        };

        var plaqueStack = new StackPanel();
        plaqueStack.Children.Add(new TextBlock
        {
            Text = $"TUNING: {curChannel.ToString().ToUpperInvariant()}",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = TideKit.WhiteBrush
        });
        plaqueStack.Children.Add(new TextBlock
        {
            Text = "Hardware hall sensor mapping with sub-millisecond dynamic contour interpolation.",
            FontFamily = TideKit.Body,
            FontSize = 12,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });
        plaque.Child = plaqueStack;
        deckStack.Children.Add(plaque);

        // Presets Section
        deckStack.Children.Add(new TextBlock
        {
            Text = "RESPONSE CURVE PRESETS",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var presetsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 24) };
        var presets = new (string Label, string PKey)[]
        {
            ("LINEAR (1:1)", "default"),
            ("QUICK (AGGRESSIVE)", "quick"),
            ("SMOOTH (SOFT)", "smooth"),
            ("FINE (PRECISION)", "fine"),
            ("SLOW (EXPONENTIAL)", "slow")
        };

        foreach (var (pLabel, pKey) in presets)
        {
            bool isPActive = string.Equals(curSetting.Preset, pKey, StringComparison.OrdinalIgnoreCase);
            var pBtn = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Background = isPActive
                    ? TideKit.HarpoonRedBrush
                    : new SolidColorBrush(Color.FromArgb(0xaa, 0x0d, 0x24, 0x56)),
                BorderBrush = isPActive
                    ? new SolidColorBrush(Color.FromRgb(0xff, 0xc0, 0xcc))
                    : new SolidColorBrush(Color.FromArgb(0x80, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(isPActive ? 1.5 : 1),
                Effect = isPActive
                    ? new DropShadowEffect { Color = TideKit.HarpoonRed, BlurRadius = 14, ShadowDepth = 2, Opacity = 0.9 }
                    : null
            };

            pBtn.Child = new TextBlock
            {
                Text = pLabel,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                Foreground = TideKit.WhiteBrush
            };

            string targetP = pKey;
            pBtn.MouseLeftButtonUp += (_, _) =>
            {
                Curves.SetPreset(targetP);
                TideShowCurves();
            };

            presetsRow.Children.Add(pBtn);
        }
        deckStack.Children.Add(presetsRow);

        // Sliders Section: Deadzone (Inner) & Outer Threshold
        deckStack.Children.Add(new TextBlock
        {
            Text = "THRESHOLD & RANGE TUNING",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 0, 0, 12)
        });

        // Deadzone Slider
        var dzBox = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0x80, 0x06, 0x16, 0x40)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var dzGrid = new Grid();
        dzGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        dzGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dzGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

        var dzTitle = new StackPanel();
        dzTitle.Children.Add(new TextBlock
        {
            Text = "INNER DEADZONE",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.WhiteBrush
        });
        dzTitle.Children.Add(new TextBlock
        {
            Text = "Zero-response stick center threshold",
            FontFamily = TideKit.Body,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8))
        });
        Grid.SetColumn(dzTitle, 0);
        dzGrid.Children.Add(dzTitle);

        var dzSlider = new Slider
        {
            Minimum = 0,
            Maximum = 30,
            Value = curSetting.Inner,
            Tag = "theme:stick:deadzone",
            Focusable = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 14, 0)
        };
        dzSlider.ValueChanged += (_, e) =>
        {
            int newVal = (int)Math.Round(e.NewValue);
            Curves.Draft.Set(curChannel, Curves.Draft[curChannel] with { Inner = newVal });
            Curves.Refresh();
        };
        Grid.SetColumn(dzSlider, 1);
        dzGrid.Children.Add(dzSlider);

        var dzValText = new TextBlock
        {
            Text = $"{curSetting.Inner}%",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = TideKit.CyanFoamBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dzValText, 2);
        dzGrid.Children.Add(dzValText);

        dzBox.Child = dzGrid;
        deckStack.Children.Add(dzBox);

        // Max Reach (Outer) Slider
        var reachBox = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0x80, 0x06, 0x16, 0x40)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 24)
        };
        var reachGrid = new Grid();
        reachGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        reachGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        reachGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

        var reachTitle = new StackPanel();
        reachTitle.Children.Add(new TextBlock
        {
            Text = "MAX REACH / OUTER",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.WhiteBrush
        });
        reachTitle.Children.Add(new TextBlock
        {
            Text = "100% saturation deflection boundary",
            FontFamily = TideKit.Body,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8))
        });
        Grid.SetColumn(reachTitle, 0);
        reachGrid.Children.Add(reachTitle);

        var reachSlider = new Slider
        {
            Minimum = 70,
            Maximum = 100,
            Value = curSetting.Outer,
            Tag = "theme:stick:outer",
            Focusable = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 14, 0)
        };
        reachSlider.ValueChanged += (_, e) =>
        {
            int newVal = (int)Math.Round(e.NewValue);
            Curves.Draft.Set(curChannel, Curves.Draft[curChannel] with { Outer = newVal });
            Curves.Refresh();
        };
        Grid.SetColumn(reachSlider, 1);
        reachGrid.Children.Add(reachSlider);

        var reachValText = new TextBlock
        {
            Text = $"{curSetting.Outer}%",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = TideKit.CyanFoamBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(reachValText, 2);
        reachGrid.Children.Add(reachValText);

        reachBox.Child = reachGrid;
        deckStack.Children.Add(reachBox);

        // Bottom Action Dock inside tuning deck
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var resetBtn = TideKit.OceanButton("RESET CHANNEL", icon: "\uE777", onClick: () =>
        {
            Curves.Reset();
            TideShowCurves();
        });
        resetBtn.Margin = new Thickness(0, 0, 10, 0);
        actionRow.Children.Add(resetBtn);

        var copyBtn = TideKit.OceanButton("APPLY TO BOTH STICKS", icon: "\uE8B5", onClick: () =>
        {
            var curS = Curves.Draft[curChannel];
            Curves.Draft.Set(CurveChannel.LeftStick, curS);
            Curves.Draft.Set(CurveChannel.RightStick, curS);
            Curves.Refresh();
            TideShowCurves();
        });
        copyBtn.Margin = new Thickness(0, 0, 10, 0);
        actionRow.Children.Add(copyBtn);

        var testBtn = TideKit.SparkButton("TEST IN TESTER ➔", icon: "\uE768", onClick: () => ShowManagement("Tester"));
        actionRow.Children.Add(testBtn);

        deckStack.Children.Add(actionRow);

        deckCard.Child = deckStack;
        board.Children.Add(deckCard);

        // 5. Bottom Navigation Dock
        var navDock = TideKit.OceanDock("Curves", TideSelectPage);
        Canvas.SetLeft(navDock, 230);
        Canvas.SetTop(navDock, 820);
        board.Children.Add(navDock);

        tidePageHost.Children.Add(board);
    }
}

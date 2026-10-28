using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product;

public sealed partial class ProductSettings
{
    internal FrameworkElement? spotlightRoot;
    public Action<string>? SpotlightNavigate { get; set; }
    private string spotlightView = "hub";

    private void BuildSpotlight()
    {
        if (pageGrid != null) pageGrid.Visibility = Visibility.Collapsed;
        hub.Visibility = Visibility.Collapsed;
        about.Visibility = Visibility.Collapsed;
        feedback.Visibility = Visibility.Collapsed;
        pcMode.Visibility = Visibility.Collapsed;
        themes.Visibility = Visibility.Collapsed;
        footer.Visibility = Visibility.Collapsed;

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        spotlightRoot = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        Children.Add(spotlightRoot);

        var changed = (Action)(() => { if (spotlightRoot.IsVisible && spotlightView == "hub") SpotlightSync(); });
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock)).AddValueChanged(updateStatus, (_, _) => changed());
        DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(UIElement)).AddValueChanged(updateGo, (_, _) => changed());
        autoUpdate.Checked += (_, _) => changed();
        autoUpdate.Unchecked += (_, _) => changed();

        SpotlightSync();
    }

    internal void SpotlightSync()
    {
        if (spotlightRoot is not Viewbox box || box.Child is not Canvas board) return;
        board.Children.Clear();

        spotlightView = pcMode.Visibility == Visibility.Visible ? "pcmode" :
                        feedback.Visibility == Visibility.Visible ? "feedback" :
                        about.Visibility == Visibility.Visible ? "about" :
                        themes.Visibility == Visibility.Visible ? "themes" : "hub";

        switch (spotlightView)
        {
            case "about":
                RenderSpotlightAbout(board);
                break;
            case "themes":
                RenderSpotlightThemes(board);
                break;
            case "pcmode":
                RenderSpotlightPcMode(board);
                break;
            case "feedback":
                BuildSpotlightFeedbackContent(board);
                break;
            case "hub":
            default:
                RenderSpotlightHub(board);
                break;
        }
    }

    private void RenderSpotlightHub(Canvas board)
    {
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Dressing-room mirror with illuminated bulbs at (320, 210, w = 860, h = 420)
        double mx = 320, my = 210, mw = 860, mh = 420;
        var mirrorCanvas = SpotlightKit.MarqueeMirror(mx, my, mw, mh, 14, 5);
        canvas.Children.Add(mirrorCanvas);

        // 8 Tool tiles inside mirror
        var tiles = new (string Icon, string Name, string Sub, Action Click)[]
        {
            ("\uE7FC", "PC mode", "Virtual Xbox controller", () => ShowPcMode(true)),
            ("\uE790", "Themes", "Visual appearance", () => ShowThemes(true)),
            ("\uE714", "Intro", "Play launch sequence", () => Replay.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE73E", "Read scan", "Scan controller packets", () => Check.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE72C", "Write scan", "Profile memory changes", () => WriteScan.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE946", "About", "Versions and credits", () => ShowAbout(true)),
            ("\uE939", "Feedback", "Report an issue", () => Feedback.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE71B", "GitHub", "Source code and docs", () => Support.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)))
        };

        for (int i = 0; i < tiles.Length; i++)
        {
            var (icon, name, sub, act) = tiles[i];
            double tx = mx + 30 + (i % 4) * 202;
            double ty = my + 34 + (i / 4) * 186;
            bool on = (i == 0); // PC mode highlighted

            var tileBorder = new Border
            {
                Width = 186, Height = 166,
                Background = on
                    ? new RadialGradientBrush
                    {
                        Center = new(0.5, 0.4), RadiusX = 0.8, RadiusY = 0.9,
                        GradientStops = { new(SpotlightKit.A(SpotlightKit.C("#ffdcaf"), 0.22), 0.0), new(Colors.Transparent, 1.0) }
                    }
                    : Brushes.Transparent,
                BorderBrush = on ? SpotlightKit.GoldBrush : SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.18)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20, 18, 20, 18),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = icon,
                FontFamily = SpotlightKit.Icons,
                FontSize = 24,
                Foreground = on ? SpotlightKit.GoldHiBrush : SpotlightKit.GoldBrush
            });
            stack.Children.Add(new TextBlock
            {
                Text = name,
                FontFamily = SpotlightKit.Body,
                FontSize = 19,
                Foreground = SpotlightKit.ChalkBrush,
                Margin = new Thickness(0, 14, 0, 0)
            });
            stack.Children.Add(new TextBlock
            {
                Text = sub,
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 13.5,
                Foreground = SpotlightKit.MuteBrush,
                Margin = new Thickness(0, 4, 0, 0)
            });

            tileBorder.Child = stack;
            tileBorder.MouseLeftButtonDown += (_, _) => act();
            Canvas.SetLeft(tileBorder, tx);
            Canvas.SetTop(tileBorder, ty);
            canvas.Children.Add(tileBorder);
        }

        // Running order cue card on right at (1230, 196, w = 330, label = "Running order")
        var cardInner = new StackPanel();
        cardInner.Children.Add(new TextBlock { Text = "PC Mode Pipeline", FontFamily = SpotlightKit.Body, FontSize = 21, Foreground = SpotlightKit.ChalkBrush });
        cardInner.Children.Add(new TextBlock { Text = "Order of hardware interception", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush, Margin = new Thickness(0, 2, 0, 10) });

        string[] pipeSteps = { "Raw Input capture", "Driver translation", "Virtual Xbox 360", "Operating system" };
        for (int i = 0; i < pipeSteps.Length; i++)
        {
            var pRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            pRow.Children.Add(new TextBlock { Text = $"{SpotlightKit.Roman(i)}.", FontFamily = SpotlightKit.Bodoni, FontSize = 18, Width = 26, Foreground = i == 0 ? SpotlightKit.GoldHiBrush : SpotlightKit.GoldBrush });
            pRow.Children.Add(new TextBlock { Text = pipeSteps[i], FontFamily = SpotlightKit.Body, FontSize = 18, Foreground = i == 0 ? SpotlightKit.B("#fff6e6") : SpotlightKit.ChalkDimBrush });
            cardInner.Children.Add(pRow);
        }

        var cueCard = SpotlightKit.CueCard(cardInner, 330, label: "Running order", padding: new Thickness(28, 24, 28, 24));
        Canvas.SetLeft(cueCard, 1230);
        Canvas.SetTop(cueCard, 196);
        canvas.Children.Add(cueCard);

        // Update cue card at bottom at (292, 690, w = 1260)
        var upInner = new Grid();
        upInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        upInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        upInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var upIcon = new TextBlock { Text = "\uE895", FontFamily = SpotlightKit.Icons, FontSize = 22, Foreground = SpotlightKit.GoldBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 22, 0) };
        Grid.SetColumn(upIcon, 0); upInner.Children.Add(upIcon);

        var upTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        upTextStack.Children.Add(new TextBlock { Text = "Automatic updates · Check on launch", FontFamily = SpotlightKit.Body, FontSize = 21, Foreground = SpotlightKit.ChalkBrush });
        upTextStack.Children.Add(new TextBlock { Text = "X20CTL checks GitHub releases quietly when opened.", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(upTextStack, 1); upInner.Children.Add(upTextStack);

        var upActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var swStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
        var swLed = new Ellipse { Width = 11, Height = 11, Fill = SpotlightKit.B("#ffc070"), Margin = new Thickness(0, 0, 8, 0) };
        swLed.Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffb860"), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 };
        swStack.Children.Add(swLed);
        swStack.Children.Add(new TextBlock { Text = "ON", FontFamily = SpotlightKit.Display, FontSize = 11, FontWeight = FontWeights.Light, Foreground = SpotlightKit.GoldHiBrush, VerticalAlignment = VerticalAlignment.Center });
        upActions.Children.Add(swStack);

        var checkBtn = SpotlightKit.TheatricalButton("Check for updates", false, null, height: 40, fontSize: 11);
        upActions.Children.Add(checkBtn);
        Grid.SetColumn(upActions, 2); upInner.Children.Add(upActions);

        var upCard = SpotlightKit.CueCard(upInner, 1260, padding: new Thickness(26, 18, 26, 18));
        Canvas.SetLeft(upCard, 292);
        Canvas.SetTop(upCard, 690);
        canvas.Children.Add(upCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "tools",
            title: "Tools",
            lead: "Quick access to calibration, diagnostics and community tools.",
            backLabel: "Back",
            onBack: () => StepBack(),
            onNavigate: target => SpotlightNavigate?.Invoke(target),
            bodyStage: canvas,
            hints: new[] { ("A", "Open"), ("B", "Back") });

        board.Children.Add(frame);
    }

    private void RenderSpotlightThemes(Canvas board)
    {
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Backstage wall slats
        var slats = new Rectangle
        {
            Width = 1350, Height = 470,
            Fill = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 120, 470),
                ViewportUnits = BrushMappingMode.Absolute,
                Drawing = new GeometryDrawing
                {
                    Brush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.GoldHi, 0.015)),
                    Pen = new Pen(SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.15)), 1),
                    Geometry = new LineGeometry(new Point(119, 0), new Point(119, 470))
                }
            }
        };
        Canvas.SetLeft(slats, 250);
        Canvas.SetTop(slats, 180);
        canvas.Children.Add(slats);

        // Follow spot on current theme poster at cx = 920, fy = 600
        var spot = SpotlightKit.FollowSpot(920, 600, 230, -60, 0.28, pool: false);
        canvas.Children.Add(spot);

        // 5 Theme posters: [-2, -1, 1, 2, 0]
        var themeItems = new (string Id, string Name)[]
        {
            ("default", "Default"),
            ("pulse", "Pulse"),
            ("spotlight", "Spotlight"),
            ("nexus", "Nexus"),
            ("tide", "Tide")
        };

        for (int i = 0; i < 5; i++)
        {
            int off = i - 2; // -2, -1, 0, 1, 2
            bool on = (off == 0);
            double w = on ? 380 : (200 - Math.Abs(off) * 15);
            double h = w * 9 / 16.0;
            double x = 920 + off * 200 + (off != 0 ? Math.Sign(off) * 110 : 0) - w / 2;
            double y = 360 - h / 2 + (on ? 0 : 10);

            var poster = new Border
            {
                Width = w, Height = h,
                Background = new LinearGradientBrush
                {
                    StartPoint = new(0, 0), EndPoint = new(0, 1),
                    GradientStops = { new(SpotlightKit.C("#241410"), 0.0), new(SpotlightKit.C("#100806"), 1.0) }
                },
                BorderBrush = on ? SpotlightKit.GoldBrush : SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.35)),
                BorderThickness = new Thickness(on ? 3 : 1),
                Effect = on
                    ? new DropShadowEffect { Color = SpotlightKit.C("#ffc878"), BlurRadius = 40, ShadowDepth = 20, Opacity = 0.5 }
                    : new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, ShadowDepth = 10, Opacity = 0.6 }
            };

            // Poster label inside
            var pCanvas = new Canvas { Width = w, Height = h };
            var pTitle = new TextBlock
            {
                Text = themeItems[i].Name,
                FontFamily = SpotlightKit.Display,
                FontSize = on ? 24 : 14,
                FontWeight = FontWeights.Light,
                Foreground = on ? SpotlightKit.GoldHiBrush : SpotlightKit.MuteBrush
            };
            Canvas.SetLeft(pTitle, 20);
            Canvas.SetTop(pTitle, h - 36);
            pCanvas.Children.Add(pTitle);
            poster.Child = pCanvas;

            Canvas.SetLeft(poster, x);
            Canvas.SetTop(poster, y);
            canvas.Children.Add(poster);

            // Label below poster
            var labelStack = new StackPanel { Width = w, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, on ? 24 : 14, 0, 0) };
            var rowStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            rowStack.Children.Add(new TextBlock
            {
                Text = themeItems[i].Name.ToUpperInvariant(),
                FontFamily = SpotlightKit.Display,
                FontSize = on ? 18 : 12,
                FontWeight = FontWeights.Light,
                Foreground = on ? SpotlightKit.B("#fff6e6") : SpotlightKit.B("#8a7c6a")
            });
            if (on)
            {
                var inUseTag = SpotlightKit.Tag("In use", true);
                inUseTag.Margin = new Thickness(10, 0, 0, 0);
                rowStack.Children.Add(inUseTag);
            }
            labelStack.Children.Add(rowStack);
            Canvas.SetLeft(labelStack, x);
            Canvas.SetTop(labelStack, y + h);
            canvas.Children.Add(labelStack);
        }

        // Cue card at bottom at (292, 650, w = 1260)
        var cardInner = new Grid();
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var introIcon = new TextBlock { Text = "\uE714", FontFamily = SpotlightKit.Icons, FontSize = 24, Foreground = SpotlightKit.GoldBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 24, 0) };
        Grid.SetColumn(introIcon, 0); cardInner.Children.Add(introIcon);

        var introText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        introText.Children.Add(new TextBlock { Text = "Opening sequence · Play film on startup", FontFamily = SpotlightKit.Body, FontSize = 21, Foreground = SpotlightKit.ChalkBrush });
        introText.Children.Add(new TextBlock { Text = "The theatrical curtain-up and star ignition video plays when X20CTL launches.", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(introText, 1); cardInner.Children.Add(introText);

        var introSwitches = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var sw1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
        var led1 = new Ellipse { Width = 11, Height = 11, Fill = SpotlightKit.B("#ffc070"), Margin = new Thickness(0, 0, 8, 0) };
        led1.Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffb860"), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 };
        sw1.Children.Add(led1);
        sw1.Children.Add(new TextBlock { Text = "PLAY FILM", FontFamily = SpotlightKit.Display, FontSize = 11, FontWeight = FontWeights.Light, Foreground = SpotlightKit.GoldHiBrush });
        introSwitches.Children.Add(sw1);

        var sw2 = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var led2 = new Ellipse { Width = 11, Height = 11, Fill = SpotlightKit.B("#ffc070"), Margin = new Thickness(0, 0, 8, 0) };
        led2.Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffb860"), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 };
        sw2.Children.Add(led2);
        sw2.Children.Add(new TextBlock { Text = "SOUND", FontFamily = SpotlightKit.Display, FontSize = 11, FontWeight = FontWeights.Light, Foreground = SpotlightKit.GoldHiBrush });
        introSwitches.Children.Add(sw2);

        Grid.SetColumn(introSwitches, 2); cardInner.Children.Add(introSwitches);

        var cueCard = SpotlightKit.CueCard(cardInner, 1260, padding: new Thickness(26, 20, 26, 20));
        Canvas.SetLeft(cueCard, 292);
        Canvas.SetTop(cueCard, 650);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "tools",
            title: "Themes",
            lead: "Changes how X20CTL looks. Your controllers and drafts stay as they are.",
            backLabel: "Back",
            onBack: () => ShowThemes(false),
            onNavigate: target => SpotlightNavigate?.Invoke(target),
            bodyStage: canvas,
            hints: new[] { ("A", "Apply"), ("B", "Back") },
            leadW: 1100);

        board.Children.Add(frame);
    }

    private void RenderSpotlightAbout(Canvas board)
    {
        // Full theatrical stage without left rail (t_spotlight.py:791)
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        canvas.Children.Add(SpotlightKit.TheatricalStage(floorY: 700, legs: false, valance: true, leftLeg: true, leftLegW: 150, rightLegW: 150));

        // Follow spot on massive logo at cx = 480, fy = 720, rx = 300, top = -60, a = 0.3
        var spot = SpotlightKit.FollowSpot(480, 720, 300, -60, 0.30);
        canvas.Children.Add(spot);

        // Logo at (280, 230), width 400
        var logoImg = SpotlightParts.GetThemeLogo();
        if (logoImg != null)
        {
            var logo = new Image
            {
                Source = logoImg, Width = 400,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 30, ShadowDepth = 20, Opacity = 0.7 }
            };
            Canvas.SetLeft(logo, 280);
            Canvas.SetTop(logo, 230);
            canvas.Children.Add(logo);
        }

        // Title and Version below logo at (230, 560, w = 500)
        var verStack = new StackPanel { Width = 500 };
        verStack.Children.Add(new TextBlock
        {
            Text = "X20CTL",
            FontFamily = SpotlightKit.Display,
            FontSize = 40,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.B("#f3e7d3"),
            TextAlignment = TextAlignment.Center
        });
        verStack.Children.Add(new TextBlock
        {
            Text = "Version 0.017-preview · Built for X20 series controllers",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 17,
            Foreground = SpotlightKit.RoseBrush,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0)
        });
        Canvas.SetLeft(verStack, 230);
        Canvas.SetTop(verStack, 560);
        canvas.Children.Add(verStack);

        // Playbill Cue Card on right at (860, 110, w = 620)
        var billInner = new StackPanel();
        billInner.Children.Add(new TextBlock
        {
            Text = "ABOUT",
            FontFamily = SpotlightKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush,
            TextAlignment = TextAlignment.Center
        });

        billInner.Children.Add(new Rectangle
        {
            Width = 60, Height = 1,
            Fill = SpotlightKit.GoldBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 14)
        });

        billInner.Children.Add(new TextBlock
        {
            Text = "Open-source control deck and configuration utility for X20 hardware.",
            FontFamily = SpotlightKit.Body,
            FontSize = 17,
            Foreground = SpotlightKit.ChalkBrush,
            TextAlignment = TextAlignment.Center
        });

        billInner.Children.Add(new TextBlock
        {
            Text = "Direct packet synthesis, custom deadzone response curves, and low-latency virtual input routing.",
            FontFamily = SpotlightKit.Body,
            FontSize = 17,
            Foreground = SpotlightKit.ChalkDimBrush,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        });

        // Credits / People
        foreach (var (role, person) in new[] { ("ARCHITECTURE", "Amjad Masad & Codex"), ("DESIGN & DIRECTION", "Gemini & Team"), ("COMMUNITY & TESTING", "Monratis & Contributors") })
        {
            var pBox = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
            pBox.Children.Add(new TextBlock
            {
                Text = role,
                FontFamily = SpotlightKit.Display,
                FontSize = 10.5,
                FontWeight = FontWeights.Light,
                Foreground = SpotlightKit.MuteBrush,
                TextAlignment = TextAlignment.Center
            });
            pBox.Children.Add(new TextBlock
            {
                Text = person,
                FontFamily = SpotlightKit.Body,
                FontSize = 18,
                Foreground = SpotlightKit.B("#f3e7d3"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            billInner.Children.Add(pBox);
        }

        // Action buttons
        var actBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) };
        actBtns.Children.Add(SpotlightKit.TheatricalButton("Back", true, "B", onClick: () => ShowAbout(false), height: 40, fontSize: 12));
        var gitBtn = SpotlightKit.TheatricalButton("GitHub", false, null, onClick: () => Support.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)), height: 40, fontSize: 11);
        gitBtn.Margin = new Thickness(10, 0, 0, 0);
        actBtns.Children.Add(gitBtn);
        var licBtn = SpotlightKit.TheatricalButton("Licenses", false, null, height: 40, fontSize: 11);
        licBtn.Margin = new Thickness(10, 0, 0, 0);
        actBtns.Children.Add(licBtn);
        billInner.Children.Add(actBtns);

        billInner.Children.Add(new TextBlock
        {
            Text = "Licensed under the MIT License. All controller trademarks belong to their respective owners.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.MuteBrush,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0)
        });

        var cueCard = SpotlightKit.CueCard(billInner, 620, padding: new Thickness(34, 30, 34, 30));
        Canvas.SetLeft(cueCard, 860);
        Canvas.SetTop(cueCard, 110);
        canvas.Children.Add(cueCard);

        // Hints
        var hints = SpotlightParts.HintsBar(new[] { ("A", "Open"), ("B", "Back") });
        Canvas.SetRight(hints, 190);
        Canvas.SetTop(hints, 856);
        canvas.Children.Add(hints);

        board.Children.Add(canvas);
    }

    private void RenderSpotlightPcMode(Canvas board)
    {
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Setting rows at (292, 220, w = 600)
        var rowsStack = new StackPanel { Width = 600 };
        rowsStack.Children.Add(SpotlightKit.Row("PC mode translation", "Create virtual Xbox 360 controller", "Off", false, null, 46));
        rowsStack.Children.Add(SpotlightKit.Row("Virtual driver", "ViGEmBus kernel driver", "Installed", true, null, 46));
        rowsStack.Children.Add(SpotlightKit.Row("Turbo repeat rate", "Rapid-fire button frequency", "12 Hz", false, null, 46));
        rowsStack.Children.Add(SpotlightKit.Row("Hide physical gamepad", "Prevent duplicate inputs via HidHide", "Active", false, null, 46));
        Canvas.SetLeft(rowsStack, 292);
        Canvas.SetTop(rowsStack, 220);
        canvas.Children.Add(rowsStack);

        // Cue card on right
        var cardInner = new StackPanel();
        cardInner.Children.Add(new TextBlock { Text = "PC Mode Configuration", FontFamily = SpotlightKit.Body, FontSize = 21, Foreground = SpotlightKit.ChalkBrush });
        cardInner.Children.Add(new TextBlock { Text = "Configure hardware translation and virtual device routing.", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush, Margin = new Thickness(0, 2, 0, 14) });

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        btnRow.Children.Add(SpotlightKit.TheatricalButton("Install drivers", true, "A", height: 44, fontSize: 13));
        cardInner.Children.Add(btnRow);

        var cueCard = SpotlightKit.CueCard(cardInner, 620, label: "Driver Control", padding: new Thickness(28, 24, 28, 24));
        Canvas.SetLeft(cueCard, 940);
        Canvas.SetTop(cueCard, 220);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "tools",
            title: "PC mode",
            lead: "Virtual Xbox controller translation and background gamepad emulation.",
            backLabel: "Back",
            onBack: () => ShowPcMode(false),
            onNavigate: target => SpotlightNavigate?.Invoke(target),
            bodyStage: canvas,
            hints: new[] { ("A", "Toggle"), ("B", "Back") });

        board.Children.Add(frame);
    }

    private void BuildSpotlightFeedbackContent(Canvas board)
    {
        var form = FeedbackForm;
        if (form.Parent is Panel p) p.Children.Remove(form);
        else if (form.Parent is ContentControl cc) cc.Content = null;
        else if (form.Parent is Decorator dec) dec.Child = null;
        Canvas.SetLeft(form, 0);
        Canvas.SetTop(form, 0);
        board.Children.Add(form);
    }
}

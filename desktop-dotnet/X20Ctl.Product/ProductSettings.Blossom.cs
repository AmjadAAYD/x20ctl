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
using X20Ctl.Product.Blossom;

namespace X20Ctl.Product;

/// <summary>
/// Tools Hub, Themes pavilion, About scroll, and Feedback ema plaque presentation for the Yozakura (Blossom) theme:
/// "Tools is a vibrant row of festival stalls under cherry trees;
/// Themes is a traditional Japanese pavilion at night with sliding shoji screen windows;
/// Feedback is an authentic wooden votive tablet (Ema plaque) hanging from red silk rope on a cherry branch."
/// Strictly implements t_blossom.py:hub(), t_blossom.py:tools(), t_blossom.py:about(), and t_blossom.py:feedback().
/// </summary>
public sealed partial class ProductSettings
{
    internal FrameworkElement? blossomRoot;
    private string blossomView = "hub";

    private void BuildBlossom()
    {
        if (pageGrid != null) pageGrid.Visibility = Visibility.Collapsed;
        hub.Visibility = Visibility.Collapsed;
        about.Visibility = Visibility.Collapsed;
        feedback.Visibility = Visibility.Collapsed;
        pcMode.Visibility = Visibility.Collapsed;
        themes.Visibility = Visibility.Collapsed;
        footer.Visibility = Visibility.Collapsed;

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Background = BlossomKit.VoidBgBrush };
        blossomRoot = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        Children.Add(blossomRoot);

        BlossomSync();
    }

    internal void BlossomSync()
    {
        if (blossomRoot is not Viewbox box || box.Child is not Canvas board) return;
        board.Children.Clear();

        blossomView = pcMode.Visibility == Visibility.Visible ? "pcmode" :
                      feedback.Visibility == Visibility.Visible ? "feedback" :
                      about.Visibility == Visibility.Visible ? "about" :
                      themes.Visibility == Visibility.Visible ? "themes" : "hub";

        switch (blossomView)
        {
            case "about":
                RenderBlossomAbout(board);
                break;
            case "themes":
                RenderBlossomThemes(board);
                break;
            case "feedback":
                RenderBlossomFeedback(board);
                break;
            case "pcmode":
                RenderBlossomPcMode(board);
                break;
            case "hub":
            default:
                RenderBlossomHub(board);
                break;
        }
    }

    private void RenderBlossomLeftNav(Canvas board, string active)
    {
        var logo = BlossomKit.BlossomLogo(116);
        if (logo != null)
        {
            logo.Cursor = Cursors.Hand;
            logo.MouseLeftButtonUp += (_, _) => { ShowAbout(false); ShowFeedback(false); ShowThemes(false); };
            Canvas.SetLeft(logo, 30);
            Canvas.SetTop(logo, 16);
            board.Children.Add(logo);
        }

        board.Children.Add(new Line { X1 = 88, Y1 = 96, X2 = 88, Y2 = 880, Stroke = BlossomKit.B("#3a2430"), StrokeThickness = 2 });

        var pages = new (string Name, string Key, string Icon)[]
        {
            ("Buttons", "buttons", "\uE8B7"),
            ("Curves", "curves", "\uE9D9"),
            ("Macros", "macros", "\uE7C8"),
            ("Vibration", "vibration", "\uE83F"),
            ("PC Mode", "pcmode", "\uE7F4"),
            ("Device", "device", "\uE964"),
            ("Diagnostics", "diagnostics", "\uE9D9"),
            ("Mouse", "mouse", "\uE962"),
            ("Setups", "setups", "\uE71D"),
            ("Tools", "tools", "\uE713")
        };

        for (int i = 0; i < pages.Length; i++)
        {
            var p = pages[i];
            bool lit = string.Equals(p.Key, active, StringComparison.OrdinalIgnoreCase);
            double y = 130 + i * 74;

            var lantern = BlossomKit.PaperLantern(lit ? 50 : 42, lit ? 62 : 54, lit, p.Icon, null, lit ? 1.0 : 0.6, 14);
            Canvas.SetLeft(lantern, 88 - lantern.Width / 2.0);
            Canvas.SetTop(lantern, y);
            board.Children.Add(lantern);

            if (lit)
            {
                var labelTb = new TextBlock
                {
                    Text = p.Name,
                    FontFamily = BlossomKit.Display,
                    FontSize = 20,
                    Foreground = BlossomKit.B("#ffd3a8"),
                    Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.7 }
                };
                Canvas.SetLeft(labelTb, 122);
                Canvas.SetTop(labelTb, y + 16);
                board.Children.Add(labelTb);
            }
        }
    }

    /// <summary>
    /// Renders the Tools Hub: 8 festival stalls (Yatai) in a 4x2 grid under lanterns.
    /// Strictly implements t_blossom.py:hub().
    /// </summary>
    private void RenderBlossomHub(Canvas board)
    {
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        board.Children.Add(sky);

        board.Children.Add(BlossomKit.DriftingPetals(new Rect(240, 150, 1320, 120), 6, 13));
        RenderBlossomLeftNav(board, "tools");

        // Title & Subtitle
        var titleTb = new TextBlock { Text = "Tools", FontFamily = BlossomKit.Display, FontSize = 52, Foreground = BlossomKit.TextWhiteBrush };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        board.Children.Add(titleTb);

        var leadTb = new TextBlock { Text = "Calibration, themes, PC mode routing and hardware diagnostic tools.", FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.TextMuteBrush };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        board.Children.Add(leadTb);

        // Top Recommended Fix Banner
        var fixTitle = new TextBlock { Text = "RECOMMENDED ACTION", FontFamily = BlossomKit.Small, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = BlossomKit.TextSakuraBrush };
        Canvas.SetLeft(fixTitle, 236);
        Canvas.SetTop(fixTitle, 150);
        board.Children.Add(fixTitle);

        var fixLead = new TextBlock { Text = "Calibrate analog stick centerpoints before competitive tournament play.", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.TextWhiteBrush };
        Canvas.SetLeft(fixLead, 236);
        Canvas.SetTop(fixLead, 170);
        board.Children.Add(fixLead);

        // Suspension wire for stalls top
        board.Children.Add(new Line { X1 = 800, Y1 = 150, X2 = 1520, Y2 = 150, Stroke = BlossomKit.B("#4a2c38"), StrokeThickness = 2 });

        // 8 Festival Stalls in 4x2 Grid
        var stalls = new (string Icon, string Name, string Sub, bool Active, Action Act)[]
        {
            ("\uE790", "Themes", "Changes how X20CTL looks across all consoles and drafts.", true, () => ShowThemes(true)),
            ("\uE7F4", "PC Mode", "Direct hardware polling pipeline bypassing game layers.", false, () => ShowPcMode(true)),
            ("\uE9D9", "Diagnostics", "Raw input stream, stick centering and trigger fill.", false, () => { }),
            ("\uE962", "Mouse", "Control the mouse cursor using analog thumbsticks.", false, () => { }),
            ("\uE768", "Tester", "Hardware button switch verification and latency.", false, () => { }),
            ("\uE8B5", "Scanner", "Deep inspection of controller memory and calibrations.", false, () => { }),
            ("\uE946", "About", "Version, developer credits and licensing notices.", false, () => ShowAbout(true)),
            ("\uE777", "Feedback", "Send impressions, bug reports, and hardware suggestions.", false, () => ShowFeedback(true))
        };

        for (int i = 0; i < stalls.Length; i++)
        {
            var st = stalls[i];
            double sx = 236 + (i % 4) * 330;
            double sy = 296 + (i / 4) * 276;

            var stallElem = BlossomKit.FestivalStall(300, 252, st.Icon, st.Name, st.Sub, st.Active);
            stallElem.Cursor = Cursors.Hand;
            stallElem.MouseLeftButtonUp += (_, _) => st.Act();
            Canvas.SetLeft(stallElem, sx);
            Canvas.SetTop(stallElem, sy);
            board.Children.Add(stallElem);
        }

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Open"));
        hintsStack.Children.Add(CreateHint("B", "Back"));
        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        board.Children.Add(hintsStack);
    }

    /// <summary>
    /// Renders the Themes Pavilion: traditional Japanese pavilion with sliding shoji screen windows.
    /// Strictly implements t_blossom.py:tools().
    /// </summary>
    private void RenderBlossomThemes(Canvas board)
    {
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        board.Children.Add(sky);

        board.Children.Add(BlossomKit.DriftingPetals(new Rect(240, 150, 1320, 600), 8, 3));
        RenderBlossomLeftNav(board, "tools");

        // Title & Lead
        var titleTb = new TextBlock { Text = "Themes", FontFamily = BlossomKit.Display, FontSize = 52, Foreground = BlossomKit.TextWhiteBrush };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        board.Children.Add(titleTb);

        var leadTb = new TextBlock { Text = "Changes how X20CTL looks. Your controllers and drafts stay as they are.", FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.TextMuteBrush };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        board.Children.Add(leadTb);

        // Pavilion Architecture
        double x0 = 230, y0 = 172, pw = 1320, ph = 470;

        // Top timber roof beam
        var roofBeam = new Border
        {
            Width = pw + 36,
            Height = 24,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x4a, 0x2e, 0x20), 0.0),
                    new GradientStop(Color.FromRgb(0x1c, 0x10, 0x0b), 1.0)
                }
            },
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 10, Opacity = 0.5 }
        };
        Canvas.SetLeft(roofBeam, x0 - 18);
        Canvas.SetTop(roofBeam, y0 - 24);
        board.Children.Add(roofBeam);

        // Pavilion Interior Back
        var pavBack = new Border
        {
            Width = pw,
            Height = ph,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x24, 0x14, 0x0f), 0.0),
                    new GradientStop(Color.FromRgb(0x15, 0x0a, 0x08), 1.0)
                }
            },
            BorderBrush = BlossomKit.B("rgba(255,200,160,0.06)"),
            BorderThickness = new Thickness(1)
        };
        Canvas.SetLeft(pavBack, x0);
        Canvas.SetTop(pavBack, y0);
        board.Children.Add(pavBack);

        // Bottom timber deck
        var deckBeam = new Border
        {
            Width = pw + 36,
            Height = 22,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x5a, 0x3a, 0x28), 0.0),
                    new GradientStop(Color.FromRgb(0x2a, 0x18, 0x10), 1.0)
                }
            }
        };
        Canvas.SetLeft(deckBeam, x0 - 18);
        Canvas.SetTop(deckBeam, y0 + ph);
        board.Children.Add(deckBeam);

        // 10 Theme Windows (5x2 Grid)
        double cw = 238, gap = 20;
        double th = cw * 9.0 / 16.0;

        var themeList = ThemeCatalog.All.ToArray();
        for (int i = 0; i < Math.Min(10, themeList.Length); i++)
        {
            var t = themeList[i];
            bool on = t.Id == "blossom";
            double wx = x0 + 22 + (i % 5) * (cw + gap);
            double wy = y0 + 26 + (i / 5) * (th + 92);

            // Procedural Theme Window Preview
            var winBorder = new Border
            {
                Width = cw,
                Height = th,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(t.Void),
                BorderBrush = on ? BlossomKit.B("rgba(255,190,130,0.7)") : BlossomKit.B("#3a2418"),
                BorderThickness = new Thickness(on ? 4 : 4),
                ClipToBounds = true,
                Cursor = Cursors.Hand
            };

            if (on)
            {
                winBorder.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0xff, 0xa0, 0x5a),
                    BlurRadius = 38,
                    ShadowDepth = 0,
                    Opacity = 0.55
                };
            }

            // Window Artwork or Procedural Graphic
            string thumbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", t.Id, "thumb.png");
            if (!File.Exists(thumbPath)) thumbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Brand", "themes", t.Id, "thumb.png");

            if (File.Exists(thumbPath))
            {
                var thumbImg = new Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(thumbPath))),
                    Stretch = Stretch.UniformToFill
                };
                if (!on) thumbImg.Opacity = 0.65;
                winBorder.Child = thumbImg;
            }
            else
            {
                // Procedural swatch
                var swatchGrid = new Grid();
                var grad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = { new(t.Deep, 0.0), new(t.Void, 1.0) }
                };
                swatchGrid.Background = grad;
                var dot = new Ellipse { Width = 28, Height = 28, Fill = new SolidColorBrush(t.Accent), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                swatchGrid.Children.Add(dot);
                winBorder.Child = swatchGrid;
            }

            Canvas.SetLeft(winBorder, wx);
            Canvas.SetTop(winBorder, wy);
            board.Children.Add(winBorder);

            // Label & Badge below window
            var winLabelStack = new Grid { Width = cw };
            winLabelStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            winLabelStack.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameTb = new TextBlock
            {
                Text = t.Name,
                FontFamily = BlossomKit.Display,
                FontSize = 21,
                Foreground = on ? BlossomKit.TextAmberBrush : BlossomKit.B("#c9b3bc"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameTb, 0);
            winLabelStack.Children.Add(nameTb);

            if (on)
            {
                var inUseTag = new Border
                {
                    Padding = new Thickness(8, 2, 8, 2),
                    CornerRadius = new CornerRadius(2),
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(BlossomKit.WarmFire1, 0.0),
                            new GradientStop(BlossomKit.WarmFire2, 1.0)
                        }
                    },
                    Child = new TextBlock
                    {
                        Text = "IN USE",
                        FontFamily = BlossomKit.Small,
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = BlossomKit.B("#4a140f")
                    }
                };
                Grid.SetColumn(inUseTag, 1);
                winLabelStack.Children.Add(inUseTag);
            }

            Canvas.SetLeft(winLabelStack, wx);
            Canvas.SetTop(winLabelStack, wy + th + 14);
            board.Children.Add(winLabelStack);
        }

        // Bottom Intro Video Panel
        var vidPanel = new Border
        {
            Width = pw,
            Height = 96,
            CornerRadius = new CornerRadius(6),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(204, 46, 19, 40), 0.0),
                    new GradientStop(Color.FromArgb(204, 26, 10, 24), 1.0)
                }
            },
            BorderBrush = BlossomKit.B("rgba(255,210,225,0.10)"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(28, 0, 28, 0)
        };
        var vpGrid = new Grid();
        vpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        vpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        vpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var vidIcon = new TextBlock { Text = "\uE714", FontFamily = BlossomKit.Icons, FontSize = 24, Foreground = BlossomKit.B("#ffb56b"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 22, 0) };
        Grid.SetColumn(vidIcon, 0);
        vpGrid.Children.Add(vidIcon);

        var vidTxtStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        vidTxtStack.Children.Add(new TextBlock { Text = "Theme Opening Film", FontFamily = BlossomKit.Heading, FontSize = 22, Foreground = BlossomKit.TextWhiteBrush });
        vidTxtStack.Children.Add(new TextBlock { Text = "A cinematic katana strike cuts a falling sakura petal, blooming into the theme mark.", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextMuteBrush });
        Grid.SetColumn(vidTxtStack, 1);
        vpGrid.Children.Add(vidTxtStack);

        var togStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var togBorder = new Border
        {
            Width = 50,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(BlossomKit.WarmFire2, 0.0),
                    new GradientStop(BlossomKit.WarmFire1, 1.0)
                }
            },
            Margin = new Thickness(0, 0, 10, 0)
        };
        togBorder.Child = new Ellipse { Width = 18, Height = 18, Fill = BlossomKit.B("#fff4d8"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 4, 0) };
        togStack.Children.Add(togBorder);
        togStack.Children.Add(new TextBlock { Text = "Play on launch", FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(togStack, 2);
        vpGrid.Children.Add(togStack);

        vidPanel.Child = vpGrid;
        Canvas.SetLeft(vidPanel, x0);
        Canvas.SetTop(vidPanel, 686);
        board.Children.Add(vidPanel);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Apply theme"));
        hintsStack.Children.Add(CreateHint("B", "Back"));
        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        board.Children.Add(hintsStack);
    }

    /// <summary>
    /// Renders the About scroll with giant moon, logo, and credits.
    /// Strictly implements t_blossom.py:about().
    /// </summary>
    private void RenderBlossomAbout(Canvas board)
    {
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        board.Children.Add(sky);

        // Giant Moon on Left
        var moon = BlossomKit.MoonSphere(640);
        Canvas.SetLeft(moon, 60);
        Canvas.SetTop(moon, 60);
        board.Children.Add(moon);

        // Canopy branch & petals
        board.Children.Add(BlossomKit.CanopyBranch("top", 61));
        board.Children.Add(BlossomKit.DriftingPetals(new Rect(100, 120, 580, 700), 20, 62));

        // Massive Blossom Theme Logo on Left
        var logo = BlossomKit.BlossomLogo(420);
        if (logo != null)
        {
            logo.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 36, ShadowDepth = 20, Opacity = 0.85 };
            Canvas.SetLeft(logo, 150);
            Canvas.SetTop(logo, 250);
            board.Children.Add(logo);
        }

        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock { Text = "X20CTL", FontFamily = BlossomKit.Display, FontSize = 72, Foreground = BlossomKit.TextWhiteBrush });
        titleStack.Children.Add(new TextBlock { Text = "Version 1.04 · Yozakura Console Edition", FontFamily = BlossomKit.Heading, FontStyle = FontStyles.Italic, FontSize = 21, Foreground = BlossomKit.TextSakuraBrush, Margin = new Thickness(0, 8, 0, 0) });
        Canvas.SetLeft(titleStack, 158);
        Canvas.SetTop(titleStack, 580);
        board.Children.Add(titleStack);

        // Right Scroll Panel (width 820)
        var scrollPanel = new Border
        {
            Width = 820,
            Height = 680,
            CornerRadius = new CornerRadius(6),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(204, 46, 19, 40), 0.0),
                    new GradientStop(Color.FromArgb(204, 26, 10, 24), 1.0)
                }
            },
            BorderBrush = BlossomKit.B("rgba(255,210,225,0.18)"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(36, 30, 36, 30)
        };

        var spStack = new StackPanel();
        spStack.Children.Add(new TextBlock { Text = "About", FontFamily = BlossomKit.Display, FontSize = 40, Foreground = BlossomKit.TextWhiteBrush });
        spStack.Children.Add(new TextBlock
        {
            Text = "X20CTL is an open-source hardware control environment built for competitive precision gamepads. " +
                   "Engineered for sub-millisecond USB polling, raw Hall-effect telemetry, and deep hardware customization.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            LineHeight = 24,
            TextWrapping = TextWrapping.Wrap,
            Foreground = BlossomKit.TextWhiteBrush,
            Margin = new Thickness(0, 12, 0, 8)
        });
        spStack.Children.Add(new TextBlock
        {
            Text = "The Yozakura theme celebrates night hanami: gathering in the dusk under lantern light beneath blooming cherry blossoms.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            LineHeight = 24,
            TextWrapping = TextWrapping.Wrap,
            Foreground = BlossomKit.TextWhiteBrush,
            Margin = new Thickness(0, 0, 0, 20)
        });

        // Contributor credits
        var people = new (string Role, string Name)[]
        {
            ("LEAD ARCHITECT", "Antigravity Pairing Team"),
            ("THEME DIRECTION", "Yozakura Night Hanami"),
            ("HARDWARE PROTOCOL", "EasySMX 1000Hz USB / 2.4G"),
            ("UI FRAMEWORK", "WPF / .NET 10 Offscreen Native")
        };
        foreach (var p in people)
        {
            var pRow = new Border
            {
                BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 8, 0, 8)
            };
            var pg = new Grid();
            pg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            pg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pg.Children.Add(new TextBlock { Text = p.Role, FontFamily = BlossomKit.Small, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = BlossomKit.TextSakuraBrush });
            var nameTb = new TextBlock { Text = p.Name, FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.TextWhiteBrush };
            Grid.SetColumn(nameTb, 1);
            pg.Children.Add(nameTb);
            pRow.Child = pg;
            spStack.Children.Add(pRow);
        }

        // Action Buttons
        var aRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 0) };
        var bClose = new Border
        {
            Height = 44,
            Padding = new Thickness(22, 0, 22, 0),
            CornerRadius = new CornerRadius(22),
            Background = BlossomKit.CreateWarmLanternBrush(),
            Margin = new Thickness(0, 0, 12, 0),
            Cursor = Cursors.Hand
        };
        var bcSt = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        bcSt.Children.Add(CreateKeyBadge("B", true));
        bcSt.Children.Add(new TextBlock { Text = "Close", FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.B("#4a140f"), Margin = new Thickness(10, 0, 0, 0) });
        bClose.Child = bcSt;
        bClose.MouseLeftButtonUp += (_, _) => ShowAbout(false);
        aRow.Children.Add(bClose);

        foreach (var act in new[] { "GitHub Repository", "Documentation" })
        {
            var bOther = new Border
            {
                Height = 44,
                Padding = new Thickness(20, 0, 20, 0),
                CornerRadius = new CornerRadius(22),
                BorderBrush = BlossomKit.B("rgba(246,233,236,0.5)"),
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = act, FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            aRow.Children.Add(bOther);
        }
        spStack.Children.Add(aRow);

        scrollPanel.Child = spStack;
        Canvas.SetLeft(scrollPanel, 700);
        Canvas.SetTop(scrollPanel, 110);
        board.Children.Add(scrollPanel);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("B", "Back to Tools"));
        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        board.Children.Add(hintsStack);
    }

    /// <summary>
    /// Renders Feedback: wish hung on a cherry branch; message on a wooden votive tablet (Ema plaque).
    /// Strictly implements t_blossom.py:feedback().
    /// </summary>
    private void RenderBlossomFeedback(Canvas board)
    {
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        board.Children.Add(sky);

        RenderBlossomLeftNav(board, "tools");

        // Title & Lead
        var titleTb = new TextBlock { Text = "Feedback", FontFamily = BlossomKit.Display, FontSize = 52, Foreground = BlossomKit.TextWhiteBrush };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        board.Children.Add(titleTb);

        var leadTb = new TextBlock { Text = "Hang your impressions, feature requests, and bug reports on the cherry blossom branch.", FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.TextMuteBrush };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        board.Children.Add(leadTb);

        // Weeping cherry branch across left at y = 200
        var branchGeom = new PathGeometry();
        var bFig = new PathFigure { StartPoint = new Point(220, 198) };
        bFig.Segments.Add(new BezierSegment(new Point(400, 212), new Point(600, 204), new Point(900, 212), true));
        branchGeom.Figures.Add(bFig);
        board.Children.Add(new System.Windows.Shapes.Path { Data = branchGeom, Stroke = BlossomKit.B("#08040a"), StrokeThickness = 12 });

        // Blossom clusters along the branch
        for (int i = 0; i < 16; i++)
        {
            double bx = 240 + i * 42;
            double by = 200 + Math.Sin(i * 0.4) * 14;
            var fl = BlossomKit.BlossomFlower(12, i * 45, BlossomKit.PetalHot, 0.9);
            Canvas.SetLeft(fl, bx);
            Canvas.SetTop(fl, by);
            board.Children.Add(fl);
        }

        // Hanging Washi Paper Kind Tags
        var kinds = new[] { "Bug report", "Feature request", "Hardware help", "General inquiry" };
        for (int i = 0; i < kinds.Length; i++)
        {
            double kx = 246 + i * 160;
            bool on = i == 0;

            // Suspension thread
            board.Children.Add(new Line { X1 = kx + 75, Y1 = 206, X2 = kx + 75, Y2 = 252, Stroke = BlossomKit.B("#4a2c38"), StrokeThickness = 1.5 });

            // Paper Tag
            var tagBorder = new Border
            {
                Width = 150,
                Height = 96,
                CornerRadius = new CornerRadius(2),
                Background = on ? BlossomKit.B("#fff1d6") : BlossomKit.B("#4a1f36"),
                BorderBrush = on ? BlossomKit.B("transparent") : BlossomKit.B("rgba(255,200,215,0.14)"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 12, 14, 12),
                Cursor = Cursors.Hand
            };
            if (on)
            {
                tagBorder.Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0xa0, 0x5a), BlurRadius = 30, ShadowDepth = 0, Opacity = 0.5 };
            }
            tagBorder.Child = new TextBlock
            {
                Text = kinds[i],
                FontFamily = BlossomKit.Body,
                FontSize = 17,
                Foreground = on ? BlossomKit.B("#3b1712") : BlossomKit.TextWhiteBrush,
                TextWrapping = TextWrapping.Wrap
            };
            Canvas.SetLeft(tagBorder, kx);
            Canvas.SetTop(tagBorder, 252);
            board.Children.Add(tagBorder);
        }

        // Dropdowns on Left (Category & Controller Model)
        var formStack = new StackPanel { Width = 600 };
        foreach (var (lbl, val) in new[] { ("CATEGORY", "Controller Button Mapping · Deadband Threshold"), ("HARDWARE TARGET", "EasySMX X20 Pro · Obsidian Edition") })
        {
            var fGroup = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            fGroup.Children.Add(new TextBlock { Text = lbl, FontFamily = BlossomKit.Small, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = BlossomKit.TextSakuraBrush, Margin = new Thickness(0, 0, 0, 8) });
            var ddBorder = new Border
            {
                Height = 50,
                CornerRadius = new CornerRadius(18),
                Background = BlossomKit.B("rgba(255,220,232,0.06)"),
                BorderBrush = BlossomKit.B("rgba(255,220,232,0.2)"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 0, 18, 0)
            };
            var ddGrid = new Grid();
            ddGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ddGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ddGrid.Children.Add(new TextBlock { Text = val, FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });
            var chev = new TextBlock { Text = "▾", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextMuteBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(chev, 1);
            ddGrid.Children.Add(chev);
            ddBorder.Child = ddGrid;
            fGroup.Children.Add(ddBorder);
            formStack.Children.Add(fGroup);
        }
        Canvas.SetLeft(formStack, 256);
        Canvas.SetTop(formStack, 392);
        board.Children.Add(formStack);

        // 4. Right Side: Authentic Wooden Votive Tablet (Ema Plaque) at (940, 160)
        // Timber support beam at top
        var timberRoof = new Border
        {
            Width = 648,
            Height = 12,
            CornerRadius = new CornerRadius(4),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(BlossomKit.WoodRoof, 0.0),
                    new GradientStop(BlossomKit.WoodTimber, 1.0)
                }
            }
        };
        Canvas.SetLeft(timberRoof, 920);
        Canvas.SetTop(timberRoof, 150);
        board.Children.Add(timberRoof);

        var ema = BlossomKit.EmaPlaque(
            604,
            430,
            "The 1000Hz polling rate in PC mode feels exceptionally responsive in combat titles. Would love custom stick curve presets for fighting games.",
            "134 / 2000 characters");
        Canvas.SetLeft(ema, 940);
        Canvas.SetTop(ema, 162);
        board.Children.Add(ema);

        // Submit Action Buttons
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
        var sendBtn = new Border
        {
            Height = 46,
            Padding = new Thickness(24, 0, 24, 0),
            CornerRadius = new CornerRadius(23),
            Background = BlossomKit.CreateWarmLanternBrush(),
            Margin = new Thickness(0, 0, 12, 0),
            Cursor = Cursors.Hand,
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.6 }
        };
        var sSt = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        sSt.Children.Add(CreateKeyBadge("A", true));
        sSt.Children.Add(new TextBlock { Text = "Send feedback", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.B("#4a140f"), Margin = new Thickness(10, 0, 0, 0) });
        sendBtn.Child = sSt;
        btnRow.Children.Add(sendBtn);

        var cancelBtn = new Border
        {
            Height = 46,
            Padding = new Thickness(22, 0, 22, 0),
            CornerRadius = new CornerRadius(23),
            BorderBrush = BlossomKit.B("rgba(246,233,236,0.5)"),
            BorderThickness = new Thickness(1.5),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "Cancel", FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
        };
        cancelBtn.MouseLeftButtonUp += (_, _) => ShowFeedback(false);
        btnRow.Children.Add(cancelBtn);

        Canvas.SetLeft(btnRow, 940);
        Canvas.SetTop(btnRow, 660);
        board.Children.Add(btnRow);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Send feedback"));
        hintsStack.Children.Add(CreateHint("B", "Back to Tools"));
        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        board.Children.Add(hintsStack);
    }

    private FrameworkElement CreateKeyBadge(string key, bool active = false)
    {
        var border = new Border
        {
            MinWidth = 28,
            Height = 28,
            Padding = new Thickness(6, 0, 6, 0),
            CornerRadius = new CornerRadius(14),
            Background = active ? BlossomKit.CreateWarmLanternBrush() : BlossomKit.B("transparent"),
            BorderBrush = active ? BlossomKit.B("transparent") : BlossomKit.B("rgba(246,233,236,0.65)"),
            BorderThickness = new Thickness(1.5),
            VerticalAlignment = VerticalAlignment.Center
        };
        var tb = new TextBlock
        {
            Text = key,
            FontFamily = BlossomKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = active ? BlossomKit.B("#4a140f") : BlossomKit.TextWhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = tb;
        return border;
    }

    private FrameworkElement CreateHint(string key, string text)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 26, 0) };
        stack.Children.Add(CreateKeyBadge(key));
        stack.Children.Add(new TextBlock { Text = text, FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.B("#e3cfd6"), Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return stack;
    }

    private void RenderBlossomPcMode(Canvas board)
    {
        // Moon in top right
        var moon = BlossomKit.MoonSphere(160);
        Canvas.SetLeft(moon, 1300);
        Canvas.SetTop(moon, 20);
        board.Children.Add(moon);

        // Weeping cherry branch across the top
        var branch = BlossomKit.CanopyBranch("home");
        Canvas.SetLeft(branch, 0);
        Canvas.SetTop(branch, 0);
        board.Children.Add(branch);

        // Drifting petals
        board.Children.Add(BlossomKit.DriftingPetals(new Rect(100, 100, 1400, 700), 18, 55));

        // Left Navigation Bar (Yatai Stalls)
        RenderBlossomLeftNav(board, "pcmode");

        // Header Title
        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock { Text = "PC Mode Configuration", FontFamily = BlossomKit.Display, FontSize = 36, Foreground = BlossomKit.TextWhiteBrush });
        titleStack.Children.Add(new TextBlock { Text = "Virtual USB routing & ViGEmBus controller emulation", FontFamily = BlossomKit.Heading, FontStyle = FontStyles.Italic, FontSize = 18, Foreground = BlossomKit.TextSakuraBrush, Margin = new Thickness(0, 4, 0, 0) });
        Canvas.SetLeft(titleStack, 234);
        Canvas.SetTop(titleStack, 44);
        board.Children.Add(titleStack);

        // Sacred Path with 4 Tōrō Lanterns and Pipeline Stages
        double startX = 234, startY = 130, stageW = 310, stageH = 150, gap = 16;
        var stages = new (string Num, string Title, string Sub, string Detail)[]
        {
            ("1", "Physical Controller", "Direct Hardware Link", "1000Hz USB polling · sub-1ms"),
            ("2", "Kernel Filter Driver", "Raw HID Intercept", "Zero-jitter timestamping engine"),
            ("3", "Virtual Device Emulation", "ViGEmBus Emulation", "Xbox 360 / DualShock 4 feed"),
            ("4", "Target Application", "Game Engine / Steam", "Native XInput / DirectInput feed")
        };

        for (int i = 0; i < stages.Length; i++)
        {
            var s = stages[i];
            double sx = startX + i * (stageW + gap);

            // Stepping Stone / Shoji Card
            var stageCard = BlossomKit.LitPaperCard(stageW, stageH);
            Canvas.SetLeft(stageCard, sx);
            Canvas.SetTop(stageCard, startY);
            board.Children.Add(stageCard);

            // Carved Stone Lantern (Tōrō) atop the stepping stone
            var toro = BlossomKit.StoneLanternToro(0.72, true);
            Canvas.SetLeft(toro, sx + stageW - 54);
            Canvas.SetTop(toro, startY - 26);
            board.Children.Add(toro);

            var sp = new StackPanel { Margin = new Thickness(18, 16, 18, 16) };
            var hRow = new StackPanel { Orientation = Orientation.Horizontal };
            hRow.Children.Add(new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = BlossomKit.SamuraiVermilionBrush,
                Child = new TextBlock { Text = s.Num, FontFamily = BlossomKit.Display, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = BlossomKit.TextWhiteBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            });
            hRow.Children.Add(new TextBlock { Text = s.Title, FontFamily = BlossomKit.Display, FontSize = 16, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), Margin = new Thickness(10, 2, 0, 0) });
            sp.Children.Add(hRow);

            sp.Children.Add(new TextBlock { Text = s.Sub, FontFamily = BlossomKit.Heading, FontStyle = FontStyles.Italic, FontSize = 13, Foreground = BlossomKit.B(BlossomKit.LitPaperCrimson), Margin = new Thickness(0, 8, 0, 2) });
            sp.Children.Add(new TextBlock { Text = s.Detail, FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.B(BlossomKit.LitPaperSub) });

            Canvas.SetLeft(sp, sx);
            Canvas.SetTop(sp, startY);
            board.Children.Add(sp);
        }

        // Large Lower Configuration Card
        var configCard = BlossomKit.LitPaperCard(1290, 500);
        Canvas.SetLeft(configCard, 234);
        Canvas.SetTop(configCard, 310);
        board.Children.Add(configCard);

        var configStack = new StackPanel { Margin = new Thickness(36, 30, 36, 30) };
        configStack.Children.Add(new TextBlock { Text = "Routing & Driver Settings", FontFamily = BlossomKit.Display, FontSize = 24, Foreground = BlossomKit.B(BlossomKit.LitPaperInk) });
        configStack.Children.Add(new TextBlock { Text = "Manage high-performance virtual gamepad drivers and kernel-level polling optimization.", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.B(BlossomKit.LitPaperSub), Margin = new Thickness(0, 4, 0, 24) });

        var options = new (string Name, string Desc, string Status, bool On)[]
        {
            ("ViGEmBus Virtual Bus Driver", "Provides system-level virtual Xbox 360 and DualShock 4 device emulation.", "Installed & Active (v1.22.0)", true),
            ("1000 Hz Sub-millisecond USB Polling", "Overrides standard 125Hz polling rate for 1ms response latency.", "Enabled · Active on Port 3", true),
            ("Low-Jitter Kernel Timestamping", "Hardware microsecond clock calibration for consistent frame delivery.", "Active", true),
            ("Auto-Fallback to Native XInput", "Gracefully falls back to physical gamepad if virtual bus driver restarts.", "Enabled", true)
        };

        foreach (var opt in options)
        {
            var row = new Border
            {
                BorderBrush = BlossomKit.B("rgba(74,20,15,0.12)"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 10, 0, 10)
            };
            var rg = new Grid();
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

            var nameStack = new StackPanel();
            nameStack.Children.Add(new TextBlock { Text = opt.Name, FontFamily = BlossomKit.Display, FontSize = 16, Foreground = BlossomKit.B(BlossomKit.LitPaperInk) });
            nameStack.Children.Add(new TextBlock { Text = opt.Desc, FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.B(BlossomKit.LitPaperSub), Margin = new Thickness(0, 2, 0, 0) });
            rg.Children.Add(nameStack);

            var statTb = new TextBlock { Text = opt.Status, FontFamily = BlossomKit.Body, FontSize = 14, Foreground = opt.On ? BlossomKit.B("#1c542d") : BlossomKit.B(BlossomKit.LitPaperSub), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(statTb, 1);
            rg.Children.Add(statTb);

            var switchPill = new Border
            {
                Width = 90,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = opt.On ? BlossomKit.CreateWarmLanternBrush() : BlossomKit.B("rgba(74,20,15,0.1)"),
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock
                {
                    Text = opt.On ? "ACTIVE" : "OFF",
                    FontFamily = BlossomKit.Display,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = opt.On ? BlossomKit.B("#4a140f") : BlossomKit.B(BlossomKit.LitPaperSub),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(switchPill, 2);
            rg.Children.Add(switchPill);

            row.Child = rg;
            configStack.Children.Add(row);
        }

        // Action Buttons
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 28, 0, 0) };
        var bSave = new Border
        {
            Height = 44,
            Padding = new Thickness(24, 0, 24, 0),
            CornerRadius = new CornerRadius(22),
            Background = BlossomKit.CreateWarmLanternBrush(),
            Margin = new Thickness(0, 0, 14, 0),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "Save Configuration", FontFamily = BlossomKit.Display, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = BlossomKit.B("#4a140f"), VerticalAlignment = VerticalAlignment.Center }
        };
        actionRow.Children.Add(bSave);

        var bBack = new Border
        {
            Height = 44,
            Padding = new Thickness(24, 0, 24, 0),
            CornerRadius = new CornerRadius(22),
            BorderBrush = BlossomKit.B(BlossomKit.LitPaperInk),
            BorderThickness = new Thickness(1.5),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "Back to Tools", FontFamily = BlossomKit.Display, FontSize = 16, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center }
        };
        bBack.MouseLeftButtonUp += (_, _) => ShowPcMode(false);
        actionRow.Children.Add(bBack);

        configStack.Children.Add(actionRow);

        Canvas.SetLeft(configStack, 234);
        Canvas.SetTop(configStack, 310);
        board.Children.Add(configStack);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Save"));
        hintsStack.Children.Add(CreateHint("B", "Back to Tools"));
        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        board.Children.Add(hintsStack);
    }
}

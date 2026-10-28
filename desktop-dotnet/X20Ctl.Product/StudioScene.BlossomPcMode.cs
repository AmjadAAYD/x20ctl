using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;

namespace X20Ctl.Product;

/// <summary>
/// PC Mode workbench presentation for the Yozakura (Blossom) theme:
/// "The route is a sacred path of stone lanterns.
/// When PC mode is enabled, 1000Hz holy fire illuminates their carved apertures."
/// Strictly implements t_blossom.py:pcmode().
/// </summary>
public partial class StudioScene
{
    private void RenderBlossomPcMode(Canvas host)
    {
        // 1. Sky & Backdrop
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(240, 160, 1320, 200), 8, 7));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "pcmode");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "PC Mode",
            FontFamily = BlossomKit.Display,
            FontSize = 52,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        host.Children.Add(titleTb);

        var leadTb = new TextBlock
        {
            Text = "Direct hardware polling pipeline bypassing game translation layers.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        host.Children.Add(leadTb);

        // 3. Sacred Path of Stone Lanterns (Tōrō) at (230, 160, width 1330, height 160)
        double px0 = 230, py0 = 160, pw = 1330, ph = 160;

        var pathBack = new Border
        {
            Width = pw,
            Height = ph,
            CornerRadius = new CornerRadius(6),
            Background = BlossomKit.B("rgba(10,4,10,0.35)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.1)"),
            BorderThickness = new Thickness(1)
        };
        Canvas.SetLeft(pathBack, px0);
        Canvas.SetTop(pathBack, py0);
        host.Children.Add(pathBack);

        var pathCanvas = new Canvas { Width = pw, Height = ph };

        // Stone stepping stones between lanterns
        double[] xs = [170, 500, 830, 1160];
        for (int i = 0; i < 3; i++)
        {
            foreach (double f in new[] { 0.22, 0.40, 0.58, 0.76 })
            {
                double sx = xs[i] + (xs[i + 1] - xs[i]) * f;
                double sy = 126 + 4 * Math.Sin(f * 9);
                double rx = 22 - 6 * Math.Abs(f - 0.5);

                var stone = new Ellipse
                {
                    Width = rx * 2,
                    Height = 14,
                    Fill = BlossomKit.B("#3a2b34")
                };
                Canvas.SetLeft(stone, sx - rx);
                Canvas.SetTop(stone, sy - 7);
                pathCanvas.Children.Add(stone);
            }
        }

        // 4 Carved Granite Stone Lanterns along the sacred path
        var routeStages = new[]
        {
            ("Controller", true),
            ("2.4G Receiver", true),
            ("XInput 1000Hz", true),
            ("Windows OS", true)
        };

        for (int i = 0; i < 4; i++)
        {
            double lx = xs[i];
            var toro = BlossomKit.StoneLanternToro(0.9, routeStages[i].Item2);
            Canvas.SetLeft(toro, lx - toro.Width / 2.0);
            Canvas.SetTop(toro, 4);
            pathCanvas.Children.Add(toro);

            var stLabel = new TextBlock
            {
                Text = routeStages[i].Item1,
                FontFamily = BlossomKit.Body,
                FontSize = 17,
                Foreground = routeStages[i].Item2 ? BlossomKit.TextWhiteBrush : BlossomKit.TextMuteBrush,
                TextAlignment = TextAlignment.Center,
                Width = 240
            };
            Canvas.SetLeft(stLabel, lx - 120);
            Canvas.SetTop(stLabel, 134);
            pathCanvas.Children.Add(stLabel);
        }
        pathBack.Child = pathCanvas;

        // Path Subtitle
        var pathSubTb = new TextBlock
        {
            Text = "Direct Kernel Pipeline · Sub-millisecond Hardware Frame Dispatch · Zero Interpolation",
            FontFamily = BlossomKit.Body,
            FontSize = 14,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(pathSubTb, 236);
        Canvas.SetTop(pathSubTb, 338);
        host.Children.Add(pathSubTb);

        // 4. Bottom Left: Configuration Rows at (236, 378, width 620)
        var leftRows = new StackPanel { Width = 620 };
        var configs = new (string Label, string Val, bool Toggle, bool Active)[]
        {
            ("PC mode pipeline", "Active", true, true),
            ("Hardware polling rate", "1000 Hz", false, false),
            ("Bypass controller curve processing", "Enabled", true, true),
            ("Raw sensor reporting", "16-bit Hall Effect", false, false)
        };

        foreach (var c in configs)
        {
            var rBorder = new Border
            {
                Height = 60,
                BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 0, 16, 0)
            };
            var grid_ = new Grid();
            grid_.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid_.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lab = new TextBlock { Text = c.Label, FontFamily = BlossomKit.Body, FontSize = 18, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lab, 0);
            grid_.Children.Add(lab);

            FrameworkElement valElem;
            if (c.Toggle)
            {
                var tog = new Border
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
                    Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.5 }
                };
                var knob = new Ellipse { Width = 18, Height = 18, Fill = BlossomKit.B("#fff4d8"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 4, 0) };
                tog.Child = knob;
                valElem = tog;
            }
            else
            {
                valElem = new TextBlock { Text = c.Val, FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.B("#c9b3bc"), VerticalAlignment = VerticalAlignment.Center };
            }
            Grid.SetColumn(valElem, 1);
            grid_.Children.Add(valElem);

            rBorder.Child = grid_;
            leftRows.Children.Add(rBorder);
        }
        Canvas.SetLeft(leftRows, 236);
        Canvas.SetTop(leftRows, 378);
        host.Children.Add(leftRows);

        // 5. Bottom Right: Illuminated Washi Parchment Card at (920, 378, width 624, height 450)
        var litCard = BlossomKit.LitPaperCard(624, 450, new Thickness(32, 28, 32, 28));
        var cardStack = new StackPanel();

        cardStack.Children.Add(new TextBlock
        {
            Text = "The Path of PC Mode",
            FontFamily = BlossomKit.Display,
            FontSize = 30,
            Foreground = BlossomKit.B(BlossomKit.LitPaperInk)
        });

        var steps = new[]
        {
            "Bypasses onboard interpolation curves and sends raw sensor positions direct to the driver.",
            "Enables sub-millisecond 1000 Hz USB & wireless packet transmission with zero jitter.",
            "Locks input state memory directly into Windows kernel space for instantaneous responsiveness."
        };

        for (int i = 0; i < steps.Length; i++)
        {
            var stepRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };

            var numBadge = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = BlossomKit.B(BlossomKit.LitPaperCrimson),
                Margin = new Thickness(0, 0, 16, 0),
                Child = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    FontFamily = BlossomKit.Display,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = BlossomKit.B("#fff1d2"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            stepRow.Children.Add(numBadge);

            var sTb = new TextBlock
            {
                Text = steps[i],
                FontFamily = BlossomKit.Body,
                FontSize = 16,
                LineHeight = 23,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
                Width = 490
            };
            stepRow.Children.Add(sTb);
            cardStack.Children.Add(stepRow);
        }

        // Action Button
        var btnBorder = new Border
        {
            Height = 46,
            Padding = new Thickness(24, 0, 24, 0),
            CornerRadius = new CornerRadius(23),
            Background = BlossomKit.B(BlossomKit.LitPaperInk),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 24, 0, 0),
            Cursor = Cursors.Hand
        };
        var btnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        btnStack.Children.Add(CreateKeyBadge("A", true));
        btnStack.Children.Add(new TextBlock
        {
            Text = "Disable PC Mode",
            FontFamily = BlossomKit.Body,
            FontSize = 17,
            Foreground = BlossomKit.B("#ffe2b8"),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        btnBorder.Child = btnStack;
        cardStack.Children.Add(btnBorder);

        litCard.Child = cardStack;
        Canvas.SetLeft(litCard, 920);
        Canvas.SetTop(litCard, 378);
        host.Children.Add(litCard);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Disable PC Mode"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }
}

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
/// Macros workbench presentation for the Yozakura (Blossom) theme:
/// "The sequence floats down a river as lanterns, one per event;
/// the focused event burns brightest, sending shimmering water reflections across the dark stream."
/// Strictly implements t_blossom.py:macros().
/// </summary>
public partial class StudioScene
{
    private void RenderBlossomMacros(Canvas host)
    {
        // 1. Sky & Canopy
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(240, 180, 1320, 250), 12, 5));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "macros");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "Macros",
            FontFamily = BlossomKit.Display,
            FontSize = 52,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        host.Children.Add(titleTb);

        var leadTb = new TextBlock
        {
            Text = "Combine button presses, stick sweeps, and timed delays into single trigger strokes.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        host.Children.Add(leadTb);

        // Top-right Macro Slot Lanterns at (1250, 34)
        var macroSlots = new[] { ("M1", "Rapid Fire", true), ("M2", "Dodge Roll", false), ("M3", "Super Jump", false), ("M4", "Empty", false) };
        for (int i = 0; i < macroSlots.Length; i++)
        {
            var ms = macroSlots[i];
            double mx = 1250 + i * 80;
            var slotLantern = BlossomKit.PaperLantern(40, 52, ms.Item3, null, ms.Item1, ms.Item3 ? 0.9 : 0.5, 18, 16);
            Canvas.SetLeft(slotLantern, mx - slotLantern.Width / 2.0);
            Canvas.SetTop(slotLantern, 34);
            host.Children.Add(slotLantern);

            var slotSubTb = new TextBlock
            {
                Text = ms.Item2,
                FontFamily = BlossomKit.Body,
                FontSize = 12,
                Foreground = ms.Item3 ? BlossomKit.TextAmberBrush : BlossomKit.TextMuteBrush,
                TextAlignment = TextAlignment.Center,
                Width = 80
            };
            Canvas.SetLeft(slotSubTb, mx - 40);
            Canvas.SetTop(slotSubTb, 106);
            host.Children.Add(slotSubTb);
        }

        // 3. Dark Night River with Floating Lantern Events at (230, 190, width 1330, height 236)
        double rx = 230, ry = 190, rw = 1330, rh = 236;

        var riverBack = new Border
        {
            Width = rw,
            Height = rh,
            CornerRadius = new CornerRadius(6),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x1c, 0x0f, 0x22), 0.0),
                    new GradientStop(Color.FromRgb(0x0d, 0x06, 0x0f), 1.0)
                }
            },
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.12)"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        Canvas.SetLeft(riverBack, rx);
        Canvas.SetTop(riverBack, ry);
        host.Children.Add(riverBack);

        // River water ripples
        var riverCanvas = new Canvas { Width = rw, Height = rh };
        for (int j = 0; j < 11; j++)
        {
            double lx = (j * 137) % 1100;
            double ly = 24 + j * 19;
            double len = 60 + (j * 53) % 140;
            riverCanvas.Children.Add(new Line
            {
                X1 = lx,
                Y1 = ly,
                X2 = lx + len,
                Y2 = ly,
                Stroke = BlossomKit.B("rgba(255,220,232,0.06)"),
                StrokeThickness = 1.6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }

        // River sequence header
        var seqTb = new TextBlock
        {
            Text = "SEQUENCE TIMELINE · 3 STEPS · 280MS TOTAL",
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.TextSakuraBrush
        };
        Canvas.SetLeft(seqTb, 20);
        Canvas.SetTop(seqTb, 12);
        riverCanvas.Children.Add(seqTb);

        // Floating Paper Lanterns along the river
        var events = new (string Name, string Letter, string Sub, bool Active, double X)[]
        {
            ("Press A", "A", "Duration 80ms", false, 200),
            ("Wait 45ms", "⏳", "Pre-delay 45ms", true, 500),
            ("Press B", "B", "Duration 120ms", false, 800)
        };

        foreach (var ev in events)
        {
            double fx = ev.X;
            double wy = 128; // water surface line

            // Water reflection streaks if lit
            if (ev.Active)
            {
                var reflGlow = new Ellipse
                {
                    Width = 80,
                    Height = 90,
                    Fill = new RadialGradientBrush
                    {
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(120, 255, 180, 110), 0.0),
                            new GradientStop(Colors.Transparent, 0.8)
                        }
                    },
                    Effect = new BlurEffect { Radius = 8 }
                };
                Canvas.SetLeft(reflGlow, fx - 40);
                Canvas.SetTop(reflGlow, wy + 4);
                riverCanvas.Children.Add(reflGlow);

                for (int k = 0; k < 3; k++)
                {
                    double sx1 = fx - 24 + (k % 2) * 10;
                    double sx2 = fx + 18 - (k % 3) * 8;
                    double sy = wy + 16 + k * 10;
                    riverCanvas.Children.Add(new Line
                    {
                        X1 = sx1,
                        Y1 = sy,
                        X2 = sx2,
                        Y2 = sy,
                        Stroke = BlossomKit.B($"rgba(255,200,140,{0.55 - k * 0.15:F2})"),
                        StrokeThickness = 2.4,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    });
                }
            }

            // Floating paper lantern box (74x66)
            var floaterBox = new Border
            {
                Width = 74,
                Height = 66,
                CornerRadius = new CornerRadius(3),
                Background = ev.Active ? BlossomKit.CreateWarmLanternBrush() : BlossomKit.CreateColdLanternBrush(),
                BorderBrush = ev.Active ? BlossomKit.B("#3a1d14") : BlossomKit.B("rgba(255,200,215,0.2)"),
                BorderThickness = new Thickness(3),
                Cursor = Cursors.Hand
            };

            if (ev.Active)
            {
                floaterBox.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0xff, 0xa0, 0x5a),
                    BlurRadius = 26,
                    ShadowDepth = 0,
                    Opacity = 0.85
                };
            }

            var fTb = new TextBlock
            {
                Text = ev.Letter,
                FontFamily = BlossomKit.Display,
                FontSize = 30,
                Foreground = ev.Active ? BlossomKit.B("#6a1f18") : BlossomKit.B("rgba(255,225,232,0.8)"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            floaterBox.Child = fTb;
            Canvas.SetLeft(floaterBox, fx - 37);
            Canvas.SetTop(floaterBox, wy - 66 - 6);
            riverCanvas.Children.Add(floaterBox);

            // Floating wooden raft base
            var raft = new Border
            {
                Width = 92,
                Height = 12,
                CornerRadius = new CornerRadius(2),
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
            Canvas.SetLeft(raft, fx - 46);
            Canvas.SetTop(raft, wy - 8);
            riverCanvas.Children.Add(raft);

            // Step Label Below
            var stepLabelStack = new StackPanel { Width = 200, HorizontalAlignment = HorizontalAlignment.Center };
            stepLabelStack.Children.Add(new TextBlock
            {
                Text = ev.Name,
                FontFamily = BlossomKit.Display,
                FontSize = 22,
                Foreground = ev.Active ? BlossomKit.B("#ffe6cc") : BlossomKit.B("#e3cfd6"),
                TextAlignment = TextAlignment.Center
            });
            stepLabelStack.Children.Add(new TextBlock
            {
                Text = ev.Sub,
                FontFamily = BlossomKit.Body,
                FontSize = 13,
                Foreground = ev.Active ? BlossomKit.TextAmberBrush : BlossomKit.TextMuteBrush,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Canvas.SetLeft(stepLabelStack, fx - 100);
            Canvas.SetTop(stepLabelStack, wy + 42);
            riverCanvas.Children.Add(stepLabelStack);
        }

        // Add Step Box at x = 1060
        var addBox = new Border
        {
            Width = 74,
            Height = 66,
            CornerRadius = new CornerRadius(3),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.3)"),
            BorderThickness = new Thickness(1.4),
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = "+",
                FontFamily = BlossomKit.Display,
                FontSize = 32,
                Foreground = BlossomKit.B("#c9b3bc"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Canvas.SetLeft(addBox, 1060);
        Canvas.SetTop(addBox, 56);
        riverCanvas.Children.Add(addBox);

        var addTb = new TextBlock
        {
            Text = "Add step",
            FontFamily = BlossomKit.Body,
            FontSize = 17,
            Foreground = BlossomKit.B("#c9b3bc"),
            TextAlignment = TextAlignment.Center,
            Width = 140
        };
        Canvas.SetLeft(addTb, 1027);
        Canvas.SetTop(addTb, 142);
        riverCanvas.Children.Add(addTb);

        riverBack.Child = riverCanvas;

        // 4. Bottom Left: DAW Timeline Waveform & Trigger Selector at (236, 452, width 640)
        var leftBottomStack = new StackPanel { Width = 640 };

        // DAW Timeline Waveform visualizer (640x150)
        var dawBack = new Border
        {
            Width = 640,
            Height = 150,
            CornerRadius = new CornerRadius(4),
            Background = BlossomKit.B("rgba(34,14,30,0.82)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.1)"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        var dawCanvas = new Canvas { Width = 640, Height = 150 };

        // Step timeline bars
        var dawBlocks = new (double X, double W, string Label, Color Col)[]
        {
            (20, 160, "[A] 80ms", Color.FromRgb(0xff, 0xc2, 0x7a)),
            (190, 100, "[WAIT] 45ms", Color.FromRgb(0xf2, 0x78, 0x4f)),
            (300, 240, "[B] 120ms", Color.FromRgb(0xff, 0x5d, 0x8a))
        };
        foreach (var blk in dawBlocks)
        {
            var b = new Border
            {
                Width = blk.W,
                Height = 80,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromArgb(140, blk.Col.R, blk.Col.G, blk.Col.B)),
                BorderBrush = new SolidColorBrush(blk.Col),
                BorderThickness = new Thickness(1.5),
                Child = new TextBlock
                {
                    Text = blk.Label,
                    FontFamily = BlossomKit.Display,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = BlossomKit.TextWhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(b, blk.X);
            Canvas.SetTop(b, 35);
            dawCanvas.Children.Add(b);
        }
        dawBack.Child = dawCanvas;
        leftBottomStack.Children.Add(dawBack);

        // Trigger Selector Row
        var trigRow = new Border
        {
            Height = 56,
            BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(0, 14, 0, 0)
        };
        var trigGrid = new Grid();
        trigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        trigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var trigLab = new TextBlock { Text = "Played by", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center };
        var trigPill = new Border
        {
            Padding = new Thickness(14, 6, 14, 6),
            CornerRadius = new CornerRadius(18),
            Background = BlossomKit.B("rgba(255,220,232,0.06)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.2)"),
            BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = "Left Stick Click ▾", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.TextWhiteBrush }
        };
        Grid.SetColumn(trigLab, 0);
        Grid.SetColumn(trigPill, 1);
        trigGrid.Children.Add(trigLab);
        trigGrid.Children.Add(trigPill);
        trigRow.Child = trigGrid;
        leftBottomStack.Children.Add(trigRow);

        // Action Tool Buttons: Record, Clear, Test Sequence
        var toolStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        foreach (var (ic, txt, prim) in new[] { ("\uE7C8", "Record", false), ("\uE896", "Clear", false), ("\uE768", "Test sequence", true) })
        {
            var btnBorder = new Border
            {
                Height = 42,
                Padding = new Thickness(18, 0, 18, 0),
                CornerRadius = new CornerRadius(21),
                Background = prim ? BlossomKit.CreateWarmLanternBrush() : BlossomKit.B("rgba(255,220,232,0.06)"),
                BorderBrush = prim ? BlossomKit.B("transparent") : BlossomKit.B("rgba(246,233,236,0.5)"),
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = Cursors.Hand
            };
            if (prim)
            {
                btnBorder.Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 16, ShadowDepth = 0, Opacity = 0.6 };
            }
            var bStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            bStack.Children.Add(new TextBlock
            {
                Text = ic,
                FontFamily = BlossomKit.Icons,
                FontSize = 14,
                Foreground = prim ? BlossomKit.B("#4a140f") : BlossomKit.TextWhiteBrush,
                Margin = new Thickness(0, 0, 8, 0)
            });
            bStack.Children.Add(new TextBlock
            {
                Text = txt,
                FontFamily = BlossomKit.Body,
                FontSize = 15,
                Foreground = prim ? BlossomKit.B("#4a140f") : BlossomKit.TextWhiteBrush
            });
            btnBorder.Child = bStack;
            toolStack.Children.Add(btnBorder);
        }
        leftBottomStack.Children.Add(toolStack);

        Canvas.SetLeft(leftBottomStack, 236);
        Canvas.SetTop(leftBottomStack, 452);
        host.Children.Add(leftBottomStack);

        // 5. Bottom Right: Illuminated Washi Parchment Card at (930, 452, width 614, height 374)
        var litCard = BlossomKit.LitPaperCard(614, 374, new Thickness(28, 28, 28, 28));
        var cardStack = new StackPanel();

        cardStack.Children.Add(new TextBlock
        {
            Text = "Event 2 · Wait",
            FontFamily = BlossomKit.Display,
            FontSize = 28,
            Foreground = BlossomKit.B(BlossomKit.LitPaperInk)
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Holds execution for a precise duration before sending the next command.",
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
            Margin = new Thickness(0, 4, 0, 14)
        });

        // Mode chips: Press, Hold, Release, Wait
        var chipStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        foreach (var c in new[] { "Press", "Hold", "Release", "Wait" })
        {
            bool on = c == "Wait";
            var chipBorder = new Border
            {
                Height = 34,
                Padding = new Thickness(15, 0, 15, 0),
                CornerRadius = new CornerRadius(17),
                Background = on ? BlossomKit.B(BlossomKit.LitPaperInk) : BlossomKit.B("transparent"),
                BorderBrush = on ? BlossomKit.B("transparent") : BlossomKit.B("rgba(59,23,18,0.35)"),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = c,
                    FontFamily = BlossomKit.Body,
                    FontSize = 14,
                    Foreground = on ? BlossomKit.B("#ffe2b8") : BlossomKit.B(BlossomKit.LitPaperSub),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            chipStack.Children.Add(chipBorder);
        }
        cardStack.Children.Add(chipStack);

        // Slider for delay duration
        var delaySlider = BlossomKit.LitCordSlider(45, 340, "45 ms");
        cardStack.Children.Add(delaySlider);

        var cardFoot = new TextBlock
        {
            Text = "Tested safe on 1000Hz XInput channels with zero frame desync.",
            FontFamily = BlossomKit.Body,
            FontSize = 13,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
            Margin = new Thickness(0, 20, 0, 0)
        };
        cardStack.Children.Add(cardFoot);

        litCard.Child = cardStack;
        Canvas.SetLeft(litCard, 930);
        Canvas.SetTop(litCard, 452);
        host.Children.Add(litCard);

        // Hints: [A] Edit step  [X] Record  [Y] Delete step  [B] Back
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Edit step"));
        hintsStack.Children.Add(CreateHint("X", "Record"));
        hintsStack.Children.Add(CreateHint("Y", "Delete step"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }
}

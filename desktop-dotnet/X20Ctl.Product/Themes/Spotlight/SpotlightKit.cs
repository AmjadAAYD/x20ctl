using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace X20Ctl.Product.Spotlight;

/// <summary>
/// Core palette, typography, brushes, and primitives for the Spotlight theme:
/// "The controller is the star" (inspired by PS4 theatrical event).
/// Velvet legs, gold-fringed valance, warm follow-spot cone, lighting desk faders,
/// and cue cards with double gold rules.
/// </summary>
internal static class SpotlightKit
{
    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    public static Color A(Color c, double a) => Color.FromArgb((byte)Math.Clamp(Math.Round(a * 255), 0, 255), c.R, c.G, c.B);
    public static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    public static SolidColorBrush B(string hex) => B(C(hex));

    // Core Theme Palette
    public static readonly Color Gold = C("#d9b77a");
    public static readonly Color GoldHi = C("#ffe6c4");
    public static readonly Color Rose = C("#e8a990");
    public static readonly Color Chalk = C("#efe6d8");
    public static readonly Color ChalkDim = C("#cdbfa9");
    public static readonly Color Mute = C("#9b8c7a");
    public static readonly Color Coral = C("#ff8a6a");

    // Background & Velvet
    public static readonly Color House0 = C("#070405");
    public static readonly Color HouseCenter = C("#1a0b0c");
    public static readonly Color Floor0 = C("#1a0f0b");
    public static readonly Color Floor1 = C("#0d0706");

    // Frozen Brushes
    public static readonly SolidColorBrush GoldBrush = B(Gold);
    public static readonly SolidColorBrush GoldHiBrush = B(GoldHi);
    public static readonly SolidColorBrush RoseBrush = B(Rose);
    public static readonly SolidColorBrush ChalkBrush = B(Chalk);
    public static readonly SolidColorBrush ChalkDimBrush = B(ChalkDim);
    public static readonly SolidColorBrush MuteBrush = B(Mute);
    public static readonly SolidColorBrush CoralBrush = B(Coral);
    public static readonly SolidColorBrush HouseBrush = B(House0);

    // Typography
    public static readonly FontFamily Display = new("Perpetua Titling MT, Georgia, Cambria, Times New Roman, serif");
    public static readonly FontFamily Bodoni = new("Bodoni MT, Georgia, Times New Roman, serif");
    public static readonly FontFamily Body = new("Garamond, Georgia, serif");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static readonly FontFamily Mono = new("Courier New, Cascadia Mono, Consolas");

    // Gradients
    public static readonly LinearGradientBrush FaderCapBrush = Freeze(new LinearGradientBrush
    {
        StartPoint = new(0, 0), EndPoint = new(0, 1),
        GradientStops = { new(C("#f4dfb4"), 0.0), new(C("#b8925a"), 0.5), new(C("#6e5026"), 1.0) }
    });

    public static readonly LinearGradientBrush GildBrush = Freeze(new LinearGradientBrush
    {
        StartPoint = new(0, 0), EndPoint = new(0, 1),
        GradientStops = { new(C("#fff2d6"), 0.0), new(C("#e6c486"), 0.55), new(C("#a77d43"), 1.0) }
    });

    public static readonly RadialGradientBrush KeyCapFill = Freeze(new RadialGradientBrush
    {
        Center = new(0.40, 0.30), RadiusX = 0.70, RadiusY = 0.70,
        GradientStops = { new(C("#f2dcae"), 0.0), new(C("#b08a4c"), 0.7), new(C("#6a4f26"), 1.0) }
    });

    public static readonly LinearGradientBrush PrimaryBtnBrush = Freeze(new LinearGradientBrush
    {
        StartPoint = new(0, 0), EndPoint = new(0, 1),
        GradientStops = { new(C("#f0d6a0"), 0.0), new(C("#c39a58"), 1.0) }
    });

    public static readonly RadialGradientBrush BulbBrush = Freeze(new RadialGradientBrush
    {
        Center = new(0.40, 0.35), RadiusX = 0.60, RadiusY = 0.60,
        GradientStops = { new(C("#fffaf0"), 0.0), new(C("#ffd9a0"), 0.5), new(C("#d08a3a"), 1.0) }
    });

    public static readonly LinearGradientBrush CardBgBrush = Freeze(new LinearGradientBrush
    {
        StartPoint = new(0, 0), EndPoint = new(0, 1),
        GradientStops = { new(A(C("#20120e"), 0.96), 0.0), new(A(C("#140b09"), 0.96), 1.0) }
    });

    public static readonly string[] RomanNumerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

    public static string Roman(int index) => (index >= 0 && index < RomanNumerals.Length) ? RomanNumerals[index] : (index + 1).ToString();

    public static T Freeze<T>(T freezable) where T : Freezable { freezable.Freeze(); return freezable; }

    /// <summary>
    /// Creates the theatrical stage background with deep dark auditorium, perspective floor, velvet curtains and valance.
    /// </summary>
    public static Canvas TheatricalStage(double floorY = 700, bool legs = true, bool valance = true, bool leftLeg = false, double leftLegW = 120, double rightLegW = 120)
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. House background
        var house = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new RadialGradientBrush
            {
                Center = new(0.5, 0.30), RadiusX = 0.60, RadiusY = 0.60,
                GradientStops = { new(HouseCenter, 0.0), new(C("#080405"), 0.70), new(House0, 1.0) }
            }
        };
        root.Children.Add(house);

        // 2. Floor
        if (floorY < 900)
        {
            var floorCanvas = new Canvas { Width = 1600, Height = 900 - floorY, ClipToBounds = true };
            Canvas.SetLeft(floorCanvas, 0);
            Canvas.SetTop(floorCanvas, floorY);

            // Floor base gradient
            var floorRect = new Rectangle
            {
                Width = 1600, Height = 900 - floorY,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new(0, 0), EndPoint = new(0, 1),
                    GradientStops = { new(Floor0, 0.0), new(Floor1, 1.0) }
                }
            };
            floorCanvas.Children.Add(floorRect);

            // Floor perspective wooden planks
            var plankBrush = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 68, 900 - floorY),
                ViewportUnits = BrushMappingMode.Absolute,
                Drawing = new GeometryDrawing
                {
                    Brush = B(A(C("#784628"), 0.16)),
                    Pen = new Pen(B(A(Colors.Black, 0.55)), 2),
                    Geometry = new LineGeometry(new Point(67, 0), new Point(67, 900 - floorY))
                }
            };
            var planks = new Rectangle { Width = 1600, Height = 900 - floorY, Fill = plankBrush, Opacity = 0.8 };
            floorCanvas.Children.Add(planks);

            // Floor top edge gold reflection line
            var edgeLine = new Rectangle
            {
                Width = 1600, Height = 2,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new(0, 0), EndPoint = new(1, 0),
                    GradientStops = { new(A(Gold, 0.0), 0.0), new(A(Gold, 0.55), 0.5), new(A(Gold, 0.0), 1.0) }
                }
            };
            floorCanvas.Children.Add(edgeLine);
            root.Children.Add(floorCanvas);
        }

        // 3. Velvet Legs (Side Curtains)
        if (leftLeg)
        {
            var legLeft = CreateVelvetCurtain(leftLegW, 900, isLeft: true);
            Canvas.SetLeft(legLeft, -20);
            Canvas.SetTop(legLeft, 0);
            root.Children.Add(legLeft);
        }

        if (legs)
        {
            var legRight = CreateVelvetCurtain(rightLegW, 900, isLeft: false);
            Canvas.SetLeft(legRight, 1600 - rightLegW + 20);
            Canvas.SetTop(legRight, 0);
            root.Children.Add(legRight);
        }

        // 4. Valance (Top Curtain / Pelmet)
        if (valance)
        {
            var valanceEl = CreateValance(1600, 64);
            Canvas.SetLeft(valanceEl, 0);
            Canvas.SetTop(valanceEl, 0);
            root.Children.Add(valanceEl);
        }

        return root;
    }

    private static UIElement CreateVelvetCurtain(double width, double height, bool isLeft)
    {
        var grid = new Grid { Width = width, Height = height, ClipToBounds = true };

        // Repeating vertical velvet folds
        var velvetStops = new GradientStopCollection
        {
            new(C("#2a050b"), 0.00), new(C("#5a0f1c"), 0.28), new(C("#7a1a26"), 0.44),
            new(C("#4a0b15"), 0.64), new(C("#24040a"), 0.88), new(C("#2a050b"), 1.00)
        };
        var foldBrush = new LinearGradientBrush
        {
            StartPoint = new(0, 0), EndPoint = new(1, 0),
            SpreadMethod = GradientSpreadMethod.Repeat,
            GradientStops = velvetStops
        };

        var curtain = new Rectangle { Width = width, Height = height, Fill = foldBrush };
        grid.Children.Add(curtain);

        // Shadow overlay (top/bottom darkness)
        var shadow = new Rectangle
        {
            Width = width, Height = height,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(A(Colors.Black, 0.50), 0.0), new(A(Colors.Black, 0.0), 0.30), new(A(Colors.Black, 0.0), 0.70), new(A(Colors.Black, 0.75), 1.0) }
            }
        };
        grid.Children.Add(shadow);

        // Edge shadow
        var edgeShadow = new Rectangle
        {
            Width = width, Height = height,
            Fill = new LinearGradientBrush
            {
                StartPoint = isLeft ? new(0, 0) : new(1, 0),
                EndPoint = isLeft ? new(1, 0) : new(0, 0),
                GradientStops = { new(A(Colors.Black, 0.60), 0.0), new(A(Colors.Black, 0.0), 1.0) }
            }
        };
        grid.Children.Add(edgeShadow);

        return grid;
    }

    private static UIElement CreateValance(double width, double height)
    {
        var grid = new Grid { Width = width, Height = height, ClipToBounds = true };

        // Velvet folds
        var foldBrush = new LinearGradientBrush
        {
            StartPoint = new(0, 0), EndPoint = new(1, 0),
            SpreadMethod = GradientSpreadMethod.Repeat,
            GradientStops =
            {
                new(C("#3a0710"), 0.00), new(C("#6c1421"), 0.30), new(C("#8a2230"), 0.45),
                new(C("#4d0c16"), 0.65), new(C("#2c050c"), 0.85), new(C("#3a0710"), 1.00)
            }
        };
        var valance = new Rectangle { Width = width, Height = height, Fill = foldBrush };
        grid.Children.Add(valance);

        // Scalloped bottom mask simulation / bottom fringe
        var bottomFringe = new Rectangle
        {
            Width = width, Height = 6,
            VerticalAlignment = VerticalAlignment.Bottom,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(A(Colors.Black, 0.0), 0.0), new(A(Colors.Black, 0.75), 1.0) }
            }
        };
        grid.Children.Add(bottomFringe);

        // Top gold fringe header
        var topGold = new Rectangle
        {
            Width = width, Height = 10,
            VerticalAlignment = VerticalAlignment.Top,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(Gold, 0.0), new(C("#8a6a3a"), 1.0) }
            },
            Opacity = 0.55
        };
        grid.Children.Add(topGold);

        return grid;
    }

    /// <summary>
    /// Follow Spot: a soft warm cone from the overhead rig and a pool of light on the floor.
    /// </summary>
    public static Canvas FollowSpot(double cx, double fy, double rx = 240, double top = -40, double opacity = 0.26, double? src = null, bool pool = true)
    {
        var canvas = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        double sx = src ?? cx;
        double ry = rx * 0.22;

        // Light cone
        var coneGeom = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(sx - 14, top), IsClosed = true };
        fig.Segments.Add(new LineSegment(new Point(sx + 14, top), true));
        fig.Segments.Add(new LineSegment(new Point(cx + rx, fy), true));
        fig.Segments.Add(new LineSegment(new Point(cx - rx, fy), true));
        coneGeom.Figures.Add(fig);

        var conePath = new Path
        {
            Data = coneGeom,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops =
                {
                    new(A(C("#fff2d8"), opacity * 1.3), 0.0),
                    new(A(C("#ffd8a0"), opacity * 0.35), 1.0)
                }
            },
            Effect = new BlurEffect { Radius = 16 }
        };
        canvas.Children.Add(conePath);

        // Floor pool of light
        if (pool)
        {
            double pw = rx * 2.5;
            double ph = ry * 2.5;
            var poolEllipse = new Ellipse
            {
                Width = pw, Height = ph,
                Fill = new RadialGradientBrush
                {
                    Center = new(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
                    GradientStops =
                    {
                        new(A(C("#ffecc8"), 0.55), 0.0),
                        new(A(C("#ffd296"), 0.22), 0.55),
                        new(A(C("#ffd296"), 0.00), 1.0)
                    }
                }
            };
            Canvas.SetLeft(poolEllipse, cx - pw / 2);
            Canvas.SetTop(poolEllipse, fy - ph / 2);
            canvas.Children.Add(poolEllipse);
        }

        return canvas;
    }

    /// <summary>
    /// Stage Mark: a T of tape on the floor.
    /// </summary>
    public static UIElement StageMark(double x, double y, bool on = false, double size = 16)
    {
        var canvas = new Canvas { Width = size * 2, Height = size * 1.4, IsHitTestVisible = false };
        Canvas.SetLeft(canvas, x - size);
        Canvas.SetTop(canvas, y);

        var color = on ? GoldHi : A(Gold, 0.55);
        double strokeW = on ? 3.0 : 2.4;

        var hLine = new Line { X1 = 0, Y1 = 0, X2 = size * 2, Y2 = 0, Stroke = B(color), StrokeThickness = strokeW };
        var vLine = new Line { X1 = size, Y1 = 0, X2 = size, Y2 = size * 0.7, Stroke = B(color), StrokeThickness = strokeW };
        canvas.Children.Add(hLine);
        canvas.Children.Add(vLine);

        if (on)
        {
            canvas.Effect = new DropShadowEffect { Color = C("#ffb860"), BlurRadius = 6, ShadowDepth = 0, Opacity = 0.8 };
        }
        return canvas;
    }

    /// <summary>
    /// Cue Card: deep warm card with double gold rules.
    /// </summary>
    public static Border CueCard(UIElement content, double width, double? height = null, string label = "", Thickness? padding = null)
    {
        var pad = padding ?? new Thickness(30, 26, 30, 26);
        var outerBorder = new Border
        {
            Width = width,
            Background = CardBgBrush,
            BorderBrush = B(A(Gold, 0.60)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 40, ShadowDepth = 20, Opacity = 0.7 }
        };
        if (height.HasValue) outerBorder.Height = height.Value;

        var grid = new Grid();

        // Inner gold rule (inset 4px from outer border)
        var innerBorder = new Border
        {
            Margin = new Thickness(4),
            BorderBrush = B(A(Gold, 0.22)),
            BorderThickness = new Thickness(1),
            Padding = pad,
            Child = content
        };
        grid.Children.Add(innerBorder);

        // Optional label header badge at top-left
        if (!string.IsNullOrEmpty(label))
        {
            var labelBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(24, -9, 0, 0),
                Background = B("#1c100c"),
                Padding = new Thickness(10, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = label.ToUpperInvariant(),
                    FontFamily = Display,
                    FontSize = 10.5,
                    FontWeight = FontWeights.Light,
                    Foreground = GoldBrush
                }
            };
            grid.Children.Add(labelBorder);
        }

        outerBorder.Child = grid;
        return outerBorder;
    }

    /// <summary>
    /// Circular brass key: button glyphs A, B, X, Y, LB, RB, etc.
    /// </summary>
    public static Border BrassKey(string text, bool focused = false, double size = 26)
    {
        var border = new Border
        {
            Width = size, Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = KeyCapFill,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 2, Opacity = 0.6 }
        };

        if (focused)
        {
            border.BorderBrush = GoldHiBrush;
            border.BorderThickness = new Thickness(1.5);
            border.Effect = new DropShadowEffect { Color = C("#ffd6a0"), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.7 };
        }

        border.Child = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.Bold,
            FontSize = size > 30 ? 18 : 12,
            Foreground = B("#1a0c08"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        return border;
    }

    /// <summary>
    /// Theatrical button in Spotlight style.
    /// </summary>
    public static Button TheatricalButton(string text, bool primary = false, string? key = null, Action? onClick = null, double height = 46, double fontSize = 13)
    {
        var btn = new Button
        {
            Height = height,
            Cursor = System.Windows.Input.Cursors.Hand,
            FocusVisualStyle = null
        };

        var border = new Border
        {
            Height = height,
            Padding = new Thickness(24, 0, 24, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = primary ? B("#f6e2b6") : B(A(Gold, 0.55)),
            Background = primary ? PrimaryBtnBrush : Brushes.Transparent
        };

        if (primary)
        {
            border.Effect = new DropShadowEffect { Color = C("#ffc878"), BlurRadius = 24, ShadowDepth = 0, Opacity = 0.4 };
        }

        var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(key))
        {
            var k = BrassKey(key, false, 24);
            k.Margin = new Thickness(0, 0, 10, 0);
            stack.Children.Add(k);
        }

        stack.Children.Add(new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Display,
            FontWeight = primary ? FontWeights.Bold : FontWeights.Light,
            FontSize = fontSize,
            Foreground = primary ? B("#1a0c08") : ChalkBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        border.Child = stack;
        btn.Content = border;
        btn.Template = new ControlTemplate(typeof(Button))
        {
            VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
        };

        if (onClick != null) btn.Click += (_, _) => onClick();
        return btn;
    }

    /// <summary>
    /// Lighting-desk vertical fader with scale tick marks and a 3D gold fader cap.
    /// </summary>
    public static Canvas Fader(double x, double y, double h, double pct, bool on = false, string label = "", string val = "")
    {
        var canvas = new Canvas { Width = 140, Height = h + 70, ClipToBounds = false };
        Canvas.SetLeft(canvas, x - 70);
        Canvas.SetTop(canvas, y);

        double trackX = 70;
        double cy = h - h * Math.Clamp(pct, 0, 100) / 100.0;

        // Slot
        var slot = new Rectangle
        {
            Width = 6, Height = h,
            RadiusX = 3, RadiusY = 3,
            Fill = B("#050302"),
            Stroke = B(A(Gold, 0.30)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(slot, trackX - 3);
        Canvas.SetTop(slot, 0);
        canvas.Children.Add(slot);

        // 11 Scale tick marks
        for (int i = 0; i <= 10; i++)
        {
            double ty = h * i / 10.0;
            double tickW = (i % 5 == 0) ? 10 : 5;
            var tick = new Line
            {
                X1 = trackX + 12, Y1 = ty,
                X2 = trackX + 12 + tickW, Y2 = ty,
                Stroke = B(A(Gold, i % 5 == 0 ? 0.60 : 0.30)),
                StrokeThickness = 1
            };
            canvas.Children.Add(tick);
        }

        // Active fill bar
        if (on && pct > 0)
        {
            var fillBar = new Rectangle
            {
                Width = 4, Height = h - cy,
                Fill = B("#ffc070"),
                Effect = new DropShadowEffect { Color = C("#ffb860"), BlurRadius = 6, ShadowDepth = 0, Opacity = 0.8 }
            };
            Canvas.SetLeft(fillBar, trackX - 2);
            Canvas.SetTop(fillBar, cy);
            canvas.Children.Add(fillBar);
        }

        // 3D Gold Fader Cap (40x26)
        var cap = new Border
        {
            Width = 40, Height = 26,
            CornerRadius = new CornerRadius(3),
            Background = FaderCapBrush,
            BorderBrush = on ? GoldHiBrush : B(A(Colors.Black, 0.60)),
            BorderThickness = new Thickness(on ? 1.5 : 1),
            Effect = new DropShadowEffect
            {
                Color = on ? C("#ffc878") : Colors.Black,
                BlurRadius = on ? 10 : 6,
                ShadowDepth = on ? 0 : 4,
                Opacity = on ? 0.7 : 0.6
            }
        };

        // Center line on cap
        cap.Child = new Rectangle
        {
            Width = 28, Height = 2,
            Fill = B("#1a0c08"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetLeft(cap, trackX - 20);
        Canvas.SetTop(cap, cy - 13);
        canvas.Children.Add(cap);

        // Indicator LED above fader if on
        var led = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = on ? B("#ffc070") : B("#3a2a20")
        };
        if (on) led.Effect = new DropShadowEffect { Color = C("#ffb860"), BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9 };
        Canvas.SetLeft(led, trackX - 5);
        Canvas.SetTop(led, -18);
        canvas.Children.Add(led);

        // Label and value below
        if (!string.IsNullOrEmpty(label) || !string.IsNullOrEmpty(val))
        {
            var textStack = new StackPanel { Width = 140, HorizontalAlignment = HorizontalAlignment.Center };
            if (!string.IsNullOrEmpty(label))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = label.ToUpperInvariant(),
                    FontFamily = Display,
                    FontSize = 11,
                    FontWeight = FontWeights.Light,
                    Foreground = on ? GoldHiBrush : GoldBrush,
                    TextAlignment = TextAlignment.Center
                });
            }
            if (!string.IsNullOrEmpty(val))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = val,
                    FontFamily = Body,
                    FontSize = 19,
                    Foreground = ChalkBrush,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }
            Canvas.SetLeft(textStack, 0);
            Canvas.SetTop(textStack, h + 20);
            canvas.Children.Add(textStack);
        }

        return canvas;
    }

    /// <summary>
    /// Mini horizontal fader for rows and tester.
    /// </summary>
    public static Canvas HorizontalFader(double pct, double w = 96, bool on = false)
    {
        var canvas = new Canvas { Width = w, Height = 22 };
        double x = 6 + (w - 12) * Math.Clamp(pct, 0, 100) / 100.0;

        var track = new Rectangle
        {
            Width = w, Height = 4,
            RadiusX = 2, RadiusY = 2,
            Fill = B("#050302"),
            Stroke = B(A(Gold, 0.30)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(track, 0);
        Canvas.SetTop(track, 9);
        canvas.Children.Add(track);

        if (pct > 0)
        {
            var fill = new Rectangle
            {
                Width = x, Height = 2,
                Fill = B("#ffc070")
            };
            Canvas.SetLeft(fill, 0);
            Canvas.SetTop(fill, 10);
            canvas.Children.Add(fill);
        }

        var cap = new Border
        {
            Width = 12, Height = 20,
            CornerRadius = new CornerRadius(2),
            Background = FaderCapBrush,
            BorderBrush = on ? GoldHiBrush : B(A(Colors.Black, 0.60)),
            BorderThickness = new Thickness(1)
        };
        Canvas.SetLeft(cap, x - 6);
        Canvas.SetTop(cap, 1);
        canvas.Children.Add(cap);

        return canvas;
    }

    /// <summary>
    /// Status badge / tag (e.g. "Local draft", "In use", "Scene I").
    /// </summary>
    public static Border Tag(string text, bool white = false)
    {
        return new Border
        {
            Height = 24,
            Padding = new Thickness(10, 0, 10, 0),
            BorderBrush = white ? GoldHiBrush : B(A(Rose, 0.70)),
            BorderThickness = new Thickness(1),
            Background = white ? B(Rose) : Brushes.Transparent,
            Child = new TextBlock
            {
                Text = text.ToUpperInvariant(),
                FontFamily = Display,
                FontSize = 10.5,
                FontWeight = white ? FontWeights.Bold : FontWeights.Light,
                Foreground = white ? B("#1a0c08") : RoseBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    /// <summary>
    /// Preset / selection chip button.
    /// </summary>
    public static Border Chip(string text, bool on = false, Action? onClick = null)
    {
        var border = new Border
        {
            Height = 34,
            Padding = new Thickness(16, 0, 16, 0),
            BorderBrush = on ? GoldBrush : B(A(Gold, 0.30)),
            BorderThickness = new Thickness(1),
            Background = on ? B("#e8cf9c") : Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        border.Child = new TextBlock
        {
            Text = text,
            FontFamily = Body,
            FontSize = 15,
            Foreground = on ? B("#1a0c08") : ChalkDimBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (onClick != null) border.MouseLeftButtonDown += (_, _) => onClick();
        return border;
    }

    /// <summary>
    /// Tab headers bar (e.g. "FACE", "SHOULDERS", "STICKS", "SYSTEM").
    /// </summary>
    public static StackPanel Tabs(string[] items, int onIndex = 0, Action<int>? onSelect = null)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < items.Length; i++)
        {
            int idx = i;
            bool on = (i == onIndex);
            var itemPanel = new StackPanel { Margin = new Thickness(0, 0, 36, 0), Cursor = System.Windows.Input.Cursors.Hand };

            var text = new TextBlock
            {
                Text = items[i].ToUpperInvariant(),
                FontFamily = Display,
                FontSize = 14,
                FontWeight = FontWeights.Light,
                Foreground = on ? GoldHiBrush : B("#8a7c6a")
            };
            itemPanel.Children.Add(text);

            if (on)
            {
                var rule = new Rectangle
                {
                    Height = 1, Width = 40,
                    Fill = GoldBrush,
                    Margin = new Thickness(0, 10, 0, 0),
                    Effect = new DropShadowEffect { Color = C("#ffb860"), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.8 }
                };
                itemPanel.Children.Add(rule);
            }

            if (onSelect != null) itemPanel.MouseLeftButtonDown += (_, _) => onSelect(idx);
            stack.Children.Add(itemPanel);
        }
        return stack;
    }

    /// <summary>
    /// A table or list row in Spotlight theatrical desk styling.
    /// </summary>
    public static Border Row(string label, string? sub = null, string? val = null, bool on = false, UIElement? ctl = null, double height = 52)
    {
        var border = new Border
        {
            Height = height,
            Padding = new Thickness(20, 0, 20, 0),
            BorderBrush = on ? Brushes.Transparent : B(A(Gold, 0.12)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = on
                ? new RadialGradientBrush
                {
                    Center = new(0.3, 0.5), RadiusX = 0.7, RadiusY = 1.6,
                    GradientStops = { new(A(C("#ffdcaf"), 0.22), 0.0), new(A(C("#ffdcaf"), 0.05), 0.7), new(Colors.Transparent, 1.0) }
                }
                : Brushes.Transparent
        };

        if (on)
        {
            border.BorderThickness = new Thickness(3, 0, 0, 0);
            border.BorderBrush = GoldBrush;
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left side: lamp indicator (if on) + label + sub
        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (on)
        {
            // Small lamp glyph
            var lamp = new Path
            {
                Data = Geometry.Parse("M3 4L13 4L11 11L5 11Z M5 11L1 18H15L11 11"),
                Fill = GoldBrush,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(lamp);
        }

        var labelStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labelStack.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = Body,
            FontSize = height <= 44 ? 17 : 18,
            Foreground = on ? B("#fff6e6") : B("#e2d6c3")
        });

        if (!string.IsNullOrEmpty(sub))
        {
            labelStack.Children.Add(new TextBlock
            {
                Text = sub,
                FontFamily = Body,
                FontStyle = FontStyles.Italic,
                FontSize = 14,
                Foreground = MuteBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }
        leftStack.Children.Add(labelStack);
        Grid.SetColumn(leftStack, 0);
        grid.Children.Add(leftStack);

        // Right side: value + control
        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(val))
        {
            rightStack.Children.Add(new TextBlock
            {
                Text = val,
                FontFamily = Body,
                FontSize = 15.5,
                Foreground = on ? GoldHiBrush : ChalkDimBrush,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        if (ctl != null)
        {
            rightStack.Children.Add(ctl);
        }
        Grid.SetColumn(rightStack, 1);
        grid.Children.Add(rightStack);

        border.Child = grid;
        return border;
    }

    /// <summary>
    /// Stick Gobo projection: concentric rings with resting crosshair.
    /// </summary>
    public static Canvas Gobo(double cx, double cy, double r, string label, string sub)
    {
        var canvas = new Canvas { Width = r * 2 + 100, Height = r * 2 + 70, ClipToBounds = false };
        Canvas.SetLeft(canvas, cx - r - 50);
        Canvas.SetTop(canvas, cy - r);

        double gx = r + 50;
        double gy = r;

        // Outer pool
        var pool = new Ellipse
        {
            Width = r * 2, Height = r * 2,
            Fill = new RadialGradientBrush
            {
                Center = new(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
                GradientStops = { new(A(C("#ffe2b0"), 0.16), 0.0), new(A(C("#ffe2b0"), 0.03), 1.0) }
            },
            Stroke = B(A(C("#ffdcaa"), 0.50)),
            StrokeThickness = 1.4
        };
        Canvas.SetLeft(pool, gx - r);
        Canvas.SetTop(pool, gy - r);
        canvas.Children.Add(pool);

        // Sub-rings
        foreach (double factor in new[] { 0.33, 0.66 })
        {
            double sr = r * factor;
            var subRing = new Ellipse
            {
                Width = sr * 2, Height = sr * 2,
                Stroke = B(A(C("#ffdcaa"), 0.14)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(subRing, gx - sr);
            Canvas.SetTop(subRing, gy - sr);
            canvas.Children.Add(subRing);
        }

        // Crosshairs
        var hLine = new Line { X1 = gx - r, Y1 = gy, X2 = gx + r, Y2 = gy, Stroke = B(A(C("#ffdcaa"), 0.14)), StrokeThickness = 1 };
        var vLine = new Line { X1 = gx, Y1 = gy - r, X2 = gx, Y2 = gy + r, Stroke = B(A(C("#ffdcaa"), 0.14)), StrokeThickness = 1 };
        canvas.Children.Add(hLine);
        canvas.Children.Add(vLine);

        // Center dot
        var dot = new Ellipse
        {
            Width = 12, Height = 12,
            Fill = B("#fff6e6"),
            Effect = new DropShadowEffect { Color = C("#ffb860"), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(dot, gx - 6);
        Canvas.SetTop(dot, gy - 6);
        canvas.Children.Add(dot);

        // Label below
        var textStack = new StackPanel { Width = 280, HorizontalAlignment = HorizontalAlignment.Center };
        textStack.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = Body,
            FontSize = 21,
            Foreground = ChalkBrush,
            TextAlignment = TextAlignment.Center
        });
        textStack.Children.Add(new TextBlock
        {
            Text = sub,
            FontFamily = Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14.5,
            Foreground = MuteBrush,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Canvas.SetLeft(textStack, gx - 140);
        Canvas.SetTop(textStack, gy + r + 22);
        canvas.Children.Add(textStack);

        return canvas;
    }

    /// <summary>
    /// Ring of bulbs: Dressing-room mirror marquee.
    /// </summary>
    public static Canvas MarqueeMirror(double x, double y, double w, double h, int nTop = 14, int nSide = 5)
    {
        var canvas = new Canvas { Width = w, Height = h, IsHitTestVisible = false };
        Canvas.SetLeft(canvas, x);
        Canvas.SetTop(canvas, y);

        // Mirror backing with brass border
        var mirror = new Border
        {
            Width = w, Height = h,
            Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(1, 1),
                GradientStops = { new(A(C("#fff0dc"), 0.07), 0.0), new(A(C("#fff0dc"), 0.02), 0.40), new(A(C("#fff0dc"), 0.05), 1.0) }
            },
            BorderBrush = B(A(Gold, 0.40)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 40, ShadowDepth = 20, Opacity = 0.7 }
        };
        canvas.Children.Add(mirror);

        void AddBulb(double bx, double by)
        {
            var bulb = new Ellipse
            {
                Width = 18, Height = 18,
                Fill = BulbBrush,
                Effect = new DropShadowEffect { Color = C("#ffc878"), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.6 }
            };
            Canvas.SetLeft(bulb, bx - 9);
            Canvas.SetTop(bulb, by - 9);
            canvas.Children.Add(bulb);
        }

        for (int i = 0; i < nTop; i++)
        {
            double bx = w * (i + 0.5) / nTop;
            AddBulb(bx, 0);
            AddBulb(bx, h);
        }

        for (int i = 0; i < nSide; i++)
        {
            double by = h * (i + 0.5) / nSide;
            AddBulb(0, by);
            AddBulb(w, by);
        }

        return canvas;
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SpotlightShowMacros()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Slot chips at (1150, 146)
        var slots = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 1; i <= 4; i++)
        {
            var slotBox = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            var chip = SpotlightKit.Chip($"Slot {i}", i == 1);
            chip.Width = 66;
            slotBox.Children.Add(chip);
            slotBox.Children.Add(new TextBlock
            {
                Text = i == 1 ? "Assigned" : "Empty",
                FontFamily = SpotlightKit.Body,
                FontSize = 12.5,
                Foreground = SpotlightKit.MuteBrush,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            slots.Children.Add(slotBox);
        }
        Canvas.SetLeft(slots, 1150);
        Canvas.SetTop(slots, 146);
        canvas.Children.Add(slots);

        // Heading: Macro sequence
        var headStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        headStack.Children.Add(new TextBlock
        {
            Text = "Macro sequence",
            FontFamily = SpotlightKit.Body,
            FontSize = 21,
            Foreground = SpotlightKit.ChalkBrush
        });
        headStack.Children.Add(new TextBlock
        {
            Text = "3 events · 140 ms total duration",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        Canvas.SetLeft(headStack, 302);
        Canvas.SetTop(headStack, 232);
        canvas.Children.Add(headStack);

        // Sequence Cue list at (292, 276, w = 620)
        var sheet = new StackPanel { Width = 620 };
        string[] cues = { "01", "02", "03" };
        string[] btns = { "A", "B", "X" };

        for (int i = 0; i < 3; i++)
        {
            bool on = (i == 1); // Cue 02 selected
            var r = new Border
            {
                Height = 60,
                BorderBrush = on ? Brushes.Transparent : SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.12)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Background = on
                    ? new RadialGradientBrush
                    {
                        Center = new(0.3, 0.5), RadiusX = 0.7, RadiusY = 1.6,
                        GradientStops = { new(SpotlightKit.A(SpotlightKit.C("#ffdcaf"), 0.22), 0.0), new(SpotlightKit.A(SpotlightKit.C("#ffdcaf"), 0.05), 0.7), new(Colors.Transparent, 1.0) }
                    }
                    : Brushes.Transparent
            };
            if (on)
            {
                r.BorderThickness = new Thickness(3, 0, 0, 0);
                r.BorderBrush = SpotlightKit.GoldBrush;
            }

            var g = new Grid { Margin = new Thickness(20, 0, 20, 0) };
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            leftStack.Children.Add(new TextBlock
            {
                Text = $"CUE {cues[i]}",
                FontFamily = SpotlightKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.Light,
                Width = 70,
                Foreground = on ? SpotlightKit.GoldHiBrush : SpotlightKit.GoldBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            leftStack.Children.Add(SpotlightKit.BrassKey(btns[i], on, 26));
            leftStack.Children.Add(new TextBlock
            {
                Text = "Press and release · 40 ms",
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 16,
                Foreground = SpotlightKit.B("#b3a48e"),
                Margin = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            g.Children.Add(leftStack);

            if (on)
            {
                var lamp = new Path
                {
                    Data = Geometry.Parse("M3 4L13 4L11 11L5 11Z M5 11L1 18H15L11 11"),
                    Fill = SpotlightKit.GoldBrush,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                g.Children.Add(lamp);
            }

            r.Child = g;
            sheet.Children.Add(r);
        }

        // Add step row
        var addRow = new Border
        {
            Height = 52,
            Padding = new Thickness(20, 0, 20, 0),
            BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.12)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var addStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        addStack.Children.Add(new TextBlock { Text = "\uE710", FontFamily = SpotlightKit.Icons, FontSize = 13, Foreground = SpotlightKit.GoldBrush, Margin = new Thickness(0, 0, 14, 0) });
        addStack.Children.Add(new TextBlock { Text = "Add event to sequence", FontFamily = SpotlightKit.Body, FontSize = 17, Foreground = SpotlightKit.MuteBrush });
        addRow.Child = addStack;
        sheet.Children.Add(addRow);

        Canvas.SetLeft(sheet, 292);
        Canvas.SetTop(sheet, 276);
        canvas.Children.Add(sheet);

        // Timeline visualization at (960, 270, w = 590, h = 150)
        var tlBorder = new Border
        {
            Width = 590, Height = 150,
            Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(SpotlightKit.C("#1b120e"), 0.0), new(SpotlightKit.C("#120b09"), 1.0) }
            },
            BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.25)),
            BorderThickness = new Thickness(1)
        };
        var tlCanvas = new Canvas { Width = 590, Height = 150 };

        // Timeline tick bars
        for (int i = 0; i < 3; i++)
        {
            double bx = 60 + i * 160;
            var bar = new Border
            {
                Width = 90, Height = 36,
                Background = (i == 1) ? SpotlightKit.GoldBrush : SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.20)),
                BorderBrush = SpotlightKit.GoldHiBrush,
                BorderThickness = new Thickness(i == 1 ? 1.5 : 1),
                Child = new TextBlock
                {
                    Text = $"Cue {cues[i]} · {btns[i]}",
                    FontFamily = SpotlightKit.Body,
                    FontSize = 14,
                    Foreground = (i == 1) ? SpotlightKit.B("#1a0c08") : SpotlightKit.ChalkDimBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(bar, bx);
            Canvas.SetTop(bar, 56);
            tlCanvas.Children.Add(bar);
        }
        tlBorder.Child = tlCanvas;
        Canvas.SetLeft(tlBorder, 960);
        Canvas.SetTop(tlBorder, 270);
        canvas.Children.Add(tlBorder);

        // Macro playback controls at (950, 470, w = 610)
        var playStack = new StackPanel { Width = 610 };
        playStack.Children.Add(SpotlightKit.Row("Played by", "Trigger activation mode", "Hold paddle while active", false, null, 48));

        var toolBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 18, 0, 0) };
        toolBtns.Children.Add(SpotlightKit.TheatricalButton("Record sequence", false, null, height: 40, fontSize: 11));
        var clearBtn = SpotlightKit.TheatricalButton("Clear slot", false, null, height: 40, fontSize: 11);
        clearBtn.Margin = new Thickness(10, 0, 0, 0);
        toolBtns.Children.Add(clearBtn);
        var playBtn = SpotlightKit.TheatricalButton("Test playback", false, null, height: 40, fontSize: 11);
        playBtn.Margin = new Thickness(10, 0, 0, 0);
        toolBtns.Children.Add(playBtn);
        playStack.Children.Add(toolBtns);

        Canvas.SetLeft(playStack, 950);
        Canvas.SetTop(playStack, 470);
        canvas.Children.Add(playStack);

        // Cue card at bottom at (292, 620, w = 1260, label = "Cue 02")
        var cardInner = new StackPanel { Orientation = Orientation.Horizontal };
        cardInner.Children.Add(SpotlightKit.BrassKey("B", true, 42));

        var cueDetail = new StackPanel { Margin = new Thickness(24, 0, 0, 0), Width = 680, VerticalAlignment = VerticalAlignment.Center };
        cueDetail.Children.Add(new TextBlock { Text = "Cue 02 · Press B", FontFamily = SpotlightKit.Body, FontSize = 21, Foreground = SpotlightKit.ChalkBrush });
        cueDetail.Children.Add(new TextBlock { Text = "Button press event · Duration 40 ms", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush, Margin = new Thickness(0, 2, 0, 0) });
        cardInner.Children.Add(cueDetail);

        var segChips = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        segChips.Children.Add(SpotlightKit.Chip("Press", true));
        segChips.Children.Add(new Border { Width = 8 });
        segChips.Children.Add(SpotlightKit.Chip("Release", false));
        segChips.Children.Add(new Border { Width = 8 });
        segChips.Children.Add(SpotlightKit.Chip("Delay 20ms", false));
        cardInner.Children.Add(segChips);

        var cueCard = SpotlightKit.CueCard(cardInner, 1260, label: "Cue 02", padding: new Thickness(28, 20, 28, 20));
        Canvas.SetLeft(cueCard, 292);
        Canvas.SetTop(cueCard, 620);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "macros",
            title: "Macros",
            lead: "Record sequences of button presses and playback timing.",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Dashboard"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Edit step"), ("X", "Record"), ("B", "Back") },
            leadW: 760);

        spotlightPageHost.Children.Add(frame);
    }

    private void SpotlightShowVibration()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Tag: Local draft · Vibration strength
        var tag = SpotlightKit.Tag("VIBRATION STRENGTH");
        Canvas.SetLeft(tag, 302);
        Canvas.SetTop(tag, 144);
        canvas.Children.Add(tag);

        var noteText = new TextBlock
        {
            Text = "Synchronized motor output · Balanced tactile response",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 17,
            Foreground = SpotlightKit.B("#b3a48e")
        };
        Canvas.SetLeft(noteText, 490);
        Canvas.SetTop(noteText, 146);
        canvas.Children.Add(noteText);

        // Center Stage Controller in Follow Spot at cx = 640, baseFloor = 600, w = 520
        double cx = 640, baseFloor = 600, w = 520;
        var spot = SpotlightKit.FollowSpot(cx, 640, 280, -60, 0.28);
        canvas.Children.Add(spot);

        var star = SpotlightParts.SpotlightStar(Model.Id, cx, baseFloor, w, reflection: 0.18);
        canvas.Children.Add(star);

        // --- RADIATING HAPTIC RESONANCE WAVES ---
        // Left Grip Motor (Heavy Low-Frequency ERM Rumble)
        double leftMotorX = cx - 180;
        double leftMotorY = baseFloor - w * 0.32;
        var leftWaves = new Canvas { Width = 300, Height = 300, IsHitTestVisible = false };
        Canvas.SetLeft(leftWaves, leftMotorX - 150);
        Canvas.SetTop(leftWaves, leftMotorY - 150);

        for (int arcIdx = 1; arcIdx <= 3; arcIdx++)
        {
            double r = 45 + arcIdx * 30;
            var arcGeom = new PathGeometry();
            var fig = new PathFigure
            {
                StartPoint = new Point(150 - r * 0.866, 150 - r * 0.5),
                IsClosed = false
            };
            fig.Segments.Add(new ArcSegment
            {
                Point = new Point(150 - r * 0.866, 150 + r * 0.5),
                Size = new Size(r, r),
                SweepDirection = SweepDirection.Counterclockwise
            });
            arcGeom.Figures.Add(fig);

            var arcPath = new Path
            {
                Data = arcGeom,
                Stroke = new LinearGradientBrush
                {
                    StartPoint = new(0, 0), EndPoint = new(0, 1),
                    GradientStops =
                    {
                        new(SpotlightKit.A(SpotlightKit.C("#ffe6c4"), 0.90 - arcIdx * 0.15), 0.0),
                        new(SpotlightKit.A(SpotlightKit.C("#ffb860"), 0.80 - arcIdx * 0.15), 0.5),
                        new(SpotlightKit.A(SpotlightKit.C("#e8a990"), 0.60 - arcIdx * 0.15), 1.0)
                    }
                },
                StrokeThickness = 3.2 - arcIdx * 0.6,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeStartLineCap = PenLineCap.Round,
                Effect = new DropShadowEffect
                {
                    Color = SpotlightKit.C("#ffb860"),
                    BlurRadius = 12 + arcIdx * 4,
                    ShadowDepth = 0,
                    Opacity = 0.80 - arcIdx * 0.15
                }
            };
            leftWaves.Children.Add(arcPath);
        }
        canvas.Children.Add(leftWaves);

        // Right Grip Motor (Light High-Frequency Precision Tactile)
        double rightMotorX = cx + 180;
        double rightMotorY = baseFloor - w * 0.32;
        var rightWaves = new Canvas { Width = 300, Height = 300, IsHitTestVisible = false };
        Canvas.SetLeft(rightWaves, rightMotorX - 150);
        Canvas.SetTop(rightWaves, rightMotorY - 150);

        for (int arcIdx = 1; arcIdx <= 3; arcIdx++)
        {
            double r = 45 + arcIdx * 28;
            var arcGeom = new PathGeometry();
            var fig = new PathFigure
            {
                StartPoint = new Point(150 + r * 0.866, 150 - r * 0.5),
                IsClosed = false
            };
            fig.Segments.Add(new ArcSegment
            {
                Point = new Point(150 + r * 0.866, 150 + r * 0.5),
                Size = new Size(r, r),
                SweepDirection = SweepDirection.Clockwise
            });
            arcGeom.Figures.Add(fig);

            var arcPath = new Path
            {
                Data = arcGeom,
                Stroke = new LinearGradientBrush
                {
                    StartPoint = new(0, 0), EndPoint = new(0, 1),
                    GradientStops =
                    {
                        new(SpotlightKit.A(SpotlightKit.C("#fff4db"), 0.90 - arcIdx * 0.15), 0.0),
                        new(SpotlightKit.A(SpotlightKit.C("#ffd27a"), 0.80 - arcIdx * 0.15), 0.5),
                        new(SpotlightKit.A(SpotlightKit.C("#d9b77a"), 0.60 - arcIdx * 0.15), 1.0)
                    }
                },
                StrokeThickness = 2.6 - arcIdx * 0.5,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeStartLineCap = PenLineCap.Round,
                Effect = new DropShadowEffect
                {
                    Color = SpotlightKit.C("#ffd27a"),
                    BlurRadius = 10 + arcIdx * 3,
                    ShadowDepth = 0,
                    Opacity = 0.75 - arcIdx * 0.15
                }
            };
            rightWaves.Children.Add(arcPath);
        }
        canvas.Children.Add(rightWaves);

        // --- MOTOR TECHNICAL DESCRIPTIONS ---
        var leftMotorBadge = new StackPanel { Width = 230 };
        leftMotorBadge.Children.Add(new TextBlock
        {
            Text = "HEAVY RUMBLE MOTOR",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        });
        leftMotorBadge.Children.Add(new TextBlock
        {
            Text = "30–80 Hz Sub-Bass Resonance",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.ChalkBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        leftMotorBadge.Children.Add(new TextBlock
        {
            Text = "Eccentric mass · Impact shocks & explosions",
            FontFamily = SpotlightKit.Body,
            FontSize = 12.5,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Canvas.SetLeft(leftMotorBadge, cx - w / 2 - 120);
        Canvas.SetTop(leftMotorBadge, baseFloor - 260);
        canvas.Children.Add(leftMotorBadge);

        var rightMotorBadge = new StackPanel { Width = 230 };
        rightMotorBadge.Children.Add(new TextBlock
        {
            Text = "LIGHT HAPTIC MOTOR",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        });
        rightMotorBadge.Children.Add(new TextBlock
        {
            Text = "120–250 Hz Tactile Click & Texture",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.ChalkBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        rightMotorBadge.Children.Add(new TextBlock
        {
            Text = "Precision counterweight · Fine surface telemetry",
            FontFamily = SpotlightKit.Body,
            FontSize = 12.5,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Canvas.SetLeft(rightMotorBadge, cx + w / 2 - 10);
        Canvas.SetTop(rightMotorBadge, baseFloor - 260);
        canvas.Children.Add(rightMotorBadge);

        // Master lighting desk fader at x = 1080, y = 220, h = 300, pct = 70
        var fader = SpotlightKit.Fader(1080, 220, 300, 70, true, "Master level", "70%");
        canvas.Children.Add(fader);

        // Monumental percentage readout in Bodoni MT at (1170, 195)
        var pctLarge = new TextBlock
        {
            Text = "70%",
            FontFamily = SpotlightKit.Bodoni,
            FontSize = 120,
            Foreground = SpotlightKit.GildBrush
        };
        Canvas.SetLeft(pctLarge, 1170);
        Canvas.SetTop(pctLarge, 195);
        canvas.Children.Add(pctLarge);

        // Preset chips: Off · Soft · Medium · Performance · Full Stage
        var presets = new StackPanel { Orientation = Orientation.Horizontal };
        presets.Children.Add(SpotlightKit.Chip("Off", false));
        presets.Children.Add(new Border { Width = 8 });
        presets.Children.Add(SpotlightKit.Chip("Soft", false));
        presets.Children.Add(new Border { Width = 8 });
        presets.Children.Add(SpotlightKit.Chip("Medium", false));
        presets.Children.Add(new Border { Width = 8 });
        presets.Children.Add(SpotlightKit.Chip("Performance", true));
        presets.Children.Add(new Border { Width = 8 });
        presets.Children.Add(SpotlightKit.Chip("Full Stage", false));
        Canvas.SetLeft(presets, 1170);
        Canvas.SetTop(presets, 370);
        canvas.Children.Add(presets);

        var animNote = new TextBlock
        {
            Text = "Dynamic harmonic modulation with matched phase coupling across both controller grips.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.MuteBrush,
            Width = 380,
            TextWrapping = TextWrapping.Wrap
        };
        Canvas.SetLeft(animNote, 1170);
        Canvas.SetTop(animNote, 424);
        canvas.Children.Add(animNote);

        // Cue card at bottom at (292, 650, w = 1260)
        var cardInner = new Grid();
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(600) });
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftRows = new StackPanel();
        leftRows.Children.Add(SpotlightKit.Row("Link motor levels", "Keep left and right equal", "Linked", true, null, 44));
        leftRows.Children.Add(SpotlightKit.Row("Test vibration", "Send haptic pulse to active motors", "Ready", false, SpotlightKit.TheatricalButton("Test Pulse", false, null, height: 32, fontSize: 11), 44));
        Grid.SetColumn(leftRows, 0);
        cardInner.Children.Add(leftRows);

        var rightCol = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        rightCol.Children.Add(new TextBlock
        {
            Text = "MOTOR REGIONS",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        });

        var regionChips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        regionChips.Children.Add(SpotlightKit.Chip("Both grips", true));
        regionChips.Children.Add(new Border { Width = 8 });
        regionChips.Children.Add(SpotlightKit.Chip("Left grip", false));
        regionChips.Children.Add(new Border { Width = 8 });
        regionChips.Children.Add(SpotlightKit.Chip("Right grip", false));
        rightCol.Children.Add(regionChips);

        rightCol.Children.Add(new TextBlock
        {
            Text = "Motors share a unified hardware PWM line on this controller model. Tactile modulation is linked.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 13.5,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(0, 10, 0, 0)
        });
        Grid.SetColumn(rightCol, 2);
        cardInner.Children.Add(rightCol);

        var cueCard = SpotlightKit.CueCard(cardInner, 1260, padding: new Thickness(24, 20, 24, 20));
        Canvas.SetLeft(cueCard, 292);
        Canvas.SetTop(cueCard, 650);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "vibration",
            title: "Vibration",
            lead: "",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Dashboard"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Adjust level"), ("X", "Test motors"), ("B", "Back") });

        spotlightPageHost.Children.Add(frame);
    }
}

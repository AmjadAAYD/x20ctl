using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using X20Ctl.Zone;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool spotlightMacrosWired;

    private void SpotlightShowMacros()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        if (Macros is not { } macro)
        {
            var none = new TextBlock { Text = "Select a supported controller to edit macros.", FontFamily = SpotlightKit.Body, FontSize = 18, Foreground = SpotlightKit.MuteBrush };
            Canvas.SetLeft(none, 302); Canvas.SetTop(none, 276);
            canvas.Children.Add(none);
        }
        else
        {
            if (!spotlightMacrosWired) { spotlightMacrosWired = true; macro.Changed += () => SpotlightLater(); }
            BuildSpotlightMacros(canvas, macro);
        }

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

    private void BuildSpotlightMacros(Canvas canvas, MacroWorkbench macro)
    {
        var draft = macro.Draft;
        bool canEdit = draft.CanEdit;
        var events = draft[macro.Slot];
        int selected = macro.SelectedIndex >= 0 && macro.SelectedIndex < events.Count ? macro.SelectedIndex : -1;

        // Slot chips at (1150, 146), from the real draft
        var slots = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string slot in draft.SlotNames)
        {
            string s = slot;
            var slotBox = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            var chip = SpotlightKit.Chip(s, s == macro.Slot);
            chip.Width = 66;
            slotBox.Children.Add(ThemeAction(chip, "theme:macro:slot:" + s, () => macro.SelectSlot(s)));
            slotBox.Children.Add(new TextBlock
            {
                Text = draft[s].Count > 0 ? "Assigned" : "Empty",
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

        var headStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        headStack.Children.Add(new TextBlock { Text = "Macro sequence", FontFamily = SpotlightKit.Body, FontSize = 21, Foreground = SpotlightKit.ChalkBrush });
        headStack.Children.Add(new TextBlock
        {
            Text = $"{events.Count} event{(events.Count == 1 ? "" : "s")} · {draft.Duration(macro.Slot)} ms total duration",
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

        // Sequence cue list at (292, 276, w = 620)
        var sheet = new StackPanel { Width = 620 };
        for (int i = 0; i < events.Count; i++)
        {
            int idx = i;
            var ev = events[i];
            bool on = i == selected;
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
            if (on) { r.BorderThickness = new Thickness(3, 0, 0, 0); r.BorderBrush = SpotlightKit.GoldBrush; }

            var g = new Grid { Margin = new Thickness(20, 0, 20, 0) };
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            leftStack.Children.Add(new TextBlock
            {
                Text = $"CUE {i + 1:00}",
                FontFamily = SpotlightKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.Light,
                Width = 70,
                Foreground = on ? SpotlightKit.GoldHiBrush : SpotlightKit.GoldBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            leftStack.Children.Add(SpotlightKit.BrassKey(ev.Buttons.Count > 0 ? ev.Buttons[0] : "·", on, 26));
            leftStack.Children.Add(new TextBlock
            {
                Text = (ev.Buttons.Count > 1 ? string.Join(" + ", ev.Buttons) + " · " : "") + $"{ev.HoldMs} ms hold · {ev.PauseMs} ms delay",
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 16,
                Foreground = SpotlightKit.B("#b3a48e"),
                Margin = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            g.Children.Add(leftStack);
            if (on)
                g.Children.Add(new Path
                {
                    Data = Geometry.Parse("M3 4L13 4L11 11L5 11Z M5 11L1 18H15L11 11"),
                    Fill = SpotlightKit.GoldBrush,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                });
            r.Child = g;
            sheet.Children.Add(ThemeAction(r, "theme:macro:event:" + i, () => macro.Select(idx)));
        }

        var addRow = new Border { Height = 52, Padding = new Thickness(20, 0, 20, 0), BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.12)), BorderThickness = new Thickness(0, 0, 0, 1) };
        var addStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        addStack.Children.Add(new TextBlock { Text = "", FontFamily = SpotlightKit.Icons, FontSize = 13, Foreground = SpotlightKit.GoldBrush, Margin = new Thickness(0, 0, 14, 0) });
        addStack.Children.Add(new TextBlock { Text = "Add event to sequence", FontFamily = SpotlightKit.Body, FontSize = 17, Foreground = SpotlightKit.MuteBrush });
        addRow.Child = addStack;
        sheet.Children.Add(ThemeAction(addRow, "theme:macro:add", macro.Add, canEdit && draft.EntryCount(macro.Slot) < 47));

        Canvas.SetLeft(sheet, 292);
        Canvas.SetTop(sheet, 276);
        canvas.Children.Add(sheet);

        // Timeline of the real events at (960, 270, w = 590, h = 150)
        var tlBorder = new Border
        {
            Width = 590, Height = 150,
            Background = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(0, 1), GradientStops = { new(SpotlightKit.C("#1b120e"), 0.0), new(SpotlightKit.C("#120b09"), 1.0) } },
            BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.25)),
            BorderThickness = new Thickness(1)
        };
        var tlCanvas = new Canvas { Width = 590, Height = 150 };
        int total = Math.Max(1, draft.Duration(macro.Slot));
        double cursor = 0;
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            double bx = 20 + cursor / total * 550;
            double bw = Math.Max(6, ev.HoldMs / (double)total * 550);
            bool on = i == selected;
            var bar = new Border
            {
                Width = bw, Height = 36,
                Background = on ? SpotlightKit.GoldBrush : SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.20)),
                BorderBrush = SpotlightKit.GoldHiBrush,
                BorderThickness = new Thickness(on ? 1.5 : 1),
                ClipToBounds = true,
                Child = new TextBlock
                {
                    Text = ev.Buttons.Count > 0 ? ev.Buttons[0] : "·",
                    FontFamily = SpotlightKit.Body,
                    FontSize = 14,
                    Foreground = on ? SpotlightKit.B("#1a0c08") : SpotlightKit.ChalkDimBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(bar, bx);
            Canvas.SetTop(bar, 56);
            tlCanvas.Children.Add(bar);
            cursor += ev.HoldMs + ev.PauseMs;
        }
        if (events.Count == 0)
        {
            var empty = new TextBlock { Text = "No events yet", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 15, Foreground = SpotlightKit.MuteBrush };
            Canvas.SetLeft(empty, 24); Canvas.SetTop(empty, 62);
            tlCanvas.Children.Add(empty);
        }
        tlBorder.Child = tlCanvas;
        Canvas.SetLeft(tlBorder, 960);
        Canvas.SetTop(tlBorder, 270);
        canvas.Children.Add(tlBorder);

        // Slot actions at (950, 470)
        var toolBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0) };
        var recordBtn = SpotlightKit.TheatricalButton(macro.IsRecording ? "Stop recording" : "Record sequence", false, null, onClick: macro.ToggleRecording, height: 40, fontSize: 11);
        toolBtns.Children.Add(recordBtn);
        var clearBtn = SpotlightKit.TheatricalButton("Clear slot", false, null, onClick: macro.Clear, height: 40, fontSize: 11);
        clearBtn.IsEnabled = canEdit && events.Count > 0;
        clearBtn.Margin = new Thickness(10, 0, 0, 0);
        toolBtns.Children.Add(clearBtn);
        var playBtn = SpotlightKit.TheatricalButton(macro.IsPreviewing ? "Stop preview" : "Preview timeline", false, null, onClick: macro.Preview, height: 40, fontSize: 11);
        playBtn.IsEnabled = events.Count > 0;
        playBtn.Margin = new Thickness(10, 0, 0, 0);
        toolBtns.Children.Add(playBtn);
        Canvas.SetLeft(toolBtns, 950);
        Canvas.SetTop(toolBtns, 470);
        canvas.Children.Add(toolBtns);

        // Selected cue card at (292, 620)
        if (selected >= 0)
        {
            var cur = events[selected];
            var cardInner = new StackPanel { Orientation = Orientation.Horizontal };
            cardInner.Children.Add(SpotlightKit.BrassKey(cur.Buttons.Count > 0 ? cur.Buttons[0] : "·", true, 42));
            var cueDetail = new StackPanel { Margin = new Thickness(24, 0, 0, 0), Width = 560, VerticalAlignment = VerticalAlignment.Center };
            cueDetail.Children.Add(new TextBlock { Text = $"Cue {selected + 1:00} · " + (cur.Buttons.Count > 0 ? string.Join(" + ", cur.Buttons.Select(Label)) : "Stick move"), FontFamily = SpotlightKit.Body, FontSize = 21, Foreground = SpotlightKit.ChalkBrush });
            cueDetail.Children.Add(new TextBlock { Text = $"Hold {cur.HoldMs} ms · delay {cur.PauseMs} ms", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush, Margin = new Thickness(0, 2, 0, 0) });
            cardInner.Children.Add(cueDetail);

            var segChips = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            void Step(string label, string tag, Func<MacroEvent> make)
            {
                var chip = SpotlightKit.Chip(label, false);
                segChips.Children.Add(ThemeAction(chip, tag, () => macro.Update(make()), canEdit));
                segChips.Children.Add(new Border { Width = 8 });
            }
            Step("Hold −5", "theme:macro:hold-dec", () => cur with { HoldMs = Math.Max(5, cur.HoldMs - 5) });
            Step("Hold +5", "theme:macro:hold-inc", () => cur with { HoldMs = Math.Min(10000, cur.HoldMs + 5) });
            Step("Delay −5", "theme:macro:pause-dec", () => cur with { PauseMs = Math.Max(0, cur.PauseMs - 5) });
            Step("Delay +5", "theme:macro:pause-inc", () => cur with { PauseMs = Math.Min(10000, cur.PauseMs + 5) });
            segChips.Children.Add(ThemeAction(SpotlightKit.Chip("Remove", false), "theme:macro:remove", macro.Remove, canEdit));
            cardInner.Children.Add(segChips);

            var cueCard = SpotlightKit.CueCard(cardInner, 1260, label: $"Cue {selected + 1:00}", padding: new Thickness(28, 20, 28, 20));
            Canvas.SetLeft(cueCard, 292);
            Canvas.SetTop(cueCard, 620);
            canvas.Children.Add(cueCard);
        }
    }

    private int spotlightVibrationPreview = VibrationDraft.DefaultStrength;

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
            Text = "Linked grip strength · Not written to controller memory.",
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
            Text = "LEFT GRIP",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        });
        leftMotorBadge.Children.Add(new TextBlock
        {
            Text = "Left grip motor",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.ChalkBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        leftMotorBadge.Children.Add(new TextBlock
        {
            Text = "Linked strength",
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
            Text = "RIGHT GRIP",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        });
        rightMotorBadge.Children.Add(new TextBlock
        {
            Text = "Right grip motor",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.ChalkBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        rightMotorBadge.Children.Add(new TextBlock
        {
            Text = "Linked strength",
            FontFamily = SpotlightKit.Body,
            FontSize = 12.5,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Canvas.SetLeft(rightMotorBadge, cx + w / 2 - 10);
        Canvas.SetTop(rightMotorBadge, baseFloor - 260);
        canvas.Children.Add(rightMotorBadge);

        // Master lighting desk fader at x = 1080, y = 220, h = 300, pct = 70
        int shownStrength = vibrationDraft.CanEdit ? vibrationDraft.Strength : spotlightVibrationPreview;
        void SetStrength(int percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            if (vibrationDraft.CanEdit) vibrationDraft.Set(percent); else spotlightVibrationPreview = percent;
            shownStrength = percent;
        }
        var fader = SpotlightKit.Fader(1080, 220, 300, shownStrength, true, "Strength", shownStrength + "%");
        fader.Cursor = System.Windows.Input.Cursors.Hand;
        fader.Focusable = true;
        fader.FocusVisualStyle = null;
        fader.Tag = "theme:vibration:strength";
        var pctLarge = new TextBlock
        {
            Text = shownStrength + "%",
            FontFamily = SpotlightKit.Bodoni,
            FontSize = 120,
            Foreground = SpotlightKit.GildBrush
        };
        void RedrawFader()
        {
            var fresh = SpotlightKit.Fader(1080, 220, 300, shownStrength, true, "Strength", shownStrength + "%");
            var moved = fresh.Children.Cast<UIElement>().ToList();
            fresh.Children.Clear();
            fader.Children.Clear();
            foreach (var child in moved) fader.Children.Add(child);
            pctLarge.Text = shownStrength + "%";
        }
        bool draggingStrength = false;
        fader.MouseLeftButtonDown += (_, e) => { draggingStrength = true; fader.CaptureMouse(); fader.Focus(); SetStrength((int)Math.Round(Math.Clamp(300 - e.GetPosition(fader).Y, 0, 300) / 3.0)); RedrawFader(); };
        fader.MouseMove += (_, e) => { if (draggingStrength && fader.IsMouseCaptured) { SetStrength((int)Math.Round(Math.Clamp(300 - e.GetPosition(fader).Y, 0, 300) / 3.0)); RedrawFader(); } };
        fader.MouseLeftButtonUp += (_, _) => { if (!draggingStrength) return; draggingStrength = false; fader.ReleaseMouseCapture(); SpotlightShowVibration(); Dispatcher.BeginInvoke(new Action(() => { foreach (var el in spotlightPageHost!.Children.OfType<Canvas>().SelectMany(c => c.Children.OfType<FrameworkElement>())) if (Equals(el.Tag, "theme:vibration:strength")) { el.Focus(); break; } }), System.Windows.Threading.DispatcherPriority.Input); };
        fader.KeyDown += (_, e) =>
        {
            int step = e.Key is Key.Up or Key.Right ? 1 : e.Key is Key.Down or Key.Left ? -1 : 0;
            if (step == 0) return;
            SetStrength(shownStrength + step * 5); RedrawFader(); e.Handled = true;
        };
        canvas.Children.Add(fader);

        // Monumental percentage readout in Bodoni MT at (1170, 195)
        Canvas.SetLeft(pctLarge, 1170);
        Canvas.SetTop(pctLarge, 195);
        canvas.Children.Add(pctLarge);

        // Preset chips: Off · Soft · Medium · Performance · Full Stage
        var presets = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (int level in new[] { 0, 25, 50, 75, 100 })
        {
            int chosen = level;
            if (presets.Children.Count > 0) presets.Children.Add(new Border { Width = 8 });
            presets.Children.Add(SpotlightKit.Chip(level == 0 ? "Off" : level + "%", shownStrength == level, () => { SetStrength(chosen); SpotlightShowVibration(); }));
        }
        Canvas.SetLeft(presets, 1170);
        Canvas.SetTop(presets, 370);
        canvas.Children.Add(presets);

        var animNote = new TextBlock
        {
            Text = vibrationDraft.CanEdit ? "Linked grip strength · Local draft. Not sent to the controller." : "Preview only · Native settings unavailable for this model.",
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
        leftRows.Children.Add(SpotlightKit.Row("Test vibration", "Needs a verified connection", "Unavailable", false, null, 44));
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
        rightCol.Children.Add(regionChips);

        rightCol.Children.Add(new TextBlock
        {
            Text = "Left and right grip strength are linked.",
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

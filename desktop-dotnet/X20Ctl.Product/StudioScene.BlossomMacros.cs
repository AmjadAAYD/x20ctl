using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Macros workbench presentation for the Yozakura (Blossom) theme:
/// "The sequence floats down a river as lanterns, one per event;
/// the focused event burns brightest, sending shimmering water reflections across the dark stream."
/// Strictly implements t_blossom.py:macros().
/// </summary>
public partial class StudioScene
{
    private bool blossomMacroWired;

    private void RenderBlossomMacros(Canvas host)
    {
        if (Macros is not { } macro) return;
        if (!blossomMacroWired)
        {
            blossomMacroWired = true;
            macro.Changed += BlossomLater;
        }

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
            Text = "Combine button presses and timed pauses into a local sequence.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        host.Children.Add(leadTb);

        // Top-right Macro Slot Lanterns at (1250, 34)
        var slotNames = macro.Draft.SlotNames;
        for (int i = 0; i < slotNames.Length; i++)
        {
            string slot = slotNames[i];
            bool active = slot == macro.Slot;
            string sub = macro.Draft[slot].Count == 0 ? "Empty" : $"{macro.Draft[slot].Count} steps";
            double mx = 1250 - Math.Max(0, slotNames.Length - 4) * 80 + i * 80;
            var slotLantern = BlossomKit.PaperLantern(40, 52, active, null, slot, active ? 0.9 : 0.5, 18, 16);
            Canvas.SetLeft(slotLantern, mx - slotLantern.Width / 2.0);
            Canvas.SetTop(slotLantern, 34);
            host.Children.Add(ThemeAction(slotLantern, "theme:macro:slot:" + slot, () => { macro.SelectSlot(slot); BlossomLater(); }));

            var slotSubTb = new TextBlock
            {
                Text = sub,
                FontFamily = BlossomKit.Body,
                FontSize = 12,
                Foreground = active ? BlossomKit.TextAmberBrush : BlossomKit.TextMuteBrush,
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

        // River sequence header
        var seqTb = new TextBlock
        {
            Text = $"SEQUENCE TIMELINE · {macro.Draft[macro.Slot].Count} STEPS · {macro.Draft.Duration(macro.Slot)}MS TOTAL",
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.TextSakuraBrush
        };
        Canvas.SetLeft(seqTb, 20);
        Canvas.SetTop(seqTb, 12);

        var events = macro.Draft[macro.Slot];
        double riverContentW = Math.Max(rw, 180 + (events.Count + 1) * 180);
        var riverCanvas = new Canvas { Width = riverContentW, Height = rh };

        // River water ripples
        for (int j = 0; j < 11; j++)
        {
            double lx = (j * 137) % (riverContentW - 200);
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
        riverCanvas.Children.Add(seqTb);

        // Floating Paper Lanterns along the river
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            bool active = i == macro.SelectedIndex;
            double fx = 120 + i * 180;
            double wy = 128; // water surface line

            // Water reflection streaks if lit
            if (active)
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
                Background = active ? BlossomKit.CreateWarmLanternBrush() : BlossomKit.CreateColdLanternBrush(),
                BorderBrush = active ? BlossomKit.B("#3a1d14") : BlossomKit.B("rgba(255,200,215,0.2)"),
                BorderThickness = new Thickness(3),
                Cursor = Cursors.Hand
            };

            if (active)
            {
                floaterBox.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0xff, 0xa0, 0x5a),
                    BlurRadius = 26,
                    ShadowDepth = 0,
                    Opacity = 0.85
                };
            }

            string letter = ev.Buttons.Count > 0 ? string.Join("+", ev.Buttons.Select(RebelInputGlyph)) : "⏳";
            var fTb = new TextBlock
            {
                Text = letter,
                FontFamily = BlossomKit.Display,
                FontSize = letter.Length > 2 ? 18 : 30,
                Foreground = active ? BlossomKit.B("#6a1f18") : BlossomKit.B("rgba(255,225,232,0.8)"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            floaterBox.Child = fTb;
            Canvas.SetLeft(floaterBox, fx - 37);
            Canvas.SetTop(floaterBox, wy - 66 - 6);
            int eventIndex = i;
            riverCanvas.Children.Add(ThemeAction(floaterBox, "theme:macro:event:" + i, () => { macro.Select(eventIndex); BlossomLater(); }));

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
            string evName = ev.Buttons.Count > 0 ? ("Press " + string.Join("+", ev.Buttons.Select(RebelInputGlyph))) : "Wait";
            string evSub = ev.HoldMs > 0 ? $"Duration {ev.HoldMs}ms" : $"Pause {ev.PauseMs}ms";
            var stepLabelStack = new StackPanel { Width = 200, HorizontalAlignment = HorizontalAlignment.Center };
            stepLabelStack.Children.Add(new TextBlock
            {
                Text = evName,
                FontFamily = BlossomKit.Display,
                FontSize = 20,
                Foreground = active ? BlossomKit.B("#ffe6cc") : BlossomKit.B("#e3cfd6"),
                TextAlignment = TextAlignment.Center
            });
            stepLabelStack.Children.Add(new TextBlock
            {
                Text = evSub,
                FontFamily = BlossomKit.Body,
                FontSize = 13,
                Foreground = active ? BlossomKit.TextAmberBrush : BlossomKit.TextMuteBrush,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Canvas.SetLeft(stepLabelStack, fx - 100);
            Canvas.SetTop(stepLabelStack, wy + 42);
            riverCanvas.Children.Add(stepLabelStack);
        }

        // Add Step Box at end
        double addX = 120 + events.Count * 180;
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
        Canvas.SetLeft(addBox, addX - 37);
        Canvas.SetTop(addBox, 56);
        riverCanvas.Children.Add(ThemeAction(addBox, "theme:macro:add", () => { macro.Add(); BlossomLater(); }, macro.Draft.CanEdit && macro.Draft.EntryCount(macro.Slot) < 47));

        var addTb = new TextBlock
        {
            Text = "Add step",
            FontFamily = BlossomKit.Body,
            FontSize = 17,
            Foreground = BlossomKit.B("#c9b3bc"),
            TextAlignment = TextAlignment.Center,
            Width = 140
        };
        Canvas.SetLeft(addTb, addX - 70);
        Canvas.SetTop(addTb, 142);
        riverCanvas.Children.Add(addTb);

        var riverScroll = new ScrollViewer
        {
            Content = riverCanvas,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Width = rw,
            Height = rh
        };
        riverBack.Child = riverScroll;

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

        int totalDur = Math.Max(1, macro.Draft.Duration(macro.Slot));
        double curDawX = 20;
        for (int i = 0; i < events.Count; i++)
        {
            var entry = events[i];
            double bWidth = Math.Clamp((entry.HoldMs + entry.PauseMs) * 580.0 / totalDur, 30, 260);
            Color col = i == macro.SelectedIndex ? Color.FromRgb(0xff, 0xc2, 0x7a) : Color.FromRgb(0xff, 0x5d, 0x8a);
            var b = new Border
            {
                Width = bWidth,
                Height = 80,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromArgb(140, col.R, col.G, col.B)),
                BorderBrush = new SolidColorBrush(col),
                BorderThickness = new Thickness(1.5),
                Child = new TextBlock
                {
                    Text = $"[{RebelPresses(entry)}] {entry.HoldMs}ms",
                    FontFamily = BlossomKit.Display,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = BlossomKit.TextWhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            };
            Canvas.SetLeft(b, curDawX);
            Canvas.SetTop(b, 35);
            dawCanvas.Children.Add(b);
            curDawX += bWidth + 8;
            if (curDawX > 600) break;
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
        string triggerText = macro.Draft.PcMode ? macro.Draft.Trigger(macro.Slot) ?? "Not used" : macro.Draft.PhysicalSlots.Contains(macro.Slot) ? $"Paddle {macro.Slot}" : "Not used";
        var trigPill = new Border
        {
            Padding = new Thickness(14, 6, 14, 6),
            CornerRadius = new CornerRadius(18),
            Background = BlossomKit.B("rgba(255,220,232,0.06)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.2)"),
            BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = triggerText + " ▾", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.TextWhiteBrush }
        };
        Grid.SetColumn(trigLab, 0);
        Grid.SetColumn(trigPill, 1);
        trigGrid.Children.Add(trigLab);
        trigGrid.Children.Add(ThemeAction(trigPill, "theme:macro:trigger", () =>
        {
            if (macro.Draft.PcMode && macro.Draft.CanEdit)
            {
                string?[] all = [null, .. MacroDraft.TriggerInputs];
                int cur = Array.IndexOf(all, macro.Draft.Trigger(macro.Slot));
                macro.SetTrigger(all[(cur + 1) % all.Length]);
                BlossomLater();
            }
        }, macro.Draft.PcMode && macro.Draft.CanEdit));
        trigRow.Child = trigGrid;
        leftBottomStack.Children.Add(trigRow);

        // Action Tool Buttons: Record, Clear, Test Sequence
        var toolStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        foreach (var (ic, txt, tag, act, prim) in new (string, string, string, Action, bool)[]
        {
            ("\uE7C8", macro.IsRecording ? "Stop" : "Record", "record", () => { macro.ToggleRecording(); BlossomLater(); }, false),
            ("\uE896", "Clear", "clear", () => { macro.Clear(); BlossomLater(); }, false),
            ("\uE768", "Test sequence", "preview", macro.Preview, true)
        })
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
            if (tag == "record" && macro.Record.Content is StackPanel { Children.Count: 2 } nativeRecord && nativeRecord.Children[1] is TextBlock nativeLabel)
                ((TextBlock)bStack.Children[1]).SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = nativeLabel });
            btnBorder.Child = bStack;
            toolStack.Children.Add(ThemeAction(btnBorder, "theme:macro:" + tag, act, tag == "preview" || macro.Draft.CanEdit));
        }
        leftBottomStack.Children.Add(toolStack);
        var status = new TextBlock { Foreground = BlossomKit.TextMuteBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
        status.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = macro.StatusText });
        leftBottomStack.Children.Add(status);

        Canvas.SetLeft(leftBottomStack, 236);
        Canvas.SetTop(leftBottomStack, 452);
        host.Children.Add(leftBottomStack);

        // 5. Bottom Right: Illuminated Washi Parchment Card at (930, 452, width 614, height 374)
        var litCard = BlossomKit.LitPaperCard(614, 374, new Thickness(24, 20, 24, 20));
        var cardStack = new StackPanel();

        if (macro.SelectedIndex >= 0 && macro.SelectedIndex < events.Count)
        {
            var selectedEvent = events[macro.SelectedIndex];
            string evTitle = selectedEvent.Buttons.Count > 0 ? string.Join("+", selectedEvent.Buttons.Select(RebelInputGlyph)) : "Wait";
            cardStack.Children.Add(new TextBlock
            {
                Text = $"Event {macro.SelectedIndex + 1} · {evTitle}",
                FontFamily = BlossomKit.Display,
                FontSize = 26,
                Foreground = BlossomKit.B(BlossomKit.LitPaperInk)
            });
            cardStack.Children.Add(new TextBlock
            {
                Text = $"{RebelPresses(selectedEvent)} · held {selectedEvent.HoldMs} ms · then {selectedEvent.PauseMs} ms pause",
                FontFamily = BlossomKit.Body,
                FontSize = 14,
                Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
                Margin = new Thickness(0, 2, 0, 10)
            });

            // Input Chord Buttons
            var inputWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (string btnKey in MacroDraft.Inputs)
            {
                bool on = selectedEvent.Buttons.Contains(btnKey);
                var chipBorder = new Border
                {
                    Height = 30,
                    Padding = new Thickness(10, 0, 10, 0),
                    CornerRadius = new CornerRadius(15),
                    Background = on ? BlossomKit.B(BlossomKit.LitPaperInk) : BlossomKit.B("transparent"),
                    BorderBrush = on ? BlossomKit.B("transparent") : BlossomKit.B("rgba(59,23,18,0.35)"),
                    BorderThickness = new Thickness(1.2),
                    Margin = new Thickness(0, 0, 6, 6),
                    Child = new TextBlock
                    {
                        Text = RebelInputGlyph(btnKey),
                        FontFamily = BlossomKit.Body,
                        FontSize = 13,
                        Foreground = on ? BlossomKit.B("#ffe2b8") : BlossomKit.B(BlossomKit.LitPaperSub),
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                string k = btnKey;
                inputWrap.Children.Add(ThemeAction(chipBorder, "theme:macro:input:" + k, () =>
                {
                    macro.Update(selectedEvent with { Buttons = selectedEvent.Buttons.Contains(k) ? selectedEvent.Buttons.Where(b => b != k).ToArray() : selectedEvent.Buttons.Append(k).ToArray() });
                    BlossomLater();
                }, macro.Draft.CanEdit));
            }
            cardStack.Children.Add(inputWrap);

            var sticks = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (bool left in new[] { true, false })
            {
                sticks.Children.Add(new TextBlock { Text = left ? "Left stick " : "Right stick ", Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center });
                var heading = new ComboBox { ItemsSource = Enum.GetValues<StickHeading>(), SelectedItem = left ? selectedEvent.Left : selectedEvent.Right, IsEnabled = macro.Draft.CanEdit, Tag = "theme:macro:stick:" + (left ? "left" : "right"), Width = 110, Margin = new Thickness(0, 0, 12, 0) };
                heading.SelectionChanged += (_, _) => { if (heading.SelectedItem is StickHeading value) { macro.Update(left ? selectedEvent with { Left = value } : selectedEvent with { Right = value }); BlossomLater(); } };
                sticks.Children.Add(heading);
            }
            cardStack.Children.Add(sticks);

            // Timing Adjustments: Hold and Pause
            foreach (var (tLabel, isPause) in new[] { ("Hold", false), ("Pause", true) })
            {
                int val = isPause ? selectedEvent.PauseMs : selectedEvent.HoldMs;
                var timeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                timeRow.Children.Add(new TextBlock
                {
                    Text = $"{tLabel} {val}ms",
                    FontFamily = BlossomKit.Display,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = BlossomKit.B(BlossomKit.LitPaperInk),
                    Width = 120,
                    VerticalAlignment = VerticalAlignment.Center
                });

                foreach (int delta in new[] { -5, 5 })
                {
                    var dBtn = new Border
                    {
                        Height = 26,
                        Padding = new Thickness(10, 0, 10, 0),
                        CornerRadius = new CornerRadius(13),
                        Background = BlossomKit.B("rgba(59,23,18,0.1)"),
                        BorderBrush = BlossomKit.B("rgba(59,23,18,0.3)"),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 6, 0),
                        Child = new TextBlock
                        {
                            Text = delta < 0 ? "−5ms" : "+5ms",
                            FontFamily = BlossomKit.Body,
                            FontSize = 12,
                            Foreground = BlossomKit.B(BlossomKit.LitPaperInk),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    int d = delta;
                    timeRow.Children.Add(ThemeAction(dBtn, $"theme:macro:time:{tLabel}:{d}", () =>
                    {
                        macro.Update(isPause ? selectedEvent with { PauseMs = Math.Clamp(val + d, 0, 327675) } : selectedEvent with { HoldMs = Math.Clamp(val + d, 0, 327675) });
                        BlossomLater();
                    }, macro.Draft.CanEdit));
                }
                cardStack.Children.Add(timeRow);
            }

            // Actions: Duplicate, Remove
            var cardActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var dupBtn = new Border
            {
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                CornerRadius = new CornerRadius(16),
                Background = BlossomKit.B("rgba(59,23,18,0.12)"),
                BorderBrush = BlossomKit.B("rgba(59,23,18,0.3)"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock { Text = "Duplicate step", FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center }
            };
            cardActions.Children.Add(ThemeAction(dupBtn, "theme:macro:duplicate", () => { macro.Duplicate(); BlossomLater(); }, macro.Draft.CanEdit && macro.Draft.EntryCount(macro.Slot) < 47));

            var delBtn = new Border
            {
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                CornerRadius = new CornerRadius(16),
                Background = BlossomKit.B("rgba(180,40,40,0.15)"),
                BorderBrush = BlossomKit.B("rgba(180,40,40,0.4)"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = "Delete step", FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.B("#6a1f18"), VerticalAlignment = VerticalAlignment.Center }
            };
            cardActions.Children.Add(ThemeAction(delBtn, "theme:macro:remove", () => { macro.Remove(); BlossomLater(); }, macro.Draft.CanEdit));
            cardStack.Children.Add(cardActions);
        }
        else
        {
            cardStack.Children.Add(new TextBlock
            {
                Text = "No step selected",
                FontFamily = BlossomKit.Display,
                FontSize = 24,
                Foreground = BlossomKit.B(BlossomKit.LitPaperInk)
            });
            cardStack.Children.Add(new TextBlock
            {
                Text = "Select a floating lantern above or click + Add step to begin constructing your macro.",
                FontFamily = BlossomKit.Body,
                FontSize = 14,
                Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
        }

        litCard.Child = new ScrollViewer { Content = cardStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
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

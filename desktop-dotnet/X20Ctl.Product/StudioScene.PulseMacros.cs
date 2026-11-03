using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    // ========================================================================
    // 6-macros: Macro Editor (matches t_pulse.py:592 macros() and macros.png)
    // Paddle slot orbs on top right, sine wave event timeline in center,
    // multi-lane track & tools in middle, rising pearl card for event details at bottom.
    // ========================================================================
    private void BuildPulseMacros(Canvas host)
    {
        Macros ??= new MacroWorkbench(macroDraft, layout, this, RegisterFocus, !animate, Model.Id);
        var m = Macros;
        string slot = m.Slot;
        var events = macroDraft[slot];
        int sel = m.SelectedIndex;

        // 1. Frame Title & Lead (x=270, y=30)
        var titleBlock = PulseParts.Disp("Macros", 44, Colors.White, FontWeights.Light);
        titleBlock.Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.45 };
        Canvas.SetLeft(titleBlock, 270);
        Canvas.SetTop(titleBlock, 30);
        host.Children.Add(titleBlock);

        var leadBlock = PulseParts.Body("Macros · PC mode · Applies on this PC, to games, for any controller. PC mode can't see a back paddle, so pick the button that plays each slot.", 14.5, PulseInk.C("#aab4c4"));
        leadBlock.Width = 820;
        Canvas.SetLeft(leadBlock, 272);
        Canvas.SetTop(leadBlock, 92);
        host.Children.Add(leadBlock);

        // 2. Paddle Slot Orbs at Top Right (x=1130 + i * 104, y=54)
        var slotNames = macroDraft.SlotNames.ToList();
        if (slotNames.Count == 0) slotNames = new() { "M1", "M2", "M3", "M4" };
        for (int i = 0; i < slotNames.Count && i < 4; i++)
        {
            string sName = slotNames[i];
            bool active = sName == slot;
            double ox = 1130 + i * 104;

            var slotOrb = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new(22),
                Background = active ? PulseParts.PearlWhiteBrush : PulseInk.B(PulseInk.A(PulseInk.C("#080c18"), 0.85)),
                BorderBrush = PulseInk.B(PulseInk.A(Colors.White, active ? 0.95 : 0.25)),
                BorderThickness = new(1.2),
                Effect = active ? new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.85 } : null,
                Child = new TextBlock
                {
                    Text = sName,
                    FontFamily = PulseParts.BodyFont,
                    FontSize = 15,
                    FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = active ? PulseInk.B(PulseInk.C("#0b162c")) : PulseInk.B(PulseInk.C("#dfe5ee")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            var slotBtn = new Button
            {
                Content = slotOrb,
                Background = Brushes.Transparent,
                BorderThickness = new(0),
                Padding = new(0),
                Cursor = Cursors.Hand
            };
            string sCopy = sName;
            slotBtn.Click += (_, _) => { m.SelectSlot(sCopy); PulseLater(); };
            Canvas.SetLeft(slotBtn, ox + 18);
            Canvas.SetTop(slotBtn, 78);
            host.Children.Add(slotBtn);

            var slotSub = new TextBlock
            {
                Text = $"Paddle {i + 1}",
                FontFamily = PulseParts.BodyFont,
                FontSize = 12,
                Foreground = PulseInk.B(PulseInk.C("#7f8aa0")),
                TextAlignment = TextAlignment.Center,
                Width = 100
            };
            Canvas.SetLeft(slotSub, ox - 10);
            Canvas.SetTop(slotSub, 128);
            host.Children.Add(slotSub);
        }

        // 3. Sequence Info (x=272, y=196)
        var seqStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        seqStack.Children.Add(PulseParts.Disp($"{slot} · Sequence", 22, Colors.White, FontWeights.SemiBold));
        int totalMs = events.Sum(e => e.HoldMs + e.PauseMs);
        seqStack.Children.Add(PulseParts.Body($"Events {events.Count} · Capacity {events.Count} / 47 · {totalMs} ms", 14, PulseInk.C("#aab4c4")).Also(t => t.Margin = new(16, 0, 0, 0)));
        Canvas.SetLeft(seqStack, 272);
        Canvas.SetTop(seqStack, 196);
        host.Children.Add(seqStack);

        // 4. Wave Timeline (x0=272, y0=330, W=1270)
        double wx0 = 272, wy0 = 330, ww = 1270;
        var waveCanvas = new Canvas { Width = ww, Height = 140, ClipToBounds = false };
        Canvas.SetLeft(waveCanvas, wx0);
        Canvas.SetTop(waveCanvas, wy0 - 70);

        // Baseline across the canvas
        var baseLine = new Line
        {
            X1 = 0, Y1 = 70, X2 = ww, Y2 = 70,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.12)),
            StrokeThickness = 1
        };
        waveCanvas.Children.Add(baseLine);

        // Wave peaks at xs = [190, 490, 790]
        double[] peakXs = [190, 490, 790];
        var waveGeo = new StreamGeometry();
        using (var ctx = waveGeo.Open())
        {
            ctx.BeginFigure(new Point(0, 70), false, false);
            double curX = 0;
            for (int p = 0; p < peakXs.Length; p++)
            {
                double px = peakXs[p];
                double amp = (p == (sel >= 0 && sel < 3 ? sel : 1)) ? 34 : 24;
                ctx.LineTo(new Point(px - 60, 70), true, false);
                ctx.BezierTo(new Point(px - 30, 70 - amp), new Point(px + 30, 70 - amp), new Point(px + 60, 70), true, false);
                curX = px + 60;
            }
            ctx.LineTo(new Point(ww, 70), true, false);
        }
        waveGeo.Freeze();

        var wavePath = new Path
        {
            Data = waveGeo,
            Stroke = new LinearGradientBrush(PulseInk.C("#5cb3ff"), PulseInk.C("#a8ccff"), 0),
            StrokeThickness = 2.2,
            Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.7 }
        };
        waveCanvas.Children.Add(wavePath);

        // Focused peak resonance ring around focused peak
        int focusedPeak = sel >= 0 && sel < 3 ? sel : 1;
        var peakRes = PulseParts.ResonanceMark();
        Canvas.SetLeft(peakRes, peakXs[focusedPeak] - 7);
        Canvas.SetTop(peakRes, 70 - 34 - 7);
        waveCanvas.Children.Add(peakRes);

        // Event Orbs on the Waveform
        for (int eIdx = 0; eIdx < 3; eIdx++)
        {
            double ex = peakXs[eIdx];
            bool isFocused = eIdx == focusedPeak;
            MacroEvent? curEv = eIdx < events.Count ? events[eIdx] : null;
            string keyGlyph = curEv != null && curEv.Buttons.Count > 0 ? TargetGlyph(curEv.Buttons[0]) : "A";
            double orbD = isFocused ? 56 : 46;

            var eOrb = new Border
            {
                Width = orbD,
                Height = orbD,
                CornerRadius = new(orbD / 2),
                Background = isFocused ? PulseParts.PearlWhiteBrush : PulseInk.B(PulseInk.A(PulseInk.C("#080c18"), 0.85)),
                BorderBrush = PulseInk.B(PulseInk.A(Colors.White, isFocused ? 0.95 : 0.35)),
                BorderThickness = new(1.4),
                Effect = isFocused ? new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.9 } : null,
                Child = new TextBlock
                {
                    Text = keyGlyph,
                    FontFamily = PulseParts.BodyFont,
                    FontSize = isFocused ? 20 : 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = isFocused ? PulseInk.B(PulseInk.C("#0b162c")) : Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            int evIndexCopy = eIdx;
            var eBtn = new Button
            {
                Content = eOrb,
                Background = Brushes.Transparent,
                BorderThickness = new(0),
                Padding = new(0),
                Cursor = Cursors.Hand
            };
            eBtn.Click += (_, _) => { m.Select(evIndexCopy); PulseLater(); };
            Canvas.SetLeft(eBtn, ex - orbD / 2);
            Canvas.SetTop(eBtn, 70 - (isFocused ? 34 : 24) - orbD / 2);
            waveCanvas.Children.Add(eBtn);

            // Step number above: "01", "02", "03"
            var numText = PulseParts.Mono($"0{eIdx + 1}", 13, isFocused ? Colors.White : PulseParts.CyanColor, FontWeights.Bold);
            numText.TextAlignment = TextAlignment.Center;
            numText.Width = 60;
            Canvas.SetLeft(numText, ex - 30);
            Canvas.SetTop(numText, 70 - (isFocused ? 34 : 24) - orbD / 2 - 24);
            waveCanvas.Children.Add(numText);

            // Timing sublabel below: "40 ms hold · 20 ms pause"
            int hold = curEv?.HoldMs ?? 40;
            int pause = curEv?.PauseMs ?? 20;
            var timingText = PulseParts.Body($"{hold} ms hold · {pause} ms pause", 12.5, PulseInk.C("#aab4c4"));
            timingText.TextAlignment = TextAlignment.Center;
            timingText.Width = 200;
            Canvas.SetLeft(timingText, ex - 100);
            Canvas.SetTop(timingText, 70 + 24);
            waveCanvas.Children.Add(timingText);
        }

        // Placeholder dashed circle for + Add event at x = 1090
        double addX = 1090;
        var addCircle = new Ellipse
        {
            Width = 54,
            Height = 54,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.35)),
            StrokeThickness = 1.4,
            StrokeDashArray = new DoubleCollection([4, 6]),
            Fill = PulseInk.B(PulseInk.A(Colors.White, 0.03))
        };
        Canvas.SetLeft(addCircle, addX - 27);
        Canvas.SetTop(addCircle, 70 - 27);
        waveCanvas.Children.Add(addCircle);

        var addBtn = new Button
        {
            Content = new TextBlock { Text = "+", FontSize = 22, Foreground = PulseInk.B(PulseInk.C("#c8d7f0")), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 54,
            Height = 54,
            Background = Brushes.Transparent,
            BorderThickness = new(0),
            Cursor = Cursors.Hand
        };
        addBtn.Click += (_, _) => { m.Add(); PulseLater(); };
        Canvas.SetLeft(addBtn, addX - 27);
        Canvas.SetTop(addBtn, 70 - 27);
        waveCanvas.Children.Add(addBtn);

        var addLabel = PulseParts.Body("+ Add event", 13, PulseInk.C("#c3ccda"));
        addLabel.TextAlignment = TextAlignment.Center;
        addLabel.Width = 120;
        Canvas.SetLeft(addLabel, addX - 60);
        Canvas.SetTop(addLabel, 70 + 34);
        waveCanvas.Children.Add(addLabel);

        host.Children.Add(waveCanvas);

        // 5. Middle: Multi-Lane Timeline Canvas at (x=262, y=440, w=640)
        var timelineCard = PulseParts.GlassCard(BuildPulseMacroTimeline(m, events, sel), new Thickness(16, 12, 16, 12), radius: 22);
        timelineCard.Width = 640;
        timelineCard.Height = 150;
        Canvas.SetLeft(timelineCard, 262);
        Canvas.SetTop(timelineCard, 440);
        host.Children.Add(timelineCard);

        // 6. Middle: Played By Row & Playback Controls at (x=950, y=440, w=600)
        var rightTools = new StackPanel { Width = 600 };
        Canvas.SetLeft(rightTools, 950);
        Canvas.SetTop(rightTools, 440);

        var playedByCtrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        playedByCtrl.Children.Add(PulseParts.Chip(macroDraft.PcMode ? $"{slot} Paddle" : "Not used", "default"));
        rightTools.Children.Add(PulseParts.GlassRow(
            PulseParts.Disp("In PC mode, played by", 15, Colors.White, FontWeights.SemiBold),
            playedByCtrl,
            active: false,
            compact: true));

        var playbackBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(6, 16, 0, 0) };
        playbackBtns.Children.Add(PulseParts.PillButton(m.IsRecording ? "Stop record" : "Record", primary: m.IsRecording, small: true, onClick: () => { m.ToggleRecording(); PulseLater(); }).Also(b => b.Margin = new(0, 0, 12, 0)));
        playbackBtns.Children.Add(PulseParts.PillButton("Read controller", primary: false, small: true, onClick: () => { _ = m.ReadController(text => { pulseMacroNote = text; PulseLater(); }); }).Also(b => b.Margin = new(0, 0, 12, 0)));
        playbackBtns.Children.Add(PulseParts.PillButton(m.IsPreviewing ? "Stop" : "Preview timeline", primary: m.IsPreviewing, small: true, onClick: () => { if (m.IsPreviewing) m.StopPreview(); else m.Preview(); PulseLater(); }));
        rightTools.Children.Add(playbackBtns);
        host.Children.Add(rightTools);

        // 7. Bottom: Large Rising Pearl Card at (x=262, y=626, w=1300)
        MacroEvent? ev = sel >= 0 && sel < events.Count ? events[sel] : (events.Count > 0 ? events[0] : null);
        string evBtn = ev != null && ev.Buttons.Count > 0 ? TargetGlyph(ev.Buttons[0]) : "A";
        int evHold = ev?.HoldMs ?? 40;
        int evPause = ev?.PauseMs ?? 20;

        var pearlContent = new DockPanel { Width = 1220 };

        // Left of pearl: Pearl key badge
        var keyBadge = PulseParts.PearlKeyBadge(evBtn, 52, 22);
        DockPanel.SetDock(keyBadge, Dock.Left);
        pearlContent.Children.Add(keyBadge);

        // Right of pearl: Timing segment chips
        var segChips = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        segChips.Children.Add(PulseParts.ChipButton("Buttons", true, () => { }, onPearl: true).Also(b => b.Margin = new(0, 0, 8, 0)));
        segChips.Children.Add(PulseParts.ChipButton("Sticks", false, () => { }, onPearl: true).Also(b => b.Margin = new(0, 0, 8, 0)));
        segChips.Children.Add(PulseParts.ChipButton("Timing", false, () => { }, onPearl: true).Also(b => b.Margin = new(0, 0, 8, 0)));
        segChips.Children.Add(PulseParts.ChipButton("Actions", false, () => { }, onPearl: true));
        DockPanel.SetDock(segChips, Dock.Right);
        pearlContent.Children.Add(segChips);

        // Center of pearl: Event Title and description
        var centerTitles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(24, 0, 24, 0) };
        centerTitles.Children.Add(PulseParts.Disp($"Event {(sel >= 0 ? sel + 1 : 1)} · what it presses", 22, PulseParts.PearlInk, FontWeights.SemiBold));
        centerTitles.Children.Add(PulseParts.Body($"{evBtn} · held {evHold} ms · then {evPause} ms pause · Pick the button that plays this slot (left), or PC mode won't play it.", 14.5, PulseParts.PearlSoft).Also(t => t.Margin = new(0, 4, 0, 0)));
        pearlContent.Children.Add(centerTitles);

        var pearlCard = PulseParts.RisingPearlCard(pearlContent, padding: new Thickness(30, 24, 30, 24), topRadius: 30, bottomRadius: 24);
        pearlCard.Width = 1300;
        Canvas.SetLeft(pearlCard, 262);
        Canvas.SetTop(pearlCard, 626);
        host.Children.Add(pearlCard);

        // 8. Note at bottom (x=272, y=852)
        var noteBlock = PulseParts.Body(pulseMacroNote ?? "Local draft · Nothing is written to the controller.", 13, PulseInk.C("#7f8aa0"));
        Canvas.SetLeft(noteBlock, 272);
        Canvas.SetTop(noteBlock, 792);
        host.Children.Add(noteBlock);
    }

    private string? pulseMacroNote;

    private static FrameworkElement BuildPulseMacroTimeline(MacroWorkbench m, System.Collections.Generic.IReadOnlyList<MacroEvent> events, int sel)
    {
        var canvas = new Canvas { Width = 608, Height = 126 };
        string[] lanes = ["Buttons", "Left stick", "Right stick", "Pause"];

        for (int l = 0; l < lanes.Length; l++)
        {
            double ly = l * 28 + 6;
            var laneName = PulseParts.Mono(lanes[l].ToUpperInvariant(), 10, PulseParts.CyanBright);
            Canvas.SetLeft(laneName, 0);
            Canvas.SetTop(laneName, ly);
            canvas.Children.Add(laneName);

            var trackLine = new Line
            {
                X1 = 80, Y1 = ly + 8, X2 = 600, Y2 = ly + 8,
                Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.12)),
                StrokeThickness = 1
            };
            canvas.Children.Add(trackLine);
        }

        // Draw event blocks on the lane
        for (int i = 0; i < events.Count && i < 6; i++)
        {
            var ev = events[i];
            double bx = 90 + i * 85;
            bool isFocused = i == sel;

            var block = new Border
            {
                Width = 75,
                Height = 20,
                CornerRadius = new(4),
                Background = isFocused ? PulseParts.PearlWhiteBrush : PulseInk.B(PulseInk.A(PulseInk.C("#5cb3ff"), 0.25)),
                BorderBrush = PulseInk.B(PulseInk.A(Colors.White, isFocused ? 0.9 : 0.4)),
                BorderThickness = new(1),
                Child = new TextBlock
                {
                    Text = ev.Buttons.Count > 0 ? ev.Buttons[0] : "A",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = isFocused ? PulseInk.B(PulseInk.C("#0b162c")) : Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(block, bx);
            Canvas.SetTop(block, 4);
            canvas.Children.Add(block);
        }

        return canvas;
    }
}

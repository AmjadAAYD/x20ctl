using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;
using X20Ctl.Zone.Mouse;
using MouseAct = X20Ctl.Zone.Mouse.MouseAction;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private MouseWorkbench? pulseMouseBench;

    private void BuildPulseMouse(Canvas host)
    {
        var m = Mouse;
        if (m == null)
        {
            var na = PulseParts.Body("Controller as mouse isn't available for this session.", 18, PulseParts.TextSoft);
            Canvas.SetLeft(na, 280);
            Canvas.SetTop(na, 200);
            host.Children.Add(na);
            return;
        }

        if (!ReferenceEquals(m, pulseMouseBench))
        {
            pulseMouseBench = m;
            m.Watch(() =>
            {
                if (CurrentPage == "Mouse" && ReferenceEquals(pulseMouseBench, Mouse))
                    PulseLater();
            });
        }

        // 1. Frame Header per t_pulse.py:735
        var title = PulseParts.Disp("Controller as mouse", 40, PulseParts.TextPrimary, FontWeights.ExtraLight);
        Canvas.SetLeft(title, 272);
        Canvas.SetTop(title, 30);
        host.Children.Add(title);

        var lead = PulseParts.Body($"Player {Player + 1} · {Model.Name}", 15, PulseParts.TextSoft);
        Canvas.SetLeft(lead, 272);
        Canvas.SetTop(lead, 80);
        host.Children.Add(lead);

        var status = PulseParts.Body(PulseStatusString(), 14, PulseParts.TextSoft);
        Canvas.SetRight(status, 44);
        Canvas.SetTop(status, 36);
        host.Children.Add(status);

        // 2. Top Rows at (x=262, y=140, w=640) per t_pulse.py:736
        var topStack = new StackPanel { Width = 640 };

        var swRow = PulseParts.GlassRow(
            PulseParts.Disp("Controller as mouse", 15, PulseParts.TextPrimary, FontWeights.SemiBold),
            PulseParts.VisualToggle(m.IsOn, () => { m.FlipSwitch(); PulseLater(); }),
            active: m.IsOn,
            compact: true,
            onClick: () => { m.FlipSwitch(); PulseLater(); }
        );
        topStack.Children.Add(swRow);

        var srcRow = PulseParts.GlassRow(
            PulseParts.Disp("Source stick", 15, PulseParts.TextPrimary, FontWeights.SemiBold),
            PulseParts.ChipButton("Left stick ▾", active: false, () => { }),
            compact: true
        );
        srcRow.Margin = new(0, 8, 0, 0);
        topStack.Children.Add(srcRow);

        Canvas.SetLeft(topStack, 262);
        Canvas.SetTop(topStack, 140);
        host.Children.Add(topStack);

        // Header label above mapping ring at (272, 286)
        var mapHead = PulseParts.Kicker("BUTTON MAPPINGS · 12 CONTROLS");
        Canvas.SetLeft(mapHead, 272);
        Canvas.SetTop(mapHead, 286);
        host.Children.Add(mapHead);

        // 2. Controller Orbit at (cx=600, cy=548, w=380, rx=330, ry=196) per t_pulse.py:734
        double cx = 600, cy = 548, wPad = 380;
        double rx = 330, ry = 196;

        // Aura and elliptical rail
        host.Children.Add(PulseKit.Aura(cx, cy, 260, 160, 0.18));
        var rail = new Ellipse
        {
            Width = rx * 2,
            Height = ry * 2,
            Stroke = PulseInk.B(PulseInk.A(PulseParts.CyanBright, 0.20)),
            StrokeThickness = 1.2
        };
        Canvas.SetLeft(rail, cx - rx);
        Canvas.SetTop(rail, cy - ry);
        host.Children.Add(rail);

        // Pedestal Controller
        var pedestal = PulseParts.ControllerPedestal(Model.Id, rear: false, width: wPad, height: wPad / 1.5, showRipples: false, finish: PadColours.For(Player, Model.Id));
        Canvas.SetLeft(pedestal, cx - wPad / 2);
        Canvas.SetTop(pedestal, cy - (wPad / 1.5) / 2);
        host.Children.Add(pedestal);

        // 12 Mappings from models.py / t_pulse.py:740
        var items = new[]
        {
            ("A", "Left click", MouseAct.LeftClick),
            ("B", "Right click", MouseAct.RightClick),
            ("X", "Middle click", MouseAct.MiddleClick),
            ("Y", "Shortcut reserved", MouseAct.None),
            ("LB", "Back", MouseAct.PageUp),
            ("RB", "Forward", MouseAct.PageDown),
            ("LT", "Precision mode", MouseAct.Precision),
            ("RT", "Left click", MouseAct.LeftClick),
            ("L3", "Enter", MouseAct.None),
            ("R3", "Task View", MouseAct.None),
            ("Menu", "Start", MouseAct.None),
            ("Home", "Disabled", MouseAct.None)
        };

        string sel = m.SelectedKey;
        for (int i = 0; i < items.Length; i++)
        {
            var (k, val, act) = items[i];
            double th = -90.0 + i * (360.0 / items.Length);
            double rad = th * Math.PI / 180.0;
            double bx = cx + rx * Math.Cos(rad);
            double by = cy + ry * Math.Sin(rad);

            bool on = string.Equals(sel, k, StringComparison.OrdinalIgnoreCase) || (string.IsNullOrEmpty(sel) && i == 0);
            double d = on ? 46 : 38;

            if (on)
            {
                var rres = PulseParts.ResonanceMark(30);
                Canvas.SetLeft(rres, bx - 30);
                Canvas.SetTop(rres, by - 30);
                host.Children.Add(rres);
            }

            var orb = new Border
            {
                Width = d,
                Height = d,
                CornerRadius = new(d / 2),
                Background = on ? PulseParts.PearlWhiteBrush : PulseInk.B(PulseInk.A(PulseParts.DeepColor, 0.85)),
                BorderBrush = PulseInk.B(on ? Colors.White : PulseInk.A(Colors.White, 0.22)),
                BorderThickness = new(on ? 2 : 1),
                Effect = on ? PulseInk.Glow(PulseParts.CyanBright, 16, 0.85) : null,
                Cursor = Cursors.Hand,
                Child = PulseParts.Mono(k, k.Length > 2 ? 11 : 14, on ? PulseParts.PearlInk : PulseParts.TextPrimary, FontWeights.SemiBold).Also(t =>
                {
                    t.HorizontalAlignment = HorizontalAlignment.Center;
                    t.VerticalAlignment = VerticalAlignment.Center;
                })
            };

            string pickKey = k;
            orb.MouseLeftButtonDown += (_, e) =>
            {
                m.PickKey(pickKey);
                PulseLater();
                e.Handled = true;
            };

            Canvas.SetLeft(orb, bx - d / 2);
            Canvas.SetTop(orb, by - d / 2);
            host.Children.Add(orb);

            // Side label per t_pulse.py:751
            bool right = Math.Cos(rad) >= -0.05;
            double lx = bx + (right ? 34 : -34 - 150);

            var actLabel = new TextBlock
            {
                Width = 150,
                Text = val,
                FontFamily = PulseParts.BodyFont,
                FontSize = 13.5,
                FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = on ? Brushes.White : PulseInk.B(PulseParts.TextSoft),
                TextAlignment = right ? TextAlignment.Left : TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(actLabel, lx);
            Canvas.SetTop(actLabel, by - 9);
            host.Children.Add(actLabel);
        }

        // 3. Right Pearl Card at (1040, 150, w=520, h=680) per t_pulse.py:761
        var pearlContent = new StackPanel();

        pearlContent.Children.Add(PulseParts.Disp($"Assign · {sel}", 22, PulseParts.PearlInk, FontWeights.SemiBold));
        pearlContent.Children.Add(PulseParts.Body("Pick an action to send when this control is pressed.", 14, PulseParts.PearlMid).Also(t => t.Margin = new(0, 4, 0, 14)));

        // Category options
        var cats = new[]
        {
            ("MOUSE BUTTONS", new[] { (MouseAct.LeftClick, "Left click"), (MouseAct.RightClick, "Right click"), (MouseAct.MiddleClick, "Middle click") }),
            ("NAVIGATION", new[] { (MouseAct.PageUp, "Back"), (MouseAct.PageDown, "Forward") }),
            ("SYSTEM SHORTCUTS", new[] { (MouseAct.Precision, "Precision mode"), (MouseAct.None, "Task View"), (MouseAct.None, "Disabled") })
        };

        foreach (var (catName, optList) in cats)
        {
            pearlContent.Children.Add(PulseParts.Kicker(catName, onPearl: true).Also(k => k.Margin = new(4, 10, 0, 6)));
            foreach (var (actType, actText) in optList)
            {
                bool isSelected = m.CurrentAction == actType && actType != MouseAct.None;
                var rowBorder = new Border
                {
                    Height = 38,
                    CornerRadius = new(10),
                    Padding = new(14, 0, 14, 0),
                    Margin = new(0, 0, 0, 4),
                    Background = isSelected ? PulseInk.B(PulseInk.A(PulseParts.CyanBright, 0.22)) : PulseInk.B(PulseInk.A(Colors.White, 0.55)),
                    BorderBrush = PulseInk.B(isSelected ? PulseParts.CyanColor : PulseInk.A(PulseParts.PearlMid, 0.18)),
                    BorderThickness = new(1),
                    Cursor = Cursors.Hand
                };

                var rowStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (isSelected)
                {
                    var dot = new Ellipse { Width = 6, Height = 6, Fill = PulseInk.B(PulseParts.CyanBright), Margin = new(0, 0, 10, 0) };
                    rowStack.Children.Add(dot);
                }
                rowStack.Children.Add(PulseParts.Body(actText, 14.5, PulseParts.PearlInk, isSelected ? FontWeights.SemiBold : FontWeights.Normal));
                rowBorder.Child = rowStack;

                var assignAct = actType;
                rowBorder.MouseLeftButtonDown += (_, e) =>
                {
                    m.Assign(assignAct);
                    PulseLater();
                    e.Handled = true;
                };

                pearlContent.Children.Add(rowBorder);
            }
        }

        pearlContent.Children.Add(PulseParts.Body("Active bindings take effect immediately when controller is in PC mode.", 13, PulseParts.PearlSoft).Also(t => t.Margin = new(0, 14, 0, 0)));

        var pearlCard = PulseParts.RisingPearlCard(pearlContent, new Thickness(30, 26, 30, 26));
        pearlCard.Width = 520;
        pearlCard.Height = 680;
        Canvas.SetLeft(pearlCard, 1040);
        Canvas.SetTop(pearlCard, 150);
        host.Children.Add(pearlCard);

        // Bottom note at (272, 852)
        var bottomNote = PulseParts.Body("Controller as mouse · Toggle with Home + Start shortcut at any time.", 13.5, PulseParts.TextSoft);
        Canvas.SetLeft(bottomNote, 272);
        Canvas.SetTop(bottomNote, 852);
        host.Children.Add(bottomNote);
    }
}

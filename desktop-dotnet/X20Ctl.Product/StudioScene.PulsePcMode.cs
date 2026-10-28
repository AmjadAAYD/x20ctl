using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    // ========================================================================
    // 8b-pcmode: PC Mode Routing (matches t_pulse.py:653 pcmode() and pcmode.png)
    // Left (x=262, y=180, w=610): Signal routing pipeline rows
    // Right (x=912, y=180, w=650): Rising pearl card with driver status, instructions, test action
    // ========================================================================
    private void BuildPulsePcMode(Canvas host)
    {
        var board = PulsePcModeBuilder.BuildBoard(
            PcMode,
            () => ShowButtons(),
            () => PulseSelectPage("Dashboard"),
            () => PulseLater());

        host.Children.Add(board);
    }
}

internal static class PulsePcModeBuilder
{
    public static Canvas BuildBoard(PcModeLink? link, Action onEditButtons, Action onBack, Action onRefresh)
    {
        var host = new Canvas { Width = 1600, Height = 900 };
        bool isEnabled = link?.Service.Enabled == true;

        // 1. Frame Title & Lead (x=270, y=30)
        var titleBlock = PulseParts.Disp("PC Mode", 44, Colors.White, FontWeights.Light);
        titleBlock.Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.45 };
        Canvas.SetLeft(titleBlock, 270);
        Canvas.SetTop(titleBlock, 30);
        host.Children.Add(titleBlock);

        var leadBlock = PulseParts.Body("Route gameplay input through your saved PC profile to a virtual Xbox controller. Nothing is written to hardware.", 14.5, PulseInk.C("#aab4c4"));
        leadBlock.Width = 620;
        Canvas.SetLeft(leadBlock, 272);
        Canvas.SetTop(leadBlock, 92);
        host.Children.Add(leadBlock);

        // Subtitle tag at (272, 126)
        var subStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        subStack.Children.Add(PulseParts.Chip("Virtual 360", "on"));
        subStack.Children.Add(PulseParts.Body("Virtual Xbox 360 controller via ViGEmBus", 14, PulseInk.C("#aab4c4")).Also(t => t.Margin = new(14, 0, 0, 0)));
        Canvas.SetLeft(subStack, 272);
        Canvas.SetTop(subStack, 126);
        host.Children.Add(subStack);

        // 2. Left Side: Signal Pipeline Glass Rows (x=262, y=180, w=610)
        var leftStage = new StackPanel { Width = 610 };
        Canvas.SetLeft(leftStage, 262);
        Canvas.SetTop(leftStage, 180);

        // Row 1: Master switch
        var switchCtrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        switchCtrl.Children.Add(PulseParts.Chip(isEnabled ? "Active" : "Off", isEnabled ? "ok" : "default").Also(c => c.Margin = new(0, 0, 12, 0)));
        switchCtrl.Children.Add(PulseParts.VisualToggle(isEnabled, () =>
        {
            if (link != null)
            {
                link.Service.Enabled = !link.Service.Enabled;
                try { link.Preferences?.SetPcMode(link.Service.Enabled); } catch { }
                onRefresh();
            }
        }));
        leftStage.Children.Add(PulseParts.GlassRow(
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { PulseParts.Disp("PC Mode Master Switch", 15.5, Colors.White, FontWeights.SemiBold), PulseParts.Body("Direct virtual Xbox 360 output to games", 12.5, PulseInk.C("#7f8aa0")) } },
            switchCtrl,
            active: isEnabled,
            compact: true));

        // Row 2: Native USB input
        var r2Ctrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        r2Ctrl.Children.Add(PulseParts.Chip("1000 Hz", "ok"));
        leftStage.Children.Add(PulseParts.GlassRow(
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { PulseParts.Disp("1 · Native X20 USB Input", 15, Colors.White, FontWeights.SemiBold), PulseParts.Body("Hardware input stream captured over USB or 2.4 GHz", 12.5, PulseInk.C("#7f8aa0")) } },
            r2Ctrl,
            active: false,
            compact: true).Also(r => r.Margin = new(0, 8, 0, 0)));

        // Row 3: Remap & Curve Engine
        var r3Ctrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        r3Ctrl.Children.Add(PulseParts.PillButton("Edit profile", primary: false, small: true, onClick: onEditButtons));
        leftStage.Children.Add(PulseParts.GlassRow(
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { PulseParts.Disp("2 · Remap & Curve Engine", 15, Colors.White, FontWeights.SemiBold), PulseParts.Body("Real-time button remapping, curves, and macros", 12.5, PulseInk.C("#7f8aa0")) } },
            r3Ctrl,
            active: false,
            compact: true).Also(r => r.Margin = new(0, 8, 0, 0)));

        // Row 4: Virtual Xbox 360 output
        var r4Ctrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        r4Ctrl.Children.Add(PulseParts.Chip("ViGEmBus", "ok"));
        leftStage.Children.Add(PulseParts.GlassRow(
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { PulseParts.Disp("3 · Virtual Xbox 360 Output", 15, Colors.White, FontWeights.SemiBold), PulseParts.Body("Feed sent directly to Windows games without driver delay", 12.5, PulseInk.C("#7f8aa0")) } },
            r4Ctrl,
            active: false,
            compact: true).Also(r => r.Margin = new(0, 8, 0, 0)));

        host.Children.Add(leftStage);

        // 3. Right Side: Resonance Arc & Rising Pearl Card (x=912, y=180, w=650)
        var resMark = PulseParts.ResonanceMark();
        Canvas.SetLeft(resMark, 1237 - 7);
        Canvas.SetTop(resMark, 360 - 7);
        host.Children.Add(resMark);

        var pearlContent = new StackPanel { Margin = new(10, 6, 10, 6) };

        // Header: Driver Status · Active
        var statusHead = new DockPanel { Margin = new(0, 0, 0, 8) };
        var actBadge = PulseParts.Chip("Active", "on", onPearl: true);
        DockPanel.SetDock(actBadge, Dock.Right);
        statusHead.Children.Add(actBadge);
        statusHead.Children.Add(PulseParts.Disp("Driver status · Active", 22, PulseParts.PearlInk, FontWeights.SemiBold));
        pearlContent.Children.Add(statusHead);

        pearlContent.Children.Add(PulseParts.Body("Virtual Xbox 360 controller via ViGEmBus. Direct output to DirectX, Steam, and native PC games.", 14.5, PulseParts.PearlSoft, wrap: true));

        // Numbered Instructions
        var instructions = new[]
        {
            "Plug in your controller via USB cable or 2.4 GHz wireless adapter.",
            "Launch your game — it automatically detects the virtual Xbox 360 controller.",
            "Disable Steam Input in game properties if you experience double-input conflicts."
        };

        for (int i = 0; i < instructions.Length; i++)
        {
            var stepRow = new DockPanel { Margin = new(0, 14, 0, 0) };
            var numBadge = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new(14),
                Background = PulseInk.B(PulseInk.A(PulseParts.PearlInk, 0.08)),
                BorderBrush = PulseInk.B(PulseInk.A(PulseParts.PearlInk, 0.20)),
                BorderThickness = new(1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new(0, 0, 14, 0),
                Child = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    FontFamily = PulseParts.BodyFont,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = PulseInk.B(PulseParts.PearlInk),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            DockPanel.SetDock(numBadge, Dock.Left);
            stepRow.Children.Add(numBadge);

            var stepText = PulseParts.Body(instructions[i], 14, PulseParts.PearlInk, wrap: true);
            stepText.VerticalAlignment = VerticalAlignment.Center;
            stepRow.Children.Add(stepText);

            pearlContent.Children.Add(stepRow);
        }

        // Action button: Test Virtual Controller
        var actionStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 24, 0, 0) };
        actionStack.Children.Add(PulseParts.PillButton("Test Virtual Controller", primary: true, onPearl: true, onClick: onEditButtons));
        actionStack.Children.Add(PulseParts.PillButton("Button Remap", primary: false, onPearl: true, onClick: onEditButtons).Also(b => b.Margin = new(12, 0, 0, 0)));
        pearlContent.Children.Add(actionStack);

        var pearlCard = PulseParts.RisingPearlCard(pearlContent, padding: new Thickness(32, 28, 32, 28), topRadius: 30, bottomRadius: 26);
        pearlCard.Width = 650;
        Canvas.SetLeft(pearlCard, 912);
        Canvas.SetTop(pearlCard, 180);
        host.Children.Add(pearlCard);

        // 4. Bottom Note (x=272, y=852)
        var noteBlock = PulseParts.Body("PC mode · Zero changes written to controller hardware memory", 13, PulseInk.C("#7f8aa0"));
        Canvas.SetLeft(noteBlock, 272);
        Canvas.SetTop(noteBlock, 852);
        host.Children.Add(noteBlock);

        return host;
    }
}

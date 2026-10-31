using System;
using System.Linq;
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
    // 8-device: Controller Management & Overview (matches t_pulse.py:678 device() and device.png)
    // Left (cx=570, cy=330, w=520): Controller on pedestal of light, model name, finish, status tag
    // Right (x=920, y=150, w=630): Glass specification rows down the right
    // Bottom (x=262, y=700, w=640): Rising pearl card for firmware update & diagnostics
    // ========================================================================
    private void BuildPulseDevice(Canvas host)
    {
        Device ??= new DeviceWorkbench(
            Model,
            Player,
            this,
            RegisterFocus,
            () => PulseSelectPage("Diagnostics"),
            () => PulseSelectPage("Setups"));

        string finishName = PadColours.Catalog.For(Model.Id).FirstOrDefault(f => f.Id == PadColours.For(Player, Model.Id))?.Name ?? "Glacier White";

        // 1. Frame Title & Lead (x=270, y=30)
        var titleBlock = PulseParts.Disp("Device", 44, Colors.White, FontWeights.Light);
        titleBlock.Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.45 };
        Canvas.SetLeft(titleBlock, 270);
        Canvas.SetTop(titleBlock, 30);
        host.Children.Add(titleBlock);

        var leadBlock = PulseParts.Body("Controller identity, transport protocol, onboard firmware, and hardware diagnostics.", 14.5, PulseInk.C("#aab4c4"));
        leadBlock.Width = 640;
        Canvas.SetLeft(leadBlock, 272);
        Canvas.SetTop(leadBlock, 92);
        host.Children.Add(leadBlock);

        // 2. Left Side: Controller on Light Pedestal (cx=570, cy=330, w=520)
        double cx = 570, cy = 330, cw = 520, ch = 320;

        // Pedestal ellipses of light
        var pedestalOuter = new Ellipse
        {
            Width = 420,
            Height = 32,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), 0.35)),
            StrokeThickness = 1.4
        };
        Canvas.SetLeft(pedestalOuter, cx - 210);
        Canvas.SetTop(pedestalOuter, cy + 184);
        host.Children.Add(pedestalOuter);

        var pedestalInner = new Ellipse
        {
            Width = 300,
            Height = 20,
            Fill = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), 0.12)),
            Effect = new BlurEffect { Radius = 8 }
        };
        Canvas.SetLeft(pedestalInner, cx - 150);
        Canvas.SetTop(pedestalInner, cy + 190);
        host.Children.Add(pedestalInner);

        // Controller illustration
        var ctlFinish = PadColours.For(Player, Model.Id);
        var pedestal = PulseParts.ControllerPedestal(Model.Id, false, cw, ch, showRipples: false, finish: ctlFinish);
        Canvas.SetLeft(pedestal, cx - cw / 2);
        Canvas.SetTop(pedestal, cy - ch / 2);
        host.Children.Add(pedestal);

        // Controller Identity Block under Pedestal (x=cx - 250, y=cy + 232)
        var idStack = new StackPanel { Width = 500 };
        idStack.Children.Add(PulseParts.Kicker("CONNECTED CONTROLLER").Also(t => t.TextAlignment = TextAlignment.Center));
        idStack.Children.Add(PulseParts.Disp($"{Model.Name} · {finishName}", 24, Colors.White, FontWeights.SemiBold).Also(t =>
        {
            t.TextAlignment = TextAlignment.Center;
            t.Margin = new(0, 6, 0, 8);
        }));

        var tagRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        tagRow.Children.Add(PulseParts.Chip($"Connected · {currentTransport ?? "2.4 GHz"}", "ok"));
        idStack.Children.Add(tagRow);

        Canvas.SetLeft(idStack, cx - 250);
        Canvas.SetTop(idStack, cy + 228);
        host.Children.Add(idStack);

        // 3. Right Side: Specification Glass Rows (x=920, y=150, w=630)
        var rightRows = new StackPanel { Width = 630 };
        Canvas.SetLeft(rightRows, 920);
        Canvas.SetTop(rightRows, 150);

        // Row 1: Connection Protocol
        rightRows.Children.Add(PulseParts.GlassRow(
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { PulseParts.Disp("Connection protocol", 15, Colors.White, FontWeights.SemiBold), PulseParts.Body("Active gameplay transport pipeline", 12.5, PulseInk.C("#7f8aa0")) } },
            PulseParts.Chip(currentTransport ?? "2.4 GHz receiver", "ok"),
            active: true,
            compact: true));

        // Row 2: Firmware Version
        rightRows.Children.Add(PulseParts.GlassRow(
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { PulseParts.Disp("Firmware revision", 15, Colors.White, FontWeights.SemiBold), PulseParts.Body("Onboard microcontroller firmware", 12.5, PulseInk.C("#7f8aa0")) } },
            PulseParts.Mono(currentFirmware ?? "Not reported", 13.5, PulseParts.CyanBright, FontWeights.Bold),
            active: false,
            compact: true).Also(r => r.Margin = new(0, 8, 0, 0)));

        // Row 3: Sensor Hardware
        rightRows.Children.Add(PulseParts.GlassRow(
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { PulseParts.Disp("Stick sensors", 15, Colors.White, FontWeights.SemiBold), PulseParts.Body("Electromagnetic contactless Hall Effect sensors", 12.5, PulseInk.C("#7f8aa0")) } },
            PulseParts.Chip("Hall Effect · Anti-drift", "on"),
            active: false,
            compact: true).Also(r => r.Margin = new(0, 8, 0, 0)));

        // Row 4: Battery & Power
        rightRows.Children.Add(PulseParts.GlassRow(
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { PulseParts.Disp("Battery & charge status", 15, Colors.White, FontWeights.SemiBold), PulseParts.Body("Lithium-ion cell power telemetry", 12.5, PulseInk.C("#7f8aa0")) } },
            PulseParts.Mono(currentBatteryLevel is { Length: > 0 } bl ? bl : "Not reported", 14, Colors.White, FontWeights.SemiBold),
            active: false,
            compact: true).Also(r => r.Margin = new(0, 8, 0, 0)));

        // Row 5: Native Engine Core
        rightRows.Children.Add(PulseParts.GlassRow(
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { PulseParts.Disp("Native engine core", 15, Colors.White, FontWeights.SemiBold), PulseParts.Body(NativeEngineStatus, 12.5, PulseInk.C("#7f8aa0")) } },
            PulseParts.Chip("C++ Driver Core", "ok"),
            active: false,
            compact: true).Also(r => r.Margin = new(0, 8, 0, 0)));

        host.Children.Add(rightRows);

        // 4. Bottom-Left: Rising Pearl Card for Firmware Update (x=262, y=700, w=640)
        var pearlContent = new DockPanel { Width = 580 };
        var updateBtn = PulseParts.PillButton("Controller check", primary: true, onPearl: true, onClick: () => PulseSelectPage("Writescan"));
        DockPanel.SetDock(updateBtn, Dock.Right);
        pearlContent.Children.Add(updateBtn);

        var updateTitles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 20, 0) };
        var headLine = new StackPanel { Orientation = Orientation.Horizontal };
        headLine.Children.Add(PulseParts.Disp("Firmware", 20, PulseParts.PearlInk, FontWeights.SemiBold));
        headLine.Children.Add(PulseParts.Mono("No update source", 13.5, PulseInk.C("#2b6fff"), FontWeights.Bold).Also(t => t.Margin = new(8, 4, 0, 0)));
        updateTitles.Children.Add(headLine);
        updateTitles.Children.Add(PulseParts.Body("X20CTL never changes controller firmware.", 13.5, PulseParts.PearlSoft).Also(t => t.Margin = new(0, 2, 0, 0)));
        pearlContent.Children.Add(updateTitles);

        var pearlCard = PulseParts.RisingPearlCard(pearlContent, padding: new Thickness(26, 20, 26, 20), topRadius: 26, bottomRadius: 22);
        pearlCard.Width = 640;
        Canvas.SetLeft(pearlCard, 262);
        Canvas.SetTop(pearlCard, 700);
        host.Children.Add(pearlCard);

        // 5. Bottom Note (x=940, y=852)
        var noteBlock = PulseParts.Body("Hardware features detected over native USB protocol", 13, PulseInk.C("#7f8aa0"));
        Canvas.SetLeft(noteBlock, 940);
        Canvas.SetTop(noteBlock, 852);
        host.Children.Add(noteBlock);
    }
}

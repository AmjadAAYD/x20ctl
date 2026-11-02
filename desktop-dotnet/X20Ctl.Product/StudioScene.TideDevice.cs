using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowDevice()
    {
        ConsoleSurface.Children.Clear();
        var frame = new TideToolFrame(
            "DEVICE",
            "Model selected manually. Connect the controller to check its device details.",
            onBack: () => TideShowDashboard(),
            hints: new[] { "\u24B7 Back", "\u24B6 Open", "LB/RB Tabs" },
            seed: 8);

        // Left List: Staircase rows
        var listStack = new StackPanel { Width = 640 };
        listStack.Children.Add(TideKit.SectionHeader("Device"));
        listStack.Children.Add(TideKit.PersonaRow("Connection", value: "Not detected", focused: true));
        listStack.Children.Add(TideKit.PersonaRow("Battery", value: "Not reported"));
        listStack.Children.Add(TideKit.PersonaRow("Firmware", value: "Not reported"));
        listStack.Children.Add(TideKit.PersonaRow("Hardware ID", value: "Not reported"));
        listStack.Children.Add(TideKit.PersonaRow("Transport", value: "Not reported"));

        listStack.Children.Add(TideKit.SectionHeader("Model capabilities"));
        listStack.Children.Add(TideKit.PersonaRow("Sticks", value: "Hall"));
        listStack.Children.Add(TideKit.PersonaRow("Triggers", value: "Hall"));
        listStack.Children.Add(TideKit.PersonaRow("Rear controls", value: "M1 · M2"));
        listStack.Children.Add(TideKit.PersonaRow("Motors", value: "2 grip motors"));
        listStack.Children.Add(TideKit.PersonaRow("Motion", value: "Unconfirmed"));

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 176);

        // Right side: Visual (Floating Character pad) + Detail Card
        var padVisual = TideKit.ControllerArt(Model.Id, 420);

        var detailCard = TideKit.DetailCard(
            title: "Connection",
            value: "Not detected",
            body: "Controller input and configuration connection are checked separately.",
            foot: "Layout and artwork available. Native configuration is unavailable for this model.",
            buttons: new[] { "Open Diagnostics" },
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(padVisual, detailCard, 580);
        frame.SetSideStack(sideStack, 176);
        frame.SetNote("Assigned model · Gameplay and configuration remain separate");

        ConsoleSurface.Children.Add(frame);
    }
}

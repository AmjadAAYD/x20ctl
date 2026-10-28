using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowMouse()
    {
        ConsoleSurface.Children.Clear();
        var frame = new TideToolFrame(
            "MOUSE",
            "Player 1 · EasySMX X15",
            onBack: () => TideShowDashboard(),
            hints: new[] { "Esc Back" },
            seed: 14);

        // Left List: Staircase rows
        var listStack = new StackPanel { Width = 640 };
        listStack.Children.Add(TideKit.PersonaRow("Controller as mouse", value: "Off", ctl: "toggle"));
        listStack.Children.Add(TideKit.PersonaRow("Desktop input source", ctl: "dd:Choose input source"));

        listStack.Children.Add(TideKit.SectionHeader("Button Mappings · 14 / 17 configured"));
        listStack.Children.Add(TideKit.PersonaRow("A", value: "Left click", focused: true));
        listStack.Children.Add(TideKit.PersonaRow("B", value: "Right click"));
        listStack.Children.Add(TideKit.PersonaRow("X", value: "Middle click"));
        listStack.Children.Add(TideKit.PersonaRow("Y", value: "Shortcut reserved"));
        listStack.Children.Add(TideKit.PersonaRow("LB", value: "Back"));
        listStack.Children.Add(TideKit.PersonaRow("RB", value: "Forward"));
        listStack.Children.Add(TideKit.PersonaRow("LT", value: "Precision mode"));
        listStack.Children.Add(TideKit.PersonaRow("RT", value: "Left click"));
        listStack.Children.Add(TideKit.PersonaRow("L3", value: "Enter"));
        listStack.Children.Add(TideKit.PersonaRow("R3", value: "Task View"));
        listStack.Children.Add(TideKit.PersonaRow("Menu", value: "Start"));
        listStack.Children.Add(TideKit.PersonaRow("Home", value: "Disabled"));

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 176);

        // Right side: Detail Card only (visual is None)
        var detailCard = TideKit.DetailCard(
            title: "Assign Input · A",
            sub: "Choose its desktop action",
            options: new (string, string[])[]
            {
                ("Disabled", Array.Empty<string>()),
                ("Mouse", new[] { "Left click", "Right click", "Middle click", "Back", "Forward" }),
                ("Precision", Array.Empty<string>()),
                ("Keyboard", Array.Empty<string>()),
                ("Windows", Array.Empty<string>()),
                ("Media", Array.Empty<string>())
            },
            selectedOption: "Left click",
            body: "One player controls the desktop at a time. PC mode output pauses while Mouse Mode is on; physical game input can still reach games unless it is hidden by your PC mode setup.",
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(null, detailCard, 580);
        frame.SetSideStack(sideStack, 176);
        frame.SetNote("Controller as mouse · Saved on this PC");

        ConsoleSurface.Children.Add(frame);
    }
}

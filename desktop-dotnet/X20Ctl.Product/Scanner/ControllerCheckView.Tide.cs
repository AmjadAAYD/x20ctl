using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product.Scanner;

public sealed partial class ControllerCheckView
{
    private void BuildTideReadWarning(Panel cards, FrameworkElement systemRow, FrameworkElement choices, FrameworkElement linux)
    {
        var frame = new TideToolFrame(
            "BEFORE YOU SCAN",
            "This is a full controller scan; you'll be guided through every button, stick and trigger, one step at a time, while X20CTL records exactly what the controller reports. Here is what it does and what to expect.",
            onBack: () => close(),
            hints: new[] { "\u24B7 Back", "\u24B6 Select" },
            seed: 21);

        // Left List: Staircase rows
        var listStack = new StackPanel { Width = 640 };
        listStack.Children.Add(TideKit.PersonaRow("Which system is the controller connected to?", ctl: "dd:This Windows PC"));

        listStack.Children.Add(TideKit.SectionHeader("What the scan does"));
        listStack.Children.Add(TideKit.PersonaRow("Reads, never writes", ctl: "chev", focused: true));
        listStack.Children.Add(TideKit.PersonaRow("Buttons, sticks and triggers", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Battery", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Stick resolution and report rate", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Lighting (RGB)", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Macro paddles (M buttons)", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Home, M and turbo", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Every step tells you what to do", ctl: "chev"));

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 176);

        // Right side: Visual (Floating Character pad) + Detail Card
        var padVisual = TideKit.CharacterArt("x15-starfield", 420, -8);

        var detailCard = TideKit.DetailCard(
            title: "Reads, never writes",
            body: "The scan only listens to what the controller already tells Windows. No settings, lighting, vibration or firmware are touched.",
            foot: "Your report: the full ZIP is always saved to Documents\\X20CTLInputReports. \"Scan and send\" also sends a compact summary (controller names, IDs and results, no personal files) to X20CTLADMIN, the X20CTL team's report site, when the scan ends. You can send the ZIP to @mistermajid on Discord too.",
            buttons: null,
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(padVisual, detailCard, 580);
        frame.SetSideStack(sideStack, 176);
        frame.SetNote("Read-only · nothing is changed or uploaded");

        warning.Child = new Viewbox { Stretch = Stretch.Uniform, Child = frame };
    }
}

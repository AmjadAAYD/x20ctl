using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product.Scanner;

public sealed partial class WriteScanView
{
    private void BuildTideWriteWarning(Panel cards, Button go, Button leave)
    {
        var frame = new TideToolFrame(
            "WRITE SCAN",
            "You'll change one setting in KeyLinker while your phone records its Bluetooth traffic. X20CTL then shows which bytes the app sent for that change. One recording teaches one setting.",
            onBack: () => leave.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)),
            hints: new[] { "\u24B7 Back", "\u24B6 Start" },
            seed: 22);

        // Left List: Staircase rows
        var listStack = new StackPanel { Width = 640 };
        listStack.Children.Add(TideKit.SectionHeader("Before you start the Write scan"));
        listStack.Children.Add(TideKit.PersonaRow("This can take a long time", ctl: "chev", focused: true));
        listStack.Children.Add(TideKit.PersonaRow("You need a phone", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("X20CTL sends nothing", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Read scan or Write scan?", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("I have the time, start", ctl: "btn:I have the time, start"));

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
            title: "This can take a long time",
            body: "Plan on 20 to 40 minutes the first time: turning on a hidden phone setting, one small test, then getting a file from the phone to this PC. A bug report alone can take a few minutes to make.",
            foot: "Nothing leaves this PC until you press Send online at the end, and you see what goes first. The findings are saved to Documents\\X20CTLWriteScans only when you press Save.",
            buttons: new[] { "I have the time, start", "Back to Tools" },
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(null, detailCard, 580);
        frame.SetSideStack(sideStack, 176);
        frame.SetNote("Nothing leaves this PC until you press Send online at the end, and you see what goes first");

        warning.Child = new Viewbox { Stretch = Stretch.Uniform, Child = frame };
    }
}

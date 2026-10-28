using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowPcMode()
    {
        ConsoleSurface.Children.Clear();
        var frame = new TideToolFrame(
            "PC MODE",
            "Route gameplay input through your saved PC profile to a virtual Xbox controller. Nothing is written to hardware.",
            onBack: () => TideShowDashboard(),
            hints: new[] { "\u24B7 Back", "\u24B6 Select", "LB/RB Tabs" },
            seed: 12);

        // Left List: Staircase rows
        var listStack = new StackPanel { Width = 640 };
        listStack.Children.Add(TideKit.PersonaRow("PC mode", ctl: "toggle", sub: "PC mode is off. Your controller goes straight to the game."));
        listStack.Children.Add(TideKit.PersonaRow("Settings for", ctl: "dd:Player 1"));
        listStack.Children.Add(TideKit.PersonaRow("Turbo bindings", value: "No turbo"));
        listStack.Children.Add(TideKit.PersonaRow("Give a game its own settings", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Import / export PC profile", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Reset Player 1 profile", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Before PC mode starts", ctl: "chev", focused: true));

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 176);

        // Right side: Route Card + Detail Card
        var routeStack = new StackPanel { Width = 556, Margin = new Thickness(0, 6, 0, 6) };
        var routeTitle = new TextBlock
        {
            Text = "ROUTE",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.SpikeRedBrush,
            Margin = new Thickness(4, 0, 0, 8)
        };
        routeStack.Children.Add(routeTitle);

        var pipelineRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        string[] steps = ["Physical XInput 1", "PC profile", "Virtual Xbox", "Game"];
        for (int i = 0; i < steps.Length; i++)
        {
            var stepBorder = new Border
            {
                Background = (i == 0) ? TideKit.SpikeRedBrush : TideKit.Sea0Brush,
                Padding = new Thickness(14, 8, 14, 8),
                CornerRadius = new CornerRadius(0)
            };
            stepBorder.Child = new TextBlock
            {
                Text = steps[i],
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 14,
                Foreground = TideKit.WhiteBrush
            };
            pipelineRow.Children.Add(stepBorder);

            if (i < 3)
            {
                var arrow = new TextBlock
                {
                    Text = " \u2192 ",
                    FontFamily = TideKit.Body,
                    FontWeight = FontWeights.Bold,
                    FontSize = 22,
                    Foreground = TideKit.SubBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 8, 0)
                };
                pipelineRow.Children.Add(arrow);
            }
        }
        routeStack.Children.Add(pipelineRow);

        var visCard = TideKit.VisualCard(routeStack, 580, -1.5);

        var detailCard = TideKit.DetailCard(
            title: "Before PC mode starts",
            numbered: new[]
            {
                "Your settings only work while X20CTL is open. Close it, or if it crashes, and your controller goes back to normal.",
                "Use turbo and macros with care in online games. Some games and anti-cheat systems treat them as cheating and may ban your account. You've been warned: you are responsible for the rules of the games you play, and X20CTL can't be held accountable for a ban.",
                "The first time you switch it on, it installs two small drivers by itself, so Windows asks for permission once."
            },
            buttons: new[] { "I understand" },
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(visCard, detailCard, 580);
        frame.SetSideStack(sideStack, 176);
        frame.SetNote("No active route for this player · Drivers installed · Processing inactive");

        ConsoleSurface.Children.Add(frame);
    }
}

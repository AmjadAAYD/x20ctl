using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowMacros()
    {
        ConsoleSurface.Children.Clear();
        var frame = new TideToolFrame(
            "MACROS",
            "Macros · PC mode · Applies on this PC, to games, for any controller. PC mode can't see a back paddle, so pick the button that plays each slot.",
            onBack: () => TideShowDashboard(),
            hints: new[] { "\u24B7 Back", "\u24B6 Edit", "\u24BD Add event", "\u24BE Preview", "LT/RT Slot", "LB/RB Tabs" },
            seed: 6);

        // Left List: Paddle Tabs + Staircase rows
        var listStack = new StackPanel { Width = 640 };
        var tabsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        tabsRow.Children.Add(TideKit.PersonaButton("M1", primary: true));
        var tab2 = TideKit.PersonaButton("M2", primary: false); tab2.Margin = new Thickness(12, 0, 0, 0); tabsRow.Children.Add(tab2);
        listStack.Children.Add(tabsRow);

        listStack.Children.Add(TideKit.SectionHeader("M1 · Sequence · Events 3 · Capacity 6 / 47 · 180 ms"));
        listStack.Children.Add(TideKit.PersonaRow("01  A", value: "40 ms hold · 20 ms pause"));
        listStack.Children.Add(TideKit.PersonaRow("02  A", value: "40 ms hold · 20 ms pause", focused: true));
        listStack.Children.Add(TideKit.PersonaRow("03  A", value: "40 ms hold · 20 ms pause"));
        listStack.Children.Add(TideKit.PersonaRow("+ Add event", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Record", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Read controller", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Preview timeline", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("In PC mode, played by", ctl: "dd:Not used"));

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 176);

        // Right side: Visual Timeline Card + Detail Card
        var tlCanvas = new Canvas
        {
            Width = 556,
            Height = 150,
            Background = TideKit.WhiteBrush
        };
        string[] lanes = ["Buttons", "D-pad", "Left stick", "Right stick", "Triggers"];
        string[] ticks = ["0 ms", "65 ms", "130 ms", "195 ms"];
        double laneW = 120;
        double tw = 556 - laneW;

        for (int i = 0; i < lanes.Length; i++)
        {
            var dot = new Ellipse { Width = 8, Height = 8, Fill = TideKit.Sea0Brush };
            Canvas.SetLeft(dot, 14);
            Canvas.SetTop(dot, 38 + i * 22);
            tlCanvas.Children.Add(dot);

            var laneText = new TextBlock
            {
                Text = lanes[i],
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = TideKit.SubBrush
            };
            Canvas.SetLeft(laneText, 28);
            Canvas.SetTop(laneText, 34 + i * 22);
            tlCanvas.Children.Add(laneText);
        }

        // Vertical divider
        var vDiv = new Line { X1 = laneW, Y1 = 0, X2 = laneW, Y2 = 150, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 };
        tlCanvas.Children.Add(vDiv);

        // Ticks
        for (int i = 0; i < ticks.Length; i++)
        {
            double x = laneW + 16 + i * (tw - 40) / 3.2;
            var tLine = new Line { X1 = x, Y1 = 0, X2 = x, Y2 = 150, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 };
            tlCanvas.Children.Add(tLine);

            var tickLabel = new TextBlock
            {
                Text = ticks[i],
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = TideKit.SubBrush
            };
            Canvas.SetLeft(tickLabel, x + 6);
            Canvas.SetTop(tickLabel, 6);
            tlCanvas.Children.Add(tickLabel);
        }

        // 3 events on Buttons lane (lane 0, Y=38)
        for (int n = 0; n < 3; n++)
        {
            double x = laneW + 22 + n * (tw - 40) / 3.2;
            double bw = (tw - 40) / 3.2 * 0.62;
            bool on = (n == 1);

            var evBorder = new Border
            {
                Width = bw,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                Background = on ? Brushes.Transparent : TideKit.Sea0Brush,
                BorderBrush = on ? TideKit.Sea0Brush : Brushes.Transparent,
                BorderThickness = on ? new Thickness(2.5) : new Thickness(0)
            };
            Canvas.SetLeft(evBorder, x);
            Canvas.SetTop(evBorder, 42);
            tlCanvas.Children.Add(evBorder);

            var aText = new TextBlock
            {
                Text = "A",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 14,
                Foreground = TideKit.Sea0Brush
            };
            Canvas.SetLeft(aText, x);
            Canvas.SetTop(aText, 22);
            tlCanvas.Children.Add(aText);

            var durPill = new Border
            {
                Background = TideKit.Sea0Brush,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6, 1, 6, 1)
            };
            durPill.Child = new TextBlock
            {
                Text = "20 ms",
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = TideKit.WhiteBrush
            };
            Canvas.SetLeft(durPill, x + bw + 4);
            Canvas.SetTop(durPill, 120);
            tlCanvas.Children.Add(durPill);
        }

        var visCard = TideKit.VisualCard(tlCanvas, 580, -2);

        var detailCard = TideKit.DetailCard(
            title: "Event 02 · what it presses",
            sub: "A · held 40 ms · then 20 ms pause",
            chips: new[] { "Buttons", "Sticks", "Timing", "Actions" },
            face: "A",
            body: "Pick the button that plays this slot (left), or PC mode won't play it.",
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(visCard, detailCard, 580);
        frame.SetSideStack(sideStack, 176);
        frame.SetNote("PC mode · No changes yet: the controller goes straight to games");

        ConsoleSurface.Children.Add(frame);
    }
}

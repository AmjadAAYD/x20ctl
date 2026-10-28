using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NexusShowMacros()
    {
        nexusPageHost!.Children.Clear();

        // 1. Background gradient
        var bg = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0.5, 0), EndPoint = new(0.5, 1),
                GradientStops = { new(NexusKit.C("#1a1c20"), 0.0), new(NexusKit.House, 1.0) }
            }
        };
        nexusPageHost.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: Player,
            model: Model.Name,
            status: "Offline",
            onZone: () => BackRequested?.Invoke(),
            onSetups: () => ShowManagement("Setups"),
            onPcMode: () => ShowManagement("PcMode"),
            onTools: () => ToolsRequested?.Invoke()
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusPageHost.Children.Add(topBar);

        // 3. Title & Lead
        var title = new TextBlock
        {
            Text = "Macros",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 138);
        nexusPageHost.Children.Add(title);

        var lead = new TextBlock
        {
            Text = "Record and time button sequences on rear paddles · 6-paddle layout available on X20 Pro",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        nexusPageHost.Children.Add(lead);

        // Paddle Tabs: M1, M2, M3, M4
        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        string[] paddles = { "M1", "M2", "M3", "M4" };
        for (int i = 0; i < paddles.Length; i++)
        {
            string p = paddles[i];
            bool on = (i == 0);
            var tabBtn = new Border
            {
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 0, 10, 0),
                CornerRadius = new CornerRadius(4),
                Background = on ? NexusKit.WhiteBrush : NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = p,
                    FontFamily = NexusKit.Body,
                    FontSize = 16,
                    FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = on ? NexusKit.B("#111111") : NexusKit.WhiteBrush
                }
            };
            tabsStack.Children.Add(tabBtn);
        }
        Canvas.SetLeft(tabsStack, 68);
        Canvas.SetTop(tabsStack, 230);
        nexusPageHost.Children.Add(tabsStack);

        // 4. Left Sequence Column at (68, 280, w=820)
        var rowPanel = new StackPanel { Width = 820 };
        rowPanel.Children.Add(new TextBlock
        {
            Text = "Sequence · M1 paddle (3 events)",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(2, 6, 0, 10)
        });

        // Event 1
        var e1 = NexusKit.RowButton("01  A button press", "40 ms hold · 20 ms delay", null, focused: false, width: 820);
        rowPanel.Children.Add(e1);

        // Event 2 (Focused)
        var e2 = NexusKit.RowButton("02  A button press", "40 ms hold · 20 ms delay", null, focused: true, width: 820);
        rowPanel.Children.Add(e2);

        // Event 3
        var e3 = NexusKit.RowButton("03  A button press", "40 ms hold · 20 ms delay", null, focused: false, width: 820);
        rowPanel.Children.Add(e3);

        // Add event
        var addRow = NexusKit.RowButton("+ Add event", null, null, focused: false, width: 820);
        rowPanel.Children.Add(addRow);

        // Playback mode dropdown
        var modeRow = NexusKit.RowButton("Playback mode", null, NexusKit.Dropdown("Hold to repeat"), focused: false, width: 820);
        rowPanel.Children.Add(modeRow);

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 280);
        nexusPageHost.Children.Add(rowPanel);

        // 5. Right Details Side at (920, 280, w=612)
        var sideStack = new StackPanel { Width = 612 };

        // Visual: Timeline Lane (612x150)
        var timelineBox = new Border
        {
            Width = 612, Height = 150,
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.B("#17191c"),
            Margin = new Thickness(0, 0, 0, 18),
            ClipToBounds = true
        };
        var tCanvas = new Canvas { Width = 612, Height = 150 };

        // Lane label
        var laneLbl = new TextBlock
        {
            Text = "Buttons",
            FontFamily = NexusKit.Body,
            FontSize = 14,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(laneLbl, 14);
        Canvas.SetTop(laneLbl, 40);
        tCanvas.Children.Add(laneLbl);

        // 3 Events on timeline
        double startX = 130;
        for (int n = 0; n < 3; n++)
        {
            double ex = startX + 22 + n * 140;
            bool on = (n == 1);

            var bar = new Border
            {
                Width = 85, Height = 14,
                CornerRadius = new CornerRadius(7),
                Background = on ? Brushes.Transparent : NexusKit.GreenBrush,
                BorderBrush = on ? NexusKit.GreenBrush : null,
                BorderThickness = on ? new Thickness(2.5) : new Thickness(0)
            };
            Canvas.SetLeft(bar, ex);
            Canvas.SetTop(bar, 44);
            tCanvas.Children.Add(bar);

            var aKey = new TextBlock
            {
                Text = "A",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = NexusKit.WhiteBrush
            };
            Canvas.SetLeft(aKey, ex + 36);
            Canvas.SetTop(aKey, 22);
            tCanvas.Children.Add(aKey);

            var delayBadge = new Border
            {
                Background = NexusKit.TileBrush,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(9, 1, 9, 1),
                Child = new TextBlock { Text = "20 ms", FontFamily = NexusKit.Body, FontSize = 12, Foreground = NexusKit.WhiteBrush }
            };
            Canvas.SetLeft(delayBadge, ex + 89);
            Canvas.SetTop(delayBadge, 118);
            tCanvas.Children.Add(delayBadge);
        }
        timelineBox.Child = tCanvas;
        sideStack.Children.Add(timelineBox);

        // Details card
        var card = new Border
        {
            Width = 612,
            Padding = new Thickness(28, 26, 28, 26),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var cardStack = new StackPanel();
        cardStack.Children.Add(new TextBlock
        {
            Text = "02 · A button press",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Button event · 40 ms duration",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 4, 0, 16)
        });

        // Face button A
        var faceA = NexusKit.FaceKey("A");
        faceA.Margin = new Thickness(0, 0, 0, 16);
        faceA.HorizontalAlignment = HorizontalAlignment.Left;
        cardStack.Children.Add(faceA);

        var cBtns = new StackPanel { Orientation = Orientation.Horizontal };
        var editBtn = NexusKit.TheatricalButton("Edit timing", primary: true, height: 46, fontSize: 15);
        editBtn.Margin = new Thickness(0, 0, 10, 0);
        cBtns.Children.Add(editBtn);

        var delBtn = NexusKit.TheatricalButton("Delete event", primary: false, height: 46, fontSize: 15);
        cBtns.Children.Add(delBtn);
        cardStack.Children.Add(cBtns);

        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 280);
        nexusPageHost.Children.Add(sideStack);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Edit cue"), ("B", "Back"), ("X", "Add event"), ("Y", "Test macro") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }
}

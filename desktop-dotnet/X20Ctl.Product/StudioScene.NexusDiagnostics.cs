using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NexusShowDiagnostics()
    {
        if (Diagnostics is not { } diagnostics) return;
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
            Text = "Diagnostics",
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
            Text = "What this PC reads from the selected controller.",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        nexusPageHost.Children.Add(lead);

        // 4. Left Rows Column at (68, 250, w=820)
        var rowPanel = new StackPanel { Width = 820 };

        // Focused: Player input
        var r1 = NexusKit.RowButton("Player input", null, NexusKit.Dropdown("XInput 1"), focused: true, width: 820);
        rowPanel.Children.Add(r1);

        TextBlock BoundValue(TextBlock source) { var tb = new TextBlock { FontFamily = NexusKit.Body, FontSize = 17, Foreground = NexusKit.SubBrush, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 520 }; tb.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = source }); return tb; }
        rowPanel.Children.Add(NexusKit.RowButton("Left stick", null, BoundValue(diagnostics.LeftView.Stats), focused: false, width: 820));
        rowPanel.Children.Add(NexusKit.RowButton("Right stick", null, BoundValue(diagnostics.RightView.Stats), focused: false, width: 820));
        rowPanel.Children.Add(NexusKit.RowButton("Left trigger", null, BoundValue(diagnostics.LtView.Stats), focused: false, width: 820));
        rowPanel.Children.Add(NexusKit.RowButton("Right trigger", null, BoundValue(diagnostics.RtView.Stats), focused: false, width: 820));

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 250);
        nexusPageHost.Children.Add(rowPanel);

        // 5. Right Details Side at (920, 250, w=612)
        var sideStack = new StackPanel { Width = 612 };

        // Visual: Sticks & Triggers plot (612x240)
        var stickBox = new Border
        {
            Width = 612, Height = 240,
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.B("#17191c"),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 18)
        };
        var sGrid = new Grid();
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

        // Left stick plot
        var lsStack = CreateStickPlot("Left stick", "—");
        Grid.SetColumn(lsStack, 0);
        sGrid.Children.Add(lsStack);

        // Right stick plot
        var rsStack = CreateStickPlot("Right stick", "—");
        Grid.SetColumn(rsStack, 1);
        sGrid.Children.Add(rsStack);

        // LT plot
        var ltStack = CreateTrigPlot("LT", "—");
        Grid.SetColumn(ltStack, 2);
        sGrid.Children.Add(ltStack);

        // RT plot
        var rtStack = CreateTrigPlot("RT", "—");
        Grid.SetColumn(rtStack, 3);
        sGrid.Children.Add(rtStack);

        stickBox.Child = sGrid;
        sideStack.Children.Add(stickBox);

        // Details card: Polling rate
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
            Text = "Input changes",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        var rateValue = new TextBlock
        {
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        };
        rateValue.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = diagnostics.RateBlock });
        cardStack.Children.Add(rateValue);
        cardStack.Children.Add(new TextBlock
        {
            Text = $"Player 1 · {Model.Name} · Reads XInput only · Nothing is written to the controller.",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.78)),
            TextWrapping = TextWrapping.Wrap
        });
        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 250);
        nexusPageHost.Children.Add(sideStack);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Capture test"), ("B", "Back"), ("X", "Reset graph") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }

    private static StackPanel CreateStickPlot(string label, string readout)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var canvas = new Canvas { Width = 110, Height = 110 };

        var outerRing = new Ellipse
        {
            Width = 110, Height = 110,
            Stroke = NexusKit.B(NexusKit.A(Colors.White, 0.2)),
            StrokeThickness = 2
        };
        canvas.Children.Add(outerRing);

        var innerRing = new Ellipse
        {
            Width = 44, Height = 44,
            Stroke = NexusKit.B(NexusKit.A(Colors.White, 0.1)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(innerRing, 33);
        Canvas.SetTop(innerRing, 33);
        canvas.Children.Add(innerRing);

        var vl = new Line { X1 = 55, Y1 = 6, X2 = 55, Y2 = 104, Stroke = NexusKit.B(NexusKit.A(Colors.White, 0.08)), StrokeThickness = 1 };
        var hl = new Line { X1 = 6, Y1 = 55, X2 = 104, Y2 = 55, Stroke = NexusKit.B(NexusKit.A(Colors.White, 0.08)), StrokeThickness = 1 };
        canvas.Children.Add(vl);
        canvas.Children.Add(hl);

        var dot = new Ellipse { Width = 12, Height = 12, Fill = NexusKit.SubBrush };
        Canvas.SetLeft(dot, 49);
        Canvas.SetTop(dot, 49);
        canvas.Children.Add(dot);

        stack.Children.Add(canvas);
        stack.Children.Add(new TextBlock { Text = label, FontFamily = NexusKit.Body, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = NexusKit.WhiteBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
        stack.Children.Add(new TextBlock { Text = readout, FontFamily = NexusKit.Body, FontSize = 12, Foreground = NexusKit.SubBrush, HorizontalAlignment = HorizontalAlignment.Center });
        return stack;
    }

    private static StackPanel CreateTrigPlot(string label, string readout)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var bar = new Border
        {
            Width = 28, Height = 110,
            CornerRadius = new CornerRadius(4),
            Background = NexusKit.TrackBrush
        };
        stack.Children.Add(bar);
        stack.Children.Add(new TextBlock { Text = label, FontFamily = NexusKit.Body, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = NexusKit.WhiteBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
        stack.Children.Add(new TextBlock { Text = readout, FontFamily = NexusKit.Body, FontSize = 12, Foreground = NexusKit.SubBrush, HorizontalAlignment = HorizontalAlignment.Center });
        return stack;
    }
}

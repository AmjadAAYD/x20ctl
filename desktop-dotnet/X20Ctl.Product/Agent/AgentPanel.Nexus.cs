using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product.Agent;

public partial class AgentPanel
{
    private void BuildNexusAgent()
    {
        var board = new Canvas { Name = "NexusAgent", Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Background dimmed home
        board.Children.Add(NexusKit.KeyArtBand(560, opacity: 0.35));
        board.Children.Add(new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0.5, 1), EndPoint = new(0.5, 0),
                GradientStops =
                {
                    new(NexusKit.House, 0.0),
                    new(Color.FromArgb(0xbb, 0x10, 0x11, 0x13), 0.18),
                    new(Color.FromArgb(0x99, 0x10, 0x11, 0x13), 1.0)
                }
            }
        });

        // 2. Xbox Guide panel at (40, 40, w=900, h=820)
        var guide = new Border
        {
            Width = 900, Height = 820,
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.B(Color.FromArgb(0xf7, 0x16, 0x18, 0x1b)),
            Effect = new DropShadowEffect { BlurRadius = 80, ShadowDepth = 30, Direction = 270, Opacity = 0.6, Color = Colors.Black },
            ClipToBounds = true
        };

        var guideCanvas = new Canvas { Width = 900, Height = 820 };

        // Header icons at top: height 70
        var topHeader = new Border
        {
            Width = 900, Height = 70,
            BorderBrush = NexusKit.B(NexusKit.A(Colors.White, 0.08)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        var hStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(30, 0, 30, 0), VerticalAlignment = VerticalAlignment.Center };
        var guideIcons = new[] { "\uE80F", "\uE99A", "\uE8B5", "\uE9A6", "\uE713" };
        for (int i = 0; i < guideIcons.Length; i++)
        {
            bool on = (i == 1); // Chat tab active
            var iconItem = new Border
            {
                Margin = new Thickness(0, 0, 34, 0),
                Padding = new Thickness(0, 0, 0, on ? 16 : 0),
                BorderBrush = on ? NexusKit.GreenBrush : null,
                BorderThickness = on ? new Thickness(0, 0, 0, 3) : new Thickness(0),
                Child = new TextBlock
                {
                    Text = guideIcons[i],
                    FontFamily = NexusKit.Icons,
                    FontSize = 22,
                    Foreground = on ? NexusKit.GreenBrush : NexusKit.SubBrush
                }
            };
            hStack.Children.Add(iconItem);
        }
        topHeader.Child = hStack;
        Canvas.SetLeft(topHeader, 0);
        Canvas.SetTop(topHeader, 0);
        guideCanvas.Children.Add(topHeader);

        // Identity at (30, 94): Avatar + Title & Status
        var idStack = new StackPanel { Orientation = Orientation.Horizontal };
        var avBorder = new Border
        {
            Width = 58, Height = 58,
            CornerRadius = new CornerRadius(29),
            BorderBrush = NexusKit.B(NexusKit.A(Colors.White, 0.4)),
            BorderThickness = new Thickness(2),
            Background = NexusKit.B("#1a1c20"),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 16, 0)
        };
        var avArt = NexusKit.ControllerArt("x20", 54);
        if (avArt != null) avBorder.Child = avArt;
        idStack.Children.Add(avBorder);

        var idText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        idText.Children.Add(new TextBlock
        {
            Text = "Agent XCTL",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        statusRow.Children.Add(new TextBlock
        {
            Text = "● ",
            FontFamily = NexusKit.Body,
            FontSize = 14,
            Foreground = NexusKit.GreenBrush
        });
        var liveStatus = new TextBlock { FontFamily = NexusKit.Body, FontSize = 15, Foreground = NexusKit.SubBrush };
        var statusRun = new System.Windows.Documents.Run(); statusRun.SetBinding(System.Windows.Documents.Run.TextProperty, new System.Windows.Data.Binding("Text") { Source = status });
        var settingsRun = new System.Windows.Documents.Run(); settingsRun.SetBinding(System.Windows.Documents.Run.TextProperty, new System.Windows.Data.Binding("Text") { Source = settingsToggle });
        liveStatus.Inlines.Add(statusRun); liveStatus.Inlines.Add(new System.Windows.Documents.Run(" · ")); liveStatus.Inlines.Add(settingsRun);
        statusRow.Children.Add(liveStatus);
        idText.Children.Add(statusRow);
        idStack.Children.Add(idText);

        idStack.Cursor = System.Windows.Input.Cursors.Hand; idStack.MouseLeftButtonUp += (_, _) => ShowSettingsPage(!SettingsOpen);
        Canvas.SetLeft(idStack, 30);
        Canvas.SetTop(idStack, 94);
        guideCanvas.Children.Add(idStack);

        // Topic Chips at (30, 178)
        var chipsStack = new StackPanel { Orientation = Orientation.Horizontal };
        string[] chips = { "Aim & sticks", "Triggers", "Buttons", "PC mode" };
        for (int i = 0; i < chips.Length; i++)
        {
            bool on = (i == 0);
            var ch = new Border
            {
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 10, 0),
                CornerRadius = new CornerRadius(4),
                Background = on ? NexusKit.WhiteBrush : NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = chips[i],
                    FontFamily = NexusKit.Body,
                    FontSize = 15,
                    FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = on ? NexusKit.B("#111111") : NexusKit.WhiteBrush
                }
            };
            string topic = new[] { "sticks", "triggers", "buttons", "PC mode" }[i];
            chipsStack.Children.Add(ChipButton(ch, () => { Search.Text = topic; ShowView(View.Ideas); }));
        }
        Canvas.SetLeft(chipsStack, 30);
        Canvas.SetTop(chipsStack, 178);
        guideCanvas.Children.Add(chipsStack);

        // The real chat, ideas, check-up and settings, and the real input
        MountRealViews(guideCanvas, 30, 230, 840, 480, NexusKit.B(Color.FromArgb(0xf7, 0x16, 0x18, 0x1b)), NexusKit.GreenBrush);
        StyleRealInput(NexusKit.Body, 17, NexusKit.WhiteBrush);
        var inputBorder = new Border
        {
            Width = 840, Height = 56, CornerRadius = new CornerRadius(4), Background = NexusKit.InputBrush,
            BorderBrush = NexusKit.GreenBrush, BorderThickness = new Thickness(3), Padding = new Thickness(18, 0, 10, 0)
        };
        var iGrid = new Grid();
        iGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        iGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(Input, 0); iGrid.Children.Add(Input);
        var sendBtn = ChipButton(new Border
        {
            Width = 44, Height = 44, CornerRadius = new CornerRadius(4), Background = NexusKit.B("#3c4046"),
            Child = new TextBlock { Text = "\uE724", FontFamily = NexusKit.Icons, FontSize = 18, Foreground = NexusKit.WhiteBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        }, ClickSend);
        Grid.SetColumn(sendBtn, 1); iGrid.Children.Add(sendBtn);
        inputBorder.Child = iGrid;
        Canvas.SetLeft(inputBorder, 30);
        Canvas.SetTop(inputBorder, 820 - 56 - 28);
        guideCanvas.Children.Add(inputBorder);

        guide.Child = guideCanvas;
        Canvas.SetLeft(guide, 40);
        Canvas.SetTop(guide, 40);
        board.Children.Add(guide);

        // Hints at bottom
        var hints = NexusKit.HintsBar(new[]
        {
            ("LB", "Undo"),
            ("RB", "Exit"),
            ("A", "Send"),
            ("B", "Back"),
            ("X", "New"),
            ("Y", "Search")
        });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        board.Children.Add(hints);

        // Mount to AgentPanel Body
        Body.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        Body.Background = NexusKit.HouseBrush;
    }
}

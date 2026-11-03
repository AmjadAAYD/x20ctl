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
    private void NexusShowVibration()
    {
        Vibration ??= new(vibrationDraft, this, RegisterFocus, animate, Model.Id);
        var vib = Vibration;
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
            Text = "Vibration",
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
            Text = "Linked grip strength · Local draft",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        nexusPageHost.Children.Add(lead);

        // 4. Left Column at (68, 236, w=820)
        var rowPanel = new StackPanel { Width = 820 };

        // Row 0 (Focused): Strength slider 70%
        var strengthSlider = ThemeSlider(vib.Strength, "theme:vibration:strength", 210, vibrationDraft.CanEdit, NexusKit.SliderTrackBrush);
        var r1 = NexusKit.RowButton("Strength", null, strengthSlider, focused: true, width: 820);
        rowPanel.Children.Add(r1);

        // Row 2: Test on controller disabled
        var disText = new TextBlock
        {
            Text = "Physical output unavailable",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.4)),
            VerticalAlignment = VerticalAlignment.Center
        };
        var r3 = NexusKit.RowButton("Test on controller", null, disText, focused: false, width: 820);
        rowPanel.Children.Add(r3);

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 236);
        nexusPageHost.Children.Add(rowPanel);

        // 5. Visual under rows: Controller with grip vibration glow at (178, 482, w=600)
        double visW = 520;
        var visHost = new Canvas { Width = visW, Height = visW * 0.65, ClipToBounds = false };
        var padArt = NexusKit.ControllerArt(Model.Id, visW);
        if (padArt != null)
        {
            // Green grip glow underneath
            var glow = NexusKit.ControllerArt(Model.Id, visW);
            if (glow != null)
            {
                glow.Effect = new DropShadowEffect
                {
                    BlurRadius = 36,
                    ShadowDepth = 0,
                    Color = NexusKit.Green,
                    Opacity = 0.85
                };
                visHost.Children.Add(glow);
            }
            visHost.Children.Add(padArt);
        }
        Canvas.SetLeft(visHost, 68 + (820 - visW) / 2);
        Canvas.SetTop(visHost, 470);
        nexusPageHost.Children.Add(visHost);

        // 6. Right Details Card at (920, 242, w=612)
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
            Text = "Vibration Preview",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        cardStack.Children.Add(new TextBlock
        {
            FontFamily = NexusKit.Display,
            FontSize = 46,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush,
            Margin = new Thickness(0, 4, 0, 0)
        }.Also(t => t.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Value") { Source = vib.Strength, StringFormat = "{0:0}%" })));

        // Chips: Off, 25%, 50%, 75%, 100%
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 14) };
        foreach (var ch in new[] { "Off", "25%", "50%", "75%", "100%" })
        {
            int level = ch == "Off" ? 0 : int.Parse(ch.TrimEnd('%'));
            var chip = new Border
            {
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(0, 0, 8, 0),
                CornerRadius = new CornerRadius(4),
                Background = NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = ch,
                    FontFamily = NexusKit.Body,
                    FontSize = 15,
                    Foreground = NexusKit.WhiteBrush
                }
            };
            chip.MouseLeftButtonDown += (_, _) => vib.Set(level);
            chips.Children.Add(chip);
        }
        cardStack.Children.Add(chips);

        cardStack.Children.Add(new TextBlock
        {
            Text = "Region selection changes the illustration only. This backend has one linked strength; independent trigger tuning is unavailable.",
            FontFamily = NexusKit.Body,
            FontSize = 16,
            Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.78)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 24,
            Margin = new Thickness(0, 0, 0, 16)
        });

        cardStack.Children.Add(new TextBlock
        {
            Text = "Preview animation · No motor telemetry or physical vibration is generated",
            FontFamily = NexusKit.Body,
            FontSize = 14,
            Foreground = NexusKit.SubBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });

        card.Child = cardStack;
        Canvas.SetLeft(card, 920);
        Canvas.SetTop(card, 242);
        nexusPageHost.Children.Add(card);

        // 7. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Select"), ("B", "Back"), ("X", "Reset"), ("LT/RT", "Strength"), ("LB/RB", "Tabs") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }
}

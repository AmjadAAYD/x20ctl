using System;
using System.Windows.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;

namespace X20Ctl.Product;

public partial class StudioScene
{
    /// <summary>
    /// Vibration workbench for Volt: Steam Deck two-pane settings sheet with dual-motor sliders,
    /// solid orange fill tracks, controller art with vibrating shockwaves, and 46px #ffad0a intensity gauge.
    /// Strictly implements t_volt.py:253 Volt and models.py:vibration.
    /// </summary>
    private UIElement BuildVoltVibration()
    {
        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Left List: Motor intensity sliders (width: 520)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        leftStack.Children.Add(new TextBlock
        {
            Text = "LINKED GRIP STRENGTH",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        if (Vibration is not {} vibration) return content;
        var strength = ThemeSlider(vibration.Strength,"theme:vibration:strength",460,true); leftStack.Children.Add(strength);
        var presets = new WrapPanel { Margin = new Thickness(0,18,0,18) };
        foreach(int percent in new[]{0,25,50,75,100})
        {
            var face = new Border { Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,0,10,0),Background=VoltKit.B("#262626"),CornerRadius=new CornerRadius(10),Child=new TextBlock{Text=percent+"%",Foreground=VoltKit.WhiteBrush}};
            presets.Children.Add(ThemeAction(face,"theme:vibration:preset:"+percent,()=>vibration.Set(percent)));
        }
        leftStack.Children.Add(presets);
        foreach(string region in new[]{"All","Both grips","Left grip","Right grip"})
        {
            var face = new Border { Height=54,Padding=new Thickness(14,0,14,0),Margin=new Thickness(0,0,0,8),Background=VoltKit.B("#262626"),CornerRadius=new CornerRadius(10),Child=new TextBlock{Text="Preview · "+region,Foreground=VoltKit.WhiteBrush,VerticalAlignment=VerticalAlignment.Center}};
            leftStack.Children.Add(ThemeAction(face,"theme:vibration:region:"+region,()=>vibration.SelectRegion(region)));
        }
        leftStack.Children.Add(new TextBlock { Text="One linked strength. Local draft and visual preview only.",Foreground=VoltKit.MuteBrush,Margin=new Thickness(0,16,0,0),TextWrapping=TextWrapping.Wrap });
        Canvas.SetLeft(leftStack,0);Canvas.SetTop(leftStack,10);content.Children.Add(leftStack);

        // 2. Right Details Card (width: 410 at x = 550)
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };

        // Controller Rumble Illustration Box (#0b0b0b)
        var visualBox = new Border
        {
            Width = 410,
            Height = 220,
            Background = VoltKit.B("#0b0b0b"),
            CornerRadius = new CornerRadius(14),
            ClipToBounds = true
        };
        var vCanvas = new Canvas { Width = 410, Height = 220 };

        var padArt = ControllerArt.Front(Model.Id, PadColours.For(Player, Model.Id)) is {} art ? new Image { Source=art,Width=340,Stretch=Stretch.Uniform } : null;
        if (padArt != null)
        {
            var pGlow = VoltKit.PedestalGlow(280, 80, VoltKit.AmberHot);
            Canvas.SetLeft(pGlow, 65);
            Canvas.SetTop(pGlow, 120);
            vCanvas.Children.Add(pGlow);

            padArt.Opacity = 0.95;
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 12, Opacity = 0.6 };
            Canvas.SetLeft(padArt, 35);
            Canvas.SetTop(padArt, 15);
            vCanvas.Children.Add(padArt);
        }

        // Inner & outer shockwaves radiating from left heavy grip
        var leftShockOuter = new Ellipse
        {
            Width = 86,
            Height = 146,
            Stroke = VoltKit.B("rgba(255,173,10,0.4)"),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 5, 4 },
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.6 }
        };
        Canvas.SetLeft(leftShockOuter, 16);
        Canvas.SetTop(leftShockOuter, 38);
        vCanvas.Children.Add(leftShockOuter);

        var leftShock = new Ellipse
        {
            Width = 60,
            Height = 110,
            Stroke = VoltKit.AmberHotBrush,
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(leftShock, 30);
        Canvas.SetTop(leftShock, 55);
        vCanvas.Children.Add(leftShock);

        // Inner & outer shockwaves radiating from right light grip
        var rightShockOuter = new Ellipse
        {
            Width = 86,
            Height = 146,
            Stroke = VoltKit.B("rgba(255,228,20,0.35)"),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 5, 4 },
            Effect = new DropShadowEffect { Color = VoltKit.C("#ffe414"), BlurRadius = 16, ShadowDepth = 0, Opacity = 0.6 }
        };
        Canvas.SetLeft(rightShockOuter, 306);
        Canvas.SetTop(rightShockOuter, 38);
        vCanvas.Children.Add(rightShockOuter);

        var rightShock = new Ellipse
        {
            Width = 60,
            Height = 110,
            Stroke = VoltKit.B("#ffe414"),
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Effect = new DropShadowEffect { Color = VoltKit.C("#ffe414"), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(rightShock, 320);
        Canvas.SetTop(rightShock, 55);
        vCanvas.Children.Add(rightShock);

        visualBox.Child = vCanvas;
        rightStack.Children.Add(visualBox);

        // Details card
        var card = new Border
        {
            Width = 410,
            Background = VoltKit.SheetDarkBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24, 20, 24, 20),
            Margin = new Thickness(0, 16, 0, 0)
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };

        cStack.Children.Add(new TextBlock
        {
            Text = "LINKED STRENGTH",
            FontFamily = VoltKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 2)
        });

        var strengthValue = new TextBlock
        {
            FontFamily = VoltKit.Display,
            FontSize = 46,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush,
            LineHeight = 48
        };
        strengthValue.SetBinding(TextBlock.TextProperty,new Binding("Value"){Source=vibration.Strength,StringFormat="{0:0}%"}); cStack.Children.Add(strengthValue);

        cStack.Children.Add(new TextBlock
        {
            Text = "One linked grip-strength value. Preview regions do not send vibration commands. Physical testing requires a verified connection and bounded motor command.",
            FontFamily = VoltKit.Body,
            FontSize = 15,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Margin = new Thickness(0, 8, 0, 16)
        });

        // Action button [▶ Test Rumble]
        var testBtn = VoltKit.OrangeActionButton("Test Rumble", 362, 54);
        testBtn.IsEnabled = false; testBtn.ToolTip = vibration.Test.ToolTip; cStack.Children.Add(testBtn);

        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        return VoltKit.TwoPaneShell("vibration", "Vibration", "Linked grip strength · local draft and visual preview.", content, VoltSelectPage);
    }
}

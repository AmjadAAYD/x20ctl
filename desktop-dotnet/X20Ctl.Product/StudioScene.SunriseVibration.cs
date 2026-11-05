using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SunriseShowVibration()
    {
        if (sunrisePageHost == null) return;
        sunrisePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateSunriseHeader("Studio"));
        var sidebar = CreateSunriseSidebar("Vibration");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area
        var content = new Canvas { Width = 1300, Height = 770 };
        Canvas.SetLeft(content, 272);
        Canvas.SetTop(content, 80);

        var titleSp = SunriseKit.SunriseTitle("HAPTIC VIBRATION", "Dual-rotor grip motor strength, trigger pulse haptics, and response damping.");
        Canvas.SetLeft(titleSp, 10);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        int currentStrength = (int)(Vibration?.Strength?.Value ?? 75);

        // ==========================================
        // LEFT STICKER CARD: Sliders & Presets
        // ==========================================
        var leftSp = new StackPanel { Width = 560 };

        var motHeader = new Grid();
        motHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        motHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        motHeader.Children.Add(new TextBlock { Text = "GRIP MOTOR INTENSITY", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12.5, Foreground = SunriseKit.InkBrush });
        var strValTb = new TextBlock { Text = $"{currentStrength}%", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Black, FontSize = 14, Foreground = SunriseKit.OrangeBrush };
        Grid.SetColumn(strValTb, 1);
        motHeader.Children.Add(strValTb);
        leftSp.Children.Add(motHeader);

        var strSlider = ThemeSlider(Vibration.Strength, "theme:vibration:strength", 540, true);
        strSlider.Margin = new Thickness(0, 10, 0, 16);
        strSlider.ValueChanged += (_, e) =>
        {
            int val = (int)e.NewValue;
            strValTb.Text = $"{val}%";
        };
        leftSp.Children.Add(strSlider);

        // Intensity Presets
        var pHeader = new TextBlock { Text = "STRENGTH PRESETS", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.InkBrush, Margin = new Thickness(0, 0, 0, 8) };
        leftSp.Children.Add(pHeader);

        var presetsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        int[] presetVals = [0, 25, 50, 75, 100];
        foreach (var pv in presetVals)
        {
            int pVal = pv;
            bool isPv = (pVal == currentStrength);
            var pBtn = SunriseKit.SunriseChip($"{pVal}%", isPv, () =>
            {
                Vibration?.Set(pVal);
                SunriseShowVibration();
            }, tag: $"theme:vibration:{pVal}");
            presetsPanel.Children.Add(pBtn);
        }
        leftSp.Children.Add(presetsPanel);

        // Haptic feedback test button
        var testBtn = SunriseKit.SunriseButton("Test Vibration Pulse", () =>
        {
            // Simulation visual preview
        }, isPrimary: false, width: 220, height: 40, tag: "theme:vibration:try");
        testBtn.Margin = new Thickness(0, 0, 0, 16);
        leftSp.Children.Add(testBtn);

        var noteTb = new TextBlock
        {
            Text = "Motor power affects battery discharge rate. Test rumble triggers a 250ms burst.",
            FontFamily = SunriseKit.Body,
            FontSize = 11.5,
            Foreground = SunriseKit.OliveBrush
        };
        leftSp.Children.Add(noteTb);

        var leftCard = SunriseKit.CreateStickerCard(leftSp, 590, 520, cornerRadius: 22);
        Canvas.SetLeft(leftCard, 10);
        Canvas.SetTop(leftCard, 100);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT TV SCREEN: Dual Rotor Visualizer
        // ==========================================
        var rightSp = new StackPanel { Width = 580 };

        var scopeTitle = new TextBlock
        {
            Text = "ROTOR TELEMETRY & TRIGGER HAPTICS",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 13,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 14)
        };
        rightSp.Children.Add(scopeTitle);

        var padBezel = new Border
        {
            Width = 560,
            Height = 280,
            Background = SunriseKit.CreamBrush,
            BorderBrush = SunriseKit.InkBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(16),
            Margin = new Thickness(0, 0, 0, 20)
        };
        var padArt = SunriseKit.ControllerArt(Model.Id, 420);
        padBezel.Child = padArt;
        rightSp.Children.Add(padBezel);

        var vibNote = new TextBlock
        {
            Text = "Vibration strength is tested live on the controller · Hardware settings remain locked.",
            FontFamily = SunriseKit.Body,
            FontSize = 11.5,
            Foreground = SunriseKit.OliveBrush,
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            TextWrapping = TextWrapping.Wrap
        };
        rightSp.Children.Add(vibNote);

        var tvCard = SunriseKit.CreateTvScreenCard(rightSp, 610, 520, withAntennae: true);
        Canvas.SetLeft(tvCard, 630);
        Canvas.SetTop(tvCard, 50);
        content.Children.Add(tvCard);

        board.Children.Add(content);

        // Bottom Bar
        var bBar = CreateSunriseBottomBar();
        Canvas.SetLeft(bBar, 0);
        Canvas.SetTop(bBar, 856);
        board.Children.Add(bBar);

        sunrisePageHost.Children.Add(board);
    }
}

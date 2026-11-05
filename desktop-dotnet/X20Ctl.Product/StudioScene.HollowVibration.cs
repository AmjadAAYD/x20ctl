using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void HollowShowVibration()
    {
        if (hollowPageHost == null) return;
        hollowPageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateHollowHeader("Studio"));
        var sidebar = CreateHollowSidebar("Vibration");
        Canvas.SetLeft(sidebar, 28);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area at x=286, y=80
        var content = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(content, 286);
        Canvas.SetTop(content, 80);

        var titleSp = HollowKit.HollowHeader("HAPTIC VIBRATION", "Tune grip motor strength and haptic frequency intensity.");
        Canvas.SetLeft(titleSp, 16);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT PANEL: Controller on Pedestal with Cavern Ripple Arcs
        // ==========================================
        var leftSp = new StackPanel { Width = 610 };

        var rumbleCanvas = new Canvas
        {
            Width = 580,
            Height = 440,
            ClipToBounds = true,
            Background = new SolidColorBrush(Color.FromArgb(0x80, 0x05, 0x0c, 0x14))
        };

        // Cavern Ripple Arcs
        for (int r = 1; r <= 4; r++)
        {
            var arcLeft = new Path
            {
                Stroke = new SolidColorBrush(Color.FromArgb((byte)(0x20 * (5 - r)), 0xb8, 0xcc, 0xe0)),
                StrokeThickness = 1.5,
                Data = Geometry.Parse($"M {180 - r * 25},240 C {160 - r * 30},180 {160 - r * 30},120 {180 - r * 25},60")
            };
            var arcRight = new Path
            {
                Stroke = new SolidColorBrush(Color.FromArgb((byte)(0x20 * (5 - r)), 0xb8, 0xcc, 0xe0)),
                StrokeThickness = 1.5,
                Data = Geometry.Parse($"M {400 + r * 25},240 C {420 + r * 30},180 {420 + r * 30},120 {400 + r * 25},60")
            };
            rumbleCanvas.Children.Add(arcLeft);
            rumbleCanvas.Children.Add(arcRight);
        }

        // Controller Image
        var padImg = HollowKit.ControllerArt(Model.Id, 340);
        Canvas.SetLeft(padImg, 120);
        Canvas.SetTop(padImg, 90);
        rumbleCanvas.Children.Add(padImg);

        leftSp.Children.Add(rumbleCanvas);

        var leftCard = HollowKit.FiligreeCard(leftSp, padding: 18, cornerSize: 14);
        leftCard.Width = 636;
        leftCard.Height = 580;
        Canvas.SetLeft(leftCard, 16);
        Canvas.SetTop(leftCard, 90);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT PANEL: Grip Sliders, Presets & Testing
        // ==========================================
        var rightSp = new StackPanel { Width = 550 };

        var settingsTitle = new TextBlock
        {
            Text = "MOTOR CALIBRATION",
            FontFamily = HollowKit.Display,
            FontSize = 14,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 12)
        };
        rightSp.Children.Add(settingsTitle);

        // Strength Slider
        int curStrength = (int)(Vibration?.Strength?.Value ?? 75);
        var sliderLabel = new TextBlock
        {
            Text = $"GRIP MOTOR STRENGTH: {curStrength}%",
            FontFamily = HollowKit.Display,
            FontSize = 12.5,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 6)
        };
        rightSp.Children.Add(sliderLabel);

        var vibSlider = ThemeSlider(Vibration.Strength, "theme:vibration:strength", 540, true);
        vibSlider.Margin = new Thickness(0, 0, 0, 16);
        vibSlider.ValueChanged += (_, e) =>
        {
            int val = (int)e.NewValue;
            sliderLabel.Text = $"GRIP MOTOR STRENGTH: {val}%";
        };
        rightSp.Children.Add(vibSlider);

        // Quick Presets: 0%, 25%, 50%, 75%, 100%
        var presetsSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        int[] presetVals = [0, 25, 50, 75, 100];
        foreach (int p in presetVals)
        {
            var pBtn = new Border
            {
                Padding = new Thickness(14, 6, 14, 6),
                Background = (curStrength == p) ? HollowKit.RaisedBrush : new SolidColorBrush(Color.FromArgb(0xaa, 0x09, 0x12, 0x1c)),
                BorderBrush = (curStrength == p) ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:vibration:preset:{p}"
            };
            pBtn.Child = new TextBlock { Text = $"{p}%", FontFamily = HollowKit.Display, FontSize = 11.5, Foreground = (curStrength == p) ? HollowKit.IvoryTextBrush : HollowKit.SoftTextBrush };
            pBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Vibration?.Set(p);
                HollowShowVibration();
            };
            presetsSp.Children.Add(pBtn);
        }
        rightSp.Children.Add(presetsSp);

        rightSp.Children.Add(HollowKit.DiamondDivider(550, 16));

        // Test rumble & Reset buttons
        var actSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 14) };

        var testBtn = HollowKit.HollowButton("Test vibration", () =>
        {
            // Simulation visual test only
        }, isPrimary: false, width: 140, height: 34, tag: "theme:vibration:test");
        testBtn.Margin = new Thickness(0, 0, 12, 0);

        var resetBtn = HollowKit.HollowButton("Reset to default", () =>
        {
            Vibration?.Set(70);
            HollowShowVibration();
        }, isPrimary: false, width: 140, height: 34, tag: "theme:vibration:reset");

        actSp.Children.Add(testBtn);
        actSp.Children.Add(resetBtn);
        rightSp.Children.Add(actSp);

        // Informational status note (Vibration does not write to controller memory)
        var vibNote = new TextBlock
        {
            Text = "Vibration strength is tested on the controller · Hardware settings remain locked.",
            FontFamily = HollowKit.Body,
            FontSize = 12.5,
            Foreground = HollowKit.SoftTextBrush,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        rightSp.Children.Add(vibNote);

        var rightCard = HollowKit.FiligreeCard(rightSp, padding: 22, cornerSize: 14);
        rightCard.Width = 590;
        rightCard.Height = 580;
        Canvas.SetLeft(rightCard, 674);
        Canvas.SetTop(rightCard, 90);
        content.Children.Add(rightCard);

        board.Children.Add(content);

        // Dock
        var dock = HollowKit.HollowDock("Vibration");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        board.Children.Add(dock);

        hollowPageHost.Children.Add(board);
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NocturneShowVibration()
    {
        if (nocturnePageHost == null) return;
        nocturnePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(NocturneKit.CreateCityBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateNocturneHeader("Studio"));
        var sidebar = CreateNocturneSidebar("Vibration");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 74);
        board.Children.Add(sidebar);

        // Title Block
        var title = NocturneKit.NocturneTitle("MOTOR", "VIBRATION", "Asymmetric haptic feedback engines and trigger rumble sensitivity.");
        Canvas.SetLeft(title, 300);
        Canvas.SetTop(title, 70);
        board.Children.Add(title);

        // Left stage: Controller visualizer with cobalt glow
        var padArt = NocturneKit.ControllerArt(Model.Id, 460);
        Canvas.SetLeft(padArt, 360);
        Canvas.SetTop(padArt, 240);
        board.Children.Add(padArt);

        // Right stage: Tuning card on slanted white card
        var rightStack = new StackPanel { Width = 510 };

        rightStack.Children.Add(new TextBlock
        {
            Text = "HAPTIC CALIBRATION",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 26,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Thin rule
        rightStack.Children.Add(new Rectangle { Height = 2, Fill = new SolidColorBrush(Color.FromArgb(0x40, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 0, 0, 14) });

        int curStrength = (int)(Vibration?.Strength?.Value ?? 75);
        var sliderLabel = new TextBlock
        {
            Text = $"GRIP MOTOR INTENSITY: {curStrength}%",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 6)
        };
        rightStack.Children.Add(sliderLabel);

        var vibSlider = ThemeSlider(Vibration.Strength, "theme:vibration:strength", 480, true);
        vibSlider.Margin = new Thickness(0, 0, 0, 16);
        vibSlider.ValueChanged += (_, e) =>
        {
            int val = (int)e.NewValue;
            sliderLabel.Text = $"GRIP MOTOR INTENSITY: {val}%";
        };
        rightStack.Children.Add(vibSlider);

        // Presets: 0, 25, 50, 75, 100
        rightStack.Children.Add(new TextBlock { Text = "QUICK INTENSITY PRESETS", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = NocturneKit.CobaltBrush, Margin = new Thickness(0, 4, 0, 8) });

        var presetsSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 18) };
        int[] presetVals = [0, 25, 50, 75, 100];
        foreach (int p in presetVals)
        {
            int pVal = p;
            bool isCur = (curStrength == pVal);
            var pBtn = new Border
            {
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Background = isCur ? NocturneKit.ActiveBlueBrush : new SolidColorBrush(Color.FromArgb(0x18, 0x01, 0x00, 0x42)),
                BorderBrush = isCur ? NocturneKit.CobaltBrush : new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Tag = $"theme:vibration:preset:{pVal}"
            };
            pBtn.Child = new TextBlock
            {
                Text = $"{pVal}%",
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = isCur ? NocturneKit.WhiteBrush : NocturneKit.InkBrush,
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            pBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Vibration?.Set(pVal);
                NocturneShowVibration();
            };
            presetsSp.Children.Add(pBtn);
        }
        rightStack.Children.Add(presetsSp);

        // Thin rule
        rightStack.Children.Add(new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 0, 0, 14) });

        // Action Buttons: Test vibration & Reset to default
        var actSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };

        var testBtn = NocturneKit.NocturneButton("Test Vibration", () =>
        {
            // Simulation visual preview
        }, isPrimary: false, width: 160, height: 36, tag: "theme:vibration:test");
        testBtn.Margin = new Thickness(0, 0, 10, 0);

        var resetBtn = NocturneKit.NocturneButton("Reset Default", () =>
        {
            Vibration?.Set(70);
            NocturneShowVibration();
        }, isPrimary: false, width: 160, height: 36, tag: "theme:vibration:reset");

        actSp.Children.Add(testBtn);
        actSp.Children.Add(resetBtn);
        rightStack.Children.Add(actSp);

        var vibNote = new TextBlock
        {
            Text = "Vibration strength is tested live on the controller · Hardware settings remain locked.",
            FontFamily = NocturneKit.Body,
            FontSize = 12,
            Foreground = NocturneKit.CobaltBrush,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        rightStack.Children.Add(vibNote);

        var whiteCard = NocturneKit.NocturneWhiteCard(rightStack, width: 570, height: 570);
        Canvas.SetLeft(whiteCard, 970);
        Canvas.SetTop(whiteCard, 220);
        board.Children.Add(whiteCard);

        // Bottom Bar
        var bottomBar = CreateNocturneBottomBar();
        Canvas.SetLeft(bottomBar, 0);
        Canvas.SetTop(bottomBar, 856);
        board.Children.Add(bottomBar);

        nocturnePageHost.Children.Add(board);
    }
}

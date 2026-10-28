using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SpotlightShowCurves()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Tag: Local draft
        var draftTag = SpotlightKit.Tag(Draft.UnsentChanges > 0 ? "Local draft" : "Synchronized");
        Canvas.SetLeft(draftTag, 302);
        Canvas.SetTop(draftTag, 144);
        canvas.Children.Add(draftTag);

        var noteText = new TextBlock
        {
            Text = "Not written to controller memory.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 17,
            Foreground = SpotlightKit.B("#b3a48e")
        };
        Canvas.SetLeft(noteText, 410);
        Canvas.SetTop(noteText, 146);
        canvas.Children.Add(noteText);

        // Tabs: Left stick · Right stick · Triggers
        var tabs = SpotlightKit.Tabs(new[] { "Left stick", "Right stick", "Triggers" }, 0);
        Canvas.SetLeft(tabs, 302);
        Canvas.SetTop(tabs, 196);
        canvas.Children.Add(tabs);

        // Lighting Desk at (292, 258, w = 620, h = 470)
        var deskBg = new Border
        {
            Width = 620, Height = 470,
            Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(SpotlightKit.C("#1b120e"), 0.0), new(SpotlightKit.C("#120b09"), 1.0) }
            },
            BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.25)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 50, ShadowDepth = 30, Opacity = 0.6 }
        };
        Canvas.SetLeft(deskBg, 292);
        Canvas.SetTop(deskBg, 258);
        canvas.Children.Add(deskBg);

        // Desk Header bar
        var deskHeader = new Border
        {
            Width = 620, Height = 44,
            Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(SpotlightKit.C("#2a1c15"), 0.0), new(SpotlightKit.C("#1b120e"), 1.0) }
            },
            BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.30)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(24, 0, 0, 0)
        };
        deskHeader.Child = new TextBlock
        {
            Text = "LEFT STICK",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetLeft(deskHeader, 292);
        Canvas.SetTop(deskHeader, 258);
        canvas.Children.Add(deskHeader);

        // 3 Vertical Faders: Inner dead zone (0%), Anti-dead zone (0%), Curve (50%)
        var fader1 = SpotlightKit.Fader(380, 340, 290, 0, true, "Inner dead zone", "0%");
        var fader2 = SpotlightKit.Fader(530, 340, 290, 0, false, "Anti-dead zone", "0%");
        var fader3 = SpotlightKit.Fader(680, 340, 290, 50, false, "Curve", "50%");
        canvas.Children.Add(fader1);
        canvas.Children.Add(fader2);
        canvas.Children.Add(fader3);

        // Cue card at (960, 258, w = 600, label = "Response")
        var plotCanvas = new Canvas { Width = 520, Height = 220, ClipToBounds = false };

        // Grid lines
        var axisPath = new Path
        {
            Data = Geometry.Parse("M0 0V220H520"),
            Stroke = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.35)),
            StrokeThickness = 1
        };
        plotCanvas.Children.Add(axisPath);

        for (int i = 1; i < 4; i++)
        {
            var gridLine = new Line
            {
                X1 = 520 * i / 4.0, Y1 = 0,
                X2 = 520 * i / 4.0, Y2 = 220,
                Stroke = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.08)),
                StrokeThickness = 1
            };
            plotCanvas.Children.Add(gridLine);
        }

        // Response curve path
        var curveGeom = new PathGeometry();
        var curveFig = new PathFigure { StartPoint = new Point(0, 220), IsClosed = false };
        curveFig.Segments.Add(new BezierSegment(new Point(180, 190), new Point(340, 60), new Point(520, 0), true));
        curveGeom.Figures.Add(curveFig);

        var curvePath = new Path
        {
            Data = curveGeom,
            Stroke = SpotlightKit.B("#ffd9a0"),
            StrokeThickness = 2.6,
            Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffb860"), BlurRadius = 6, ShadowDepth = 0, Opacity = 0.8 }
        };
        plotCanvas.Children.Add(curvePath);

        // Current point indicator
        var curPoint = new Ellipse
        {
            Width = 12, Height = 12,
            Fill = SpotlightKit.B("#fff6e6"),
            Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffb860"), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(curPoint, 286 - 6);
        Canvas.SetTop(curPoint, 110 - 6);
        plotCanvas.Children.Add(curPoint);

        // Labels
        var inText = new TextBlock { Text = "Input %", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush };
        Canvas.SetLeft(inText, 0); Canvas.SetTop(inText, 224); plotCanvas.Children.Add(inText);

        var outText = new TextBlock { Text = "Output % ↑", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush };
        Canvas.SetRight(outText, 0); Canvas.SetTop(outText, 224); plotCanvas.Children.Add(outText);

        var plotInner = new StackPanel();
        plotInner.Children.Add(plotCanvas);

        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 44, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var kbBtn = SpotlightKit.TheatricalButton("Keyboard test", false, null, height: 42, fontSize: 12);
        actionsRow.Children.Add(kbBtn);

        var testNote = new TextBlock
        {
            Text = "Connect a controller or use Keyboard test",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        actionsRow.Children.Add(testNote);
        plotInner.Children.Add(actionsRow);

        var cueCard = SpotlightKit.CueCard(plotInner, 600, label: "Response", padding: new Thickness(40, 40, 40, 34));
        Canvas.SetLeft(cueCard, 960);
        Canvas.SetTop(cueCard, 258);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "curves",
            title: "Curves",
            lead: "",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Dashboard"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Adjust"), ("X", "Reset"), ("B", "Back") });

        spotlightPageHost.Children.Add(frame);
    }

    public void SpotlightShowAdvanced()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Chips: Left stick · Right stick
        var chips = new StackPanel { Orientation = Orientation.Horizontal };
        chips.Children.Add(SpotlightKit.Chip("Left stick", true));
        chips.Children.Add(new Border { Width = 10 });
        chips.Children.Add(SpotlightKit.Chip("Right stick", false));
        Canvas.SetLeft(chips, 302);
        Canvas.SetTop(chips, 186);
        canvas.Children.Add(chips);

        // Desk setting rows at (292, 236, w = 620)
        var rowsStack = new StackPanel { Width = 620 };
        rowsStack.Children.Add(SpotlightKit.Row("Gate shape", "Circular gate geometry", "Circular", true, null, 44));
        rowsStack.Children.Add(SpotlightKit.Row("Minimum output", "Anti-deadzone start", "0%", false, null, 44));
        rowsStack.Children.Add(SpotlightKit.Row("Maximum output", "Outer saturation clamp", "100%", false, null, 44));
        rowsStack.Children.Add(SpotlightKit.Row("Full output at", "Outer deadzone edge", "100%", false, null, 44));
        rowsStack.Children.Add(SpotlightKit.Row("Link sticks", "Apply settings to both sticks", "Off", false, null, 44));
        rowsStack.Children.Add(SpotlightKit.Row("Invert vertical axis", "Reverse Y axis direction", "Off", false, null, 44));
        Canvas.SetLeft(rowsStack, 292);
        Canvas.SetTop(rowsStack, 236);
        canvas.Children.Add(rowsStack);

        // Follow spot on tall fader at x = 1250, y = 250, h = 330
        var spot = SpotlightKit.FollowSpot(1250, 640, 200, -60, 0.20, pool: false);
        canvas.Children.Add(spot);

        var tallFader = SpotlightKit.Fader(1250, 250, 330, 0, true, "Gate shape", "Circular");
        canvas.Children.Add(tallFader);

        // Preset chips below fader at (1100, 660)
        var presetChips = new StackPanel { Orientation = Orientation.Horizontal, Width = 320, HorizontalAlignment = HorizontalAlignment.Center };
        presetChips.Children.Add(SpotlightKit.Chip("Linear", true));
        presetChips.Children.Add(new Border { Width = 8 });
        presetChips.Children.Add(SpotlightKit.Chip("Aggressive", false));
        presetChips.Children.Add(new Border { Width = 8 });
        presetChips.Children.Add(SpotlightKit.Chip("Instant", false));
        presetChips.Children.Add(new Border { Width = 8 });
        presetChips.Children.Add(SpotlightKit.Chip("Smooth", false));
        Canvas.SetLeft(presetChips, 1090);
        Canvas.SetTop(presetChips, 660);
        canvas.Children.Add(presetChips);

        // Cue card at (980, 712, w = 580)
        var cardInner = new StackPanel();
        cardInner.Children.Add(new TextBlock
        {
            Text = "Gate & Travel Limits",
            FontFamily = SpotlightKit.Body,
            FontSize = 21,
            Foreground = SpotlightKit.ChalkBrush
        });
        cardInner.Children.Add(new TextBlock
        {
            Text = "Calibrate physical stick range and diagonal acceleration.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var applyBtn = SpotlightKit.TheatricalButton("Apply to right stick", true, "A", height: 40, fontSize: 11);
        btnRow.Children.Add(applyBtn);

        var resetBtn = SpotlightKit.TheatricalButton("Reset selected", false, null, height: 40, fontSize: 11);
        resetBtn.Margin = new Thickness(12, 0, 0, 0);
        btnRow.Children.Add(resetBtn);
        cardInner.Children.Add(btnRow);

        var cueCard = SpotlightKit.CueCard(cardInner, 580, padding: new Thickness(24, 20, 24, 20));
        Canvas.SetLeft(cueCard, 980);
        Canvas.SetTop(cueCard, 712);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "curves",
            title: "Advanced stick tools",
            lead: "Calibrate physical stick gate, dead zones, and diagonal curve scaling.",
            backLabel: "Done",
            onBack: () => SpotlightSelectPage("Curves"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Apply"), ("B", "Done") },
            leadW: 640);

        spotlightPageHost.Children.Add(frame);
    }
}

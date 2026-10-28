using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SpotlightShowDiagnostics()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Chips bar at (302, 226)
        var chipsBar = new StackPanel { Orientation = Orientation.Horizontal };
        chipsBar.Children.Add(SpotlightKit.Chip("Player input · XInput 1", true));
        chipsBar.Children.Add(new Border { Width = 8 });
        chipsBar.Children.Add(SpotlightKit.Chip("Raw Input", false));
        chipsBar.Children.Add(new Border { Width = 8 });
        chipsBar.Children.Add(SpotlightKit.Chip("Polling rate 250Hz", false));
        chipsBar.Children.Add(new Border { Width = 8 });
        chipsBar.Children.Add(SpotlightKit.Chip("Latency 4ms", false));
        Canvas.SetLeft(chipsBar, 302);
        Canvas.SetTop(chipsBar, 226);
        canvas.Children.Add(chipsBar);

        // Soft spotlight behind stick gobos
        var spot = SpotlightKit.FollowSpot(655, 560, 380, -60, 0.14, pool: false);
        canvas.Children.Add(spot);

        // 2 Stick Gobos: Left stick at cx = 480, Right stick at cx = 830, cy = 430, r = 120
        var goboL = SpotlightKit.Gobo(480, 430, 120, "Left stick", "X: 0.00 · Y: 0.00");
        var goboR = SpotlightKit.Gobo(830, 430, 120, "Right stick", "X: 0.00 · Y: 0.00");
        canvas.Children.Add(goboL);
        canvas.Children.Add(goboR);

        // 2 Trigger Faders at x = 1150 and x = 1330, y = 320, h = 240
        var faderLT = SpotlightKit.Fader(1150, 320, 240, 0, false, "LT", "0%");
        var faderRT = SpotlightKit.Fader(1330, 320, 240, 0, false, "RT", "0%");
        canvas.Children.Add(faderLT);
        canvas.Children.Add(faderRT);

        // Cue card at bottom at (292, 690, w = 1260)
        var cardInner = new Grid();
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var rateStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        rateStack.Children.Add(new TextBlock
        {
            Text = "POLLING RATE",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.MuteBrush
        });
        rateStack.Children.Add(new TextBlock
        {
            Text = "250 Hz · 4.0 ms",
            FontFamily = SpotlightKit.Body,
            FontSize = 21,
            Foreground = SpotlightKit.ChalkBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(rateStack, 0);
        cardInner.Children.Add(rateStack);

        var noteText = new TextBlock
        {
            Text = "Listening only. Raw input events are captured directly from controller hardware without modification.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = SpotlightKit.ChalkDimBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(noteText, 2);
        cardInner.Children.Add(noteText);

        var cueCard = SpotlightKit.CueCard(cardInner, 1260, padding: new Thickness(30, 18, 30, 18));
        Canvas.SetLeft(cueCard, 292);
        Canvas.SetTop(cueCard, 690);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "device",
            title: "Diagnostics",
            lead: "Monitor real-time sensor output, trigger deflection, and bus polling frequency.",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Device"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Select channel"), ("X", "Reset counters"), ("B", "Back") },
            leadW: 1220,
            titleSize: 36);

        spotlightPageHost.Children.Add(frame);
    }

    private void SpotlightShowMouse()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Setting rows at (292, 176, w = 640)
        var topRows = new StackPanel { Width = 640 };
        topRows.Children.Add(SpotlightKit.Row("Desktop mouse control", "Emulate cursor via controller", "Off", false, null, 46));
        topRows.Children.Add(SpotlightKit.Row("Pointer source", "Stick used for cursor motion", "Left stick", false, null, 46));
        Canvas.SetLeft(topRows, 292);
        Canvas.SetTop(topRows, 176);
        canvas.Children.Add(topRows);

        // Controller in follow spot at cx = 620, cy = 560, w = 330
        double cx = 620, cy = 560, w = 330;
        var spot = SpotlightKit.FollowSpot(cx, 650, 210, -60, 0.22);
        canvas.Children.Add(spot);

        var star = SpotlightParts.SpotlightStar(Model.Id, cx, 640, w, reflection: 0.12);
        canvas.Children.Add(star);

        // Header for blocking notes
        var secHead = new TextBlock
        {
            Text = "STAGE DIRECTIONS · BLOCKING NOTES",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        };
        Canvas.SetLeft(secHead, 302);
        Canvas.SetTop(secHead, 300);
        canvas.Children.Add(secHead);

        // Radial button notes around the controller
        var radialCallouts = new (string Key, string Action, double Angle)[]
        {
            ("Y", "Double click", -90),
            ("B", "Right click", -45),
            ("A", "Left click", 0),
            ("X", "Middle click", 45),
            ("RB", "Scroll down", 90),
            ("RT", "Drag / select", 135),
            ("LT", "Precision mode", 180),
            ("LB", "Scroll up", 225)
        };

        double rx = 280, ry = 170;
        foreach (var (k, act, angleDeg) in radialCallouts)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double bx = cx + rx * Math.Cos(rad);
            double by = cy - 30 + ry * Math.Sin(rad);
            bool right = Math.Cos(rad) >= -0.05;

            var callout = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Width = 200,
                HorizontalAlignment = right ? HorizontalAlignment.Left : HorizontalAlignment.Right
            };

            var kEl = SpotlightKit.BrassKey(k, k == "A", 24);
            var actEl = new TextBlock
            {
                Text = act,
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 15,
                Foreground = (k == "A") ? SpotlightKit.GoldHiBrush : SpotlightKit.B("#b3a48e"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };

            if (right)
            {
                callout.Children.Add(kEl);
                callout.Children.Add(actEl);
            }
            else
            {
                callout.Children.Add(actEl);
                callout.Children.Add(kEl);
            }

            Canvas.SetLeft(callout, bx - (right ? 10 : 190));
            Canvas.SetTop(callout, by - 12);
            canvas.Children.Add(callout);
        }

        // Cue card at (1030, 150, w = 530)
        var cardInner = new StackPanel();
        cardInner.Children.Add(new TextBlock { Text = "Cursor Dynamics & Acceleration", FontFamily = SpotlightKit.Body, FontSize = 21, Foreground = SpotlightKit.ChalkBrush });
        cardInner.Children.Add(new TextBlock { Text = "Configure desktop pointer precision and scrolling rates.", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush, Margin = new Thickness(0, 2, 0, 14) });

        cardInner.Children.Add(SpotlightKit.Row("Pointer speed", "Base cursor velocity", "12 px/tick", true, null, 40));
        cardInner.Children.Add(SpotlightKit.Row("Acceleration curve", "Non-linear stick speed ramp", "Smooth", false, null, 40));
        cardInner.Children.Add(SpotlightKit.Row("Scroll speed", "Vertical scroll rate", "3 lines", false, null, 40));
        cardInner.Children.Add(SpotlightKit.Row("Invert axes", "Horizontal and vertical inversion", "Standard", false, null, 40));
        cardInner.Children.Add(SpotlightKit.Row("Dead zone", "Minimum movement threshold", "5%", false, null, 40));

        var cueCard = SpotlightKit.CueCard(cardInner, 530, label: "Motion Profile", padding: new Thickness(26, 22, 26, 22));
        Canvas.SetLeft(cueCard, 1030);
        Canvas.SetTop(cueCard, 150);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "mouse",
            title: "Mouse mode",
            lead: "Control your desktop pointer, scrolling and mouse clicks using the gamepad.",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Dashboard"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Toggle Mouse mode"), ("X", "Reset defaults"), ("B", "Back") },
            leadW: 600,
            titleSize: 36);

        spotlightPageHost.Children.Add(frame);
    }
}

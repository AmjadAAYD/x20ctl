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
    private void SpotlightShowPcMode()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Overhead rig bar at (320, 214, w = 1220, h = 8)
        var rigBar = new Rectangle
        {
            Width = 1220, Height = 8,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(SpotlightKit.C("#5a4a3a"), 0.0), new(SpotlightKit.C("#2a2018"), 1.0) }
            },
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 4, Opacity = 0.6 }
        };
        Canvas.SetLeft(rigBar, 320);
        Canvas.SetTop(rigBar, 214);
        canvas.Children.Add(rigBar);

        // 4 Suspended Rig Lamps
        string[] stages = { "Physical input", "Filter", "Virtual Xbox", "Game / OS" };
        for (int i = 0; i < 4; i++)
        {
            double lx = 420 + i * 340;
            bool on = (i == 0); // Only first lamp lit when PC mode off/idle

            // Lamp housing
            var housing = new Border
            {
                Width = 36, Height = 46,
                CornerRadius = new CornerRadius(4, 4, 10, 10),
                Background = new LinearGradientBrush
                {
                    StartPoint = new(0, 0), EndPoint = new(0, 1),
                    GradientStops = { new(SpotlightKit.C("#3a2e24"), 0.0), new(SpotlightKit.C("#16100c"), 1.0) }
                },
                BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.30)),
                BorderThickness = new Thickness(1)
            };
            Canvas.SetLeft(housing, lx - 18);
            Canvas.SetTop(housing, 222);
            canvas.Children.Add(housing);

            // Bulb
            var bulb = new Ellipse
            {
                Width = 24, Height = 8,
                Fill = on ? SpotlightKit.B("#fff2d0") : SpotlightKit.B("#2a1f18")
            };
            if (on)
            {
                bulb.Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffd296"), BlurRadius = 20, ShadowDepth = 0, Opacity = 0.8 };
            }
            Canvas.SetLeft(bulb, lx - 12);
            Canvas.SetTop(bulb, 262);
            canvas.Children.Add(bulb);

            // Light cone if on
            if (on)
            {
                var spot = SpotlightKit.FollowSpot(lx, 400, 120, 266, 0.28, pool: true);
                canvas.Children.Add(spot);
            }

            // Lamp label
            var labelStack = new StackPanel { Width = 240 };
            labelStack.Children.Add(new TextBlock
            {
                Text = SpotlightKit.Roman(i),
                FontFamily = SpotlightKit.Display,
                FontSize = 11,
                FontWeight = FontWeights.Light,
                Foreground = on ? SpotlightKit.GoldHiBrush : SpotlightKit.B("#7a6c5a"),
                TextAlignment = TextAlignment.Center
            });
            labelStack.Children.Add(new TextBlock
            {
                Text = stages[i],
                FontFamily = SpotlightKit.Body,
                FontSize = 19,
                Foreground = on ? SpotlightKit.B("#fff6e6") : SpotlightKit.MuteBrush,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Canvas.SetLeft(labelStack, lx - 120);
            Canvas.SetTop(labelStack, 420);
            canvas.Children.Add(labelStack);
        }

        // Subtitle line at (302, 482)
        var subText = new TextBlock
        {
            Text = "Raw Input / XInput · Kernel hook · ViGEmBus driver · Emulated device",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.MuteBrush
        };
        Canvas.SetLeft(subText, 302);
        Canvas.SetTop(subText, 482);
        canvas.Children.Add(subText);

        // Desk setting rows at (292, 520, w = 600)
        var rowsStack = new StackPanel { Width = 600 };
        rowsStack.Children.Add(SpotlightKit.Row("PC mode translation", "Create virtual Xbox 360 controller", "Off", false, null, 44));
        rowsStack.Children.Add(SpotlightKit.Row("Virtual driver", "ViGEmBus kernel driver", "Installed", true, null, 44));
        rowsStack.Children.Add(SpotlightKit.Row("Turbo repeat rate", "Rapid-fire button frequency", "12 Hz", false, null, 44));
        rowsStack.Children.Add(SpotlightKit.Row("Hide physical gamepad", "Prevent duplicate inputs via HidHide", "Active", false, null, 44));
        Canvas.SetLeft(rowsStack, 292);
        Canvas.SetTop(rowsStack, 520);
        canvas.Children.Add(rowsStack);

        // Cue card at (940, 500, w = 620, label = "Before the curtain")
        var cardInner = new StackPanel();
        cardInner.Children.Add(new TextBlock
        {
            Text = "Virtual Driver Installation",
            FontFamily = SpotlightKit.Body,
            FontSize = 21,
            Foreground = SpotlightKit.ChalkBrush
        });

        string[] steps =
        {
            "ViGEmBus creates virtual Xbox 360 devices that games accept automatically.",
            "HidHide prevents double-input artifacts in legacy titles.",
            "Drivers can be removed cleanly at any time through Windows Settings."
        };
        for (int i = 0; i < steps.Length; i++)
        {
            var sRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            sRow.Children.Add(new TextBlock
            {
                Text = $"{SpotlightKit.Roman(i)}.",
                FontFamily = SpotlightKit.Bodoni,
                FontSize = 18,
                Width = 30,
                Foreground = SpotlightKit.GoldBrush
            });
            sRow.Children.Add(new TextBlock
            {
                Text = steps[i],
                FontFamily = SpotlightKit.Body,
                FontSize = 15.5,
                Foreground = SpotlightKit.ChalkDimBrush,
                Width = 530,
                TextWrapping = TextWrapping.Wrap
            });
            cardInner.Children.Add(sRow);
        }

        var btnRow = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        var installBtn = SpotlightKit.TheatricalButton("Install virtual drivers", true, "A", height: 44, fontSize: 13);
        installBtn.HorizontalAlignment = HorizontalAlignment.Left;
        btnRow.Children.Add(installBtn);
        cardInner.Children.Add(btnRow);

        var cueCard = SpotlightKit.CueCard(cardInner, 620, label: "Before the curtain", padding: new Thickness(28, 24, 28, 24));
        Canvas.SetLeft(cueCard, 940);
        Canvas.SetTop(cueCard, 500);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "pcmode",
            title: "PC mode",
            lead: "Route controller inputs through high-speed virtual device emulation.",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Dashboard"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Toggle PC mode"), ("X", "Driver options"), ("B", "Back") },
            leadW: 1100);

        spotlightPageHost.Children.Add(frame);
    }

    private void SpotlightShowDevice()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Controller in follow spot at cx = 600, base = 600, w = 480
        double cx = 600, baseFloor = 600, w = 480;
        var spot = SpotlightKit.FollowSpot(cx, 640, 250, -60, 0.28);
        canvas.Children.Add(spot);

        var star = SpotlightParts.SpotlightStar(Model.Id, cx, baseFloor, w, reflection: 0.18);
        canvas.Children.Add(star);

        // Identity below controller
        var idStack = new StackPanel { Width = 500 };
        idStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER IDENTITY",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush,
            TextAlignment = TextAlignment.Center
        });
        idStack.Children.Add(new TextBlock
        {
            Text = $"{Model.Name} · Shadow",
            FontFamily = SpotlightKit.Body,
            FontSize = 23,
            Foreground = SpotlightKit.ChalkBrush,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var tagBorder = SpotlightKit.Tag("Connected via USB", false);
        tagBorder.HorizontalAlignment = HorizontalAlignment.Center;
        tagBorder.Margin = new Thickness(0, 8, 0, 0);
        idStack.Children.Add(tagBorder);

        Canvas.SetLeft(idStack, 350);
        Canvas.SetTop(idStack, 670);
        canvas.Children.Add(idStack);

        // Cue card on right at (950, 170, w = 610, label = "Programme note")
        var cardInner = new StackPanel();
        cardInner.Children.Add(SpotlightKit.Row("Firmware version", "Hardware controller runtime", "v1.0.4", false, null, 44));
        cardInner.Children.Add(SpotlightKit.Row("Hardware revision", "Motherboard revision", "Rev 2.1", false, null, 44));
        cardInner.Children.Add(SpotlightKit.Row("Battery status", "Power level and charge state", "Not reported", false, null, 44));
        cardInner.Children.Add(SpotlightKit.Row("Transport protocol", "Active transport mechanism", "USB HID", false, null, 44));
        cardInner.Children.Add(SpotlightKit.Row("Calibration storage", "Controller EEPROM memory", "16 KB flash", false, null, 44));

        var updateRow = new Grid { Margin = new Thickness(0, 20, 0, 0) };
        updateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        updateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var updateInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        updateInfo.Children.Add(new TextBlock { Text = "Firmware Update · Current", FontFamily = SpotlightKit.Body, FontSize = 21, Foreground = SpotlightKit.ChalkBrush });
        updateInfo.Children.Add(new TextBlock { Text = "Latest stable release verified for this model.", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 13.5, Foreground = SpotlightKit.MuteBrush, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(updateInfo, 0);
        updateRow.Children.Add(updateInfo);

        var checkBtn = SpotlightKit.TheatricalButton("Check update", true, "A", height: 40, fontSize: 11);
        Grid.SetColumn(checkBtn, 1);
        updateRow.Children.Add(checkBtn);
        cardInner.Children.Add(updateRow);

        var cueCard = SpotlightKit.CueCard(cardInner, 610, label: "Programme note", padding: new Thickness(24, 20, 24, 20));
        Canvas.SetLeft(cueCard, 950);
        Canvas.SetTop(cueCard, 170);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "device",
            title: "Device",
            lead: "Hardware specifications, firmware status and connection identity.",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Dashboard"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Check firmware"), ("X", "Diagnostics"), ("B", "Back") },
            leadW: 640);

        spotlightPageHost.Children.Add(frame);
    }
}

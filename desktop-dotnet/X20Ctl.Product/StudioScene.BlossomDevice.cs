using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;

namespace X20Ctl.Product;

/// <summary>
/// Device workbench presentation for the Yozakura (Blossom) theme:
/// "The pad sits on a low wooden stand under one lantern;
/// its facts are painted on a festival stall's wooden menu board hanging from iron chains."
/// Strictly implements t_blossom.py:device().
/// </summary>
public partial class StudioScene
{
    private bool blossomDeviceWired;
    private void RenderBlossomDevice(Canvas host)
    {
        if (!blossomDeviceWired && Device != null) { blossomDeviceWired = true; Device.Watch(BlossomLater); }

        // 1. Sky & Backdrop
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(240, 160, 620, 500), 10, 8));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "device");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "Device",
            FontFamily = BlossomKit.Display,
            FontSize = 52,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        host.Children.Add(titleTb);

        var leadTb = new TextBlock
        {
            Text = "Hardware identification, firmware revision, and low-level sensor telemetry.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        host.Children.Add(leadTb);

        // 3. Left Side: Pad on Stand under Hanging Lantern at cx = 520
        double cx = 520;

        // Overhead Paper Lantern
        var lantern = BlossomKit.PaperLantern(54, 70, true, "\uE964", null, 0.9, 24);
        Canvas.SetLeft(lantern, cx - lantern.Width / 2.0);
        Canvas.SetTop(lantern, 140);
        host.Children.Add(lantern);

        // Light cone beaming down onto low stand
        var cone = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse($"M {cx - 20:F1} 234 L {cx + 20:F1} 234 L {cx + 180:F1} 482 L {cx - 180:F1} 482 Z"),
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(70, 255, 190, 120), 0.0),
                    new GradientStop(Colors.Transparent, 1.0)
                }
            }
        };
        host.Children.Add(cone);

        // Low Dark Wooden Stand
        var standBack = new Ellipse { Width = 460, Height = 48, Fill = BlossomKit.B(BlossomKit.WoodTimber) };
        Canvas.SetLeft(standBack, cx - 230);
        Canvas.SetTop(standBack, 460);
        host.Children.Add(standBack);

        var standTop = new Ellipse { Width = 460, Height = 44, Fill = BlossomKit.B(BlossomKit.WoodMid) };
        Canvas.SetLeft(standTop, cx - 230);
        Canvas.SetTop(standTop, 454);
        host.Children.Add(standTop);

        var standGlow = new Ellipse
        {
            Width = 380,
            Height = 30,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(60, 255, 170, 110), 0.0),
                    new GradientStop(Colors.Transparent, 1.0)
                }
            },
            Effect = new BlurEffect { Radius = 8 }
        };
        Canvas.SetLeft(standGlow, cx - 190);
        Canvas.SetTop(standGlow, 460);
        host.Children.Add(standGlow);

        // Controller Art on Stand
        var padImg = BlossomKit.ControllerArt(Model.Id, 340);
        if (padImg != null)
        {
            padImg.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 18, Opacity = 0.65 };
            Canvas.SetLeft(padImg, cx - 170);
            Canvas.SetTop(padImg, 320);
            host.Children.Add(padImg);
        }

        // Left Info Stack
        var leftStack = new StackPanel { Width = 600 };
        leftStack.Children.Add(new TextBlock
        {
            Text = "PLAYER 1",
            FontFamily = BlossomKit.Small,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.TextSakuraBrush
        });

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 16) };
        titleRow.Children.Add(new TextBlock
        {
            Text = Model.Name,
            FontFamily = BlossomKit.Heading,
            FontSize = 24,
            Foreground = BlossomKit.TextWhiteBrush,
            Margin = new Thickness(0, 0, 14, 0)
        });
        var assignedTag = new Border
        {
            Padding = new Thickness(12, 4, 12, 4),
            CornerRadius = new CornerRadius(3),
            BorderBrush = BlossomKit.B("rgba(255,190,130,0.7)"),
            BorderThickness = new Thickness(1.3),
            Child = new TextBlock
            {
                Text = "ASSIGNED MODEL",
                FontFamily = BlossomKit.Small,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BlossomKit.TextAmberBrush
            }
        };
        titleRow.Children.Add(assignedTag);
        leftStack.Children.Add(titleRow);

        leftStack.Children.Add(new TextBlock
        {
            Text = "The model assigned to this player. Nothing is read from the controller here.",
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            LineHeight = 22,
            Foreground = BlossomKit.TextWhiteBrush,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var calibBtn = new Border
        {
            Height = 46,
            Padding = new Thickness(24, 0, 24, 0),
            CornerRadius = new CornerRadius(23),
            Background = BlossomKit.CreateWarmLanternBrush(),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.6 }
        };
        var btnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        btnStack.Children.Add(CreateKeyBadge("A", true));
        btnStack.Children.Add(new TextBlock
        {
            Text = "Calibrate sensors",
            FontFamily = BlossomKit.Body,
            FontSize = 17,
            Foreground = BlossomKit.B("#4a140f"),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        calibBtn.Child = btnStack;

        Canvas.SetLeft(leftStack, 244);
        Canvas.SetTop(leftStack, 530);
        host.Children.Add(leftStack);

        // 4. Right Side: Suspended Wooden Tea-Stall Menu Board at (900, 180, width 644, height 640)
        // Suspension chains
        host.Children.Add(new Line { X1 = 1010, Y1 = 0, X2 = 1010, Y2 = 180, Stroke = BlossomKit.B("#2a1418"), StrokeThickness = 2 });
        host.Children.Add(new Line { X1 = 1430, Y1 = 0, X2 = 1430, Y2 = 180, Stroke = BlossomKit.B("#2a1418"), StrokeThickness = 2 });

        var boardBorder = new Border
        {
            Width = 644,
            Height = 640,
            CornerRadius = new CornerRadius(4),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x3d, 0x26, 0x19), 0.0),
                    new GradientStop(Color.FromRgb(0x21, 0x13, 0x0c), 1.0)
                }
            },
            BorderBrush = BlossomKit.B("#2a170f"),
            BorderThickness = new Thickness(6),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 40, ShadowDepth = 24, Opacity = 0.55 }
        };
        Canvas.SetLeft(boardBorder, 900);
        Canvas.SetTop(boardBorder, 180);
        host.Children.Add(boardBorder);

        // Board Inscription Content
        var boardContent = new StackPanel { Width = 584, Margin = new Thickness(30, 24, 30, 24) };

        // Section: Device
        boardContent.Children.Add(new TextBlock
        {
            Text = "DEVICE",
            FontFamily = BlossomKit.Small,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.B("#ffb56b"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var devProps = new (string Key, string Val)[]
        {
            ("Model", Model.Name),
            ("Hardware revision", Device?.RevisionText ?? "Not reported"),
            ("Firmware", Device?.FirmwareText ?? "Not reported"),
            ("Battery Level", Device?.BatteryText ?? "Not reported"),
            ("Connection", Device?.ConnectionText ?? "Not detected")
        };
        foreach (var (k, v) in devProps)
        {
            var row_ = new Grid { Height = 42 };
            row_.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row_.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row_.Children.Add(new TextBlock { Text = k, FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.B("#f3e2c8"), VerticalAlignment = VerticalAlignment.Center });
            var valTb = new TextBlock { Text = v, FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.B("#b89a80"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(valTb, 1);
            row_.Children.Add(valTb);
            boardContent.Children.Add(row_);
        }

        // Section: Capabilities
        boardContent.Children.Add(new TextBlock
        {
            Text = "CAPABILITIES",
            FontFamily = BlossomKit.Small,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.B("#ffb56b"),
            Margin = new Thickness(0, 24, 0, 8)
        });

        var caps = new (string Key, string Val)[]
        {
            ("Polling rate", "Not measured"),
            ("Transport", Device?.TransportText ?? "Not reported"),
            ("Configuration writes", "Locked"),
            ("Local drafts", ModelProfiles.For(Model.Id).SupportsLocalDrafts ? "Available" : "Unavailable"),
            ("Rear artwork", HasRearArtwork ? "Available" : "Unavailable")
        };
        foreach (var (k, v) in caps)
        {
            var row_ = new Grid { Height = 42 };
            row_.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row_.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row_.Children.Add(new TextBlock { Text = k, FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.B("#b89a80"), VerticalAlignment = VerticalAlignment.Center });
            var valTb = new TextBlock { Text = v, FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.B("#f3e2c8"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(valTb, 1);
            row_.Children.Add(valTb);
            boardContent.Children.Add(row_);
        }

        boardBorder.Child = boardContent;

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SunriseShowPcMode()
    {
        if (sunrisePageHost == null) return;
        sunrisePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateSunriseHeader("Studio"));
        var sidebar = CreateSunriseSidebar("PC Mode");
        Canvas.SetLeft(sidebar, 16);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area
        var content = new Canvas { Width = 1300, Height = 770 };
        Canvas.SetLeft(content, 272);
        Canvas.SetTop(content, 80);

        var titleSp = SunriseKit.SunriseTitle("PC MODE EMULATION", "Virtual Xbox 360 and DualShock 4 controller routing for Windows gaming.");
        Canvas.SetLeft(titleSp, 10);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT STICKER CARD: Target Protocol Selector
        // ==========================================
        var leftSp = new StackPanel { Width = 560 };

        var protoTitle = new TextBlock
        {
            Text = "EMULATION PROTOCOL",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12.5,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        leftSp.Children.Add(protoTitle);

        var protoOptions = new (string Name, string Desc, bool Active, string Tag)[]
        {
            ("XBOX 360 CONTROLLER (XINPUT)", "Default for Steam, Game Pass, and Windows titles", true, "xinput"),
            ("DUALSHOCK 4 (DIRECTINPUT)", "PlayStation PC ports with touchpad / gyro support", false, "ds4"),
            ("NATIVE RAW HID PASSTHROUGH", "Direct hardware communication without emulation layer", false, "raw")
        };

        foreach (var opt in protoOptions)
        {
            var pCard = new Border
            {
                Background = opt.Active ? SunriseKit.InkBrush : SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:pcmode:{opt.Tag}"
            };
            var pSp = new StackPanel();
            pSp.Children.Add(new TextBlock { Text = opt.Name, FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = opt.Active ? SunriseKit.CreamBrush : SunriseKit.InkBrush });
            pSp.Children.Add(new TextBlock { Text = opt.Desc, FontFamily = SunriseKit.Body, FontSize = 11, Foreground = opt.Active ? SunriseKit.GoldBrush : SunriseKit.OliveBrush, Margin = new Thickness(0, 3, 0, 0) });
            pCard.Child = pSp;
            leftSp.Children.Add(pCard);
        }

        var pcmodeNote = new TextBlock
        {
            Text = Draft.PcMode ? "PC mode is active · Mappings and stick settings apply directly on this PC." : "PC mode is disabled · Enable it to remap inputs via virtual controller.",
            FontFamily = SunriseKit.Body,
            FontSize = 11.5,
            Foreground = SunriseKit.OliveBrush,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        leftSp.Children.Add(pcmodeNote);

        var leftCard = SunriseKit.CreateStickerCard(leftSp, 590, 520, cornerRadius: 22);
        Canvas.SetLeft(leftCard, 10);
        Canvas.SetTop(leftCard, 100);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT TV SCREEN: Driver Stack & Diagnostic
        // ==========================================
        var rightSp = new StackPanel { Width = 580 };

        var drvTitle = new TextBlock
        {
            Text = "KERNEL DRIVER STACK STATUS",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 13,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 14)
        };
        rightSp.Children.Add(drvTitle);

        var drivers = new (string Name, string Status, string Sub, bool Ok)[]
        {
            ("ViGEmBus Virtual Bus", "Active & Operational", "Virtual controller bus · Signed kernel driver", true),
            ("HidHide Input Masking", "Active & Masking", "Input device cloaking · Double-input blocked", true),
            ("Windows XInput Hook", "Ready", "4/4 Player slots available", true)
        };

        foreach (var drv in drivers)
        {
            var dCard = new Border
            {
                Background = SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var dGrid = new Grid();
            dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dLeft = new StackPanel();
            dLeft.Children.Add(new TextBlock { Text = drv.Name, FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.InkBrush });
            dLeft.Children.Add(new TextBlock { Text = drv.Sub, FontFamily = SunriseKit.Body, FontSize = 10.5, Foreground = SunriseKit.OliveBrush, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(dLeft, 0);
            dGrid.Children.Add(dLeft);

            var dBadge = new Border
            {
                Background = SunriseKit.InkBrush,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            dBadge.Child = new TextBlock { Text = drv.Status.ToUpperInvariant(), FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 10, Foreground = SunriseKit.YellowBrush };
            Grid.SetColumn(dBadge, 1);
            dGrid.Children.Add(dBadge);

            dCard.Child = dGrid;
            rightSp.Children.Add(dCard);
        }

        var reinstallBtn = SunriseKit.SunriseButton("Verify Driver Stack", () =>
        {
        }, isPrimary: true, width: 220, height: 42);
        reinstallBtn.HorizontalAlignment = HorizontalAlignment.Right;
        reinstallBtn.Margin = new Thickness(0, 14, 0, 0);
        rightSp.Children.Add(reinstallBtn);

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

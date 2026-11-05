using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool sunriseDeviceWatched;
    private bool sunriseResetConfirming;

    private void SunriseShowDevice()
    {
        if (sunrisePageHost == null) return;
        sunrisePageHost.Children.Clear();

        if (!sunriseDeviceWatched && Device != null)
        {
            sunriseDeviceWatched = true;
            Device.Watch(() => SunriseLater());
        }

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateSunriseHeader("Studio"));
        var sidebar = CreateSunriseSidebar("Device");
        Canvas.SetLeft(sidebar, 16);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area
        var content = new Canvas { Width = 1300, Height = 770 };
        Canvas.SetLeft(content, 272);
        Canvas.SetTop(content, 80);

        var titleSp = SunriseKit.SunriseTitle("CONTROLLER HARDWARE", "On-board firmware telemetry, hardware calibration, and memory management.");
        Canvas.SetLeft(titleSp, 10);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT STICKER CARD: Controller Art & Hardware Identity Tiles
        // ==========================================
        var leftSp = new StackPanel { Width = 560 };

        // Real controller image on pedestal
        var padArt = SunriseKit.ControllerArt(Model.Id, 420);
        padArt.HorizontalAlignment = HorizontalAlignment.Center;
        padArt.Margin = new Thickness(0, 0, 0, 10);
        leftSp.Children.Add(padArt);

        var hwTitle = new TextBlock
        {
            Text = "HARDWARE SPECIFICATIONS & TELEMETRY",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12.5,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        leftSp.Children.Add(hwTitle);

        var infoTiles = new (string Label, string Value, string Sub)[]
        {
            ("CONTROLLER MODEL", Model.Name, $"Hardware ID: {Model.Id.ToUpperInvariant()}"),
            ("FIRMWARE REVISION", Device?.FirmwareText ?? "Not reported", "Live firmware version report"),
            ("WIRELESS TRANSPORT", Device?.TransportText ?? "Standard HID", Device?.ConnectionText ?? "Input endpoint active"),
            ("POWER CELL STATE", Device?.BatteryText ?? "Not reported", "Live battery telemetry")
        };

        foreach (var info in infoTiles)
        {
            var tCard = new Border
            {
                Background = SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var tGrid = new Grid();
            tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tLeft = new StackPanel();
            tLeft.Children.Add(new TextBlock { Text = info.Label, FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = SunriseKit.OliveBrush });
            tLeft.Children.Add(new TextBlock { Text = info.Sub, FontFamily = SunriseKit.Body, FontSize = 10.5, Foreground = SunriseKit.OliveBrush, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(tLeft, 0);
            tGrid.Children.Add(tLeft);

            var tVal = new TextBlock
            {
                Text = info.Value,
                FontFamily = SunriseKit.Display,
                FontWeight = FontWeights.Black,
                FontSize = 13,
                Foreground = SunriseKit.InkBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tVal, 1);
            tGrid.Children.Add(tVal);

            tCard.Child = tGrid;
            leftSp.Children.Add(tCard);
        }

        var leftCard = SunriseKit.CreateStickerCard(leftSp, 590, 560, cornerRadius: 22);
        Canvas.SetLeft(leftCard, 10);
        Canvas.SetTop(leftCard, 90);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT TV SCREEN: Calibration & Safety Reset
        // ==========================================
        var rightSp = new StackPanel { Width = 560 };

        var calTitle = new TextBlock
        {
            Text = "HARDWARE ACTIONS & RECOVERY",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 13,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 12)
        };
        rightSp.Children.Add(calTitle);

        // Diagnostics & Refresh Card
        var actCard = new Border
        {
            Background = SunriseKit.CreamBrush,
            BorderBrush = SunriseKit.InkBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var actSp = new StackPanel();
        actSp.Children.Add(new TextBlock { Text = "HARDWARE TELEMETRY & DIAGNOSTICS", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12.5, Foreground = SunriseKit.InkBrush });
        actSp.Children.Add(new TextBlock { Text = "Inspect raw packet streams, link latency, and query current hardware state.", FontFamily = SunriseKit.Body, FontSize = 11, Foreground = SunriseKit.OliveBrush, Margin = new Thickness(0, 2, 0, 10) });

        var actBtns = new StackPanel { Orientation = Orientation.Horizontal };
        var diagBtn = SunriseKit.SunriseButton("Open Diagnostics", () =>
        {
            SunriseSelectPage("Diagnostics");
        }, isPrimary: false, width: 170, height: 36, tag: "theme:device:diag");
        diagBtn.Margin = new Thickness(0, 0, 10, 0);
        actBtns.Children.Add(diagBtn);

        Button? refreshBtn = null;
        refreshBtn = SunriseKit.SunriseButton("Refresh details", async () =>
        {
            if (Device != null)
            {
                refreshBtn!.IsEnabled = false;
                await Device.RefreshDetailsAsync();
                refreshBtn.IsEnabled = true;
                SunriseShowDevice();
            }
        }, isPrimary: false, width: 170, height: 36, tag: "theme:device:refresh");
        actBtns.Children.Add(refreshBtn);

        actSp.Children.Add(actBtns);
        actCard.Child = actSp;
        rightSp.Children.Add(actCard);

        // Factory Reset Section
        if (Model.Id == "x20_pro" && !Draft.PcMode)
        {
            var resetCard = new Border
            {
                Background = SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.DangerBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14)
            };
            var resetSp = new StackPanel();
            var resetHdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            resetHdr.Children.Add(new TextBlock { Text = "⚠️ ", FontSize = 14 });
            resetHdr.Children.Add(new TextBlock { Text = "FACTORY RESET (DANGER ZONE)", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Black, FontSize = 12.5, Foreground = SunriseKit.DangerBrush });
            resetSp.Children.Add(resetHdr);

            const string resetWarning = "Erases all settings saved on the controller.";
            var resetNote = new TextBlock
            {
                Text = resetWarning,
                FontFamily = SunriseKit.Body,
                FontSize = 11,
                Foreground = SunriseKit.OliveBrush,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            resetSp.Children.Add(resetNote);

            if (!sunriseResetConfirming)
            {
                var factoryBtn = SunriseKit.SunriseButton("Reset to Factory Defaults", () =>
                {
                    sunriseResetConfirming = true;
                    SunriseShowDevice();
                }, isPrimary: false, isDanger: true, width: 240, height: 38, tag: "theme:danger:factory-reset");
                resetSp.Children.Add(factoryBtn);
            }
            else
            {
                var confirmSp = new StackPanel { Orientation = Orientation.Horizontal };
                var cancelBtn = SunriseKit.SunriseButton("Cancel", () =>
                {
                    sunriseResetConfirming = false;
                    SunriseShowDevice();
                }, isPrimary: false, width: 100, height: 38, tag: "theme:danger:factory-reset-cancel");
                cancelBtn.Margin = new Thickness(0, 0, 10, 0);
                confirmSp.Children.Add(cancelBtn);

                var confirmBtn = SunriseKit.SunriseButton("Confirm Reset", async () =>
                {
                    sunriseResetConfirming = false;
                    await FactoryReset(say =>
                    {
                        resetNote.Text = say;
                        SunriseLater();
                    });
                }, isPrimary: true, isDanger: true, width: 160, height: 38, tag: "theme:danger:factory-reset-confirm");
                confirmSp.Children.Add(confirmBtn);
                resetSp.Children.Add(confirmSp);
            }

            resetCard.Child = resetSp;
            rightSp.Children.Add(resetCard);
        }
        else
        {
            var lockedCard = new Border
            {
                Background = SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14)
            };
            var lSp = new StackPanel();
            lSp.Children.Add(new TextBlock { Text = "HARDWARE RESET", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.InkBrush });
            lSp.Children.Add(new TextBlock
            {
                Text = Draft.PcMode ? "Factory reset is not available in PC mode · Controller memory remains protected." : "Factory reset is supported on X20 Pro controllers.",
                FontFamily = SunriseKit.Body,
                FontSize = 11,
                Foreground = SunriseKit.OliveBrush,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            lockedCard.Child = lSp;
            rightSp.Children.Add(lockedCard);
        }

        var tvCard = SunriseKit.CreateTvScreenCard(rightSp, 590, 500, withAntennae: true);
        Canvas.SetLeft(tvCard, 630);
        Canvas.SetTop(tvCard, 70);
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

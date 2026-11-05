using System.Windows.Controls.Primitives;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool hollowDeviceWired;

    private void HollowShowDevice()
    {
        if (hollowPageHost == null) return;
        hollowPageHost.Children.Clear();

        Device ??= new(Model, Player, this, RegisterFocus, () => ShowManagement("Diagnostics"), () => ShowManagement("Setups"), () => ShowManagement("Screen"), Model.Id == "x20_pro" && !Draft.PcMode ? FactoryReset : null);
        if (!hollowDeviceWired && Device != null)
        {
            hollowDeviceWired = true;
            Device.Watch(() => { if (currentHollowPage == "Device") HollowLater(); });
        }

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateHollowHeader("Studio"));
        var sidebar = CreateHollowSidebar("Device");
        Canvas.SetLeft(sidebar, 28);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area at x=286, y=80
        var content = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(content, 286);
        Canvas.SetTop(content, 80);

        var titleSp = HollowKit.HollowHeader("DEVICE INFORMATION", "Hardware identity, firmware status, and maintenance options.");
        Canvas.SetLeft(titleSp, 16);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT PANEL: Controller & 4 Identity Tiles
        // ==========================================
        var leftSp = new StackPanel { Width = 610 };

        // Controller Image on Pedestal
        var padCanvas = new Canvas { Width = 560, Height = 220 };
        var pedestal = new Ellipse
        {
            Width = 420,
            Height = 80,
            Fill = new RadialGradientBrush(Color.FromArgb(0x35, 0xb8, 0xcc, 0xe0), Colors.Transparent)
        };
        Canvas.SetLeft(pedestal, 70);
        Canvas.SetTop(pedestal, 120);
        padCanvas.Children.Add(pedestal);

        var padArt = HollowKit.ControllerArt(Model.Id, 340);
        Canvas.SetLeft(padArt, 110);
        Canvas.SetTop(padArt, 10);
        padCanvas.Children.Add(padArt);
        leftSp.Children.Add(padCanvas);

        // 4 Tiles: Battery, Firmware, Hardware ID, Transport (all bound to real Device telemetry)
        var tileGrid = new UniformGrid { Columns = 2, Rows = 2, Margin = new Thickness(0, 10, 0, 10) };
        tileGrid.Children.Add(CreateHollowDeviceTile("BATTERY", Device?.BatteryText ?? "Not reported", ""));
        tileGrid.Children.Add(CreateHollowDeviceTile("FIRMWARE", Device?.FirmwareText ?? "Not reported", ""));
        tileGrid.Children.Add(CreateHollowDeviceTile("HARDWARE ID", Device?.RevisionText ?? "Not reported", ""));
        tileGrid.Children.Add(CreateHollowDeviceTile("TRANSPORT", Device?.TransportText ?? "Not reported", ""));
        leftSp.Children.Add(tileGrid);

        // Real Model Capabilities from ModelProfiles
        var profile = ModelProfiles.For(Model.Id);
        var capsHeader = new TextBlock
        {
            Text = $"CAPABILITIES: {profile.Sticks} · {profile.Triggers} · {profile.Motors}",
            FontFamily = HollowKit.Display,
            FontSize = 10.5,
            Foreground = HollowKit.MutedTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        leftSp.Children.Add(capsHeader);

        var leftCard = HollowKit.FiligreeCard(leftSp, padding: 18, cornerSize: 14);
        leftCard.Width = 636;
        leftCard.Height = 580;
        Canvas.SetLeft(leftCard, 16);
        Canvas.SetTop(leftCard, 90);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT PANEL: Actions & Real Factory Reset
        // ==========================================
        var rightSp = new StackPanel { Width = 550 };

        var connTitle = new TextBlock
        {
            Text = "CONNECTION & RECOVERY",
            FontFamily = HollowKit.Display,
            FontSize = 14,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 12)
        };
        rightSp.Children.Add(connTitle);

        // Connection status
        var connRow = new TextBlock
        {
            Text = Device?.ConnectionText ?? "Not detected",
            FontFamily = HollowKit.Display,
            FontSize = 12.5,
            Foreground = HollowKit.SoftTextBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        rightSp.Children.Add(connRow);

        // Diagnostics Button
        var diagBtn = HollowKit.HollowButton("Open Diagnostics", () =>
        {
            HollowSelectPage("Diagnostics");
        }, isPrimary: false, height: 36, tag: "theme:device:diag");
        diagBtn.Margin = new Thickness(0, 0, 0, 8);
        rightSp.Children.Add(diagBtn);

        // Real Refresh Device Details button: calls real Device.RefreshDetailsAsync()
        Button? refreshBtn = null;
        refreshBtn = HollowKit.HollowButton("Refresh device details", async () =>
        {
            if (Device != null)
            {
                refreshBtn!.IsEnabled = false;
                await Device.RefreshDetailsAsync();
                refreshBtn.IsEnabled = true;
                HollowShowDevice();
            }
        }, isPrimary: false, height: 36, tag: "theme:device:refresh");
        refreshBtn.Margin = new Thickness(0, 0, 0, 14);
        rightSp.Children.Add(refreshBtn);

        rightSp.Children.Add(HollowKit.DiamondDivider(550, 16));

        // Real Factory Reset Section
        if (Model.Id == "x20_pro" && !Draft.PcMode)
        {
            var resetCard = new Border
            {
                Background = new SolidColorBrush(HollowKit.A(HollowKit.DangerBg, 0.45)),
                BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.DangerBorder, 0.6)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 10, 0, 0)
            };
            var rSp = new StackPanel();

            var rTitle = new TextBlock
            {
                Text = "FACTORY RESET",
                FontFamily = HollowKit.Display,
                FontSize = 13,
                Foreground = HollowKit.IvoryTextBrush,
                Margin = new Thickness(0, 0, 0, 6)
            };
            rSp.Children.Add(rTitle);

            const string resetWarning = "Erases the settings saved on the controller.";
            var resetNote = new TextBlock
            {
                Text = resetWarning,
                FontFamily = HollowKit.Body,
                FontSize = 12,
                Foreground = HollowKit.MutedTextBrush,
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap
            };
            rSp.Children.Add(resetNote);

            var confirmRow = new StackPanel { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 4) };

            Button? resetAsk = null;
            var resetNo = HollowKit.HollowButton("Cancel", () =>
            {
                confirmRow.Visibility = Visibility.Collapsed;
                resetNote.Text = resetWarning;
            }, isPrimary: false, width: 100, height: 32, tag: "theme:device:cancel-reset");
            resetNo.Margin = new Thickness(0, 0, 12, 0);

            var resetYes = HollowKit.HollowButton("Reset controller", async () =>
            {
                confirmRow.Visibility = Visibility.Collapsed;
                resetAsk!.IsEnabled = false;
                await FactoryReset(text => resetNote.Text = text);
                resetAsk.IsEnabled = true;
            }, isPrimary: true, width: 140, height: 32, tag: "theme:device:confirm-reset");

            confirmRow.Children.Add(resetNo);
            confirmRow.Children.Add(resetYes);

            resetAsk = HollowKit.HollowButton("Factory reset", () =>
            {
                confirmRow.Visibility = Visibility.Visible;
                resetNote.Text = "Reset this controller? This can't be undone.";
            }, isPrimary: false, width: 130, height: 32, tag: "theme:device:reset");

            rSp.Children.Add(resetAsk);
            rSp.Children.Add(confirmRow);
            resetCard.Child = rSp;
            rightSp.Children.Add(resetCard);
        }

        var rightCard = HollowKit.FiligreeCard(rightSp, padding: 22, cornerSize: 14);
        rightCard.Width = 590;
        rightCard.Height = 580;
        Canvas.SetLeft(rightCard, 674);
        Canvas.SetTop(rightCard, 90);
        content.Children.Add(rightCard);

        board.Children.Add(content);

        // Dock
        var dock = HollowKit.HollowDock("Device");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        board.Children.Add(dock);

        hollowPageHost.Children.Add(board);
    }

    private FrameworkElement CreateHollowDeviceTile(string title, string value, string icon)
    {
        var tile = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x05, 0x0c, 0x14)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(6)
        };
        var sp = new StackPanel();
        var headRow = new StackPanel { Orientation = Orientation.Horizontal };
        headRow.Children.Add(new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 12, Foreground = HollowKit.MutedTextBrush, Margin = new Thickness(0, 0, 6, 0) });
        headRow.Children.Add(new TextBlock { Text = title, FontFamily = HollowKit.Display, FontSize = 10.5, Foreground = HollowKit.MutedTextBrush });
        sp.Children.Add(headRow);

        var valBlock = new TextBlock
        {
            Text = value,
            FontFamily = HollowKit.Display,
            FontSize = 14,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 6, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        sp.Children.Add(valBlock);
        tile.Child = sp;
        return tile;
    }
}

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool nocturneDeviceWired;

    private void NocturneShowDevice()
    {
        if (nocturnePageHost == null) return;
        nocturnePageHost.Children.Clear();

        Device ??= new(Model, Player, this, RegisterFocus, () => ShowManagement("Diagnostics"), () => ShowManagement("Setups"), () => ShowManagement("Screen"), Model.Id == "x20_pro" && !Draft.PcMode ? FactoryReset : null);
        if (!nocturneDeviceWired && Device != null)
        {
            nocturneDeviceWired = true;
            Device.Watch(() => { if (currentNocturnePage == "Device") NocturneLater(); });
        }

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(NocturneKit.CreateCityBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateNocturneHeader("Studio"));
        var sidebar = CreateNocturneSidebar("Device");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 74);
        board.Children.Add(sidebar);

        // Title Block
        var title = NocturneKit.NocturneTitle("DEVICE", "INFORMATION", "Hardware identity, telemetry status, and maintenance options.");
        Canvas.SetLeft(title, 300);
        Canvas.SetTop(title, 70);
        board.Children.Add(title);

        // Left stage: Controller visual + Real Telemetry specs
        var leftStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
        leftStack.Children.Add(new TextBlock
        {
            Text = $"{Model.Name.ToUpperInvariant()} HARDWARE",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var padArt = NocturneKit.ControllerArt(Model.Id, 380);
        leftStack.Children.Add(padArt);

        var statsGrid = new UniformGrid { Columns = 2, Rows = 2, Margin = new Thickness(0, 14, 0, 0) };
        var stats = new (string K, string V)[]
        {
            ("Battery Level", Device?.BatteryText ?? "Not reported"),
            ("Firmware", Device?.FirmwareText ?? "Not reported"),
            ("Hardware ID", Device?.RevisionText ?? "Not reported"),
            ("Active Transport", Device?.TransportText ?? "Not reported")
        };
        foreach (var (k, v) in stats)
        {
            var sBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x18, 0x00, 0x56, 0xfb)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x56, 0xfb)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(4)
            };
            var sStack = new StackPanel();
            sStack.Children.Add(new TextBlock { Text = k.ToUpperInvariant(), FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 10, Foreground = NocturneKit.CobaltBrush });
            sStack.Children.Add(new TextBlock { Text = v, FontFamily = NocturneKit.Body, FontSize = 11.5, Foreground = NocturneKit.InkBrush, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            sBox.Child = sStack;
            statsGrid.Children.Add(sBox);
        }
        leftStack.Children.Add(statsGrid);

        var leftCard = NocturneKit.NocturneWhiteCard(leftStack, width: 570, height: 570);
        Canvas.SetLeft(leftCard, 300);
        Canvas.SetTop(leftCard, 220);
        board.Children.Add(leftCard);

        // Right stage: Maintenance & Real Factory Reset
        var rightStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
        rightStack.Children.Add(new TextBlock
        {
            Text = "MAINTENANCE & CALIBRATION",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var calibNotice = new TextBlock
        {
            Text = "Controller input and configuration connection are checked separately.",
            FontFamily = NocturneKit.Body,
            FontSize = 12.5,
            Foreground = NocturneKit.InkBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        };
        rightStack.Children.Add(calibNotice);

        // Diagnostics button
        var diagBtn = NocturneKit.NocturneButton("OPEN DIAGNOSTICS", () =>
        {
            ShowManagement("Diagnostics");
        }, isPrimary: false, width: null, height: 38, tag: "theme:device:diag");
        diagBtn.Margin = new Thickness(0, 0, 0, 10);
        rightStack.Children.Add(diagBtn);

        // Real Refresh Device Details button
        Button? refreshBtn = null;
        refreshBtn = NocturneKit.NocturneButton("REFRESH DEVICE DETAILS", async () =>
        {
            if (Device != null)
            {
                refreshBtn!.IsEnabled = false;
                await Device.RefreshDetailsAsync();
                refreshBtn.IsEnabled = true;
                NocturneShowDevice();
            }
        }, isPrimary: false, width: null, height: 38, tag: "theme:device:refresh");
        refreshBtn.Margin = new Thickness(0, 0, 0, 16);
        rightStack.Children.Add(refreshBtn);

        // Thin navy rule
        rightStack.Children.Add(new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 0, 0, 16) });

        // Real Factory Reset Section
        if (Model.Id == "x20_pro" && !Draft.PcMode)
        {
            rightStack.Children.Add(new TextBlock
            {
                Text = "FACTORY RESET (DANGER ZONE)",
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 18,
                Foreground = NocturneKit.DangerBrush,
                Margin = new Thickness(0, 0, 0, 6)
            });

            const string resetWarning = "Erases the settings saved on the controller.";
            var resetNote = new TextBlock
            {
                Text = resetWarning,
                FontFamily = NocturneKit.Body,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(0xbb, 0x01, 0x00, 0x42)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            rightStack.Children.Add(resetNote);

            var confirmSp = new StackPanel { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 8) };

            Button? resetBtn = null;
            var confirmBtn = NocturneKit.NocturneButton("Reset controller", async () =>
            {
                confirmSp.Visibility = Visibility.Collapsed;
                resetBtn!.IsEnabled = false;
                await FactoryReset(text => resetNote.Text = text);
                resetBtn.IsEnabled = true;
            }, isPrimary: true, isDanger: true, width: 160, height: 38, tag: "theme:device:confirm-reset");
            confirmBtn.Margin = new Thickness(0, 0, 10, 0);

            var cancelBtn = NocturneKit.NocturneButton("Cancel", () =>
            {
                confirmSp.Visibility = Visibility.Collapsed;
                resetNote.Text = resetWarning;
            }, isPrimary: false, width: 100, height: 38, tag: "theme:device:cancel-reset");

            confirmSp.Children.Add(confirmBtn);
            confirmSp.Children.Add(cancelBtn);

            resetBtn = NocturneKit.NocturneButton("Factory reset", () =>
            {
                confirmSp.Visibility = Visibility.Visible;
                resetNote.Text = "Reset this controller? This can't be undone.";
            }, isPrimary: false, isDanger: true, width: null, height: 38, tag: "theme:device:reset");

            rightStack.Children.Add(resetBtn);
            rightStack.Children.Add(confirmSp);
        }

        var rightCard = NocturneKit.NocturneWhiteCard(rightStack, width: 570, height: 570);
        Canvas.SetLeft(rightCard, 960);
        Canvas.SetTop(rightCard, 220);
        board.Children.Add(rightCard);

        // Bottom Bar
        var bottomBar = CreateNocturneBottomBar();
        Canvas.SetLeft(bottomBar, 0);
        Canvas.SetTop(bottomBar, 856);
        board.Children.Add(bottomBar);

        nocturnePageHost.Children.Add(board);
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NocturneShowPcMode()
    {
        if (nocturnePageHost == null) return;
        nocturnePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(NocturneKit.CreateCityBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateNocturneHeader("Studio"));
        var sidebar = CreateNocturneSidebar("PC Mode");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 74);
        board.Children.Add(sidebar);

        // Title Block
        var title = NocturneKit.NocturneTitle("PC MODE", "EMULATION", "Configure virtual Xbox 360 and DualShock 4 system-level gamepad passthrough.");
        Canvas.SetLeft(title, 300);
        Canvas.SetTop(title, 70);
        board.Children.Add(title);

        // Left stage: Emulation selection card
        var leftStack = new StackPanel { Width = 520 };
        leftStack.Children.Add(new TextBlock
        {
            Text = "EMULATED PROTOCOL",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var protoOptions = new (string Name, string Desc, string Tag)[]
        {
            ("Xbox 360 (XInput)", "Standard Windows compatibility for Steam, Epic, and legacy PC games.", "theme:pcmode:x360"),
            ("DualShock 4 (DirectInput)", "PlayStation layout emulation for remote play and motion-supported titles.", "theme:pcmode:ds4")
        };

        for (int i = 0; i < protoOptions.Length; i++)
        {
            var p = protoOptions[i];
            bool isSel = (i == 0);
            var optCard = new Border
            {
                Background = isSel ? NocturneKit.ActiveBlueBrush : new SolidColorBrush(Color.FromArgb(0x18, 0x01, 0x00, 0x42)),
                BorderBrush = isSel ? NocturneKit.CobaltBrush : new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 12),
                Cursor = System.Windows.Input.Cursors.Hand,
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Tag = p.Tag
            };

            var oStack = new StackPanel
            {
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            oStack.Children.Add(new TextBlock { Text = p.Name.ToUpperInvariant(), FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = isSel ? NocturneKit.WhiteBrush : NocturneKit.InkBrush });
            oStack.Children.Add(new TextBlock { Text = p.Desc, FontFamily = NocturneKit.Body, FontSize = 11.5, Foreground = isSel ? NocturneKit.CoolGreyBrush : new SolidColorBrush(Color.FromArgb(0x99, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 4, 0, 0) });
            optCard.Child = oStack;
            leftStack.Children.Add(optCard);
        }

        leftStack.Children.Add(new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 10, 0, 14) });

        var pcmodeNote = new TextBlock
        {
            Text = Draft.PcMode ? "PC mode is active · Mappings and stick settings apply directly on this PC." : "PC mode is disabled · Enable it to remap inputs via virtual controller.",
            FontFamily = NocturneKit.Body,
            FontSize = 12,
            Foreground = NocturneKit.CobaltBrush,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        leftStack.Children.Add(pcmodeNote);

        var leftCard = NocturneKit.NocturneWhiteCard(leftStack, width: 570, height: 520);
        Canvas.SetLeft(leftCard, 300);
        Canvas.SetTop(leftCard, 220);
        board.Children.Add(leftCard);

        // Right stage: System drivers status card
        var rightStack = new StackPanel { Width = 520 };
        rightStack.Children.Add(new TextBlock
        {
            Text = "SYSTEM DRIVER REVISION",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var drivers = new (string Name, string Status, string Version)[]
        {
            ("ViGEmBus Virtual Bus", "INSTALLED & ACTIVE", "Virtual pad"),
            ("HidHide Device Cloaking", "INSTALLED & ACTIVE", "Device filter"),
            ("Controller Mapping Hook", "ACTIVE", "PC mode")
        };

        foreach (var d in drivers)
        {
            var dRow = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x15, 0x00, 0x56, 0xfb)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x56, 0xfb)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var dGrid = new Grid();
            dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dInfo = new StackPanel();
            dInfo.Children.Add(new TextBlock { Text = d.Name.ToUpperInvariant(), FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = NocturneKit.InkBrush });
            dInfo.Children.Add(new TextBlock { Text = d.Version, FontFamily = NocturneKit.Body, FontSize = 11, Foreground = NocturneKit.CobaltBrush });
            Grid.SetColumn(dInfo, 0);

            var statusBadge = new Border
            {
                Background = NocturneKit.ActiveBlueBrush,
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            statusBadge.Child = new TextBlock
            {
                Text = d.Status,
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 9.5,
                Foreground = NocturneKit.WhiteBrush,
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            Grid.SetColumn(statusBadge, 1);

            dGrid.Children.Add(dInfo);
            dGrid.Children.Add(statusBadge);
            dRow.Child = dGrid;
            rightStack.Children.Add(dRow);
        }

        var rightCard = NocturneKit.NocturneWhiteCard(rightStack, width: 570, height: 520);
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

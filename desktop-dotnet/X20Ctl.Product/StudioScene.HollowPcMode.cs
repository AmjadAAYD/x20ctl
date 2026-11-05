using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private string hollowPcModeSection = "Routing";

    private void HollowShowPcMode()
    {
        if (hollowPageHost == null) return;
        hollowPageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateHollowHeader("Studio"));
        var sidebar = CreateHollowSidebar("PC mode");
        Canvas.SetLeft(sidebar, 28);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area at x=286, y=80
        var content = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(content, 286);
        Canvas.SetTop(content, 80);

        var titleSp = HollowKit.HollowHeader("PC MODE EMULATION", "Virtual gamepad translation, driver routing, and per-game overrides.");
        Canvas.SetLeft(titleSp, 16);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // Navigation Rail
        string[] pcmSections = ["Routing", "Per-Player", "Turbo", "Per-Game", "Drivers"];
        var navSp = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < pcmSections.Length; i++)
        {
            string sec = pcmSections[i];
            bool isCur = (sec == hollowPcModeSection);

            var sTab = new Border
            {
                Padding = new Thickness(14, 6, 14, 6),
                Background = isCur ? HollowKit.RaisedBrush : Brushes.Transparent,
                BorderBrush = isCur ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:pcmode:tab:{sec.ToLowerInvariant()}"
            };
            var tabSp = new StackPanel { Orientation = Orientation.Horizontal };
            if (isCur)
            {
                tabSp.Children.Add(new Path { Data = HollowKit.CreateDiamond(6), Fill = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            }
            tabSp.Children.Add(new TextBlock { Text = sec, FontFamily = HollowKit.Display, FontSize = 12, Foreground = isCur ? HollowKit.IvoryTextBrush : HollowKit.SoftTextBrush });
            sTab.Child = tabSp;

            sTab.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                hollowPcModeSection = sec;
                HollowShowPcMode();
            };
            navSp.Children.Add(sTab);
        }
        Canvas.SetLeft(navSp, 16);
        Canvas.SetTop(navSp, 68);
        content.Children.Add(navSp);

        // ==========================================
        // LEFT PANEL: 4-Node Pipeline Diagram
        // ==========================================
        var leftSp = new StackPanel { Width = 610 };

        var pipeHeader = new TextBlock
        {
            Text = "TRANSLATION PIPELINE",
            FontFamily = HollowKit.Display,
            FontSize = 13,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 14)
        };
        leftSp.Children.Add(pipeHeader);

        var pipeCanvas = new Canvas
        {
            Width = 590,
            Height = 360,
            Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x05, 0x0c, 0x14)),
            ClipToBounds = true
        };

        // Connecting lines with diamond junctions
        var connectLine = new Line { X1 = 60, Y1 = 180, X2 = 530, Y2 = 180, Stroke = HollowKit.SilverBorderBrush, StrokeThickness = 1 };
        pipeCanvas.Children.Add(connectLine);

        string[] nodeTitles = ["PHYSICAL PAD", "PC PROFILE", "VIRTUAL XBOX", "GAME ENGINE"];
        for (int n = 0; n < 4; n++)
        {
            double nx = 30 + n * 140;
            var nodeBox = new Border
            {
                Width = 110,
                Height = 70,
                Background = HollowKit.NightBrush,
                BorderBrush = HollowKit.IvoryBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8)
            };
            var nSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            nSp.Children.Add(new TextBlock { Text = $"NODE {n + 1}", FontFamily = HollowKit.Display, FontSize = 10, Foreground = HollowKit.MutedTextBrush, HorizontalAlignment = HorizontalAlignment.Center });
            nSp.Children.Add(new TextBlock { Text = nodeTitles[n], FontFamily = HollowKit.Display, FontSize = 11, Foreground = HollowKit.IvoryTextBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            nodeBox.Child = nSp;

            Canvas.SetLeft(nodeBox, nx);
            Canvas.SetTop(nodeBox, 145);
            pipeCanvas.Children.Add(nodeBox);

            // Diamond junction
            var junc = new Path
            {
                Data = HollowKit.CreateDiamond(8),
                Fill = HollowKit.IvoryTextBrush,
                Stroke = HollowKit.IvoryBorderBrush,
                StrokeThickness = 0.5
            };
            Canvas.SetLeft(junc, nx + 55);
            Canvas.SetTop(junc, 180);
            pipeCanvas.Children.Add(junc);
        }

        leftSp.Children.Add(pipeCanvas);

        var leftCard = HollowKit.FiligreeCard(leftSp, padding: 18, cornerSize: 14);
        leftCard.Width = 636;
        leftCard.Height = 580;
        Canvas.SetLeft(leftCard, 16);
        Canvas.SetTop(leftCard, 115);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT PANEL: Configuration & Driver Status
        // ==========================================
        var rightSp = new StackPanel { Width = 550 };

        var secTitle = new TextBlock
        {
            Text = "TRANSLATION PARAMETERS",
            FontFamily = HollowKit.Display,
            FontSize = 14,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 14)
        };
        rightSp.Children.Add(secTitle);

        // Toggle Switch for Emulation
        var toggleRow = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        toggleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toggleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tLabelSp = new StackPanel();
        tLabelSp.Children.Add(new TextBlock { Text = "ENABLE VIRTUAL GAMEPAD", FontFamily = HollowKit.Display, FontSize = 12.5, Foreground = HollowKit.IvoryTextBrush });
        tLabelSp.Children.Add(new TextBlock { Text = "Translate input events to virtual XInput controller", FontFamily = HollowKit.Body, FontSize = 11.5, Foreground = HollowKit.MutedTextBrush });
        Grid.SetColumn(tLabelSp, 0);

        var toggle = HollowKit.HollowToggle(true, on => { }, "theme:pcmode:toggle");
        Grid.SetColumn(toggle, 1);

        toggleRow.Children.Add(tLabelSp);
        toggleRow.Children.Add(toggle);
        rightSp.Children.Add(toggleRow);

        rightSp.Children.Add(HollowKit.DiamondDivider(550, 16));

        // Turbo repeat slider
        rightSp.Children.Add(new TextBlock { Text = "TURBO REPEAT RATE: 12 HZ", FontFamily = HollowKit.Display, FontSize = 12, Foreground = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 8, 0, 4) });
        var turboSlider = new Slider { Minimum = 2, Maximum = 25, Value = 12, Width = 540, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) };
        rightSp.Children.Add(turboSlider);

        // Driver Status Chip
        var driverChip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x05, 0x0c, 0x14)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 16)
        };
        var dSp = new StackPanel { Orientation = Orientation.Horizontal };
        dSp.Children.Add(new TextBlock { Text = "ViGEmBus: ", FontFamily = HollowKit.Display, FontSize = 11.5, Foreground = HollowKit.MutedTextBrush });
        dSp.Children.Add(new TextBlock { Text = "Active  ·  ", FontFamily = HollowKit.Display, FontSize = 11.5, Foreground = HollowKit.IvoryTextBrush });
        dSp.Children.Add(new TextBlock { Text = "HidHide: ", FontFamily = HollowKit.Display, FontSize = 11.5, Foreground = HollowKit.MutedTextBrush });
        dSp.Children.Add(new TextBlock { Text = "Operational", FontFamily = HollowKit.Display, FontSize = 11.5, Foreground = HollowKit.IvoryTextBrush });
        driverChip.Child = dSp;
        rightSp.Children.Add(driverChip);

        // Note on local PC mode operation (PC mode has no Apply to controller)
        var pcmodeNote = new TextBlock
        {
            Text = Draft.PcMode ? "PC mode is active · Mappings and stick settings apply directly on this PC." : "PC mode is disabled · Enable it to remap inputs via virtual controller.",
            FontFamily = HollowKit.Body,
            FontSize = 12.5,
            Foreground = HollowKit.SoftTextBrush,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        rightSp.Children.Add(pcmodeNote);

        var rightCard = HollowKit.FiligreeCard(rightSp, padding: 22, cornerSize: 14);
        rightCard.Width = 590;
        rightCard.Height = 580;
        Canvas.SetLeft(rightCard, 674);
        Canvas.SetTop(rightCard, 115);
        content.Children.Add(rightCard);

        board.Children.Add(content);

        // Dock
        var dock = HollowKit.HollowDock("PC mode");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        board.Children.Add(dock);

        hollowPageHost.Children.Add(board);
    }
}

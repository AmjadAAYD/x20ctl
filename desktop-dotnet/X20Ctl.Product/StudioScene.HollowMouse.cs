using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool hollowMouseWired;
    private string hollowMouseGroup = "Mouse";

    private void HollowShowMouse()
    {
        if (hollowPageHost == null) return;
        hollowPageHost.Children.Clear();

        if (Mouse is not { } mouse)
        {
            var emptyBoard = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
            emptyBoard.Children.Add(HollowKit.CreateCavernBackdrop());
            emptyBoard.Children.Add(CreateHollowHeader("Studio"));
            var msgCard = HollowKit.FiligreeCard(new TextBlock
            {
                Text = "Mouse emulation unavailable for this controller.",
                FontFamily = HollowKit.Display,
                FontSize = 14,
                Foreground = HollowKit.MutedTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            }, padding: 24);
            Canvas.SetLeft(msgCard, 500);
            Canvas.SetTop(msgCard, 350);
            emptyBoard.Children.Add(msgCard);
            emptyBoard.Children.Add(HollowKit.HollowDock("Mouse"));
            hollowPageHost.Children.Add(emptyBoard);
            return;
        }

        if (!hollowMouseWired)
        {
            hollowMouseWired = true;
            mouse.Watch(() => HollowLater());
        }

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateHollowHeader("Studio"));
        var sidebar = CreateHollowSidebar("Mouse");
        Canvas.SetLeft(sidebar, 28);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area at x=286, y=80
        var content = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(content, 286);
        Canvas.SetTop(content, 80);

        var titleSp = HollowKit.HollowHeader("MOUSE EMULATION", "Control desktop cursor, scroll, and clicks with gamepad inputs.");
        Canvas.SetLeft(titleSp, 16);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT PANEL: Enable Toggle & 17 Inputs List
        // ==========================================
        var leftSp = new StackPanel { Width = 610 };

        // Toggle Row
        var toggleBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x05, 0x0c, 0x14)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var tGrid = new Grid();
        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tInfo = new StackPanel();
        tInfo.Children.Add(new TextBlock { Text = "CONTROLLER AS DESKTOP MOUSE", FontFamily = HollowKit.Display, FontSize = 12, Foreground = HollowKit.IvoryTextBrush });
        tInfo.Children.Add(new TextBlock { Text = "Translate analog sticks and buttons to mouse inputs", FontFamily = HollowKit.Body, FontSize = 11, Foreground = HollowKit.MutedTextBrush });
        Grid.SetColumn(tInfo, 0);
        tGrid.Children.Add(tInfo);

        var toggle = HollowKit.HollowToggle(mouse.Switch.IsChecked ?? false, val =>
        {
            mouse.Switch.IsChecked = val;
            HollowShowMouse();
        }, "theme:mouse:toggle");
        Grid.SetColumn(toggle, 1);
        tGrid.Children.Add(toggle);
        toggleBorder.Child = tGrid;
        leftSp.Children.Add(toggleBorder);

        // 17-Inputs List
        var listHeader = new TextBlock
        {
            Text = "MAPPED CONTROLS (17 / 17 CONFIGURED)",
            FontFamily = HollowKit.Display,
            FontSize = 11.5,
            Foreground = HollowKit.MutedTextBrush,
            Margin = new Thickness(4, 0, 0, 6)
        };
        leftSp.Children.Add(listHeader);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 410 };
        var inputsStack = new StackPanel();
        foreach (var inKey in mouse.Inputs)
        {
            bool isSelected = (mouse.SelectedKey == inKey);
            var rowBorder = new Border
            {
                Background = isSelected ? HollowKit.RaisedBrush : Brushes.Transparent,
                BorderBrush = isSelected ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.25)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:mouse:input:{inKey.ToLowerInvariant()}"
            };

            var rGrid = new Grid();
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var keyBadge = new Border
            {
                Width = 28,
                Height = 20,
                Background = new SolidColorBrush(Color.FromArgb(0xbb, 0x14, 0x22, 0x32)),
                BorderBrush = isSelected ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            };
            keyBadge.Child = new TextBlock { Text = inKey, FontFamily = HollowKit.Display, FontSize = 10, Foreground = HollowKit.IvoryTextBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(keyBadge, 0);

            var keyName = new TextBlock { Text = mouse.LabelFor(inKey), FontFamily = HollowKit.Display, FontSize = 12, Foreground = isSelected ? HollowKit.IvoryTextBrush : HollowKit.SoftTextBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(keyName, 1);

            var actLabel = new TextBlock { Text = mouse.ActionFor(inKey), FontFamily = HollowKit.Body, FontSize = 12, Foreground = isSelected ? HollowKit.IvoryTextBrush : HollowKit.MutedTextBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(actLabel, 2);

            rGrid.Children.Add(keyBadge);
            rGrid.Children.Add(keyName);
            rGrid.Children.Add(actLabel);
            rowBorder.Child = rGrid;

            rowBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                mouse.PickKey(inKey);
                HollowShowMouse();
            };
            inputsStack.Children.Add(rowBorder);
        }
        scroll.Content = inputsStack;
        leftSp.Children.Add(scroll);

        var leftCard = HollowKit.FiligreeCard(leftSp, padding: 18, cornerSize: 14);
        leftCard.Width = 636;
        leftCard.Height = 580;
        Canvas.SetLeft(leftCard, 16);
        Canvas.SetTop(leftCard, 80);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT PANEL: Actions Grid & Speed Slider
        // ==========================================
        var rightSp = new StackPanel { Width = 550 };

        string curInputName = mouse.LabelFor(mouse.SelectedKey);

        var inspectorTitle = new TextBlock
        {
            Text = $"ASSIGN ACTION: {curInputName.ToUpperInvariant()}",
            FontFamily = HollowKit.Display,
            FontSize = 14,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        rightSp.Children.Add(inspectorTitle);

        // Category Tabs
        var catSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        foreach (var grp in mouse.Groups)
        {
            bool isCurGrp = (grp.Group == hollowMouseGroup);
            var gBtn = new Border
            {
                Padding = new Thickness(10, 4, 10, 4),
                Background = isCurGrp ? HollowKit.RaisedBrush : Brushes.Transparent,
                BorderBrush = isCurGrp ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            gBtn.Child = new TextBlock { Text = grp.Group, FontFamily = HollowKit.Display, FontSize = 11, Foreground = isCurGrp ? HollowKit.IvoryTextBrush : HollowKit.SoftTextBrush };
            gBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                hollowMouseGroup = grp.Group;
                HollowShowMouse();
            };
            catSp.Children.Add(gBtn);
        }
        rightSp.Children.Add(catSp);

        // Action Chips Grid
        var curGrp = mouse.Groups.FirstOrDefault(g => g.Group == hollowMouseGroup);
        if (curGrp.Actions != null)
        {
            var chipsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            string curAct = mouse.ActionFor(mouse.SelectedKey);
            foreach (var act in curGrp.Actions)
            {
                bool isCurrentAction = (MouseWorkbench.Friendly(act) == curAct);
                var chip = new Border
                {
                    Padding = new Thickness(10, 6, 10, 6),
                    Background = isCurrentAction ? HollowKit.RaisedBrush : new SolidColorBrush(Color.FromArgb(0xaa, 0x09, 0x12, 0x1c)),
                    BorderBrush = isCurrentAction ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(3),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = $"theme:mouse:action:{act}"
                };
                chip.Child = new TextBlock { Text = MouseWorkbench.Friendly(act), FontFamily = HollowKit.Display, FontSize = 11.5, Foreground = isCurrentAction ? HollowKit.IvoryTextBrush : HollowKit.SoftTextBrush };
                chip.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    mouse.Assign(act);
                    HollowShowMouse();
                };
                chipsWrap.Children.Add(chip);
            }
            rightSp.Children.Add(chipsWrap);
        }

        rightSp.Children.Add(HollowKit.DiamondDivider(550, 16));

        // Speed Slider
        rightSp.Children.Add(new TextBlock { Text = "POINTER SPEED", FontFamily = HollowKit.Display, FontSize = 12, Foreground = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 10, 0, 4) });
        var speedSlider = mouse.SpeedSlider;
        speedSlider.Width = 540;
        speedSlider.HorizontalAlignment = HorizontalAlignment.Left;
        speedSlider.Margin = new Thickness(0, 0, 0, 16);
        rightSp.Children.Add(speedSlider);

        // Reset Mode Button
        var resetModeBtn = HollowKit.HollowButton("Reset mouse layout", () =>
        {
            mouse.ResetMode();
            HollowShowMouse();
        }, isPrimary: false, width: 160, height: 34, tag: "theme:mouse:reset");
        rightSp.Children.Add(resetModeBtn);

        var rightCard = HollowKit.FiligreeCard(rightSp, padding: 22, cornerSize: 14);
        rightCard.Width = 590;
        rightCard.Height = 580;
        Canvas.SetLeft(rightCard, 674);
        Canvas.SetTop(rightCard, 80);
        content.Children.Add(rightCard);

        board.Children.Add(content);

        // Dock
        var dock = HollowKit.HollowDock("Mouse");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        board.Children.Add(dock);

        hollowPageHost.Children.Add(board);
    }
}

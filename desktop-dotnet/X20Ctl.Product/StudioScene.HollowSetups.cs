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
    private void HollowShowSetups()
    {
        if (hollowPageHost == null) return;
        hollowPageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateHollowHeader("Studio"));
        var sidebar = CreateHollowSidebar("Setups");
        Canvas.SetLeft(sidebar, 28);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        var setups = Setups;
        var cards = setups?.Cards ?? Array.Empty<(string Id, string Name, string State)>();
        string selectedId = setups?.SelectedId ?? "";
        string activeId = setups?.ActiveId ?? "";

        var activeCard = cards.FirstOrDefault(c => c.Id == selectedId);
        if (string.IsNullOrEmpty(activeCard.Id) && cards.Count > 0) activeCard = cards[0];

        // Content Area at x=286, y=80
        var content = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(content, 286);
        Canvas.SetTop(content, 80);

        var titleSp = HollowKit.HollowHeader("SETUPS & PROFILES", "Saved controller configurations, game presets, and cloud backups.");
        Canvas.SetLeft(titleSp, 16);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT PANEL: Setups List & Actions
        // ==========================================
        var leftSp = new StackPanel { Width = 610 };

        var listHeader = new TextBlock
        {
            Text = $"SAVED PROFILES ({cards.Count})",
            FontFamily = HollowKit.Display,
            FontSize = 12.5,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        leftSp.Children.Add(listHeader);

        var scroll = new ScrollViewer { Height = 440, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var cardsStack = new StackPanel();

        foreach (var c in cards)
        {
            bool isSel = (c.Id == activeCard.Id);
            bool isActive = (c.Id == activeId);

            var cBorder = new Border
            {
                Background = isSel ? HollowKit.RaisedBrush : new SolidColorBrush(Color.FromArgb(0x80, 0x05, 0x0c, 0x14)),
                BorderBrush = isSel ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 3, 0, 3),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:setup:{c.Id}"
            };

            var cGrid = new Grid();
            cGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameSp = new StackPanel();
            nameSp.Children.Add(new TextBlock { Text = c.Name, FontFamily = HollowKit.Display, FontSize = 13, Foreground = isSel ? HollowKit.IvoryTextBrush : HollowKit.SoftTextBrush });
            nameSp.Children.Add(new TextBlock { Text = c.State, FontFamily = HollowKit.Body, FontSize = 11, Foreground = HollowKit.MutedTextBrush, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(nameSp, 0);

            if (isActive)
            {
                var actBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x14, 0x22, 0x32)),
                    BorderBrush = HollowKit.IvoryBorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(8, 2, 8, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                actBadge.Child = new TextBlock { Text = "ACTIVE", FontFamily = HollowKit.Display, FontSize = 10, Foreground = HollowKit.IvoryTextBrush };
                Grid.SetColumn(actBadge, 1);
                cGrid.Children.Add(actBadge);
            }

            cGrid.Children.Add(nameSp);
            cBorder.Child = cGrid;

            string capturedId = c.Id;
            cBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                setups?.Select(capturedId);
                HollowShowSetups();
            };
            cardsStack.Children.Add(cBorder);
        }
        scroll.Content = cardsStack;
        leftSp.Children.Add(scroll);

        // New Setup Button
        var newSetupBtn = HollowKit.HollowButton("+ New Profile", () =>
        {
            setups?.Click("new");
            HollowShowSetups();
        }, isPrimary: false, height: 34, tag: "theme:setups:new");
        newSetupBtn.Margin = new Thickness(0, 10, 0, 0);
        leftSp.Children.Add(newSetupBtn);

        var leftCard = HollowKit.FiligreeCard(leftSp, padding: 18, cornerSize: 14);
        leftCard.Width = 636;
        leftCard.Height = 580;
        Canvas.SetLeft(leftCard, 16);
        Canvas.SetTop(leftCard, 90);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT PANEL: Profile Details & Activate
        // ==========================================
        var rightSp = new StackPanel { Width = 550 };

        var dTitle = new TextBlock
        {
            Text = $"PROFILE: {(string.IsNullOrEmpty(activeCard.Name) ? "CUSTOM" : activeCard.Name.ToUpperInvariant())}",
            FontFamily = HollowKit.Display,
            FontSize = 14,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 12)
        };
        rightSp.Children.Add(dTitle);

        var dDesc = new TextBlock
        {
            Text = "Contains button mapping, trigger calibrations, deadzone curves, and macro assignments.",
            FontFamily = HollowKit.Body,
            FontSize = 12.5,
            Foreground = HollowKit.SoftTextBrush,
            Margin = new Thickness(0, 0, 0, 16)
        };
        rightSp.Children.Add(dDesc);

        rightSp.Children.Add(HollowKit.DiamondDivider(550, 16));

        // Activate Button
        var activateBtn = HollowKit.HollowButton("Activate profile", () =>
        {
            setups?.Click("apply");
            HollowShowSetups();
        }, isPrimary: true, height: 40, tag: "theme:setups:activate");
        activateBtn.Margin = new Thickness(0, 14, 0, 10);
        rightSp.Children.Add(activateBtn);

        var rightCard = HollowKit.FiligreeCard(rightSp, padding: 22, cornerSize: 14);
        rightCard.Width = 590;
        rightCard.Height = 580;
        Canvas.SetLeft(rightCard, 674);
        Canvas.SetTop(rightCard, 90);
        content.Children.Add(rightCard);

        board.Children.Add(content);

        // Dock
        var dock = HollowKit.HollowDock("Setups");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        board.Children.Add(dock);

        hollowPageHost.Children.Add(board);
    }
}

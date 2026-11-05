using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SunriseShowSetups()
    {
        if (sunrisePageHost == null) return;
        sunrisePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateSunriseHeader("Studio"));
        var sidebar = CreateSunriseSidebar("Setups");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        var setups = Setups;
        var cards = setups?.Cards ?? Array.Empty<(string Id, string Name, string State)>();
        string selId = setups?.SelectedId ?? "";
        string activeId = setups?.ActiveId ?? "";

        var activeCard = cards.FirstOrDefault(c => c.Id == selId);
        if (string.IsNullOrEmpty(activeCard.Id) && cards.Count > 0) activeCard = cards[0];

        // Content Area
        var content = new Canvas { Width = 1300, Height = 770 };
        Canvas.SetLeft(content, 272);
        Canvas.SetTop(content, 80);

        var titleSp = SunriseKit.SunriseTitle("PROFILE SETUPS", "Save, duplicate, and switch between customized button, curve, and macro loadouts.");
        Canvas.SetLeft(titleSp, 10);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT STICKER CARD: Profile Loadouts Grid
        // ==========================================
        var leftSp = new StackPanel { Width = 560 };

        var gridHeader = new Grid();
        gridHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gridHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var sTitle = new TextBlock
        {
            Text = $"STORED CONTROLLER SETUPS ({cards.Count})",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12.5,
            Foreground = SunriseKit.InkBrush
        };
        Grid.SetColumn(sTitle, 0);
        gridHeader.Children.Add(sTitle);

        var newBtn = SunriseKit.SunriseButton("+ New Setup", () =>
        {
            setups?.Click("new");
            SunriseShowSetups();
        }, isPrimary: true, width: 130, height: 32, tag: "theme:setup:new");
        Grid.SetColumn(newBtn, 1);
        gridHeader.Children.Add(newBtn);
        leftSp.Children.Add(gridHeader);

        var setupsGrid = new UniformGrid { Columns = 2, Rows = (int)Math.Max(2, Math.Ceiling(cards.Count / 2.0)), Margin = new Thickness(0, 12, 0, 0) };
        foreach (var c in cards)
        {
            bool isSel = (c.Id == selId);
            string id = c.Id;

            var card = new Border
            {
                Background = isSel ? SunriseKit.InkBrush : SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:setup:item:{id}"
            };

            var sSp = new StackPanel();
            sSp.Children.Add(new TextBlock { Text = c.Name.ToUpperInvariant(), FontFamily = SunriseKit.Display, FontWeight = FontWeights.Black, FontSize = 13, Foreground = isSel ? SunriseKit.CreamBrush : SunriseKit.InkBrush });
            sSp.Children.Add(new TextBlock { Text = c.State, FontFamily = SunriseKit.Body, FontSize = 10.5, Foreground = isSel ? SunriseKit.GoldBrush : SunriseKit.OliveBrush, Margin = new Thickness(0, 3, 0, 0) });
            card.Child = sSp;

            card.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                setups?.Select(id);
                SunriseShowSetups();
            };

            setupsGrid.Children.Add(card);
        }
        leftSp.Children.Add(setupsGrid);

        var leftCard = SunriseKit.CreateStickerCard(leftSp, 590, 520, cornerRadius: 22);
        Canvas.SetLeft(leftCard, 10);
        Canvas.SetTop(leftCard, 100);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT TV SCREEN: Active Profile Details & Actions
        // ==========================================
        var rightSp = new StackPanel { Width = 580 };

        string currentName = string.IsNullOrEmpty(activeCard.Name) ? "Default Profile" : activeCard.Name;

        var dHeader = new TextBlock
        {
            Text = $"SETUP DETAILS: {currentName.ToUpperInvariant()}",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 13,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 14)
        };
        rightSp.Children.Add(dHeader);

        var detailsCard = new Border
        {
            Background = SunriseKit.CreamBrush,
            BorderBrush = SunriseKit.InkBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 20)
        };
        var dStack = new StackPanel();
        dStack.Children.Add(new TextBlock { Text = "HARDWARE TARGET: EASYSMX X20", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.InkBrush });
        dStack.Children.Add(new TextBlock { Text = "Contains: 16 remapped inputs, 4 analog deadzone definitions, 4 paddle macro sequences.", FontFamily = SunriseKit.Body, FontSize = 11.5, Foreground = SunriseKit.OliveBrush, Margin = new Thickness(0, 4, 0, 14) });

        var actRow = new StackPanel { Orientation = Orientation.Horizontal };
        var dupBtn = SunriseKit.SunriseButton("Duplicate", () =>
        {
            setups?.Click("duplicate");
            SunriseShowSetups();
        }, isPrimary: false, width: 120, height: 36, tag: "theme:setup:duplicate");
        dupBtn.Margin = new Thickness(0, 0, 10, 0);

        var delBtn = SunriseKit.SunriseButton("Delete", () =>
        {
            setups?.Click("delete");
            SunriseShowSetups();
        }, isPrimary: false, isDanger: true, width: 110, height: 36, tag: "theme:setup:delete");

        actRow.Children.Add(dupBtn);
        actRow.Children.Add(delBtn);
        dStack.Children.Add(actRow);
        detailsCard.Child = dStack;
        rightSp.Children.Add(detailsCard);

        var activateBtn = SunriseKit.SunriseButton("Activate Setup on Pad", () =>
        {
            setups?.Click("apply");
            SunriseShowSetups();
        }, isPrimary: true, width: 240, height: 42, tag: "theme:setup:activate");
        activateBtn.HorizontalAlignment = HorizontalAlignment.Right;
        rightSp.Children.Add(activateBtn);

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

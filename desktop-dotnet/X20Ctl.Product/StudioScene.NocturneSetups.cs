using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NocturneShowSetups()
    {
        if (nocturnePageHost == null) return;
        nocturnePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(NocturneKit.CreateCityBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateNocturneHeader("Studio"));
        var sidebar = CreateNocturneSidebar("Setups");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 74);
        board.Children.Add(sidebar);

        // Title Block
        var title = NocturneKit.NocturneTitle("LOCAL", "SETUPS", "Stored calibration profiles, custom mappings, and tournament presets.");
        Canvas.SetLeft(title, 300);
        Canvas.SetTop(title, 70);
        board.Children.Add(title);

        var setups = Setups;
        var cards = setups?.Cards ?? Array.Empty<(string Id, string Name, string State)>();
        string selId = setups?.SelectedId ?? "";
        string activeId = setups?.ActiveId ?? "";

        var activeCard = cards.FirstOrDefault(c => c.Id == selId);
        if (string.IsNullOrEmpty(activeCard.Id) && cards.Count > 0) activeCard = cards[0];

        // ==========================================
        // LEFT STAGE: Profile Cards List on Slanted White Card
        // ==========================================
        var leftStack = new StackPanel { Width = 520 };
        leftStack.Children.Add(new TextBlock
        {
            Text = $"PROFILES ({cards.Count})",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 410 };
        var cardsStack = new StackPanel();

        foreach (var c in cards)
        {
            bool isSel = (c.Id == activeCard.Id);
            bool isAct = (c.Id == activeId);

            var cBorder = new Border
            {
                Background = isSel ? NocturneKit.ActiveBlueBrush : new SolidColorBrush(Color.FromArgb(0x15, 0x01, 0x00, 0x42)),
                BorderBrush = isSel ? NocturneKit.CobaltBrush : new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 3, 0, 3),
                Cursor = System.Windows.Input.Cursors.Hand,
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Tag = $"theme:setup:card:{c.Id}"
            };

            var cGrid = new Grid
            {
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            cGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameSp = new StackPanel();
            nameSp.Children.Add(new TextBlock { Text = c.Name.ToUpperInvariant(), FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = isSel ? NocturneKit.WhiteBrush : NocturneKit.InkBrush });
            nameSp.Children.Add(new TextBlock { Text = c.State, FontFamily = NocturneKit.Body, FontSize = 11, Foreground = isSel ? NocturneKit.CoolGreyBrush : new SolidColorBrush(Color.FromArgb(0x99, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(nameSp, 0);

            if (isAct)
            {
                var actBadge = new Border
                {
                    Background = NocturneKit.CobaltBrush,
                    Padding = new Thickness(8, 2, 8, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                actBadge.Child = new TextBlock { Text = "ACTIVE", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 10, Foreground = NocturneKit.WhiteBrush };
                Grid.SetColumn(actBadge, 1);
                cGrid.Children.Add(actBadge);
            }

            cGrid.Children.Add(nameSp);
            cBorder.Child = cGrid;

            string targetId = c.Id;
            cBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                setups?.Select(targetId);
                NocturneShowSetups();
            };
            cardsStack.Children.Add(cBorder);
        }
        scroll.Content = cardsStack;
        leftStack.Children.Add(scroll);

        // New Profile Button
        var newSetupBtn = NocturneKit.NocturneButton("+ New Profile", () =>
        {
            setups?.Click("new");
            NocturneShowSetups();
        }, isPrimary: false, width: 220, height: 36, tag: "theme:setups:new");
        newSetupBtn.Margin = new Thickness(0, 10, 0, 0);
        leftStack.Children.Add(newSetupBtn);

        var leftCard = NocturneKit.NocturneWhiteCard(leftStack, width: 570, height: 570);
        Canvas.SetLeft(leftCard, 300);
        Canvas.SetTop(leftCard, 220);
        board.Children.Add(leftCard);

        // ==========================================
        // RIGHT STAGE: Profile Details & Actions
        // ==========================================
        var rightStack = new StackPanel { Width = 520 };

        rightStack.Children.Add(new TextBlock
        {
            Text = $"PROFILE: {(string.IsNullOrEmpty(activeCard.Name) ? "DEFAULT" : activeCard.Name.ToUpperInvariant())}",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        rightStack.Children.Add(new TextBlock
        {
            Text = "Contains dedicated trigger calibrations, stick deadzones, RGB lighting, and custom button layouts.",
            FontFamily = NocturneKit.Body,
            FontSize = 12.5,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 16)
        });

        // Thin rule
        rightStack.Children.Add(new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 0, 0, 14) });

        // Action Buttons Row: Duplicate, Delete
        var opSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        var dupBtn = NocturneKit.NocturneButton("Duplicate", () =>
        {
            setups?.Click("duplicate");
            NocturneShowSetups();
        }, isPrimary: false, width: 140, height: 36, tag: "theme:setup:duplicate");
        dupBtn.Margin = new Thickness(0, 0, 10, 0);

        var delBtn = NocturneKit.NocturneButton("Delete", () =>
        {
            setups?.Click("delete");
            NocturneShowSetups();
        }, isPrimary: false, isDanger: true, width: 120, height: 36, tag: "theme:setup:delete");

        opSp.Children.Add(dupBtn);
        opSp.Children.Add(delBtn);
        rightStack.Children.Add(opSp);

        // Activate Button
        var activateBtn = NocturneKit.NocturneButton("Activate Profile", () =>
        {
            setups?.Click("apply");
            NocturneShowSetups();
        }, isPrimary: true, width: 340, height: 44, tag: "theme:setups:activate");
        rightStack.Children.Add(activateBtn);

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

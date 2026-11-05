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
    private string nocturneMouseGroup = "Mouse";

    private void NocturneShowMouse()
    {
        if (nocturnePageHost == null) return;
        nocturnePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(NocturneKit.CreateCityBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateNocturneHeader("Studio"));
        var sidebar = CreateNocturneSidebar("Mouse");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 74);
        board.Children.Add(sidebar);

        // Title Block
        var title = NocturneKit.NocturneTitle("MOUSE", "EMULATION", "Desktop cursor control and customizable button assignment mappings.");
        Canvas.SetLeft(title, 300);
        Canvas.SetTop(title, 70);
        board.Children.Add(title);

        var mouse = Mouse;
        if (mouse == null) return;

        // ==========================================
        // LEFT STAGE: Inputs List on Slanted White Card
        // ==========================================
        var leftStack = new StackPanel { Width = 520 };
        leftStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER CONTROLS",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 460 };
        var inputsStack = new StackPanel();

        foreach (var inKey in mouse.Inputs)
        {
            bool isSelected = (mouse.SelectedKey == inKey);
            var rowBorder = new Border
            {
                Background = isSelected ? NocturneKit.ActiveBlueBrush : new SolidColorBrush(Color.FromArgb(0x12, 0x01, 0x00, 0x42)),
                BorderBrush = isSelected ? NocturneKit.CobaltBrush : new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Tag = $"theme:mouse:input:{inKey.ToLowerInvariant()}"
            };

            var rGrid = new Grid
            {
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var keyBadge = new Border
            {
                Width = 28,
                Height = 20,
                Background = isSelected ? NocturneKit.WhiteBrush : new SolidColorBrush(Color.FromArgb(0xbb, 0x00, 0x56, 0xfb)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            keyBadge.Child = new TextBlock { Text = inKey, FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 10, Foreground = isSelected ? NocturneKit.InkBrush : NocturneKit.WhiteBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(keyBadge, 0);

            var keyName = new TextBlock { Text = mouse.LabelFor(inKey), FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = isSelected ? NocturneKit.WhiteBrush : NocturneKit.InkBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            Grid.SetColumn(keyName, 1);

            var actLabel = new TextBlock { Text = mouse.ActionFor(inKey), FontFamily = NocturneKit.Body, FontSize = 11.5, Foreground = isSelected ? NocturneKit.WhiteBrush : new SolidColorBrush(Color.FromArgb(0xbb, 0x01, 0x00, 0x42)), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(actLabel, 2);

            rGrid.Children.Add(keyBadge);
            rGrid.Children.Add(keyName);
            rGrid.Children.Add(actLabel);
            rowBorder.Child = rGrid;

            string capturedKey = inKey;
            rowBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                mouse.PickKey(capturedKey);
                NocturneShowMouse();
            };
            inputsStack.Children.Add(rowBorder);
        }
        scroll.Content = inputsStack;
        leftStack.Children.Add(scroll);

        var leftCard = NocturneKit.NocturneWhiteCard(leftStack, width: 570, height: 570);
        Canvas.SetLeft(leftCard, 300);
        Canvas.SetTop(leftCard, 220);
        board.Children.Add(leftCard);

        // ==========================================
        // RIGHT STAGE: Action Assignment Deck
        // ==========================================
        var rightStack = new StackPanel { Width = 520 };

        string curInputName = mouse.LabelFor(mouse.SelectedKey);
        rightStack.Children.Add(new TextBlock
        {
            Text = $"ASSIGN: {curInputName.ToUpperInvariant()}",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Category Tabs
        var catSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        foreach (var grp in mouse.Groups)
        {
            bool isCurGrp = (grp.Group == nocturneMouseGroup);
            var gBtn = NocturneKit.NocturneChip(grp.Group, isCurGrp, () =>
            {
                nocturneMouseGroup = grp.Group;
                NocturneShowMouse();
            });
            catSp.Children.Add(gBtn);
        }
        rightStack.Children.Add(catSp);

        // Action Chips Grid
        var curGrp = mouse.Groups.FirstOrDefault(g => g.Group == nocturneMouseGroup);
        if (curGrp.Actions != null)
        {
            var chipsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 14), Height = 180 };
            string curAct = mouse.ActionFor(mouse.SelectedKey);

            foreach (var act in curGrp.Actions)
            {
                bool isCurrentAction = (MouseWorkbench.Friendly(act) == curAct);
                var chip = new Border
                {
                    Padding = new Thickness(10, 5, 10, 5),
                    Background = isCurrentAction ? NocturneKit.ActiveBlueBrush : new SolidColorBrush(Color.FromArgb(0x18, 0x01, 0x00, 0x42)),
                    BorderBrush = isCurrentAction ? NocturneKit.CobaltBrush : new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(2),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    RenderTransform = NocturneKit.CreateSlant(-12),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    Tag = $"theme:mouse:action:{act}"
                };
                chip.Child = new TextBlock
                {
                    Text = MouseWorkbench.Friendly(act),
                    FontFamily = NocturneKit.Display,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Foreground = isCurrentAction ? NocturneKit.WhiteBrush : NocturneKit.InkBrush,
                    RenderTransform = NocturneKit.CreateSlant(12),
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };

                var assignAct = act;
                chip.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    mouse.Assign(assignAct);
                    NocturneShowMouse();
                };
                chipsWrap.Children.Add(chip);
            }
            rightStack.Children.Add(chipsWrap);
        }

        // Thin rule
        rightStack.Children.Add(new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 0, 0, 14) });

        // Speed Slider
        rightStack.Children.Add(new TextBlock { Text = "POINTER SPEED", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = NocturneKit.InkBrush, Margin = new Thickness(0, 4, 0, 4) });
        var speedSlider = mouse.SpeedSlider;
        speedSlider.Width = 480;
        speedSlider.HorizontalAlignment = HorizontalAlignment.Left;
        speedSlider.Margin = new Thickness(0, 0, 0, 16);
        rightStack.Children.Add(speedSlider);

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

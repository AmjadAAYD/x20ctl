using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private string sunriseMouseCategory = "Left click";
    private int sunriseMouseSelectedInput = 0;

    private void SunriseShowMouse()
    {
        if (sunrisePageHost == null) return;
        sunrisePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateSunriseHeader("Studio"));
        var sidebar = CreateSunriseSidebar("Mouse");
        Canvas.SetLeft(sidebar, 16);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        var mouse = Mouse;

        // Content Area
        var content = new Canvas { Width = 1300, Height = 770 };
        Canvas.SetLeft(content, 272);
        Canvas.SetTop(content, 80);

        var titleSp = SunriseKit.SunriseTitle("MOUSE & DESKTOP NAVIGATION", "Map gamepad thumbsticks and buttons to Windows mouse pointer, scroll wheel, and click actions.");
        Canvas.SetLeft(titleSp, 10);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // 17-input list
        var inputs = mouse?.Inputs ?? new[] { "Left stick", "Right stick", "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "D-pad up", "D-pad down", "D-pad left", "D-pad right", "Start", "Back", "Guide" };
        if (sunriseMouseSelectedInput >= inputs.Count) sunriseMouseSelectedInput = 0;
        string activeInput = inputs[sunriseMouseSelectedInput];

        // ==========================================
        // LEFT STICKER CARD: 17-Input Scrollable List
        // ==========================================
        var leftSp = new StackPanel { Width = 560 };

        var listHeader = new TextBlock
        {
            Text = "CONTROLLER INPUTS FOR MOUSE MAPPING",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12.5,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        leftSp.Children.Add(listHeader);

        var inputScroll = new ScrollViewer
        {
            Width = 540,
            Height = 440,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var inputStack = new StackPanel();

        for (int i = 0; i < inputs.Count; i++)
        {
            int idx = i;
            string inKey = inputs[i];
            bool isSel = (idx == sunriseMouseSelectedInput);
            string actionText = mouse != null ? mouse.ActionFor(inKey) : "Mouse action";

            var itemCard = new Border
            {
                Background = isSel ? SunriseKit.InkBrush : SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:mouse:input:{idx}"
            };

            var iGrid = new Grid();
            iGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            iGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameTb = new TextBlock { Text = inKey.ToUpperInvariant(), FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = isSel ? SunriseKit.CreamBrush : SunriseKit.InkBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(nameTb, 0);
            iGrid.Children.Add(nameTb);

            var actTb = new TextBlock { Text = actionText, FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = isSel ? SunriseKit.YellowBrush : SunriseKit.OrangeBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(actTb, 1);
            iGrid.Children.Add(actTb);

            itemCard.Child = iGrid;
            itemCard.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                sunriseMouseSelectedInput = idx;
                SunriseShowMouse();
            };

            inputStack.Children.Add(itemCard);
        }
        inputScroll.Content = inputStack;
        leftSp.Children.Add(inputScroll);

        var leftCard = SunriseKit.CreateStickerCard(leftSp, 590, 520, cornerRadius: 22);
        Canvas.SetLeft(leftCard, 10);
        Canvas.SetTop(leftCard, 100);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT TV SCREEN: Action Assignment & Speed
        // ==========================================
        var rightSp = new StackPanel { Width = 580 };

        var assignHeader = new TextBlock
        {
            Text = $"ASSIGN ACTION: {activeInput.ToUpperInvariant()}",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 13,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        rightSp.Children.Add(assignHeader);

        // Category Tabs
        var catSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        foreach (var grp in mouse.Groups)
        {
            bool isCurGrp = (grp.Group == sunriseMouseCategory);
            var gBtn = SunriseKit.SunriseChip(grp.Group, isCurGrp, () =>
            {
                sunriseMouseCategory = grp.Group;
                SunriseShowMouse();
            });
            catSp.Children.Add(gBtn);
        }
        rightSp.Children.Add(catSp);

        // Action Chips Grid
        var curGrp = mouse.Groups.FirstOrDefault(g => g.Group == sunriseMouseCategory);
        if (curGrp.Actions != null)
        {
            var actionsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 16), Height = 140 };
            string curAct = mouse.ActionFor(mouse.SelectedKey);
            foreach (var act in curGrp.Actions)
            {
                string friendly = MouseWorkbench.Friendly(act);
                bool isAct = (friendly == curAct);
                var aChip = SunriseKit.SunriseChip(friendly, isAct, () =>
                {
                    mouse?.Assign(act);
                    SunriseShowMouse();
                }, tag: $"theme:mouse:act:{act}");
                actionsWrap.Children.Add(aChip);
            }
            rightSp.Children.Add(actionsWrap);
        }

        // Pointer Speed Slider
        var speedHeader = new TextBlock { Text = "POINTER SENSITIVITY & SCROLL SPEED", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.InkBrush, Margin = new Thickness(0, 0, 0, 8) };
        rightSp.Children.Add(speedHeader);

        var speedSlider = new Slider { Minimum = 10, Maximum = 100, Value = 50, Width = 560, HorizontalAlignment = HorizontalAlignment.Left, Tag = "theme:mouse:speed" };
        speedSlider.Margin = new Thickness(0, 0, 0, 20);
        rightSp.Children.Add(speedSlider);

        var applyBtn = SunriseKit.SunriseButton("Apply Mouse Mapping", () =>
        {
            Apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            SunriseShowMouse();
        }, isPrimary: true, width: 220, height: 42);
        applyBtn.HorizontalAlignment = HorizontalAlignment.Right;
        rightSp.Children.Add(applyBtn);

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

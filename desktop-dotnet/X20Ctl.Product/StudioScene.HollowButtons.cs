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
    private string hollowButtonCategory = "Face";
    private int hollowButtonIndex = 0;

    private static readonly Dictionary<string, string[]> HollowCategories = new()
    {
        ["Face"] = ["A", "B", "X", "Y"],
        ["Shoulders"] = ["LB", "RB", "LT", "RT"],
        ["Sticks"] = ["L3", "R3"],
        ["System"] = ["View", "Menu"],
        ["D-pad"] = ["Up", "Down", "Left", "Right"],
        ["Paddles"] = ["M1", "M2", "M3", "M4"]
    };

    private void HollowShowButtons()
    {
        if (hollowPageHost == null) return;
        hollowPageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateHollowHeader("Studio"));
        var sidebar = CreateHollowSidebar("Buttons");
        Canvas.SetLeft(sidebar, 28);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Sync with SelectedControl
        if (!string.IsNullOrEmpty(SelectedControl))
        {
            foreach (var (cat, bKeys) in HollowCategories)
            {
                int idx = Array.IndexOf(bKeys, SelectedControl);
                if (idx >= 0)
                {
                    hollowButtonCategory = cat;
                    hollowButtonIndex = idx;
                    break;
                }
            }
        }

        string[] cats = ["Face", "Shoulders", "Sticks", "System", "D-pad", "Paddles"];
        if (!cats.Contains(hollowButtonCategory)) hollowButtonCategory = "Face";
        var currentButtons = HollowCategories[hollowButtonCategory];
        if (hollowButtonIndex >= currentButtons.Length) hollowButtonIndex = 0;
        string focusedBtn = currentButtons[hollowButtonIndex];

        // Content Area at x=286, y=80
        var content = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(content, 286);
        Canvas.SetTop(content, 80);

        // Page Title & Subtitle
        var titleSp = HollowKit.HollowHeader("BUTTON ASSIGNMENTS", "Choose a control, then select what the game receives.");
        Canvas.SetLeft(titleSp, 16);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // Sub-tabs row
        int selCatIdx = Array.IndexOf(cats, hollowButtonCategory);
        var subTabs = HollowKit.HollowSubTabs(cats, selCatIdx, newIdx =>
        {
            hollowButtonCategory = cats[newIdx];
            hollowButtonIndex = 0;
            SelectControl(HollowCategories[hollowButtonCategory][0]);
            HollowShowButtons();
        });
        Canvas.SetLeft(subTabs, 16);
        Canvas.SetTop(subTabs, 68);
        content.Children.Add(subTabs);

        // ==========================================
        // LEFT FRAMED PANEL: Controller & All Assignments List
        // ==========================================
        var leftSp = new StackPanel { Width = 610 };

        // Front / Back Segmented Pill
        var frontBackSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
        var frontBtn = new Border
        {
            Padding = new Thickness(14, 4, 14, 4),
            Background = IsRear ? Brushes.Transparent : HollowKit.RaisedBrush,
            BorderBrush = IsRear ? new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)) : HollowKit.IvoryBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12, 0, 0, 12),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        frontBtn.Child = new TextBlock { Text = "FRONT", FontFamily = HollowKit.Display, FontSize = 11, Foreground = IsRear ? HollowKit.MutedTextBrush : HollowKit.IvoryTextBrush };
        frontBtn.MouseLeftButtonUp += (_, _) => { ShowController(false); HollowShowButtons(); };

        var rearBtn = new Border
        {
            Padding = new Thickness(14, 4, 14, 4),
            Background = IsRear ? HollowKit.RaisedBrush : Brushes.Transparent,
            BorderBrush = IsRear ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0, 12, 12, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        rearBtn.Child = new TextBlock { Text = "BACK", FontFamily = HollowKit.Display, FontSize = 11, Foreground = IsRear ? HollowKit.IvoryTextBrush : HollowKit.MutedTextBrush };
        rearBtn.MouseLeftButtonUp += (_, _) => { ShowController(true); HollowShowButtons(); };

        frontBackSp.Children.Add(frontBtn);
        frontBackSp.Children.Add(rearBtn);
        leftSp.Children.Add(frontBackSp);

        // Controller Visual
        var controllerCanvas = new Canvas { Width = 560, Height = 250, HorizontalAlignment = HorizontalAlignment.Center };
        var controllerPedestal = new Ellipse
        {
            Width = 460,
            Height = 110,
            Fill = new RadialGradientBrush(Color.FromArgb(0x40, 0x14, 0x22, 0x32), Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        Canvas.SetLeft(controllerPedestal, 50);
        Canvas.SetTop(controllerPedestal, 130);
        controllerCanvas.Children.Add(controllerPedestal);

        var controllerImg = HollowKit.ControllerArt(Model.Id, 380);
        controllerImg.Width = 380;
        controllerImg.Height = 220;
        Canvas.SetLeft(controllerImg, 90);
        Canvas.SetTop(controllerImg, 15);
        controllerCanvas.Children.Add(controllerImg);

        // Control indicator
        var indicatorSp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 8)
        };
        indicatorSp.Children.Add(new TextBlock { Text = "Selected control: ", FontFamily = HollowKit.Display, FontSize = 13, Foreground = HollowKit.MutedTextBrush });
        indicatorSp.Children.Add(new TextBlock { Text = focusedBtn, FontFamily = HollowKit.Display, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = HollowKit.IvoryTextBrush });
        controllerCanvas.Children.Add(indicatorSp);
        Canvas.SetLeft(indicatorSp, 210);
        Canvas.SetTop(indicatorSp, 226);

        leftSp.Children.Add(controllerCanvas);

        // "ALL ASSIGNMENTS (CATEGORY)" List
        var listHeader = new TextBlock
        {
            Text = $"ALL ASSIGNMENTS ({hollowButtonCategory.ToUpperInvariant()})",
            FontFamily = HollowKit.Display,
            FontSize = 12,
            Foreground = HollowKit.MutedTextBrush,
            Margin = new Thickness(4, 10, 0, 6)
        };
        leftSp.Children.Add(listHeader);

        var listBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x80, 0x07, 0x10, 0x18)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(6),
            MaxHeight = 175
        };
        var listStack = new StackPanel();
        for (int i = 0; i < currentButtons.Length; i++)
        {
            int bIdx = i;
            string key = currentButtons[i];
            bool isCurrent = (key == focusedBtn);

            var rowGrid = new Grid
            {
                Margin = new Thickness(0, 2, 0, 2),
                Background = isCurrent ? HollowKit.RaisedBrush : Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Key badge
            var badge = new Border
            {
                Width = 26,
                Height = 22,
                Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x14, 0x22, 0x32)),
                BorderBrush = isCurrent ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock { Text = key, FontFamily = HollowKit.Display, FontSize = 11, Foreground = HollowKit.IvoryTextBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(badge, 0);

            // Assignment text
            string target = Draft.Target(key);
            var mapText = new TextBlock
            {
                Text = $"{key}  →  {target}",
                FontFamily = HollowKit.Display,
                FontSize = 12.5,
                Foreground = isCurrent ? HollowKit.IvoryTextBrush : HollowKit.SoftTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(mapText, 1);

            rowGrid.Children.Add(badge);
            rowGrid.Children.Add(mapText);

            rowGrid.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                hollowButtonIndex = bIdx;
                SelectControl(key);
                HollowShowButtons();
            };

            listStack.Children.Add(rowGrid);
        }
        listBorder.Child = listStack;
        leftSp.Children.Add(listBorder);

        var leftCard = HollowKit.FiligreeCard(leftSp, padding: 18, cornerSize: 14);
        leftCard.Width = 636;
        leftCard.Height = 580;
        Canvas.SetLeft(leftCard, 16);
        Canvas.SetTop(leftCard, 115);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT FRAMED PANEL: Selected Control Configuration
        // ==========================================
        var rightSp = new StackPanel { Width = 550 };

        // Card Header: e.g. "FACE / A"
        var cardTitle = new TextBlock
        {
            Text = $"{hollowButtonCategory.ToUpperInvariant()} / {focusedBtn}",
            FontFamily = HollowKit.Display,
            FontSize = 20,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 14)
        };
        rightSp.Children.Add(cardTitle);

        // "You press" Key Box
        var youPressSp = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        youPressSp.Children.Add(new TextBlock { Text = "You press", FontFamily = HollowKit.Display, FontSize = 12, Foreground = HollowKit.MutedTextBrush, Margin = new Thickness(0, 0, 0, 6) });
        var youPressBox = new Border
        {
            Width = 90,
            Height = 36,
            Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x14, 0x22, 0x32)),
            BorderBrush = HollowKit.IvoryBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        youPressBox.Child = new TextBlock { Text = focusedBtn, FontFamily = HollowKit.Display, FontSize = 15, Foreground = HollowKit.IvoryTextBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        youPressSp.Children.Add(youPressBox);
        rightSp.Children.Add(youPressSp);

        // "Game gets" Row + Output Chooser
        var gameGetsSp = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        gameGetsSp.Children.Add(new TextBlock { Text = "Game gets", FontFamily = HollowKit.Display, FontSize = 12, Foreground = HollowKit.MutedTextBrush, Margin = new Thickness(0, 0, 0, 6) });

        var getsGrid = new Grid();
        getsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        getsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var gameGetsPill = new Border
        {
            Height = 36,
            Background = new SolidColorBrush(Color.FromArgb(0xbb, 0x07, 0x10, 0x18)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.5)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(12, 0, 12, 0)
        };
        gameGetsPill.Child = new TextBlock
        {
            Text = Label(Draft.Target(focusedBtn)),
            FontFamily = HollowKit.Display,
            FontSize = 13.5,
            Foreground = HollowKit.IvoryTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(gameGetsPill, 0);

        // Choose output button wired to ThemeMappingAction (Required by ThemeReview.cs)
        var chooseFace = new Border
        {
            Background = HollowKit.RaisedBrush,
            BorderBrush = HollowKit.IvoryBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(14, 8, 14, 8)
        };
        chooseFace.Child = new TextBlock
        {
            Text = "Choose output ▾",
            FontFamily = HollowKit.Display,
            FontSize = 12,
            Foreground = HollowKit.IvoryTextBrush
        };
        var chooseBtn = ThemeMappingAction(chooseFace, focusedBtn);
        chooseBtn.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(chooseBtn, 1);

        getsGrid.Children.Add(gameGetsPill);
        getsGrid.Children.Add(chooseBtn);
        gameGetsSp.Children.Add(getsGrid);
        rightSp.Children.Add(gameGetsSp);

        // Diamond divider
        rightSp.Children.Add(HollowKit.DiamondDivider(550, 16));

        // Note text
        var noteBlock = new TextBlock
        {
            Text = "Changes affect the local draft.",
            FontFamily = HollowKit.Body,
            FontSize = 12,
            Foreground = HollowKit.MutedTextBrush,
            Margin = new Thickness(0, 8, 0, 14)
        };
        rightSp.Children.Add(noteBlock);

        // Side-by-side action buttons: "Reset this control" / "Test mapping"
        var twoBtnsGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        twoBtnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        twoBtnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        twoBtnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var resetCtrlBtn = HollowKit.HollowButton("Reset this control", () =>
        {
            Draft.Reset(focusedBtn); HollowLater();
            HollowShowButtons();
        }, isPrimary: false, height: 34, tag: "theme:reset:control");
        Grid.SetColumn(resetCtrlBtn, 0);

        var testMapBtn = HollowKit.HollowButton("Test mapping", () =>
        {
            HollowSelectPage("Tester");
        }, isPrimary: false, height: 34, tag: "theme:test:mapping");
        Grid.SetColumn(testMapBtn, 2);

        twoBtnsGrid.Children.Add(resetCtrlBtn);
        twoBtnsGrid.Children.Add(testMapBtn);
        rightSp.Children.Add(twoBtnsGrid);

        // Full-width "Reset all controls"
        var resetAllBtn = HollowKit.HollowButton("Reset all controls", () =>
        {
            Draft.ResetAll(); HollowLater();
            HollowShowButtons();
        }, isPrimary: false, height: 34, tag: "theme:reset:all");
        resetAllBtn.Margin = new Thickness(0, 0, 0, 16);
        rightSp.Children.Add(resetAllBtn);

        // Primary "Apply to controller" flanked by diamonds matching refs/hollow.png
        int unsentCount = Draft.UnsentChanges;
        string applyLabel = ControllerWrites ? "Apply to controller" : "Apply Changes";
        var applyBtn = HollowKit.HollowPrimaryApplyButton(applyLabel, () =>
        {
            if (ControllerWrites)
            {
                _ = ApplyToController(text => { ChangeCount.Text = text; HollowLater(); });
            }
            else
            {
                Apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                HollowLater();
            }
            HollowShowButtons();
        }, width: double.NaN, height: 44, tag: "theme:apply:buttons");
        applyBtn.Margin = new Thickness(0, 0, 0, 12);
        rightSp.Children.Add(applyBtn);

        // Footer line: "To local draft" left, unsaved count / "Showing what's on the controller." right
        var footGrid = new Grid { Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftFoot = new TextBlock
        {
            Text = "To local draft",
            FontFamily = HollowKit.Body,
            FontSize = 11.5,
            Foreground = HollowKit.MutedTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(leftFoot, 0);

        string unsavedText = unsentCount == 1 ? "1 unsaved local change" : $"{unsentCount} unsaved local changes";
        var rightFoot = new TextBlock
        {
            Text = unsentCount > 0 ? unsavedText : "Showing what's on the controller.",
            FontFamily = HollowKit.Body,
            FontSize = 11.5,
            Foreground = unsentCount > 0 ? HollowKit.AccentBrush : HollowKit.MutedTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(rightFoot, 1);

        footGrid.Children.Add(leftFoot);
        footGrid.Children.Add(rightFoot);
        rightSp.Children.Add(footGrid);

        var rightCard = HollowKit.FiligreeCard(rightSp, padding: 22, cornerSize: 14);
        rightCard.Width = 590;
        rightCard.Height = 580;
        Canvas.SetLeft(rightCard, 674);
        Canvas.SetTop(rightCard, 115);
        content.Children.Add(rightCard);

        board.Children.Add(content);

        // Dock
        var dock = HollowKit.HollowDock("Buttons");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        board.Children.Add(dock);

        hollowPageHost.Children.Add(board);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private string nocturneButtonCategory = "Face";
    private int nocturneButtonIndex = 0;

    private static readonly Dictionary<string, string[]> NocturneCategories = new()
    {
        ["Face"] = ["A", "B", "X", "Y"],
        ["Shoulders"] = ["LB", "RB", "LT", "RT"],
        ["Sticks"] = ["L3", "R3"],
        ["System"] = ["View", "Menu"],
        ["D-pad"] = ["Up", "Down", "Left", "Right"],
        ["Paddles"] = ["M1", "M2", "M3", "M4"]
    };

    private void NocturneShowButtons()
    {
        if (nocturnePageHost == null) return;
        nocturnePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(NocturneKit.CreateCityBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateNocturneHeader("Studio"));
        var sidebar = CreateNocturneSidebar("Buttons");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 74);
        board.Children.Add(sidebar);

        // Sync with SelectedControl
        if (!string.IsNullOrEmpty(SelectedControl))
        {
            foreach (var (cat, bKeys) in NocturneCategories)
            {
                int idx = Array.IndexOf(bKeys, SelectedControl);
                if (idx >= 0)
                {
                    nocturneButtonCategory = cat;
                    nocturneButtonIndex = idx;
                    break;
                }
            }
        }

        string[] cats = ["Face", "Shoulders", "Sticks", "System", "D-pad", "Paddles"];
        if (!cats.Contains(nocturneButtonCategory)) nocturneButtonCategory = "Face";
        var currentButtons = NocturneCategories[nocturneButtonCategory];
        if (nocturneButtonIndex >= currentButtons.Length) nocturneButtonIndex = 0;
        string activeKey = currentButtons[nocturneButtonIndex];

        // 1. Title Block (x=300, y=70)
        var title = NocturneKit.NocturneTitle("BUTTON", "ASSIGNMENTS", "Choose a control, then select what the game receives.");
        Canvas.SetLeft(title, 300);
        Canvas.SetTop(title, 70);
        board.Children.Add(title);

        // 2. Sub-tab strip (x=300, y=210)
        var subTabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var c in cats)
        {
            bool isCur = (c == nocturneButtonCategory);
            var chip = NocturneKit.NocturneChip(c, isCur, () =>
            {
                nocturneButtonCategory = c;
                nocturneButtonIndex = 0;
                NocturneShowButtons();
            }, tag: $"theme:cat:{c.ToLowerInvariant()}");
            subTabs.Children.Add(chip);
        }
        Canvas.SetLeft(subTabs, 300);
        Canvas.SetTop(subTabs, 210);
        board.Children.Add(subTabs);

        // 3. Front / Back toggle (x=300, y=260)
        var fbSp = new StackPanel { Orientation = Orientation.Horizontal };
        bool isRear = IsRear;
        var frontChip = NocturneKit.NocturneChip("Front Face", !isRear, () =>
        {
            if (isRear) { ShowController(false); NocturneShowButtons(); }
        }, tag: "theme:toggle:front");
        var backChip = NocturneKit.NocturneChip("Rear Paddles", isRear, () =>
        {
            if (!isRear) { ShowController(true); NocturneShowButtons(); }
        }, tag: "theme:toggle:back");
        fbSp.Children.Add(frontChip);
        fbSp.Children.Add(backChip);
        Canvas.SetLeft(fbSp, 300);
        Canvas.SetTop(fbSp, 260);
        board.Children.Add(fbSp);

        // 4. Center Controller on cobalt field (x=310, y=300)
        var padArt = NocturneKit.ControllerArt(Model.Id, 440);
        Canvas.SetLeft(padArt, 430);
        Canvas.SetTop(padArt, 270);
        board.Children.Add(padArt);

        // "Selected control: A" label chip
        var selControlPill = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xee, 0x01, 0x1d, 0x69)),
            BorderBrush = NocturneKit.LightBlueBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 5, 14, 5),
            RenderTransform = NocturneKit.CreateSlant(-12),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        selControlPill.Child = new TextBlock
        {
            Text = $"SELECTED CONTROL: {activeKey}",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            Foreground = NocturneKit.WhiteBrush,
            RenderTransform = NocturneKit.CreateSlant(12),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        Canvas.SetLeft(selControlPill, 570);
        Canvas.SetTop(selControlPill, 570);
        board.Children.Add(selControlPill);

        // 5. List Panel "All assignments (Face)" (x=300, y=620, w=640)
        var listCard = new Border
        {
            Width = 660,
            Height = 220,
            Background = new SolidColorBrush(Color.FromArgb(0xf0, 0x01, 0x12, 0x3e)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x56, 0xfb)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14)
        };
        var listStack = new StackPanel();

        var listHeader = new Border
        {
            Background = NocturneKit.WhiteBrush,
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            RenderTransform = NocturneKit.CreateSlant(-12),
            RenderTransformOrigin = new Point(0, 0.5)
        };
        listHeader.Child = new TextBlock
        {
            Text = $"ALL ASSIGNMENTS ({nocturneButtonCategory.ToUpperInvariant()})",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = NocturneKit.InkBrush,
            RenderTransform = NocturneKit.CreateSlant(12),
            RenderTransformOrigin = new Point(0, 0.5)
        };
        listStack.Children.Add(listHeader);

        var rowsGrid = new UniformGrid { Columns = 2, Rows = 2 };
        for (int i = 0; i < currentButtons.Length; i++)
        {
            int btnIdx = i;
            string key = currentButtons[i];
            bool isCurrentBtn = (btnIdx == nocturneButtonIndex);
            string actionText = Draft.Target(key) ?? key;

            var rowBorder = new Border
            {
                Background = isCurrentBtn ? NocturneKit.WhiteBrush : new SolidColorBrush(Color.FromArgb(0xdd, 0x01, 0x1d, 0x69)),
                BorderBrush = isCurrentBtn ? NocturneKit.WhiteBrush : new SolidColorBrush(Color.FromArgb(0x40, 0x5c, 0x80, 0xc8)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(4),
                Cursor = Cursors.Hand,
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Tag = $"theme:btn:{key.ToLowerInvariant()}"
            };

            var rGrid = new Grid
            {
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Square key badge
            var badge = new Border
            {
                Width = 22,
                Height = 22,
                Background = isCurrentBtn ? NocturneKit.ActiveBlueBrush : new SolidColorBrush(Color.FromArgb(0xbb, 0x00, 0x56, 0xfb)),
                BorderBrush = NocturneKit.WhiteBrush,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = key,
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 10.5,
                Foreground = NocturneKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(badge, 0);
            rGrid.Children.Add(badge);

            var mappingTb = new TextBlock
            {
                Text = $"  {key} ➔ {actionText}",
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = isCurrentBtn ? NocturneKit.InkBrush : NocturneKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(mappingTb, 1);
            rGrid.Children.Add(mappingTb);

            var chev = new TextBlock
            {
                Text = "▸",
                FontFamily = NocturneKit.Display,
                FontSize = 12,
                Foreground = isCurrentBtn ? NocturneKit.CobaltBrush : NocturneKit.CoolGreyBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(chev, 2);
            rGrid.Children.Add(chev);

            rowBorder.Child = rGrid;
            rowBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                nocturneButtonIndex = btnIdx;
                NocturneShowButtons();
            };
            rowsGrid.Children.Add(rowBorder);
        }
        listStack.Children.Add(rowsGrid);
        listCard.Child = listStack;
        Canvas.SetLeft(listCard, 300);
        Canvas.SetTop(listCard, 620);
        board.Children.Add(listCard);

        // 6. Right White Slanted Action Card (x=1010, y=80, w=560, h=760)
        var rightStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

        // Card Title: "FACE / A" heavy italic black
        var cTitle = new TextBlock
        {
            Text = $"{nocturneButtonCategory.ToUpperInvariant()} / {activeKey}",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 38,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 14)
        };
        rightStack.Children.Add(cTitle);

        // Thin navy rule
        rightStack.Children.Add(new Rectangle { Height = 2, Fill = new SolidColorBrush(Color.FromArgb(0x40, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 0, 0, 14) });

        // "You press" key box
        var youPressSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        youPressSp.Children.Add(new TextBlock { Text = "YOU PRESS:  ", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = NocturneKit.InkBrush, VerticalAlignment = VerticalAlignment.Center });
        var ypBadge = new Border
        {
            Background = NocturneKit.ActiveBlueBrush,
            Padding = new Thickness(12, 4, 12, 4),
            BorderBrush = NocturneKit.CobaltBrush,
            BorderThickness = new Thickness(1),
            RenderTransform = NocturneKit.CreateSlant(-12),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        ypBadge.Child = new TextBlock { Text = activeKey, FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = NocturneKit.WhiteBrush, RenderTransform = NocturneKit.CreateSlant(12), RenderTransformOrigin = new Point(0.5, 0.5) };
        youPressSp.Children.Add(ypBadge);
        rightStack.Children.Add(youPressSp);

        // "Game gets" dropdown / selector
        rightStack.Children.Add(new TextBlock { Text = "GAME RECEIVES:", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = NocturneKit.InkBrush, Margin = new Thickness(0, 4, 0, 4) });

        var gameGetsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        string[] candidateActions = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "View", "Menu", "Disabled"];
        string currentAction = Draft.Target(activeKey) ?? activeKey;

        foreach (var act in candidateActions)
        {
            bool isAct = (act == currentAction);
            var aChip = new Border
            {
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(2),
                Background = isAct ? NocturneKit.ActiveBlueBrush : new SolidColorBrush(Color.FromArgb(0x18, 0x01, 0x00, 0x42)),
                BorderBrush = isAct ? NocturneKit.CobaltBrush : new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Tag = $"theme:action:{act.ToLowerInvariant()}"
            };
            aChip.Child = new TextBlock
            {
                Text = act,
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = isAct ? NocturneKit.WhiteBrush : NocturneKit.InkBrush,
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            string assignTarget = act;
            aChip.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Draft.Set(activeKey, assignTarget == "Disabled" ? "" : assignTarget);
                NocturneShowButtons();
            };
            gameGetsWrap.Children.Add(aChip);
        }
        rightStack.Children.Add(gameGetsWrap);

        // Outlined "Choose output" button wired to ThemeMappingAction (per spec and ThemeReview.cs)
        var chooseFace = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = NocturneKit.ActiveBlueBrush,
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 0, 10),
            RenderTransform = NocturneKit.CreateSlant(-12),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        chooseFace.Child = new TextBlock
        {
            Text = "CHOOSE OUTPUT ▾",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyles.Italic,
            FontSize = 11,
            Foreground = NocturneKit.ActiveBlueBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransform = NocturneKit.CreateSlant(12),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        var chooseBtn = ThemeMappingAction(chooseFace, activeKey);
        chooseBtn.HorizontalAlignment = HorizontalAlignment.Left;
        chooseBtn.Margin = new Thickness(0, 0, 0, 10);
        rightStack.Children.Add(chooseBtn);

        // Thin navy rule
        rightStack.Children.Add(new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 0, 0, 10) });

        // Note: "Changes affect the local draft."
        rightStack.Children.Add(new TextBlock
        {
            Text = "Changes affect the local draft.",
            FontFamily = NocturneKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0x01, 0x00, 0x42)),
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Buttons: "Reset this control" & "Test mapping"
        var rowActions = new Grid { Margin = new Thickness(0, 0, 0, 10), HorizontalAlignment = HorizontalAlignment.Stretch };
        rowActions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowActions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        rowActions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var resetControlBtn = NocturneKit.NocturneButton("Reset Control", () =>
        {
            Draft.Reset(activeKey);
            NocturneShowButtons();
        }, isPrimary: false, width: null, height: 34, tag: "theme:buttons:reset-control");
        Grid.SetColumn(resetControlBtn, 0);

        var testMappingBtn = NocturneKit.NocturneButton("Test Mapping", () =>
        {
            ShowManagement("Tester");
        }, isPrimary: false, width: null, height: 34, tag: "theme:buttons:test-mapping");
        Grid.SetColumn(testMappingBtn, 2);

        rowActions.Children.Add(resetControlBtn);
        rowActions.Children.Add(testMappingBtn);
        rightStack.Children.Add(rowActions);

        // "Reset all controls" full width
        var resetAllBtn = NocturneKit.NocturneButton("Reset All Controls", () =>
        {
            Draft.ResetAll();
            NocturneShowButtons();
        }, isPrimary: false, width: null, height: 34, tag: "theme:buttons:reset-all");
        resetAllBtn.Margin = new Thickness(0, 0, 0, 14);
        rightStack.Children.Add(resetAllBtn);

        // Big blue Apply button wired to real controller or local draft
        int unsentCount = Draft.UnsentChanges;
        string applyLabel = ControllerWrites ? "Apply to controller" : "Apply Changes";
        var applyBtn = NocturneKit.NocturneButton(applyLabel, () =>
        {
            if (ControllerWrites)
            {
                _ = ApplyToController(text => { ChangeCount.Text = text; NocturneLater(); });
            }
            else
            {
                Apply.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                NocturneLater();
            }
            NocturneShowButtons();
        }, isPrimary: true, width: null, height: 44, tag: "theme:apply:buttons");
        rightStack.Children.Add(applyBtn);

        // Status Line: "To local draft / 1 unsaved local change / Showing what's on the controller."
        var draftStatus = new Grid { Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
        draftStatus.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        draftStatus.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dsLeft = new TextBlock
        {
            Text = "To local draft",
            FontFamily = NocturneKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0x01, 0x00, 0x42)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dsLeft, 0);

        string unsavedText = unsentCount == 1 ? "1 unsaved local change" : $"{unsentCount} unsaved local changes";
        var dsRight = new TextBlock
        {
            Text = unsentCount > 0 ? unsavedText : "Showing what's on the controller.",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyles.Italic,
            FontSize = 11,
            Foreground = unsentCount > 0 ? NocturneKit.CobaltBrush : new SolidColorBrush(Color.FromArgb(0x99, 0x01, 0x00, 0x42)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dsRight, 1);

        draftStatus.Children.Add(dsLeft);
        draftStatus.Children.Add(dsRight);
        rightStack.Children.Add(draftStatus);

        var whiteCard = NocturneKit.NocturneWhiteCard(rightStack, width: 570, height: 750);
        Canvas.SetLeft(whiteCard, 1000);
        Canvas.SetTop(whiteCard, 68);
        board.Children.Add(whiteCard);

        // Bottom Bar
        var bottomBar = CreateNocturneBottomBar();
        Canvas.SetLeft(bottomBar, 0);
        Canvas.SetTop(bottomBar, 856);
        board.Children.Add(bottomBar);

        nocturnePageHost.Children.Add(board);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private string sunriseButtonCategory = "Face";
    private int sunriseButtonIndex = 0;

    private void SunriseShowButtons()
    {
        if (sunrisePageHost == null) return;
        sunrisePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Top bar & Sidebar
        board.Children.Add(CreateSunriseHeader("Studio"));
        var sidebar = CreateSunriseSidebar("Buttons");
        Canvas.SetLeft(sidebar, 16);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // 1. One-line Display Title
        var titleSp = SunriseKit.SunriseTitle("BUTTON ASSIGNMENTS", "Choose a control, then select what the game receives.");
        Canvas.SetLeft(titleSp, 272);
        Canvas.SetTop(titleSp, 75);
        board.Children.Add(titleSp);

        // 2. Sub-tab strip (Face, Shoulders, D-Pad, System, Paddles)
        var subTabs = new StackPanel { Orientation = Orientation.Horizontal };
        var categories = new[] { "Face", "Shoulders", "D-Pad", "System", "Paddles" };

        foreach (var cat in categories)
        {
            bool isCat = (cat == sunriseButtonCategory);
            string thisCat = cat;
            var cChip = SunriseKit.SunriseChip(cat, isCat, () =>
            {
                sunriseButtonCategory = thisCat;
                sunriseButtonIndex = 0;
                SunriseShowButtons();
            }, tag: $"theme:cat:{cat.ToLowerInvariant()}");
            subTabs.Children.Add(cChip);
        }
        Canvas.SetLeft(subTabs, 272);
        Canvas.SetTop(subTabs, 160);
        board.Children.Add(subTabs);

        // Filter button list
        var currentButtons = sunriseButtonCategory switch
        {
            "Shoulders" => new[] { "LB", "RB", "LT", "RT" },
            "D-Pad" => new[] { "DPAD UP", "DPAD DOWN", "DPAD LEFT", "DPAD RIGHT" },
            "System" => new[] { "VIEW", "MENU", "GUIDE" },
            "Paddles" => new[] { "M1", "M2", "M3", "M4" },
            _ => new[] { "A", "B", "X", "Y" }
        };

        if (sunriseButtonIndex >= currentButtons.Length) sunriseButtonIndex = 0;
        string activeKey = currentButtons[sunriseButtonIndex];

        // =========================================================================
        // 3. Left Card: Front/Back, Controller Art, and Assignments List
        // =========================================================================
        var leftSp = new StackPanel { Width = 730 };

        // Front / Back Capsule Toggle
        var fbSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
        bool isRear = IsRear;
        var frontChip = SunriseKit.SunriseChip("Front Face", !isRear, () =>
        {
            if (isRear) { ShowController(false); SunriseShowButtons(); }
        }, tag: "theme:toggle:front");
        var backChip = SunriseKit.SunriseChip("Rear Paddles", isRear, () =>
        {
            if (!isRear) { ShowController(true); SunriseShowButtons(); }
        }, tag: "theme:toggle:back");
        fbSp.Children.Add(frontChip);
        fbSp.Children.Add(backChip);
        leftSp.Children.Add(fbSp);

        // Controller Art with concentric rings behind it
        var padArt = SunriseKit.ControllerArt(Model.Id, 460);
        leftSp.Children.Add(padArt);

        // Selected control chip
        var selControlPill = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = SunriseKit.InkBrush,
            Padding = new Thickness(14, 5, 14, 5),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 12)
        };
        selControlPill.Child = new TextBlock
        {
            Text = $"SELECTED CONTROL: {activeKey}",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = SunriseKit.CreamBrush
        };
        leftSp.Children.Add(selControlPill);

        // Assignments List
        var listHeader = new Border
        {
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Background = SunriseKit.InkBrush,
            Padding = new Thickness(14, 8, 14, 8)
        };
        listHeader.Child = new TextBlock
        {
            Text = $"ALL ASSIGNMENTS ({sunriseButtonCategory.ToUpperInvariant()})",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 12,
            Foreground = SunriseKit.CreamBrush
        };
        leftSp.Children.Add(listHeader);

        var rowsGrid = new UniformGrid { Columns = 2, Rows = (int)Math.Ceiling(currentButtons.Length / 2.0) };
        for (int i = 0; i < currentButtons.Length; i++)
        {
            int btnIdx = i;
            string key = currentButtons[i];
            bool isCurrentBtn = (btnIdx == sunriseButtonIndex);
            string actionText = Draft.Target(key) ?? key;

            var rowBorder = new Border
            {
                Background = isCurrentBtn ? SunriseKit.InkBrush : SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(3),
                Cursor = Cursors.Hand
            };

            var rGrid = new Grid();
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Key badge
            var badge = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(4),
                Background = isCurrentBtn ? SunriseKit.OrangeBrush : SunriseKit.InkBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = key,
                FontFamily = SunriseKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = SunriseKit.CreamBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(badge, 0);
            rGrid.Children.Add(badge);

            var mappingTb = new TextBlock
            {
                Text = $"  {key} ➔ {actionText}",
                FontFamily = SunriseKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = isCurrentBtn ? SunriseKit.CreamBrush : SunriseKit.InkBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(mappingTb, 1);
            rGrid.Children.Add(mappingTb);

            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = SunriseKit.OrangeBrush,
                Visibility = isCurrentBtn ? Visibility.Visible : Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(dot, 2);
            rGrid.Children.Add(dot);

            rowBorder.Child = rGrid;
            rowBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                sunriseButtonIndex = btnIdx;
                SunriseShowButtons();
            };
            rowsGrid.Children.Add(rowBorder);
        }
        leftSp.Children.Add(rowsGrid);

        var leftCard = SunriseKit.CreateStickerCard(leftSp, 768, 620, cornerRadius: 22);
        Canvas.SetLeft(leftCard, 272);
        Canvas.SetTop(leftCard, 215);
        board.Children.Add(leftCard);

        // =========================================================================
        // 4. Right TV Screen Panel with Twin Antennae (x=1054, y=170)
        // =========================================================================
        var tvStack = new StackPanel();

        // Title: orange ring dot + category/key name
        var tvTitleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        tvTitleRow.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = SunriseKit.OrangeBrush, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
        tvTitleRow.Children.Add(new TextBlock
        {
            Text = $"{sunriseButtonCategory.ToUpperInvariant()} / {activeKey}",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 26,
            Foreground = SunriseKit.InkBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        tvStack.Children.Add(tvTitleRow);

        tvStack.Children.Add(new Rectangle { Height = 2, Fill = SunriseKit.InkBrush, Margin = new Thickness(0, 0, 0, 10) });

        // "You press" key box
        var youPressSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        youPressSp.Children.Add(new TextBlock { Text = "YOU PRESS:  ", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.InkBrush, VerticalAlignment = VerticalAlignment.Center });
        var ypBadge = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = SunriseKit.InkBrush,
            Padding = new Thickness(12, 3, 12, 3)
        };
        ypBadge.Child = new TextBlock { Text = activeKey, FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.CreamBrush };
        youPressSp.Children.Add(ypBadge);
        tvStack.Children.Add(youPressSp);

        // "Game gets" action grid
        tvStack.Children.Add(new TextBlock { Text = "GAME RECEIVES:", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.InkBrush, Margin = new Thickness(0, 2, 0, 4) });

        var gameGetsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        string[] candidateActions = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "View", "Menu", "Disabled"];
        string currentAction = Draft.Target(activeKey) ?? activeKey;

        foreach (var act in candidateActions)
        {
            bool isAct = (act == currentAction);
            var aChip = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(2),
                Background = isAct ? SunriseKit.OrangeBrush : SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(isAct ? 0 : 1.5),
                Cursor = Cursors.Hand,
                Tag = $"theme:action:{act.ToLowerInvariant()}"
            };
            aChip.Child = new TextBlock
            {
                Text = act,
                FontFamily = SunriseKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = isAct ? SunriseKit.CreamBrush : SunriseKit.InkBrush
            };

            string assignTarget = act;
            aChip.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Draft.Set(activeKey, assignTarget == "Disabled" ? "" : assignTarget);
                SunriseShowButtons();
            };
            gameGetsWrap.Children.Add(aChip);
        }
        tvStack.Children.Add(gameGetsWrap);

        // Outlined "Choose output" cream pill wired to ThemeMappingAction
        var chooseFace = new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = SunriseKit.CreamBrush,
            BorderBrush = SunriseKit.InkBrush,
            BorderThickness = new Thickness(2),
            Padding = new Thickness(14, 5, 14, 5),
            Margin = new Thickness(0, 0, 0, 8)
        };
        chooseFace.Child = new TextBlock
        {
            Text = "CHOOSE OUTPUT ▾",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = SunriseKit.InkBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var chooseBtn = ThemeMappingAction(chooseFace, activeKey);
        chooseBtn.HorizontalAlignment = HorizontalAlignment.Left;
        chooseBtn.Margin = new Thickness(0, 0, 0, 8);
        tvStack.Children.Add(chooseBtn);

        // Thin rule
        tvStack.Children.Add(new Rectangle { Height = 1.5, Fill = new SolidColorBrush(Color.FromArgb(0x40, 0x1E, 0x1D, 0x1A)), Margin = new Thickness(0, 0, 0, 8) });

        // Note: "Changes affect the local draft."
        tvStack.Children.Add(new TextBlock
        {
            Text = ControllerWrites ? "Configure assignments and apply to save directly to the controller profile." : "Changes affect the local draft · Apply to controller is active on supported devices.",
            FontFamily = SunriseKit.Body,
            FontSize = 11.5,
            Foreground = SunriseKit.OliveBrush,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        });

        // "Reset this control" & "Test mapping" pills
        var rowActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var resetControlBtn = SunriseKit.SunriseButton("Reset Control", () =>
        {
            Draft.Reset(activeKey);
            SunriseShowButtons();
        }, isPrimary: false, width: 140, height: 34, tag: "theme:buttons:reset-control");
        resetControlBtn.Margin = new Thickness(0, 0, 8, 0);

        var testMappingBtn = SunriseKit.SunriseButton("Test Mapping", () =>
        {
            ShowManagement("Tester");
        }, isPrimary: false, width: 140, height: 34, tag: "theme:buttons:test-mapping");

        rowActions.Children.Add(resetControlBtn);
        rowActions.Children.Add(testMappingBtn);
        tvStack.Children.Add(rowActions);

        // "Reset all controls" pill
        var resetAllBtn = SunriseKit.SunriseButton("Reset All Controls", () =>
        {
            Draft.ResetAll();
            SunriseShowButtons();
        }, isPrimary: false, width: 288, height: 34, tag: "theme:buttons:reset-all");
        resetAllBtn.Margin = new Thickness(0, 0, 0, 10);
        tvStack.Children.Add(resetAllBtn);

        // Real Apply to controller button
        if (ControllerWrites)
        {
            var applyChangesBtn = SunriseKit.SunriseButton("APPLY TO CONTROLLER", () =>
            {
                _ = ApplyToController(text => { ChangeCount.Text = text; SunriseLater(); });
                SunriseShowButtons();
            }, isPrimary: true, width: null, height: 42, tag: "theme:buttons:apply");
            tvStack.Children.Add(applyChangesBtn);
        }
        else
        {
            var pcmodeNote = new TextBlock
            {
                Text = Draft.PcMode ? "Changes apply through active PC mode." : "Changes affect the local draft · Apply to controller is active on supported devices.",
                FontFamily = SunriseKit.Body,
                FontSize = 11.5,
                Foreground = SunriseKit.OliveBrush,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            tvStack.Children.Add(pcmodeNote);
        }

        // Status Line: "To local draft / 1 unsaved local change" or "Showing what's on the controller."
        var draftStatus = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        draftStatus.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        draftStatus.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dsLeft = new TextBlock { Text = "📄 To local draft", FontFamily = SunriseKit.Body, FontSize = 11, Foreground = SunriseKit.OliveBrush };
        Grid.SetColumn(dsLeft, 0);
        draftStatus.Children.Add(dsLeft);

        var dsRight = new TextBlock
        {
            Text = (Draft.UnsentChanges > 0) ? $"{Draft.UnsentChanges} unsaved local change{(Draft.UnsentChanges == 1 ? "" : "s")}" : "Showing what's on the controller.",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = (Draft.UnsentChanges > 0) ? SunriseKit.OrangeBrush : SunriseKit.OliveBrush
        };
        Grid.SetColumn(dsRight, 1);
        draftStatus.Children.Add(dsRight);
        tvStack.Children.Add(draftStatus);

        var tvCard = SunriseKit.CreateTvScreenCard(tvStack, 516, 615, withAntennae: true);
        Canvas.SetLeft(tvCard, 1054);
        Canvas.SetTop(tvCard, 170);
        board.Children.Add(tvCard);

        // Bottom Bar
        var bBar = CreateSunriseBottomBar();
        Canvas.SetLeft(bBar, 0);
        Canvas.SetTop(bBar, 856);
        board.Children.Add(bBar);

        sunrisePageHost.Children.Add(board);
    }
}

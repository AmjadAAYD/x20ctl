using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private string tideButtonCategory = "Face";
    private int tideButtonIndex = 0;

    private static readonly Dictionary<string, string[]> CategoryButtons = new()
    {
        ["Face"] = ["A", "B", "X", "Y"],
        ["D-pad"] = ["Up", "Down", "Left", "Right"],
        ["Bumpers"] = ["LB", "RB"],
        ["Triggers"] = ["LT", "RT"],
        ["Rear"] = ["M1", "M2", "M3", "M4"],
        ["Sticks"] = ["L3", "R3"],
        ["System"] = ["View", "Menu"]
    };

    private static readonly Dictionary<string, (double fx, double fy)> ControlOffsets = new()
    {
        ["A"] = (0.760, 0.430),
        ["B"] = (0.815, 0.370),
        ["X"] = (0.705, 0.370),
        ["Y"] = (0.760, 0.310),
        ["Up"] = (0.330, 0.490),
        ["Down"] = (0.330, 0.610),
        ["Left"] = (0.270, 0.550),
        ["Right"] = (0.390, 0.550),
        ["LB"] = (0.270, 0.160),
        ["RB"] = (0.730, 0.160),
        ["LT"] = (0.220, 0.080),
        ["RT"] = (0.780, 0.080),
        ["M1"] = (0.360, 0.680),
        ["M2"] = (0.640, 0.680),
        ["M3"] = (0.320, 0.740),
        ["M4"] = (0.680, 0.740),
        ["L3"] = (0.260, 0.380),
        ["R3"] = (0.660, 0.550),
        ["View"] = (0.440, 0.370),
        ["Menu"] = (0.560, 0.370)
    };

    private void TideShowButtons()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Cinematic Sea Background
        board.Children.Add(TideKit.SeaBackground(2, "BUTTON REMAP"));

        // Sync with SelectedControl if active
        if (!string.IsNullOrEmpty(SelectedControl))
        {
            foreach (var (cat, bKeys) in CategoryButtons)
            {
                int idx = Array.IndexOf(bKeys, SelectedControl);
                if (idx >= 0)
                {
                    tideButtonCategory = cat;
                    tideButtonIndex = idx;
                    break;
                }
            }
        }

        string[] groups = ["Face", "D-pad", "Bumpers", "Triggers", "Rear", "Sticks", "System"];
        if (!groups.Contains(tideButtonCategory)) tideButtonCategory = "Face";

        var currentButtons = CategoryButtons.TryGetValue(tideButtonCategory, out var curList) ? curList : ["A", "B", "X", "Y"];
        if (tideButtonIndex >= currentButtons.Length) tideButtonIndex = 0;
        string focusedBtn = currentButtons[tideButtonIndex];

        // 2. Top Header Bar: Logo, Title, Category Ribbon, Actions
        var topBar = new Canvas { Width = 1520, Height = 64 };
        Canvas.SetLeft(topBar, 40);
        Canvas.SetTop(topBar, 20);

        // Title
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var moonIcon = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = new RadialGradientBrush
            {
                Center = new Point(0.4, 0.4),
                GradientStops =
                {
                    new(Colors.White, 0.0),
                    new(Color.FromRgb(0x9f, 0xf6, 0xff), 0.7),
                    new(Color.FromRgb(0x3a, 0xa5, 0xff), 1.0)
                }
            },
            Margin = new Thickness(0, 0, 12, 0),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x9f, 0xe0, 0xff), BlurRadius = 14, ShadowDepth = 0 }
        };
        titleStack.Children.Add(moonIcon);
        titleStack.Children.Add(new TextBlock
        {
            Text = "TIDE // BUTTON REMAP STUDIO",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 20,
            Foreground = TideKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        topBar.Children.Add(titleStack);
        Canvas.SetLeft(titleStack, 0);
        Canvas.SetTop(titleStack, 14);

        // Category Ribbon
        var catPanel = new Border
        {
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(Color.FromArgb(0xbb, 0x06, 0x14, 0x38)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x9f, 0xf6, 0xff)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 3, 4, 3)
        };
        var catStack = new StackPanel { Orientation = Orientation.Horizontal };

        foreach (var g in groups)
        {
            bool isCatActive = (g == tideButtonCategory);
            var catBtn = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                Background = isCatActive
                    ? new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1),
                        GradientStops =
                        {
                            new(Color.FromRgb(0x18, 0x5a, 0xe0), 0.0),
                            new(Color.FromRgb(0x0c, 0x32, 0x96), 1.0)
                        }
                    }
                    : Brushes.Transparent,
                BorderBrush = isCatActive ? TideKit.CyanFoamBrush : Brushes.Transparent,
                BorderThickness = new Thickness(isCatActive ? 1 : 0)
            };

            var catLabel = new TextBlock
            {
                Text = g.ToUpperInvariant(),
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                Foreground = isCatActive ? TideKit.WhiteBrush : new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8))
            };
            catBtn.Child = catLabel;

            string targetGroup = g;
            catBtn.MouseLeftButtonUp += (_, _) =>
            {
                tideButtonCategory = targetGroup;
                tideButtonIndex = 0;
                var bList = CategoryButtons[targetGroup];
                if (bList.Length > 0) SelectControl(bList[0]);
                TideShowButtons();
            };

            catStack.Children.Add(catBtn);
        }
        catPanel.Child = catStack;
        topBar.Children.Add(catPanel);
        Canvas.SetLeft(catPanel, 450);
        Canvas.SetTop(catPanel, 10);

        // Right Actions: Reset All & Back
        var actStack = new StackPanel { Orientation = Orientation.Horizontal };

        var resetAllBtn = TideKit.OceanButton("RESET ALL", icon: "\uE777", onClick: () =>
        {
            Draft.ResetAll();
            TideShowButtons();
        });
        resetAllBtn.Margin = new Thickness(0, 0, 10, 0);
        actStack.Children.Add(resetAllBtn);

        var backBtn = TideKit.OceanButton("DASHBOARD", icon: "\uE72B", onClick: () => TideShowDashboard());
        actStack.Children.Add(backBtn);

        topBar.Children.Add(actStack);
        Canvas.SetRight(actStack, 0);
        Canvas.SetTop(actStack, 10);

        board.Children.Add(topBar);

        // 3. Left Stage — Interactive Controller Artwork with Active Control Marker
        double padX = 60, padY = 120, padW = 680;
        var padArt = TideKit.ControllerArt(Model.Id, padW);
        Canvas.SetLeft(padArt, padX);
        Canvas.SetTop(padArt, padY + 40);
        board.Children.Add(padArt);
        TideKit.AnimateFloat(padArt, 0, 6, 4.2);

        // Animated Harpoon Red Targeting Marker over active button
        var activeMarker = TideKit.ActiveControlMarker(68);
        if (TryCroppedControlCenter(focusedBtn, out var realFx, out var realFy))
        {
            Canvas.SetLeft(activeMarker, padX + realFx * padW - 34);
            Canvas.SetTop(activeMarker, padY + 40 + realFy * (padW * 739 / 1100) - 34);
        }
        else if (ControlOffsets.TryGetValue(focusedBtn, out var off))
        {
            double cx = padX + off.fx * padW - 34;
            double cy = padY + 40 + off.fy * (padW * 0.66) - 34;
            Canvas.SetLeft(activeMarker, cx);
            Canvas.SetTop(activeMarker, cy);
        }
        else
        {
            Canvas.SetLeft(activeMarker, padX + 0.76 * padW - 34);
            Canvas.SetTop(activeMarker, padY + 40 + 0.40 * (padW * 0.66) - 34);
        }
        board.Children.Add(activeMarker);

        // Physical Control Selector Chips below controller
        var selectorRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        for (int i = 0; i < currentButtons.Length; i++)
        {
            int bIdx = i;
            string bKey = currentButtons[i];
            bool isCurrent = (i == tideButtonIndex);
            string assigned = Draft.Target(bKey);

            var chip = new Border
            {
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(5, 0, 5, 0),
                Cursor = Cursors.Hand,
                Background = isCurrent
                    ? new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1),
                        GradientStops =
                        {
                            new(Color.FromRgb(0x1a, 0x5e, 0xee), 0.0),
                            new(Color.FromRgb(0x0e, 0x36, 0xa0), 1.0)
                        }
                    }
                    : new SolidColorBrush(Color.FromArgb(0xaa, 0x06, 0x16, 0x40)),
                BorderBrush = isCurrent ? TideKit.HarpoonRedBrush : new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(isCurrent ? 2 : 1),
                Effect = isCurrent
                    ? new DropShadowEffect { Color = TideKit.HarpoonRed, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.9 }
                    : null
            };

            var chipContent = new StackPanel { Orientation = Orientation.Horizontal };
            chipContent.Children.Add(new TextBlock
            {
                Text = bKey,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 14,
                Foreground = isCurrent ? TideKit.WhiteBrush : TideKit.CyanFoamBrush,
                Margin = new Thickness(0, 0, 6, 0)
            });
            chipContent.Children.Add(new TextBlock
            {
                Text = $"▸ {assigned}",
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = isCurrent ? TideKit.CyanFoamBrush : new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8))
            });
            chip.Child = chipContent;

            chip.MouseLeftButtonUp += (_, _) =>
            {
                tideButtonIndex = bIdx;
                SelectControl(bKey);
                TideShowButtons();
            };

            selectorRow.Children.Add(chip);
        }

        Canvas.SetLeft(selectorRow, padX + padW / 2 - 200);
        Canvas.SetTop(selectorRow, padY + padW * 0.66 + 60);
        board.Children.Add(selectorRow);

        // 4. Right Stage — Crystalline Button Remap Studio Console
        var consoleCard = TideKit.OceanGlassCard(w: 660, h: 680);
        Canvas.SetLeft(consoleCard, 850);
        Canvas.SetTop(consoleCard, 100);

        var consoleStack = new StackPanel();

        // Control Header Plaque
        var plaque = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new(Color.FromArgb(0xcc, 0x18, 0x5a, 0xe0), 0.0),
                    new(Color.FromArgb(0xee, 0x0c, 0x32, 0x96), 1.0)
                }
            },
            BorderBrush = TideKit.CyanFoamBrush,
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(18, 12, 18, 12),
            Margin = new Thickness(0, 0, 0, 16),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x1a, 0x5a, 0xd8), BlurRadius = 18, ShadowDepth = 4 }
        };

        var plaqueGrid = new Grid();
        plaqueGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        plaqueGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var plaqueLeft = new StackPanel();
        plaqueLeft.Children.Add(new TextBlock
        {
            Text = $"BUTTON {focusedBtn}",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 26,
            Foreground = TideKit.WhiteBrush
        });
        plaqueLeft.Children.Add(new TextBlock
        {
            Text = $"CURRENT ASSIGNMENT: {Draft.Target(focusedBtn)}",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(plaqueLeft, 0);
        plaqueGrid.Children.Add(plaqueLeft);

        // Interactive Remap Trigger Button (Required by ThemeReview.cs for "theme:input:A")
        var mapTriggerBtn = ThemeMappingAction(
            new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = TideKit.HarpoonRedBrush,
                Padding = new Thickness(14, 8, 14, 8),
                Child = new TextBlock
                {
                    Text = "REMAP OUTPUT ▾",
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 12,
                    Foreground = TideKit.WhiteBrush
                }
            },
            focusedBtn
        );
        Grid.SetColumn(mapTriggerBtn, 1);
        plaqueGrid.Children.Add(mapTriggerBtn);

        plaque.Child = plaqueGrid;
        consoleStack.Children.Add(plaque);

        // Section Title: Quick Remap Target Matrix
        consoleStack.Children.Add(new TextBlock
        {
            Text = "SELECT TARGET GAMEPAD OUTPUT",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Target Output Matrix Grid
        var targetMatrix = new UniformGrid { Columns = 5, Margin = new Thickness(0, 0, 0, 16) };
        string[] allTargets =
        [
            "A", "B", "X", "Y",
            "LB", "RB", "LT", "RT",
            "Up", "Down", "Left", "Right",
            "L3", "R3", "View", "Menu",
            "Turbo", "Disabled"
        ];

        string currentTarget = Draft.Target(focusedBtn);

        foreach (var tgt in allTargets)
        {
            bool isTargetSelected = (tgt == currentTarget);
            var tBtn = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = isTargetSelected
                    ? TideKit.HarpoonRedBrush
                    : new SolidColorBrush(Color.FromArgb(0xaa, 0x0a, 0x22, 0x54)),
                BorderBrush = isTargetSelected
                    ? new SolidColorBrush(Color.FromRgb(0xff, 0xc0, 0xcc))
                    : new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(isTargetSelected ? 1.5 : 1),
                Padding = new Thickness(8, 10, 8, 10),
                Margin = new Thickness(3),
                Cursor = Cursors.Hand,
                Effect = isTargetSelected
                    ? new DropShadowEffect { Color = TideKit.HarpoonRed, BlurRadius = 14, ShadowDepth = 2, Opacity = 0.9 }
                    : null
            };

            var tText = new TextBlock
            {
                Text = tgt,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 13,
                Foreground = TideKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            tBtn.Child = tText;

            string selectedTgt = tgt;
            tBtn.MouseLeftButtonUp += (_, _) =>
            {
                if (SelectedControl != focusedBtn) SelectControl(focusedBtn);
                SetDraftTarget(selectedTgt);
                TideShowButtons();
            };

            targetMatrix.Children.Add(tBtn);
        }
        consoleStack.Children.Add(targetMatrix);

        // Turbo / Rapid Fire Options
        consoleStack.Children.Add(new TextBlock
        {
            Text = "RAPID FIRE / TURBO SPEED",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var turboRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        string[] turboSpeeds = ["OFF", "5 HZ (STANDARD)", "10 HZ (FAST)", "20 HZ (BURST)"];
        for (int s = 0; s < turboSpeeds.Length; s++)
        {
            bool isTurboOn = (s == 0); // default off
            var tPill = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                Background = isTurboOn
                    ? new SolidColorBrush(Color.FromArgb(0xcc, 0x18, 0x5a, 0xe0))
                    : new SolidColorBrush(Color.FromArgb(0x60, 0x06, 0x16, 0x40)),
                BorderBrush = isTurboOn ? TideKit.CyanFoamBrush : new SolidColorBrush(Color.FromArgb(0x40, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(1)
            };
            tPill.Child = new TextBlock
            {
                Text = turboSpeeds[s],
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = isTurboOn ? TideKit.WhiteBrush : new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8))
            };
            turboRow.Children.Add(tPill);
        }
        consoleStack.Children.Add(turboRow);

        // Bottom Action Bar inside console card
        var bActionRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var resetBtn = TideKit.OceanButton("RESET THIS BUTTON", icon: "\uE777", onClick: () =>
        {
            SetDraftTarget(focusedBtn);
            TideShowButtons();
        });
        resetBtn.Margin = new Thickness(0, 0, 10, 0);
        bActionRow.Children.Add(resetBtn);

        var testBtn = TideKit.SparkButton("TEST IN TESTER ➔", icon: "\uE768", onClick: () => ShowManagement("Tester"));
        bActionRow.Children.Add(testBtn);

        consoleStack.Children.Add(bActionRow);

        consoleCard.Child = consoleStack;
        board.Children.Add(consoleCard);

        // 5. Bottom Navigation Dock
        var navDock = TideKit.OceanDock("Buttons", TideSelectPage);
        Canvas.SetLeft(navDock, 230);
        Canvas.SetTop(navDock, 820);
        board.Children.Add(navDock);

        tidePageHost.Children.Add(board);
    }
}

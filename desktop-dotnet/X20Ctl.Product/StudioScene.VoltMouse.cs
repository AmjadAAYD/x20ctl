using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;
using X20Ctl.Zone;
using ZoneMouseAction = X20Ctl.Zone.Mouse.MouseAction;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool voltMouseWired;
    private string voltMouseDetailTab = "actions"; // "actions", "sensitivity", "scrolling", "presets"

    /// <summary>
    /// Mouse workbench for Volt: Gyroscope air mouse emulation, cursor sensitivity, and button mapping options.
    /// Strictly implements t_volt.py:253 Volt and models.py:mouse.
    /// </summary>
    private UIElement BuildVoltMouse()
    {
        if (Mouse is not { } mouse)
        {
            var empty = new Canvas { Width = 970, Height = 560 };
            empty.Children.Add(new TextBlock { Text = "Mouse emulation unavailable.", Foreground = VoltKit.MuteBrush, FontSize = 18, Margin = new Thickness(40) });
            return VoltKit.TwoPaneShell("mouse", "Mouse", "Desktop mouse emulation, joystick speed curves, and button clicks.", empty, VoltSelectPage);
        }

        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Left List: Mouse options and button mappings (width: 520)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        leftStack.Children.Add(new TextBlock
        {
            Text = "MOUSE EMULATION SETTINGS",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Top toggle: Mouse Emulation Mode
        var swRow = new Border
        {
            MinHeight = 56,
            Background = Brushes.Transparent,
            BorderBrush = VoltKit.B("#222222"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 6, 16, 6),
            Cursor = Cursors.Hand
        };
        var swGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        swGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        swGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        swGrid.Children.Add(new TextBlock
        {
            Text = "Mouse Emulation Mode",
            FontFamily = VoltKit.Display,
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.LightBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var swToggle = new CheckBox
        {
            Content = "Enabled",
            Foreground = VoltKit.WhiteBrush,
            FontFamily = VoltKit.Display,
            FontSize = 15,
            Tag = "theme:mouse:switch",
            VerticalAlignment = VerticalAlignment.Center
        };
        swToggle.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = mouse.Switch, Mode = BindingMode.TwoWay });
        Grid.SetColumn(swToggle, 1);
        swGrid.Children.Add(swToggle);
        swRow.Child = swGrid;
        leftStack.Children.Add(swRow);

        // Tracking source dropdown
        var srcRow = new Border
        {
            MinHeight = 56,
            Background = Brushes.Transparent,
            BorderBrush = VoltKit.B("#222222"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 6, 16, 6)
        };
        var srcGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        srcGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        srcGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        srcGrid.Children.Add(new TextBlock
        {
            Text = "Tracking Source Input",
            FontFamily = VoltKit.Display,
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.LightBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var srcCombo = new ComboBox
        {
            Width = 220,
            Height = 36,
            DisplayMemberPath = mouse.SourceBox.DisplayMemberPath,
            SelectedValuePath = mouse.SourceBox.SelectedValuePath,
            Tag = "theme:mouse:source"
        };
        srcCombo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("ItemsSource") { Source = mouse.SourceBox });
        srcCombo.SetBinding(Selector.SelectedIndexProperty, new Binding("SelectedIndex") { Source = mouse.SourceBox, Mode = BindingMode.TwoWay });
        Grid.SetColumn(srcCombo, 1);
        srcGrid.Children.Add(srcCombo);
        srcRow.Child = srcGrid;
        leftStack.Children.Add(srcRow);

        // Header for Button Mappings
        var mappingsHeaderTb = new TextBlock
        {
            Text = $"BUTTON MAPPINGS · {mouse.MappedText.ToUpperInvariant()}",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 14, 0, 8)
        };
        leftStack.Children.Add(mappingsHeaderTb);

        // Right details elements references for in-place updates
        var card = new Border
        {
            Width = 410,
            Background = VoltKit.SheetDarkBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24, 20, 24, 20)
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };
        card.Child = cStack;

        var detailTitleTb = new TextBlock
        {
            Text = $"BUTTON {mouse.LabelFor(mouse.SelectedKey).ToUpperInvariant()} ASSIGNMENT",
            FontFamily = VoltKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 2)
        };
        cStack.Children.Add(detailTitleTb);

        var detailActionTb = new TextBlock
        {
            Text = mouse.ActionFor(mouse.SelectedKey),
            FontFamily = VoltKit.Display,
            FontSize = 30,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush,
            LineHeight = 34
        };
        cStack.Children.Add(detailActionTb);

        var detailSubTb = new TextBlock
        {
            Text = $"{mouse.StatusText} · {mouse.MappedText}",
            FontFamily = VoltKit.Body,
            FontSize = 14,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            Margin = new Thickness(0, 4, 0, 10)
        };
        cStack.Children.Add(detailSubTb);

        // Map of UI elements for all 17 inputs
        var inputRows = new Dictionary<string, (Border Row, Border Badge, TextBlock BadgeText, TextBlock ActionText)>();
        Action? rebuildActionsPanelAction = null;

        void UpdateSelectionState()
        {
            string sel = mouse.SelectedKey;
            detailTitleTb.Text = $"BUTTON {mouse.LabelFor(sel).ToUpperInvariant()} ASSIGNMENT";
            detailActionTb.Text = mouse.ActionFor(sel);
            detailSubTb.Text = $"{mouse.StatusText} · {mouse.MappedText}";
            mappingsHeaderTb.Text = $"BUTTON MAPPINGS · {mouse.MappedText.ToUpperInvariant()}";

            foreach (var (k, row) in inputRows)
            {
                bool active = k == sel;
                row.Row.Background = active ? VoltKit.B("#2a2a2a") : Brushes.Transparent;
                row.Row.BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#222222");
                row.Row.BorderThickness = new Thickness(active ? 2 : 0, active ? 2 : 0, active ? 2 : 0, active ? 2 : 1);
                row.Row.CornerRadius = active ? new CornerRadius(10) : new CornerRadius(0);

                row.Badge.Background = active ? VoltKit.AmberHotBrush : VoltKit.B("#262626");
                row.BadgeText.Foreground = active ? VoltKit.B("#111111") : VoltKit.WhiteBrush;

                row.ActionText.Text = mouse.ActionFor(k);
                row.ActionText.Foreground = active ? VoltKit.AmberHotBrush : VoltKit.B("#bdbdbd");
            }

            rebuildActionsPanelAction?.Invoke();
        }

        // Tabs row: [ Actions ] [ Sensitivity ] [ Scrolling ] [ Presets ]
        var tabsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var tabBtns = new Dictionary<string, Border>();
        var tabPanels = new Dictionary<string, FrameworkElement>();

        void SwitchTab(string tabKey)
        {
            voltMouseDetailTab = tabKey;
            foreach (var (k, b) in tabBtns)
            {
                bool active = k == tabKey;
                b.Background = active ? VoltKit.AmberHotBrush : VoltKit.B("#262626");
                if (b.Child is TextBlock tb)
                    tb.Foreground = active ? VoltKit.B("#111111") : VoltKit.WhiteBrush;
            }
            foreach (var (k, p) in tabPanels)
            {
                p.Visibility = k == tabKey ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        foreach (var (tKey, tLabel) in new[] { ("actions", "Actions"), ("sensitivity", "Sensitivity"), ("scrolling", "Scrolling"), ("presets", "Presets") })
        {
            string tk = tKey;
            var tBorder = new Border
            {
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                CornerRadius = new CornerRadius(6),
                Background = tKey == voltMouseDetailTab ? VoltKit.AmberHotBrush : VoltKit.B("#262626"),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = tLabel,
                    FontFamily = VoltKit.Display,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = tKey == voltMouseDetailTab ? VoltKit.B("#111111") : VoltKit.WhiteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            tabBtns[tk] = tBorder;
            tabsRow.Children.Add(ThemeAction(tBorder, "theme:mouse:tab:" + tk, () => SwitchTab(tk)));
        }
        cStack.Children.Add(tabsRow);

        // Tab Content Panels:
        // 1. Actions Panel (Scrollable list of all categories and actions)
        var actionsPanel = new StackPanel { Orientation = Orientation.Vertical };
        var actionsScroll = new ScrollViewer
        {
            MaxHeight = 330,
            Content = actionsPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        tabPanels["actions"] = actionsScroll;
        cStack.Children.Add(actionsScroll);

        var actionButtons = new List<(ZoneMouseAction Action, Border Border, TextBlock Text)>();

        void RebuildActionsPanel()
        {
            actionsPanel.Children.Clear();
            actionButtons.Clear();
            var curAction = mouse.CurrentAction;

            foreach (var group in mouse.Groups)
            {
                actionsPanel.Children.Add(new TextBlock
                {
                    Text = group.Group,
                    FontFamily = VoltKit.Display,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = VoltKit.B("#8a8a8a"),
                    Margin = new Thickness(0, 8, 0, 4)
                });

                var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
                foreach (var act in group.Actions)
                {
                    var choice = act;
                    bool isSelected = curAction == choice;

                    var optText = new TextBlock
                    {
                        Text = MouseWorkbench.Friendly(choice),
                        FontFamily = VoltKit.Display,
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = isSelected ? VoltKit.B("#111111") : VoltKit.WhiteBrush,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var optRow = new Border
                    {
                        Height = 34,
                        Background = isSelected ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                        BorderBrush = isSelected ? VoltKit.AmberHotBrush : VoltKit.B("#333333"),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12, 0, 12, 0),
                        Margin = new Thickness(0, 0, 6, 6),
                        Cursor = Cursors.Hand,
                        Child = optText
                    };

                    actionButtons.Add((choice, optRow, optText));

                    wrap.Children.Add(ThemeAction(optRow, "theme:mouse:assign:" + choice, () =>
                    {
                        mouse.Assign(choice);
                        UpdateSelectionState();
                    }));
                }
                actionsPanel.Children.Add(wrap);
            }
        }
        rebuildActionsPanelAction = RebuildActionsPanel;

        // 2. Sensitivity Panel
        var sensitivityPanel = new StackPanel { Orientation = Orientation.Vertical };
        var ptrRow = new Grid { Height = 40, Margin = new Thickness(0, 0, 0, 6) };
        ptrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        ptrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ptrRow.Children.Add(new TextBlock { Text = "Pointer Stick", FontFamily = VoltKit.Display, FontSize = 14, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
        var ptrCombo = new ComboBox
        {
            Height = 32,
            DisplayMemberPath = mouse.PointerBox.DisplayMemberPath,
            SelectedValuePath = mouse.PointerBox.SelectedValuePath,
            Tag = "theme:mouse:pointer"
        };
        ptrCombo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("ItemsSource") { Source = mouse.PointerBox });
        ptrCombo.SetBinding(Selector.SelectedIndexProperty, new Binding("SelectedIndex") { Source = mouse.PointerBox, Mode = BindingMode.TwoWay });
        Grid.SetColumn(ptrCombo, 1);
        ptrRow.Children.Add(ptrCombo);
        sensitivityPanel.Children.Add(ptrRow);

        var sensSliders = new (string Label, RangeBase Source)[]
        {
            ("Pointer Speed", mouse.SpeedSlider),
            ("Response Curve", mouse.CurveSlider),
            ("Dead Zone", mouse.DeadzoneSlider),
            ("Precision Smoothing", mouse.PrecisionSlider)
        };
        foreach (var (lbl, src) in sensSliders)
        {
            var sRow = new Grid { Height = 40, Margin = new Thickness(0, 0, 0, 4) };
            sRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            sRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            sRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sRow.Children.Add(new TextBlock { Text = lbl, FontFamily = VoltKit.Display, FontSize = 14, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
            var slider = ThemeSlider(src, "theme:mouse:" + lbl, 140, true);
            Grid.SetColumn(slider, 1);
            sRow.Children.Add(slider);
            var valTb = new TextBlock { Foreground = VoltKit.AmberHotBrush, FontFamily = VoltKit.Display, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            valTb.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = src, StringFormat = "{0:0.##}" });
            Grid.SetColumn(valTb, 2);
            sRow.Children.Add(valTb);
            sensitivityPanel.Children.Add(sRow);
        }

        var testBtn = new Border
        {
            Height = 36,
            CornerRadius = new CornerRadius(6),
            Background = VoltKit.B("#262626"),
            Margin = new Thickness(0, 10, 0, 0),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "Test Pointer Movement", FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.WhiteBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        sensitivityPanel.Children.Add(ThemeAction(testBtn, "theme:mouse:test", () => mouse.TestPointer()));

        var readoutTb = new TextBlock
        {
            Text = mouse.ReadoutText,
            FontFamily = VoltKit.Body,
            FontSize = 12,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        mouse.WatchReadout(txt => readoutTb.Text = txt);
        sensitivityPanel.Children.Add(readoutTb);

        sensitivityPanel.Visibility = Visibility.Collapsed;
        tabPanels["sensitivity"] = sensitivityPanel;
        cStack.Children.Add(sensitivityPanel);

        // 3. Scrolling Panel
        var scrollingPanel = new StackPanel { Orientation = Orientation.Vertical };
        var scRow = new Grid { Height = 36 };
        scRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        scRow.Children.Add(new TextBlock { Text = "Enable Thumbstick Scrolling", FontFamily = VoltKit.Display, FontSize = 14, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
        var scToggle = new CheckBox { Tag = "theme:mouse:scrolling", VerticalAlignment = VerticalAlignment.Center };
        scToggle.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = mouse.ScrollsBox, Mode = BindingMode.TwoWay });
        Grid.SetColumn(scToggle, 1);
        scRow.Children.Add(scToggle);
        scrollingPanel.Children.Add(scRow);

        var scrSpeedRow = new Grid { Height = 40 };
        scrSpeedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        scrSpeedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        scrSpeedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scrSpeedRow.Children.Add(new TextBlock { Text = "Scroll Speed", FontFamily = VoltKit.Display, FontSize = 14, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
        var scrSlider = ThemeSlider(mouse.ScrollSlider, "theme:mouse:scrollspeed", 140, true);
        Grid.SetColumn(scrSlider, 1);
        scrSpeedRow.Children.Add(scrSlider);
        var scrValTb = new TextBlock { Foreground = VoltKit.AmberHotBrush, FontFamily = VoltKit.Display, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        scrValTb.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = mouse.ScrollSlider, StringFormat = "{0:0.##}" });
        Grid.SetColumn(scrValTb, 2);
        scrSpeedRow.Children.Add(scrValTb);
        scrollingPanel.Children.Add(scrSpeedRow);

        foreach (var (lbl, src) in new (string, CheckBox)[] { ("Horizontal Scroll", mouse.HorizontalBox), ("Invert Vertical", mouse.InvertVBox), ("Invert Horizontal", mouse.InvertHBox) })
        {
            var optRow = new Grid { Height = 34 };
            optRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            optRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optRow.Children.Add(new TextBlock { Text = lbl, FontFamily = VoltKit.Display, FontSize = 13, Foreground = VoltKit.MuteBrush, VerticalAlignment = VerticalAlignment.Center });
            var tog = new CheckBox { Tag = "theme:mouse:" + lbl, VerticalAlignment = VerticalAlignment.Center };
            tog.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = src, Mode = BindingMode.TwoWay });
            Grid.SetColumn(tog, 1);
            optRow.Children.Add(tog);
            scrollingPanel.Children.Add(optRow);
        }

        scrollingPanel.Visibility = Visibility.Collapsed;
        tabPanels["scrolling"] = scrollingPanel;
        cStack.Children.Add(scrollingPanel);

        // 4. Presets & Shortcuts Panel
        var presetsPanel = new StackPanel { Orientation = Orientation.Vertical };
        var qkRow = new Grid { Height = 36 };
        qkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        qkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        qkRow.Children.Add(new TextBlock { Text = "Quick Toggle Chord", FontFamily = VoltKit.Display, FontSize = 14, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
        var qkToggle = new CheckBox { Tag = "theme:mouse:combo", VerticalAlignment = VerticalAlignment.Center };
        qkToggle.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = mouse.ComboToggle, Mode = BindingMode.TwoWay });
        Grid.SetColumn(qkToggle, 1);
        qkRow.Children.Add(qkToggle);
        presetsPanel.Children.Add(qkRow);

        presetsPanel.Children.Add(new TextBlock
        {
            Text = "When shortcut toggle is active, both buttons are reserved and regular clicks will not fire.",
            FontFamily = VoltKit.Body,
            FontSize = 12,
            Foreground = VoltKit.B("#8a8a8a"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 10)
        });

        presetsPanel.Children.Add(new TextBlock
        {
            Text = "CONFIGURATION PRESETS",
            FontFamily = VoltKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 4, 0, 6)
        });

        // Presets row: all 4 presets
        var presetRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        foreach (string preset in MouseWorkbench.PresetNames)
        {
            string pr = preset;
            var pBtn = new Border
            {
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                CornerRadius = new CornerRadius(6),
                Background = VoltKit.B("#262626"),
                Margin = new Thickness(0, 0, 8, 6),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = pr, FontFamily = VoltKit.Display, FontSize = 13, Foreground = VoltKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            presetRow.Children.Add(ThemeAction(pBtn, "theme:mouse:preset:" + pr, () =>
            {
                mouse.ApplyPreset(pr);
                UpdateSelectionState();
            }));
        }
        presetsPanel.Children.Add(presetRow);

        // Reset button (no double wrapping)
        var resetFace = VoltKit.OrangeActionButton("Reset Mouse Settings", 362, 42, null);
        resetFace.Margin = new Thickness(0, 4, 0, 0);
        presetsPanel.Children.Add(ThemeAction(resetFace, "theme:mouse:reset", () =>
        {
            mouse.ResetMode();
            UpdateSelectionState();
        }));

        presetsPanel.Visibility = Visibility.Collapsed;
        tabPanels["presets"] = presetsPanel;
        cStack.Children.Add(presetsPanel);

        // Full list of all inputs in mouse.Inputs (NO TRUNCATION!)
        foreach (string key in mouse.Inputs)
        {
            string k = key;
            bool active = k == mouse.SelectedKey;

            var row = new Border
            {
                MinHeight = 44,
                Background = active ? VoltKit.B("#2a2a2a") : Brushes.Transparent,
                BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                BorderThickness = new Thickness(active ? 2 : 0, active ? 2 : 0, active ? 2 : 0, active ? 2 : 1),
                CornerRadius = active ? new CornerRadius(10) : new CornerRadius(0),
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(0, 0, 0, 2),
                Cursor = Cursors.Hand
            };

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var keyBadgeText = new TextBlock
            {
                Text = mouse.LabelFor(k),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                FontWeight = FontWeights.ExtraBold,
                Foreground = active ? VoltKit.B("#111111") : VoltKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var keyBadge = new Border
            {
                MinWidth = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = active ? VoltKit.AmberHotBrush : VoltKit.B("#262626"),
                Padding = new Thickness(6, 0, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = keyBadgeText
            };
            Grid.SetColumn(keyBadge, 0);
            grid.Children.Add(keyBadge);

            var actionLabel = new TextBlock
            {
                Text = mouse.ActionFor(k),
                FontFamily = VoltKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.AmberHotBrush : VoltKit.B("#bdbdbd"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(actionLabel, 1);
            grid.Children.Add(actionLabel);

            row.Child = grid;
            inputRows[k] = (row, keyBadge, keyBadgeText, actionLabel);

            leftStack.Children.Add(ThemeAction(row, "theme:mouse:in:" + k, () =>
            {
                mouse.PickKey(k);
                UpdateSelectionState();
            }));
        }

        var leftScroll = new ScrollViewer { Width = 530, Height = 540, Content = leftStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Canvas.SetLeft(leftScroll, 0);
        Canvas.SetTop(leftScroll, 10);
        content.Children.Add(leftScroll);

        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        RebuildActionsPanel();

        // Subscribe to MouseWorkbench in-place updates without tearing down visual tree
        if (!voltMouseWired)
        {
            voltMouseWired = true;
            mouse.Watch(() =>
            {
                mappingsHeaderTb.Text = $"BUTTON MAPPINGS · {mouse.MappedText.ToUpperInvariant()}";
                detailSubTb.Text = $"{mouse.StatusText} · {mouse.MappedText}";
                detailActionTb.Text = mouse.ActionFor(mouse.SelectedKey);
                foreach (var (k, row) in inputRows)
                {
                    row.ActionText.Text = mouse.ActionFor(k);
                }
            });
        }

        return VoltKit.TwoPaneShell("mouse", "Mouse", "Desktop mouse emulation, joystick speed curves, and button clicks.", content, VoltSelectPage);
    }
}

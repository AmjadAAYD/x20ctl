using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;
using X20Ctl.Zone;
using X20Ctl.Zone.Mouse;
using ZoneMouseAction = X20Ctl.Zone.Mouse.MouseAction;

namespace X20Ctl.Product;

/// <summary>
/// Mouse emulation workbench presentation for the Yozakura (Blossom) theme:
/// "The button map is a shrine's wooden wall of lanterns, one per controller button,
/// with its mapped mouse action painted on a plaque below."
/// Strictly implements t_blossom.py:mouse().
/// </summary>
public partial class StudioScene
{
    private bool blossomMouseWired;
    private string blossomMouseDetailTab = "actions"; // "actions", "sensitivity", "scrolling", "presets"

    private void RenderBlossomMouse(Canvas host)
    {
        if (Mouse is not { } mouse) return;

        // 1. Sky & Backdrop
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(240, 140, 660, 200), 8, 10));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "mouse");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "Mouse",
            FontFamily = BlossomKit.Display,
            FontSize = 52,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        host.Children.Add(titleTb);

        var leadTb = new TextBlock
        {
            Text = "Map controller buttons and thumbsticks to direct Windows mouse cursor motions and clicks.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        host.Children.Add(leadTb);

        // 3. Top Controls at (236, 142, width 640)
        var topControls = new StackPanel { Width = 640 };

        var rToggle = new Border
        {
            Height = 54,
            BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 0, 12, 0)
        };
        var gTog = new Grid();
        gTog.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gTog.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        gTog.Children.Add(new TextBlock { Text = "Mouse emulation", FontFamily = BlossomKit.Body, FontSize = 18, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });

        var tog = new CheckBox
        {
            Content = "Enabled",
            Foreground = BlossomKit.TextWhiteBrush,
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            Tag = "theme:mouse:switch",
            VerticalAlignment = VerticalAlignment.Center
        };
        tog.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = mouse.Switch, Mode = BindingMode.TwoWay });
        Grid.SetColumn(tog, 1);
        gTog.Children.Add(tog);
        rToggle.Child = gTog;
        topControls.Children.Add(rToggle);

        var rSource = new Border
        {
            Height = 54,
            BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 0, 12, 0)
        };
        var gSrc = new Grid();
        gSrc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gSrc.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        gSrc.Children.Add(new TextBlock { Text = "Stick motion source", FontFamily = BlossomKit.Body, FontSize = 18, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });

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
        gSrc.Children.Add(srcCombo);
        rSource.Child = gSrc;
        topControls.Children.Add(rSource);

        Canvas.SetLeft(topControls, 236);
        Canvas.SetTop(topControls, 142);
        host.Children.Add(topControls);

        // 4. Shrine Wall of Lanterns at (232, 280, width 660)
        var shrineHeader = new TextBlock
        {
            Text = $"BUTTON ASSIGNMENTS · {mouse.MappedText.ToUpperInvariant()}",
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.TextSakuraBrush
        };
        Canvas.SetLeft(shrineHeader, 240);
        Canvas.SetTop(shrineHeader, 280);
        host.Children.Add(shrineHeader);

        // Wooden shelving beams (horizontal)
        foreach (double yb in new[] { 334.0, 578.0 })
        {
            var beam = new Border
            {
                Width = 660,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(0x5a, 0x3a, 0x28), 0.0),
                        new GradientStop(Color.FromRgb(0x2a, 0x18, 0x10), 1.0)
                    }
                },
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 8, Opacity = 0.45 }
            };
            Canvas.SetLeft(beam, 232);
            Canvas.SetTop(beam, yb);
            host.Children.Add(beam);
        }

        // Wooden vertical pillars
        foreach (double xp in new[] { 240.0, 874.0 })
        {
            var pillar = new Rectangle { Width = 10, Height = 480, Fill = BlossomKit.B("#2a1810") };
            Canvas.SetLeft(pillar, xp);
            Canvas.SetTop(pillar, 334);
            host.Children.Add(pillar);
        }

        // Right Side: Illuminated Washi Parchment Detail Panel at (940, 140, width 604, height 690)
        var litCard = BlossomKit.LitPaperCard(604, 690, new Thickness(28, 24, 28, 20));
        var cardStack = new StackPanel();

        var cardTitleTb = new TextBlock
        {
            Text = $"Button {mouse.LabelFor(mouse.SelectedKey)} Mapping",
            FontFamily = BlossomKit.Display,
            FontSize = 28,
            Foreground = BlossomKit.B(BlossomKit.LitPaperInk)
        };
        cardStack.Children.Add(cardTitleTb);

        var cardActionTb = new TextBlock
        {
            Text = $"Assigned action: {mouse.ActionFor(mouse.SelectedKey)}",
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
            Margin = new Thickness(0, 2, 0, 8)
        };
        cardStack.Children.Add(cardActionTb);

        // Footnote on washi card
        var footNote = new TextBlock
        {
            Text = $"{mouse.StatusText} · {mouse.MappedText}",
            FontFamily = BlossomKit.Body,
            FontSize = 13,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
            Margin = new Thickness(0, 12, 0, 0)
        };

        // Dictionary of lantern elements and plaque textblocks for in-place updates
        var lanternElements = new Dictionary<string, (FrameworkElement Lantern, TextBlock Plaque)>();

        // Washi Card Tabs: [ Actions ] [ Sensitivity ] [ Scrolling ] [ Presets ]
        var washiTabsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var washiTabBtns = new Dictionary<string, Border>();
        var washiTabPanels = new Dictionary<string, FrameworkElement>();

        void SwitchWashiTab(string tabKey)
        {
            blossomMouseDetailTab = tabKey;
            foreach (var (k, b) in washiTabBtns)
            {
                bool active = k == tabKey;
                b.Background = active ? BlossomKit.B(BlossomKit.LitPaperInk) : BlossomKit.B("rgba(59,23,18,0.08)");
                if (b.Child is TextBlock tb)
                    tb.Foreground = active ? BlossomKit.B("#ffe2b8") : BlossomKit.B(BlossomKit.LitPaperInk);
            }
            foreach (var (k, p) in washiTabPanels)
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
                Padding = new Thickness(14, 0, 14, 0),
                CornerRadius = new CornerRadius(15),
                Background = tKey == blossomMouseDetailTab ? BlossomKit.B(BlossomKit.LitPaperInk) : BlossomKit.B("rgba(59,23,18,0.08)"),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = tLabel,
                    FontFamily = BlossomKit.Body,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = tKey == blossomMouseDetailTab ? BlossomKit.B("#ffe2b8") : BlossomKit.B(BlossomKit.LitPaperInk),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            washiTabBtns[tk] = tBorder;
            washiTabsRow.Children.Add(ThemeAction(tBorder, "theme:mouse:tab:" + tk, () => SwitchWashiTab(tk)));
        }
        cardStack.Children.Add(washiTabsRow);

        // 1. Actions Panel on Washi Card (Scrollable list of all categories and all actions)
        var washiActionsPanel = new StackPanel { Orientation = Orientation.Vertical };
        var washiActionsScroll = new ScrollViewer
        {
            MaxHeight = 480,
            Content = washiActionsPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        washiTabPanels["actions"] = washiActionsScroll;
        cardStack.Children.Add(washiActionsScroll);

        var washiActionButtons = new List<(ZoneMouseAction Action, Border Border, TextBlock Text)>();

        void RebuildWashiActionsPanel()
        {
            washiActionsPanel.Children.Clear();
            washiActionButtons.Clear();
            var curAction = mouse.CurrentAction;

            foreach (var group in mouse.Groups)
            {
                washiActionsPanel.Children.Add(new TextBlock
                {
                    Text = group.Group,
                    FontFamily = BlossomKit.Small,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = BlossomKit.B(BlossomKit.LitPaperCrimson),
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
                        FontFamily = BlossomKit.Body,
                        FontSize = 13,
                        Foreground = isSelected ? BlossomKit.B("#ffe2b8") : BlossomKit.B(BlossomKit.LitPaperInk),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var optBorder = new Border
                    {
                        Height = 32,
                        CornerRadius = new CornerRadius(16),
                        Background = isSelected ? BlossomKit.B(BlossomKit.LitPaperInk) : BlossomKit.B("rgba(59,23,18,0.08)"),
                        BorderBrush = isSelected ? Brushes.Transparent : BlossomKit.B("rgba(59,23,18,0.2)"),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(14, 0, 14, 0),
                        Margin = new Thickness(0, 0, 6, 6),
                        Cursor = Cursors.Hand,
                        Child = optText
                    };

                    washiActionButtons.Add((choice, optBorder, optText));

                    wrap.Children.Add(ThemeAction(optBorder, "theme:mouse:assign:" + choice, () =>
                    {
                        mouse.Assign(choice);
                        UpdateBlossomSelection();
                    }));
                }
                washiActionsPanel.Children.Add(wrap);
            }
        }

        // 2. Sensitivity Panel on Washi Card
        var washiSensitivityPanel = new StackPanel { Orientation = Orientation.Vertical };
        var ptrRow = new Grid { Height = 40, Margin = new Thickness(0, 0, 0, 6) };
        ptrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        ptrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ptrRow.Children.Add(new TextBlock { Text = "Pointer Stick", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center });
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
        washiSensitivityPanel.Children.Add(ptrRow);

        var sensSliders = new (string Label, RangeBase Source)[]
        {
            ("Pointer speed", mouse.SpeedSlider),
            ("Response curve", mouse.CurveSlider),
            ("Dead zone", mouse.DeadzoneSlider),
            ("Precision", mouse.PrecisionSlider)
        };
        foreach (var (lbl, src) in sensSliders)
        {
            var sRow = new Grid { Height = 40 };
            sRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            sRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            sRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            sRow.Children.Add(new TextBlock
            {
                Text = lbl,
                FontFamily = BlossomKit.Body,
                FontSize = 14,
                Foreground = BlossomKit.B(BlossomKit.LitPaperInk),
                VerticalAlignment = VerticalAlignment.Center
            });

            var slider = ThemeSlider(src, "theme:mouse:" + lbl, 270, true);
            Grid.SetColumn(slider, 1);
            sRow.Children.Add(slider);

            var valTb = new TextBlock
            {
                FontFamily = BlossomKit.Body,
                FontSize = 13,
                Foreground = BlossomKit.B(BlossomKit.LitPaperCrimson),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            valTb.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = src, StringFormat = "{0:0.##}" });
            Grid.SetColumn(valTb, 2);
            sRow.Children.Add(valTb);

            washiSensitivityPanel.Children.Add(sRow);
        }

        var testBtn = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Background = BlossomKit.B("rgba(59,23,18,0.12)"),
            Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "Test Pointer Movement", FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center }
        };
        washiSensitivityPanel.Children.Add(ThemeAction(testBtn, "theme:mouse:test", () => mouse.TestPointer()));

        var readoutTb = new TextBlock
        {
            Text = mouse.ReadoutText,
            FontFamily = BlossomKit.Body,
            FontSize = 12,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        mouse.WatchReadout(txt => readoutTb.Text = txt);
        washiSensitivityPanel.Children.Add(readoutTb);

        washiSensitivityPanel.Visibility = Visibility.Collapsed;
        washiTabPanels["sensitivity"] = washiSensitivityPanel;
        cardStack.Children.Add(washiSensitivityPanel);

        // 3. Scrolling Panel on Washi Card
        var washiScrollingPanel = new StackPanel { Orientation = Orientation.Vertical };
        var scRow = new Grid { Height = 36 };
        scRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        scRow.Children.Add(new TextBlock { Text = "Enable Thumbstick Scrolling", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center });
        var scToggle = new CheckBox { Tag = "theme:mouse:scrolling", VerticalAlignment = VerticalAlignment.Center };
        scToggle.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = mouse.ScrollsBox, Mode = BindingMode.TwoWay });
        Grid.SetColumn(scToggle, 1);
        scRow.Children.Add(scToggle);
        washiScrollingPanel.Children.Add(scRow);

        var scrSpeedRow = new Grid { Height = 40 };
        scrSpeedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        scrSpeedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        scrSpeedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scrSpeedRow.Children.Add(new TextBlock { Text = "Scroll speed", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center });
        var scrSlider = ThemeSlider(mouse.ScrollSlider, "theme:mouse:scrollspeed", 270, true);
        Grid.SetColumn(scrSlider, 1);
        scrSpeedRow.Children.Add(scrSlider);
        var scrValTb = new TextBlock { Foreground = BlossomKit.B(BlossomKit.LitPaperCrimson), FontFamily = BlossomKit.Body, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        scrValTb.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = mouse.ScrollSlider, StringFormat = "{0:0.##}" });
        Grid.SetColumn(scrValTb, 2);
        scrSpeedRow.Children.Add(scrValTb);
        washiScrollingPanel.Children.Add(scrSpeedRow);

        foreach (var (lbl, src) in new (string, CheckBox)[] { ("Horizontal scroll", mouse.HorizontalBox), ("Invert vertical", mouse.InvertVBox), ("Invert horizontal", mouse.InvertHBox) })
        {
            var optRow = new Grid { Height = 34 };
            optRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            optRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optRow.Children.Add(new TextBlock { Text = lbl, FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.B(BlossomKit.LitPaperSub), VerticalAlignment = VerticalAlignment.Center });
            var scOptTog = new CheckBox { Tag = "theme:mouse:" + lbl, VerticalAlignment = VerticalAlignment.Center };
            scOptTog.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = src, Mode = BindingMode.TwoWay });
            Grid.SetColumn(scOptTog, 1);
            optRow.Children.Add(scOptTog);
            washiScrollingPanel.Children.Add(optRow);
        }

        washiScrollingPanel.Visibility = Visibility.Collapsed;
        washiTabPanels["scrolling"] = washiScrollingPanel;
        cardStack.Children.Add(washiScrollingPanel);

        // 4. Presets & Shortcuts Panel on Washi Card
        var washiPresetsPanel = new StackPanel { Orientation = Orientation.Vertical };
        var qkRow = new Grid { Height = 36 };
        qkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        qkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        qkRow.Children.Add(new TextBlock { Text = "Quick Toggle Chord", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center });
        var qkToggle = new CheckBox { Tag = "theme:mouse:combo", VerticalAlignment = VerticalAlignment.Center };
        qkToggle.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = mouse.ComboToggle, Mode = BindingMode.TwoWay });
        Grid.SetColumn(qkToggle, 1);
        qkRow.Children.Add(qkToggle);
        washiPresetsPanel.Children.Add(qkRow);

        washiPresetsPanel.Children.Add(new TextBlock
        {
            Text = "When quick toggle is enabled, both shortcut buttons are reserved for mouse mode switching.",
            FontFamily = BlossomKit.Body,
            FontSize = 12,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 10)
        });

        washiPresetsPanel.Children.Add(new TextBlock
        {
            Text = "CONFIGURATION PRESETS",
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.B(BlossomKit.LitPaperCrimson),
            Margin = new Thickness(0, 4, 0, 6)
        });

        // Presets row: all 4 presets from MouseWorkbench.PresetNames
        var prRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        foreach (string pr in MouseWorkbench.PresetNames)
        {
            string p = pr;
            var prBorder = new Border
            {
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = BlossomKit.B("rgba(59,23,18,0.12)"),
                Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = p, FontFamily = BlossomKit.Body, FontSize = 12, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center }
            };
            prRow.Children.Add(ThemeAction(prBorder, "theme:mouse:preset:" + p, () =>
            {
                mouse.ApplyPreset(p);
                UpdateBlossomSelection();
            }));
        }
        washiPresetsPanel.Children.Add(prRow);

        // Reset button
        var rstBorder = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Background = BlossomKit.B("rgba(180,40,40,0.15)"),
            Padding = new Thickness(16, 0, 16, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "Reset Defaults", FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.B("#6a1f18"), VerticalAlignment = VerticalAlignment.Center }
        };
        washiPresetsPanel.Children.Add(ThemeAction(rstBorder, "theme:mouse:reset", () =>
        {
            mouse.ResetMode();
            UpdateBlossomSelection();
        }));

        washiPresetsPanel.Visibility = Visibility.Collapsed;
        washiTabPanels["presets"] = washiPresetsPanel;
        cardStack.Children.Add(washiPresetsPanel);

        // Footnote on washi card
        cardStack.Children.Add(footNote);

        litCard.Child = new ScrollViewer { Content = cardStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Canvas.SetLeft(litCard, 940);
        Canvas.SetTop(litCard, 140);
        host.Children.Add(litCard);

        void UpdateBlossomSelection()
        {
            string sel = mouse.SelectedKey;
            cardTitleTb.Text = $"Button {mouse.LabelFor(sel)} Mapping";
            cardActionTb.Text = $"Assigned action: {mouse.ActionFor(sel)}";
            footNote.Text = $"{mouse.StatusText} · {mouse.MappedText}";
            shrineHeader.Text = $"BUTTON ASSIGNMENTS · {mouse.MappedText.ToUpperInvariant()}";

            foreach (var (k, item) in lanternElements)
            {
                bool active = k == sel;
                item.Plaque.Text = mouse.ActionFor(k);
                item.Plaque.Foreground = active ? BlossomKit.TextAmberBrush : BlossomKit.B("#c9b3bc");
                // Refresh lantern visual glow
                item.Lantern.Opacity = active ? 1.0 : 0.6;
            }

            RebuildWashiActionsPanel();
        }

        // Hanging Paper Lanterns on Shrine Wall - ALL 17 INPUTS (NO TRUNCATION!)
        var allInputs = mouse.Inputs.ToList();
        for (int i = 0; i < allInputs.Count; i++)
        {
            string key = allInputs[i];
            bool active = key == mouse.SelectedKey;

            // Distribute across 2 shelves: 9 on top shelf, remaining on bottom shelf
            int row = i < 9 ? 0 : 1;
            int col = i < 9 ? i : (i - 9);
            double spacing = row == 0 ? 68.0 : 76.0;
            double startX = row == 0 ? 280.0 : 310.0;
            double lx = startX + col * spacing;
            double ly = row == 0 ? 372.0 : 616.0;

            var lantern = BlossomKit.PaperLantern(56, 76, active, null, mouse.LabelFor(key), active ? 1.0 : 0.6, 20, key.Length > 2 ? 15 : 22);
            lantern.Cursor = Cursors.Hand;
            Canvas.SetLeft(lantern, lx - lantern.Width / 2.0);
            Canvas.SetTop(lantern, ly - 20);

            string k = key;
            host.Children.Add(ThemeAction(lantern, "theme:mouse:in:" + k, () =>
            {
                mouse.PickKey(k);
                UpdateBlossomSelection();
            }));

            // Plaque Below with mapped action
            var plaqueTb = new TextBlock
            {
                Text = mouse.ActionFor(key),
                FontFamily = BlossomKit.Body,
                FontSize = 11,
                Foreground = active ? BlossomKit.TextAmberBrush : BlossomKit.B("#c9b3bc"),
                TextAlignment = TextAlignment.Center,
                Width = 72
            };
            Canvas.SetLeft(plaqueTb, lx - 36);
            Canvas.SetTop(plaqueTb, ly + 82);
            host.Children.Add(plaqueTb);

            lanternElements[key] = (lantern, plaqueTb);
        }

        RebuildWashiActionsPanel();

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Assign action"));
        hintsStack.Children.Add(CreateHint("X", "Toggle mouse"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);

        // Subscribe to MouseWorkbench in-place updates without tearing down visual tree
        if (!blossomMouseWired)
        {
            blossomMouseWired = true;
            mouse.Watch(() =>
            {
                shrineHeader.Text = $"BUTTON ASSIGNMENTS · {mouse.MappedText.ToUpperInvariant()}";
                footNote.Text = $"{mouse.StatusText} · {mouse.MappedText}";
                cardActionTb.Text = $"Assigned action: {mouse.ActionFor(mouse.SelectedKey)}";
                foreach (var (k, item) in lanternElements)
                {
                    item.Plaque.Text = mouse.ActionFor(k);
                }
            });
        }
    }
}

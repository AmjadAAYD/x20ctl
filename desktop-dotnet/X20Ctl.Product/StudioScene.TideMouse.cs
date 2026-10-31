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
using X20Ctl.Product.Tide;
using ZoneMouseAction = X20Ctl.Zone.Mouse.MouseAction;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private bool tideMouseWired;
    private string tideMouseGroup = "Mouse";

    private void TideShowMouse()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();

        if (Mouse is not { } mouse)
        {
            var emptyFrame = new TideToolFrame(
                "MOUSE",
                "Mouse emulation unavailable for this device.",
                onBack: () => TideShowDashboard(),
                hints: new[] { "\u24B7 Back" },
                seed: 14);
            ConsoleSurface.Children.Add(emptyFrame);
            return;
        }

        if (!tideMouseWired)
        {
            tideMouseWired = true;
            mouse.Watch(() => TideLater());
        }

        var frame = new TideToolFrame(
            "MOUSE",
            "Controller desktop mouse emulation & custom button mapping · Local draft",
            onBack: () => TideShowDashboard(),
            hints: new[] { "\u24B7 Back", "\u24B6 Select Input", "\u24BD Assign Action", "\u2295 Reset" },
            seed: 14);

        TideKit.AnimateEntrance(frame);

        // 1. Left Column: Toggle + Scrollable Inputs List (230, 150, w=580)
        var leftCol = new StackPanel { Width = 580 };

        // Mouse Mode Toggle Row
        var swBorder = new Border
        {
            Height = 56,
            Background = TideKit.Sea0Brush,
            BorderBrush = TideKit.WhiteBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(0, 0, 0, 10),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 4, Direction = 315, Color = TideKit.Ink }
        };
        var swGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        swGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        swGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        swGrid.Children.Add(new TextBlock
        {
            Text = "CONTROLLER AS MOUSE",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = TideKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var swCheck = new CheckBox
        {
            Content = "ENABLE",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.LightCyanBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = "theme:mouse:switch"
        };
        swCheck.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsChecked") { Source = mouse.Switch, Mode = BindingMode.TwoWay });
        Grid.SetColumn(swCheck, 1);
        swGrid.Children.Add(swCheck);
        swBorder.Child = swGrid;
        leftCol.Children.Add(swBorder);

        leftCol.Children.Add(TideKit.SectionHeader($"ALL {mouse.Inputs.Count} CONTROLLER INPUTS"));

        var inputsScroll = new ScrollViewer
        {
            Height = 460,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var inputsStack = new StackPanel { Width = 560 };

        Action? updateCard = null;
        var inputRows = new Dictionary<string, FrameworkElement>();

        foreach (var inKey in mouse.Inputs)
        {
            string k = inKey;
            bool isCur = k == mouse.SelectedKey;
            string actLabel = mouse.ActionFor(k);

            var row = TideKit.PersonaRow(k, value: actLabel, focused: isCur, onClick: () =>
            {
                mouse.PickKey(k);
                foreach (var (rKey, rEl) in inputRows)
                {
                    bool sel = rKey == k;
                    if (rEl is Border b)
                    {
                        b.Background = sel ? TideKit.WhiteBrush : TideKit.Sea0Brush;
                        b.BorderBrush = sel ? TideKit.SpikeRedBrush : TideKit.WhiteBrush;
                    }
                }
                updateCard?.Invoke();
            });

            inputRows[k] = row;
            inputsStack.Children.Add(ThemeAction(row, "theme:mouse:input:" + k, () =>
            {
                mouse.PickKey(k);
                updateCard?.Invoke();
            }));
        }

        inputsScroll.Content = inputsStack;
        leftCol.Children.Add(inputsScroll);

        var listHost = new Canvas
        {
            Width = 580,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-2)
        };
        listHost.Children.Add(leftCol);
        frame.SetLeftList(listHost, 150);

        // 2. Right Side: Persona Assignment Card (880, 150, w=660, h=550)
        var assignCard = TideKit.PersonaCard(660, 550, 2);
        var cardInner = new StackPanel { Margin = new Thickness(18, 16, 18, 16) };

        var curInputTitle = new TextBlock
        {
            Text = $"ASSIGN · {mouse.SelectedKey}",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 22,
            Foreground = TideKit.Sea0Brush
        };
        cardInner.Children.Add(curInputTitle);

        var curActionSub = new TextBlock
        {
            Text = $"Sends: {mouse.ActionFor(mouse.SelectedKey)}",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = TideKit.SubBrush,
            Margin = new Thickness(0, 4, 0, 12)
        };
        cardInner.Children.Add(curActionSub);

        // Group selector tabs
        var grpRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var grpTabs = new Dictionary<string, Border>();

        Action? rebuildActions = null;

        foreach (var grp in mouse.Groups)
        {
            string gName = grp.Group;
            bool active = gName == tideMouseGroup;

            var tabBtn = new Border
            {
                Padding = new Thickness(12, 6, 12, 6),
                Background = active ? TideKit.WhiteBrush : TideKit.Sea0Brush,
                BorderBrush = active ? TideKit.SpikeRedBrush : TideKit.WhiteBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                RenderTransform = new SkewTransform(-10, 0)
            };
            tabBtn.Child = new TextBlock
            {
                Text = gName.ToUpperInvariant(),
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Foreground = active ? TideKit.Sea0Brush : TideKit.WhiteBrush
            };
            grpTabs[gName] = tabBtn;

            tabBtn.MouseLeftButtonDown += (_, _) =>
            {
                tideMouseGroup = gName;
                foreach (var (gn, tb) in grpTabs)
                {
                    bool on = gn == gName;
                    tb.Background = on ? TideKit.WhiteBrush : TideKit.Sea0Brush;
                    if (tb.Child is TextBlock txt) txt.Foreground = on ? TideKit.Sea0Brush : TideKit.WhiteBrush;
                }
                rebuildActions?.Invoke();
            };
            grpRow.Children.Add(tabBtn);
        }
        cardInner.Children.Add(grpRow);

        // Actions Grid inside a ScrollViewer
        var actionsScroll = new ScrollViewer { Height = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var actionsWrap = new WrapPanel { Width = 610 };

        rebuildActions = () =>
        {
            actionsWrap.Children.Clear();
            var targetGroup = mouse.Groups.FirstOrDefault(g => g.Group.Equals(tideMouseGroup, StringComparison.OrdinalIgnoreCase));
            if (targetGroup.Actions == null && mouse.Groups.Count > 0) targetGroup = mouse.Groups[0];
            if (targetGroup.Actions == null) return;

            foreach (var act in targetGroup.Actions)
            {
                var choice = act;
                bool isSelected = mouse.CurrentAction == choice;

                var optBtn = new Border
                {
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = isSelected ? TideKit.SpikeRedBrush : TideKit.Sea0Brush,
                    BorderBrush = isSelected ? TideKit.InkBrush : TideKit.WhiteBrush,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 6, 6),
                    Cursor = Cursors.Hand,
                    Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 2, Direction = 315, Color = TideKit.Ink }
                };
                optBtn.Child = new TextBlock
                {
                    Text = MouseWorkbench.Friendly(choice),
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 12,
                    Foreground = TideKit.WhiteBrush
                };
                actionsWrap.Children.Add(ThemeAction(optBtn, "theme:mouse:assign:" + choice, () =>
                {
                    mouse.Assign(choice);
                    updateCard?.Invoke();
                    rebuildActions?.Invoke();
                }));
            }
        };

        actionsScroll.Content = actionsWrap;
        cardInner.Children.Add(actionsScroll);

        // Sliders: Cursor Speed
        var speedRow = new Grid { Margin = new Thickness(0, 14, 0, 8) };
        speedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        speedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        speedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

        speedRow.Children.Add(new TextBlock
        {
            Text = "CURSOR SPEED",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.Sea0Brush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var speedSlider = ThemeSlider(mouse.SpeedSlider, "theme:mouse:speed", 360, true);
        speedSlider.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(speedSlider, 1);
        speedRow.Children.Add(speedSlider);

        var speedVal = new TextBlock
        {
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.SpikeRedBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        speedVal.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = mouse.SpeedSlider, StringFormat = "{0:0}" });
        Grid.SetColumn(speedVal, 2);
        speedRow.Children.Add(speedVal);

        cardInner.Children.Add(speedRow);

        // Action Buttons: [ Reset Mouse Mappings ]
        var actRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var resetBtn = TideKit.PersonaButton("RESET MAPPINGS", primary: false, onClick: () =>
        {
            mouse.ResetMode();
            TideShowMouse();
        });
        resetBtn.Height = 36;
        actRow.Children.Add(ThemeAction(resetBtn, "theme:mouse:reset", () =>
        {
            mouse.ResetMode();
            TideShowMouse();
        }));
        cardInner.Children.Add(actRow);

        updateCard = () =>
        {
            curInputTitle.Text = $"ASSIGN · {mouse.SelectedKey}";
            curActionSub.Text = $"Sends: {mouse.ActionFor(mouse.SelectedKey)}";
            rebuildActions?.Invoke();
        };

        rebuildActions();

        assignCard.Child = cardInner;
        Canvas.SetLeft(assignCard, 880);
        Canvas.SetTop(assignCard, 150);
        frame.ContentArea.Children.Add(assignCard);

        ConsoleSurface.Children.Add(frame);
    }
}

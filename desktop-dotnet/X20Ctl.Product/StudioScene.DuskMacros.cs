using X20Ctl.Zone;
using System.Windows.Data;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool duskMacroWired;
    private Button DuskMacroAction(string text, string tag, Action action, bool enabled = true)
    {
        return ThemeAction(new Border { MinHeight = 38, Padding = new(12,8,12,8), Margin = new(0,0,6,6), Background = DuskKit.SlateItemBrush, Child = new TextBlock { Text = text, Foreground = DuskKit.WhiteBrush, FontFamily = DuskKit.Display, FontSize = 15 } }, "theme:macro:" + tag, action, enabled);
    }
    private void DuskShowMacros()
    {
        if (Macros is not {} macro) return;
        string? focused = (System.Windows.Input.Keyboard.FocusedElement as FrameworkElement)?.Tag as string;
        if (!duskMacroWired) { duskMacroWired = true; macro.Changed += DuskLater; }
        duskPageHost!.Children.Clear();

        var hints = new[]
        {
            ("X", "RECORD"),
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Left list (w: 580)
        var leftStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };

        leftStack.Children.Add(new TextBlock
        {
            Text = "RECORDED SEQUENCES",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        leftStack.Children.Add(new TextBlock
        {
            Text = "LOCAL DRAFT · Not written to controller memory.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var macroSlots = macro.Draft.SlotNames.Select(slot => (Name: slot, Steps: macro.Draft[slot].Count + " events · " + macro.Draft.Duration(slot) + "ms", Trigger: macro.Draft.PcMode ? macro.Draft.Trigger(slot) ?? "Not used" : macro.Draft.PhysicalSlots.Contains(slot) ? "Paddle " + slot : "Not used", Active: slot == macro.Slot)).ToArray();

        foreach (var (name, steps, trigger, active) in macroSlots)
        {
            var row = new Border
            {
                Height = 64,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = DuskKit.B("rgba(255,255,255,.07)"),
                Background = active ? DuskKit.B("rgba(255,255,255,.05)") : Brushes.Transparent,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = name,
                FontFamily = DuskKit.Display,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush
            });
            nameStack.Children.Add(new TextBlock
            {
                Text = steps,
                FontFamily = DuskKit.Body,
                FontSize = 13,
                Foreground = DuskKit.MuteBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(nameStack, 0);
            grid.Children.Add(nameStack);

            var trigPill = new Border
            {
                Padding = new Thickness(14, 6, 14, 6),
                CornerRadius = new CornerRadius(14),
                Background = active ? DuskKit.B("#1a9fff") : DuskKit.SlateItemBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            trigPill.Child = new TextBlock
            {
                Text = trigger,
                FontFamily = DuskKit.Display,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush
            };
            Grid.SetColumn(trigPill, 1);
            grid.Children.Add(trigPill);

            row.Child = grid;
            leftStack.Children.Add(ThemeAction(row, "theme:macro:slot:" + name, () => macro.SelectSlot(name)));
        }

        var actions = new WrapPanel { Margin = new(0,16,0,0) };
        actions.Children.Add(DuskMacroAction("+ Add event", "add", macro.Add, macro.Draft.CanEdit && macro.Draft.EntryCount(macro.Slot) < 47));
        var record = DuskMacroAction(macro.IsRecording ? "Stop recording" : "Record", "record", () => { macro.ToggleRecording(); DuskLater(); }); record.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = macro.Record });
        if (macro.Record.Content is StackPanel { Children.Count: 2 } nativeRecord && nativeRecord.Children[1] is TextBlock nativeLabel && record.Content is Border { Child: TextBlock recordLabel })
            recordLabel.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = nativeLabel });
        actions.Children.Add(record);
        var preview = DuskMacroAction("Preview timeline", "preview", macro.Preview); preview.SetBinding(ContentControl.ToolTipProperty, new Binding("Text") { Source = macro.StatusText }); actions.Children.Add(preview);
        leftStack.Children.Add(actions);
        if (macro.Draft.PcMode)
        {
            var trigger = new ComboBox { ItemsSource = new[] { "Not used" }.Concat(MacroDraft.TriggerInputs), SelectedItem = macro.Draft.Trigger(macro.Slot) ?? "Not used", IsEnabled = macro.Draft.CanEdit, Tag = "theme:macro:trigger", Height = 38, Margin = new(0,12,0,0) };
            trigger.SelectionChanged += (_, _) => { if (trigger.SelectedItem is string choice) macro.SetTrigger(choice == "Not used" ? null : choice); }; leftStack.Children.Add(trigger);
        }
        var status = new TextBlock { Foreground = DuskKit.MuteBrush, TextWrapping = TextWrapping.Wrap, Margin = new(0,12,0,0) }; status.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = macro.StatusText }); leftStack.Children.Add(status);
        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        contentCanvas.Children.Add(leftStack);

        // Right side: Timeline sequence visual
        var rightStack = new StackPanel { Width = 380, Orientation = Orientation.Vertical };

        var timeCard = new Border
        {
            Width = 360,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.B("#141920"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.35)"),
            BorderThickness = new Thickness(1, 2, 1, 1),
            Padding = new Thickness(20),
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.2 }
        };
        var timeStack = new StackPanel { Orientation = Orientation.Vertical };

        var timeHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        timeHead.Children.Add(new TextBlock
        {
            Text = "SEQUENCE TIMELINE",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush
        });
        var loopChip = new Border
        {
            Padding = new Thickness(8, 2, 8, 2),
            CornerRadius = new CornerRadius(3),
            Background = DuskKit.B("rgba(89,191,64,0.18)"),
            BorderBrush = DuskKit.B("rgba(89,191,64,0.4)"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(12, 0, 0, 0),
            Child = new TextBlock
            {
                Text = macro.Draft.Duration(macro.Slot) + "MS · PREVIEW ONLY",
                FontFamily = DuskKit.Display,
                FontSize = 10,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DuskKit.GreenOnlineBrush
            }
        };
        timeHead.Children.Add(loopChip);
        timeStack.Children.Add(timeHead);

        // Sequence blocks with luminous dropshadows
        var blockRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
        var events = macro.Draft[macro.Slot];
        var seqBlocks = events.Select((entry, index) => (RebelPresses(entry), index == macro.SelectedIndex ? DuskKit.GreenOnline : DuskKit.CyanNeon)).ToArray();
        for (int i = 0; i < seqBlocks.Length; i++)
        {
            var (bName, bCol) = seqBlocks[i];
            var block = new Border
            {
                Padding = new Thickness(14, 8, 14, 8),
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.B(bCol),
                Effect = new DropShadowEffect { Color = bCol, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.7 },
                Child = new TextBlock
                {
                    Text = bName,
                    FontFamily = DuskKit.Display,
                    FontSize = 15,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = DuskKit.SlateBgBrush
                }
            };
            int eventIndex = i; blockRow.Children.Add(ThemeAction(block, "theme:macro:event:" + i, () => { macro.Select(eventIndex); DuskLater(); }));

            if (i < seqBlocks.Length - 1)
            {
                var arrow = new TextBlock
                {
                    Text = "→ " + events[i].PauseMs + "ms →",
                    FontFamily = DuskKit.Display,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DuskKit.MuteBrush,
                    Margin = new Thickness(8, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                blockRow.Children.Add(arrow);
            }
        }
        timeStack.Children.Add(new ScrollViewer { Content = blockRow, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });

        timeStack.Children.Add(new TextBlock { Text = macro.CapacityDescription, Foreground = DuskKit.LightBrush, TextWrapping = TextWrapping.Wrap });
        timeCard.Child = timeStack;
        rightStack.Children.Add(timeCard);

        // Detail card with cyan specular border
        var detailCard = new Border
        {
            Background = DuskKit.SlateItemBrush,
            BorderBrush = DuskKit.CyanNeonBrush,
            BorderThickness = new Thickness(3, 1, 1, 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            Width = 360,
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.2 }
        };
        var detailStack = new StackPanel { Orientation = Orientation.Vertical };
        detailStack.Children.Add(new TextBlock
        {
            Text = "EXECUTION MODE",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Event editor",
            FontFamily = DuskKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Local sequence draft. Preview emits no Windows or controller input.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        if (macro.SelectedIndex >= 0 && macro.SelectedIndex < events.Count)
        {
            var entry = events[macro.SelectedIndex]; var inputs = new WrapPanel { Margin = new(0,12,0,0) };
            foreach (string key in MacroDraft.Inputs)
            {
                inputs.Children.Add(DuskMacroAction((entry.Buttons.Contains(key) ? "● " : "") + key, "input:" + key, () => macro.Update(entry with { Buttons = entry.Buttons.Contains(key) ? entry.Buttons.Where(b => b != key).ToArray() : entry.Buttons.Append(key).ToArray() }), macro.Draft.CanEdit));
            }
            detailStack.Children.Add(inputs);
            foreach (var (label, pause) in new[] { ("Hold", false), ("Pause", true) })
            {
                var value = pause ? entry.PauseMs : entry.HoldMs; var timing = new WrapPanel(); timing.Children.Add(new TextBlock { Text = label + " " + value + "ms", Foreground = DuskKit.WhiteBrush, Width = 140, VerticalAlignment = VerticalAlignment.Center });
                foreach (int delta in new[] { -5, 5 }) timing.Children.Add(DuskMacroAction(delta < 0 ? "−5" : "+5", "time:" + label + ":" + delta, () => macro.Update(pause ? entry with { PauseMs = Math.Clamp(value + delta, 0, 327675) } : entry with { HoldMs = Math.Clamp(value + delta, 0, 327675) }), macro.Draft.CanEdit));
                detailStack.Children.Add(timing);
            }
            foreach (var (label, left) in new[] { ("Left stick", true), ("Right stick", false) })
            {
                detailStack.Children.Add(new TextBlock { Text = label, Foreground = DuskKit.LightBrush, Margin = new(0,8,0,6) });
                var directions = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Width = 210, HorizontalAlignment = HorizontalAlignment.Left };
                StickHeading[] order = [StickHeading.UpLeft, StickHeading.Up, StickHeading.UpRight, StickHeading.Left, StickHeading.Neutral, StickHeading.Right, StickHeading.DownLeft, StickHeading.Down, StickHeading.DownRight];
                foreach (var direction in order) directions.Children.Add(DuskMacroAction(RebelArrow(direction), "stick:" + label + ":" + direction, () => macro.Update(left ? entry with { Left = direction } : entry with { Right = direction }), macro.Draft.CanEdit));
                detailStack.Children.Add(directions);
            }
            var editActions = new WrapPanel();
            foreach (var (text, key, action) in new (string,string,Action)[] { ("Earlier", "earlier", () => macro.Move(-1)), ("Later", "later", () => macro.Move(1)), ("Duplicate", "duplicate", macro.Duplicate), ("Remove", "remove", macro.Remove) }) editActions.Children.Add(DuskMacroAction(text, key, action, macro.Draft.CanEdit && (key != "duplicate" || macro.Draft.EntryCount(macro.Slot) < 47)));
            detailStack.Children.Add(editActions);
        }
        detailCard.Child = detailStack;
        rightStack.Children.Add(detailCard);

        // Action button to test macro
        var testMacroBtn = DuskKit.GreenActionButton("Preview sequence", 360, 48, macro.Preview);
        testMacroBtn.Margin = new Thickness(0, 12, 0, 0);
        rightStack.Children.Add(testMacroBtn);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        var rightScroll = new ScrollViewer { Width = 380, Height = 580, Content = rightStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Canvas.SetLeft(rightScroll, 640); Canvas.SetTop(rightScroll, 0); contentCanvas.Children.Add(rightScroll);

        var shell = DuskKit.SettingsShell(
            activeKey: "macros",
            title: "Macros & Sequences",
            lead: "Sequence timing, delay calibration, and trigger binding combinations.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskPageHost.Children.Add(shell);
        if (focused?.StartsWith("theme:macro:") == true) Dispatcher.BeginInvoke(() => FindTagged(shell, focused)?.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }
}

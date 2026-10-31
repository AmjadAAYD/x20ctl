using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool nexusMacrosWired;

    private void NexusShowMacros()
    {
        nexusPageHost!.Children.Clear();

        // 1. Background gradient
        var bg = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0.5, 0), EndPoint = new(0.5, 1),
                GradientStops = { new(NexusKit.C("#1a1c20"), 0.0), new(NexusKit.House, 1.0) }
            }
        };
        nexusPageHost.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: Player,
            model: Model.Name,
            status: "Offline",
            onZone: () => BackRequested?.Invoke(),
            onSetups: () => ShowManagement("Setups"),
            onPcMode: () => ShowManagement("PcMode"),
            onTools: () => ToolsRequested?.Invoke()
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusPageHost.Children.Add(topBar);

        // 3. Title & Lead
        var title = new TextBlock
        {
            Text = "Macros",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 138);
        nexusPageHost.Children.Add(title);

        var lead = new TextBlock
        {
            Text = Macros is { } m0 ? $"{m0.Slot} · local draft · {m0.Draft.EntryCount(m0.Slot)} of 47 entries" : "Macros are not available for this controller",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        nexusPageHost.Children.Add(lead);

        if (Macros is not { } macro)
        {
            var none = new TextBlock { Text = "Select a supported controller to edit macros.", FontFamily = NexusKit.Body, FontSize = 17, Foreground = NexusKit.SubBrush };
            Canvas.SetLeft(none, 70); Canvas.SetTop(none, 250);
            nexusPageHost.Children.Add(none);
            return;
        }
        if (!nexusMacrosWired) { nexusMacrosWired = true; macro.Changed += () => NexusLater(); }
        var draft = macro.Draft;
        bool canEdit = draft.CanEdit;

        // Slot tabs from the real draft
        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string slot in draft.SlotNames)
        {
            string p = slot;
            bool on = p == macro.Slot;
            var tabBtn = new Border
            {
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 0, 10, 0),
                CornerRadius = new CornerRadius(4),
                Background = on ? NexusKit.WhiteBrush : NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = p,
                    FontFamily = NexusKit.Body,
                    FontSize = 16,
                    FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = on ? NexusKit.B("#111111") : NexusKit.WhiteBrush
                }
            };
            tabsStack.Children.Add(ThemeAction(tabBtn, "theme:macro:slot:" + p, () => macro.SelectSlot(p)));
        }
        Canvas.SetLeft(tabsStack, 68);
        Canvas.SetTop(tabsStack, 230);
        nexusPageHost.Children.Add(tabsStack);

        var events = draft[macro.Slot];
        int selected = macro.SelectedIndex >= 0 && macro.SelectedIndex < events.Count ? macro.SelectedIndex : -1;

        // Left: the sequence
        var rowPanel = new StackPanel { Width = 820 };
        rowPanel.Children.Add(new TextBlock
        {
            Text = $"Sequence · {macro.Slot} ({events.Count} event{(events.Count == 1 ? "" : "s")})",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(2, 6, 0, 10)
        });
        if (events.Count == 0)
            rowPanel.Children.Add(new TextBlock { Text = "No events yet.", FontFamily = NexusKit.Body, FontSize = 16, Foreground = NexusKit.SubBrush, Margin = new Thickness(2, 0, 0, 12) });
        for (int i = 0; i < events.Count; i++)
        {
            int idx = i;
            var ev = events[i];
            string name = ev.Buttons.Count > 0 ? string.Join(" + ", ev.Buttons.Select(Label)) + " press" : "Stick move";
            var row = NexusKit.RowButton($"{i + 1:00}  {name}", $"{ev.HoldMs} ms hold · {ev.PauseMs} ms delay", null, focused: i == selected, width: 820);
            rowPanel.Children.Add(ThemeAction(row, "theme:macro:event:" + i, () => macro.Select(idx)));
        }
        var addRow = NexusKit.RowButton("+ Add event", null, null, focused: false, width: 820);
        rowPanel.Children.Add(ThemeAction(addRow, "theme:macro:add", macro.Add, canEdit && draft.EntryCount(macro.Slot) < 47));
        var clearRow = NexusKit.RowButton("Clear sequence", null, null, focused: false, width: 820);
        rowPanel.Children.Add(ThemeAction(clearRow, "theme:macro:clear", macro.Clear, canEdit && events.Count > 0));
        if (!canEdit)
            rowPanel.Children.Add(new TextBlock { Text = "Macros are read only for this controller.", FontFamily = NexusKit.Body, FontSize = 15, Foreground = NexusKit.SubBrush, Margin = new Thickness(2, 6, 0, 0) });
        var rowScroll = new ScrollViewer { Width = 836, Height = 560, Content = rowPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Canvas.SetLeft(rowScroll, 68);
        Canvas.SetTop(rowScroll, 280);
        nexusPageHost.Children.Add(rowScroll);

        // Right: timeline of the real events and the selected event
        var sideStack = new StackPanel { Width = 612 };
        var timelineBox = new Border
        {
            Width = 612, Height = 150,
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.B("#17191c"),
            Margin = new Thickness(0, 0, 0, 18),
            ClipToBounds = true
        };
        var tCanvas = new Canvas { Width = 612, Height = 150 };
        var laneLbl = new TextBlock { Text = "Buttons", FontFamily = NexusKit.Body, FontSize = 14, Foreground = NexusKit.SubBrush };
        Canvas.SetLeft(laneLbl, 14); Canvas.SetTop(laneLbl, 40);
        tCanvas.Children.Add(laneLbl);
        int total = Math.Max(1, draft.Duration(macro.Slot));
        double laneX = 100, laneW = 492, cursor = 0;
        for (int n = 0; n < events.Count; n++)
        {
            var ev = events[n];
            double ex = laneX + cursor / total * laneW;
            double w = Math.Max(4, ev.HoldMs / (double)total * laneW);
            bool on = n == selected;
            var bar = new Border
            {
                Width = w, Height = 14,
                CornerRadius = new CornerRadius(7),
                Background = on ? Brushes.Transparent : NexusKit.GreenBrush,
                BorderBrush = on ? NexusKit.GreenBrush : null,
                BorderThickness = on ? new Thickness(2.5) : new Thickness(0)
            };
            Canvas.SetLeft(bar, ex); Canvas.SetTop(bar, 44);
            tCanvas.Children.Add(bar);
            if (ev.Buttons.Count > 0)
            {
                var key = new TextBlock { Text = string.Join("+", ev.Buttons), FontFamily = new FontFamily("Segoe UI"), FontSize = 13, FontWeight = FontWeights.Bold, Foreground = NexusKit.WhiteBrush };
                Canvas.SetLeft(key, ex); Canvas.SetTop(key, 22);
                tCanvas.Children.Add(key);
            }
            cursor += ev.HoldMs + ev.PauseMs;
        }
        var totalLbl = new TextBlock { Text = events.Count == 0 ? "Empty" : $"{draft.Duration(macro.Slot)} ms total", FontFamily = NexusKit.Body, FontSize = 12, Foreground = NexusKit.SubBrush };
        Canvas.SetLeft(totalLbl, 14); Canvas.SetTop(totalLbl, 122);
        tCanvas.Children.Add(totalLbl);
        timelineBox.Child = tCanvas;
        sideStack.Children.Add(timelineBox);

        var card = new Border { Width = 612, Padding = new Thickness(28, 26, 28, 26), CornerRadius = new CornerRadius(8), Background = NexusKit.PanelBrush };
        var cardStack = new StackPanel();
        if (selected < 0)
        {
            cardStack.Children.Add(new TextBlock { Text = "No event selected", FontFamily = NexusKit.Display, FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = NexusKit.WhiteBrush });
            cardStack.Children.Add(new TextBlock { Text = "Pick an event on the left, or add one.", FontFamily = NexusKit.Body, FontSize = 15, Foreground = NexusKit.SubBrush, Margin = new Thickness(0, 4, 0, 0) });
        }
        else
        {
            var cur = events[selected];
            cardStack.Children.Add(new TextBlock { Text = $"{selected + 1:00} · " + (cur.Buttons.Count > 0 ? string.Join(" + ", cur.Buttons.Select(Label)) : "Stick move"), FontFamily = NexusKit.Display, FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = NexusKit.WhiteBrush });
            cardStack.Children.Add(new TextBlock { Text = $"{cur.HoldMs} ms hold · {cur.PauseMs} ms delay", FontFamily = NexusKit.Body, FontSize = 15, Foreground = NexusKit.SubBrush, Margin = new Thickness(0, 4, 0, 16) });
            var keys = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            foreach (string b in MacroDraft.Inputs)
            {
                string bn = b;
                bool has = cur.Buttons.Contains(bn);
                var chip = new Border { MinWidth = 52, Height = 34, Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 0, 8, 0), Background = has ? NexusKit.WhiteBrush : NexusKit.TileBrush, Child = new TextBlock { Text = Label(bn), FontFamily = NexusKit.Body, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Foreground = has ? NexusKit.B("#111111") : NexusKit.WhiteBrush } };
                keys.Children.Add(ThemeAction(chip, "theme:macro:key:" + bn, () =>
                {
                    var next = cur.Buttons.Contains(bn) ? cur.Buttons.Where(x => x != bn).ToList() : cur.Buttons.Append(bn).ToList();
                    if (next.Count == 0) return;
                    macro.Update(cur with { Buttons = next });
                }, canEdit));
            }
            cardStack.Children.Add(keys);
            var timing = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            void Step(string label, string tag, Func<MacroEvent> make)
            {
                var btn = NexusKit.TheatricalButton(label, primary: false, height: 40, fontSize: 14);
                btn.Margin = new Thickness(0, 0, 8, 0);
                timing.Children.Add(ThemeAction(btn, tag, () => macro.Update(make()), canEdit));
            }
            Step("Hold −5", "theme:macro:hold-dec", () => cur with { HoldMs = Math.Max(5, cur.HoldMs - 5) });
            Step("Hold +5", "theme:macro:hold-inc", () => cur with { HoldMs = Math.Min(10000, cur.HoldMs + 5) });
            Step("Delay −5", "theme:macro:pause-dec", () => cur with { PauseMs = Math.Max(0, cur.PauseMs - 5) });
            Step("Delay +5", "theme:macro:pause-inc", () => cur with { PauseMs = Math.Min(10000, cur.PauseMs + 5) });
            cardStack.Children.Add(timing);
            var cBtns = new StackPanel { Orientation = Orientation.Horizontal };
            var dup = NexusKit.TheatricalButton("Duplicate", primary: false, height: 46, fontSize: 15);
            dup.Margin = new Thickness(0, 0, 10, 0);
            cBtns.Children.Add(ThemeAction(dup, "theme:macro:duplicate", macro.Duplicate, canEdit && draft.EntryCount(macro.Slot) < 46));
            var delBtn = NexusKit.TheatricalButton("Delete event", primary: false, height: 46, fontSize: 15);
            cBtns.Children.Add(ThemeAction(delBtn, "theme:macro:remove", macro.Remove, canEdit));
            cardStack.Children.Add(cBtns);
        }
        card.Child = cardStack;
        sideStack.Children.Add(card);
        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 280);
        nexusPageHost.Children.Add(sideStack);

        var hints = NexusKit.HintsBar(new[] { ("A", "Select"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }
}

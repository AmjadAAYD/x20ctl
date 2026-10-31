using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private StackPanel? hybridMacroChain, hybridMacroSlots, hybridMacroActions;
    private MacroWorkbench? hybridMacroWorkbench;
    private Canvas? hybridMacroTimeline;
    private StackPanel? hybridMacroEditor;
    private string hybridMacroTab = "buttons";
    private System.Windows.Threading.DispatcherTimer? hybridMacroTimer;
    private void HybridMacrosPage()
    {
        var macro = Macros; if (hybridMacros == null || macro == null) return;
        if (ReferenceEquals(hybridMacroWorkbench, macro) && hybridMacros.Children.Count > 0) { HybridMacroUpdate(); return; }
        hybridMacroWorkbench = macro; macro.Changed += HybridMacroUpdate; hybridMacros.Children.Clear();
        hybridMacros.IsVisibleChanged += (_, _) => { if (!hybridMacros.IsVisible && ReferenceEquals(hybridMacroWorkbench, macro)) { if (macro.IsRecording) macro.ToggleRecording(); macro.StopPreview(); hybridMacroTimer?.Stop(); } };
        var board = new Canvas { Width = 1600, Height = 848, Background = HBrush("#17151c"), ClipToBounds = true }; hybridMacros.Children.Add(board);
        HAt(board, new Rectangle { Width = 156, Height = 848, Fill = HBrush("#cbb5e4") }, 0, 0);
        HAt(board, new Rectangle { Width = 140, Height = 848, Fill = HBrush("#29262f") }, 1460, 0);
        HAt(board, HText("Macros", 38, "#f5f1fa", FontWeights.ExtraBold), 216, 14);
        HAt(board, HText("LOCAL DRAFT · Nothing is written to controller memory.", 14, "#bdb4c8", FontWeights.SemiBold), 218, 64);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++) { var (label, key) = pages[i]; var nav = HybridButton(label, "hybrid:nav:" + key, key == "macros", 120, 50); nav.Click += (_, _) => HybridGo(key); HAt(board, nav, 18, 88 + i * 67); }
        hybridMacroSlots = new StackPanel { Orientation = Orientation.Horizontal };
        HAt(board, hybridMacroSlots, 770, 5);
        HAt(board, HText("SEQUENCE · Select an event to edit", 14, "#bdb4c8", FontWeights.Bold), 216, 110);
        hybridMacroChain = new StackPanel { Orientation = Orientation.Horizontal };
        var chainViewport = new ScrollViewer { Content = hybridMacroChain, Width = 1168, Height = 80, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }; HAt(board, chainViewport, 216, 140);
        // The original workbench retains its full editor, record/preview controls and trigger logic.
        if (macro.Body.Parent is Panel parent) parent.Children.Remove(macro.Body);
        else if (macro.Body.Parent is Decorator decorator) decorator.Child = null;
        hybridMacroTimeline = new Canvas { Width = 640, Height = 150, Background = HBrush("#211e27"), ClipToBounds = true, Tag = "hybrid:macro:timeline" };
        HAt(board, hybridMacroTimeline, 216, 270);
        hybridMacroActions = new StackPanel { Width = 640, Tag = "hybrid:macro:tools" };
        HAt(board, hybridMacroActions, 216, 460);
        HAt(board, new Rectangle { Width = 2, Height = 560, Fill = HBrush("#52dcff") }, 888, 250);
        HAt(board, HText("EVENT EDITOR", 13, "#bdb4c8", FontWeights.Bold), 930, 260);
        hybridMacroEditor = new StackPanel { Width = 454, Tag = "hybrid:macro:editor" };
        var editorScroll = new ScrollViewer { Width = 454, Height = 500, Content = hybridMacroEditor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        HAt(board, editorScroll, 930, 295);
        var holder = new Viewbox { Width = 1168, Height = 500, Stretch = Stretch.Uniform, Child = macro.Body, Visibility = Visibility.Collapsed }; HAt(board, holder, 216, 295);
        var expand = HybridButton("Expand editor", "hybrid:macro:expand", false, 160, 38);
        HAt(board, expand, 1205, 248); Panel.SetZIndex(expand, 3);
        bool expanded = false;
        var cover = new Rectangle { Width = 1168, Height = 562, Fill = HBrush("#17151c"), Visibility = Visibility.Collapsed };
        HAt(board, cover, 216, 246); Panel.SetZIndex(cover, 1); Panel.SetZIndex(holder, 2);
        expand.Click += (_, _) =>
        {
            expanded = !expanded; expand.Content = expanded ? "Close editor" : "Expand editor";
            cover.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            holder.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            editorScroll.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        };
        HybridMacroUpdate();
    }
    private void HybridMacroUpdate()
    {
        if (hybridMacroChain == null || Macros == null || !IsMacros) return;
        var m = Macros; hybridMacroChain.Children.Clear();
        if (hybridMacroSlots != null)
        {
            hybridMacroSlots.Children.Clear();
            foreach (string slot in m.Draft.SlotNames)
            {
                var tab = HybridButton(slot, "hybrid:macro:slot:" + slot, slot == m.Slot, 100, 38);
                tab.Margin = new(0, 0, 5, 0); tab.Click += (_, _) => { m.SelectSlot(slot); hybridMacroSlots.Children.OfType<Button>().FirstOrDefault(b => b.Tag as string == "hybrid:macro:slot:" + slot)?.Focus(); }; hybridMacroSlots.Children.Add(tab);
            }
        }
        HybridMacroDrawTimeline(m);
        HybridMacroDrawTools(m);
        HybridMacroDrawEditor(m);
        for (int i = 0; i < m.Draft[m.Slot].Count; i++)
        {
            int index = i; var ev = m.Draft[m.Slot][i];
            var card = HybridButton($"{i + 1:00}   {RebelPresses(ev)}", "hybrid:macro:event:" + i, i == m.SelectedIndex, 200, 56); card.Margin = new(0, 0, 8, 0);
            card.Click += (_, _) => { m.Select(index); HybridMacroUpdate(); }; hybridMacroChain.Children.Add(card);
        }
        var add = HybridButton("+ Add event", "hybrid:macro:add", false, 180, 56); add.IsEnabled = m.Draft.CanEdit && m.Draft.EntryCount(m.Slot) < 47; add.Click += (_, _) => m.Add(); hybridMacroChain.Children.Add(add);
    }
    private void HybridMacroDrawTimeline(MacroWorkbench m)
    {
        if (hybridMacroTimeline == null) return;
        var canvas = hybridMacroTimeline; canvas.Children.Clear();
        var events = m.Draft[m.Slot]; double total = Math.Max(1, events.Sum(e => e.HoldMs + e.PauseMs));
        string[] labels = ["Buttons", "Left stick", "Right stick", "Pause"];
        for (int i = 0; i < labels.Length; i++)
        {
            HAt(canvas, HText(labels[i], 12, "#bdb4c8", FontWeights.SemiBold), 10, 14 + i * 32);
            HAt(canvas, new Rectangle { Width = 510, Height = 1, Fill = HBrush("#514a58") }, 120, 30 + i * 32);
        }
        double at = 0;
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i]; double x = 120 + 510 * at / total, width = Math.Max(3, 510 * ev.HoldMs / total);
            var stroke = m.IsPreviewing && i == m.PreviewIndex ? "#ffffff" : i == m.SelectedIndex ? "#52dcff" : "#d5bff6";
            if (ev.Buttons.Count > 0 || ev.Left == X20Ctl.Zone.StickHeading.Neutral && ev.Right == X20Ctl.Zone.StickHeading.Neutral) AddBar(x, width, 0, stroke);
            if (ev.Left != X20Ctl.Zone.StickHeading.Neutral) AddBar(x, width, 1, stroke);
            if (ev.Right != X20Ctl.Zone.StickHeading.Neutral) AddBar(x, width, 2, stroke);
            if (ev.PauseMs > 0) AddBar(x + width, Math.Max(3, 510 * ev.PauseMs / total), 3, "#847894");
            at += ev.HoldMs + ev.PauseMs;
        }
        void AddBar(double x, double width, int lane, string color) => HAt(canvas, new Rectangle { Width = width, Height = 10, RadiusX = 5, RadiusY = 5, Fill = HBrush(color) }, x, 20 + lane * 32);
    }

    private void HybridMacroDrawEditor(MacroWorkbench m)
    {
        if (hybridMacroEditor == null) return;
        var panel = hybridMacroEditor;
        string? focusedTag = System.Windows.Input.Keyboard.FocusedElement is FrameworkElement focused && panel.IsAncestorOf(focused) ? focused.Tag as string : null;
        panel.Children.Clear();
        if (focusedTag != null) Dispatcher.BeginInvoke(() =>
        {
            if (!panel.IsVisible) return;
            Button? Find(DependencyObject node)
            {
                if (node is Button button && button.Tag as string == focusedTag) return button;
                foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) if (Find(child) is { } found) return found;
                return null;
            }
            Find(panel)?.Focus();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
        var events = m.Draft[m.Slot]; int selected = m.SelectedIndex;
        X20Ctl.Zone.MacroEvent? ev = selected >= 0 && selected < events.Count ? events[selected] : null;
        panel.Children.Add(HText(ev == null ? "Event editor" : $"Event {selected + 1:00} · {RebelPresses(ev)}", 22, "#f5f1fa", FontWeights.Bold));
        var tabs = new WrapPanel { Margin = new(0, 14, 0, 14) }; panel.Children.Add(tabs);
        foreach (var (label, key) in new[] { ("Buttons", "buttons"), ("Sticks", "sticks"), ("Timing", "timing"), ("Actions", "actions") })
        {
            var tab = HybridButton(label, "hybrid:macro:tab:" + key, key == hybridMacroTab, 100, 36); tab.IsEnabled = ev != null;
            tab.Click += (_, _) => { hybridMacroTab = key; HybridMacroDrawEditor(m); }; tabs.Children.Add(tab);
        }
        if (ev == null) { panel.Children.Add(HText("Select or add an event to edit it.", 15, "#bdb4c8", FontWeights.Normal)); return; }
        bool edit = m.Draft.CanEdit;
        void Set(X20Ctl.Zone.MacroEvent next) => m.Update(next);
        switch (hybridMacroTab)
        {
            case "sticks":
                foreach (var (label, left) in new[] { ("Left stick", true), ("Right stick", false) })
                {
                    var caption = HText(label, 16, "#f5f1fa", FontWeights.Bold); caption.Margin = new(0, 8, 0, 6); panel.Children.Add(caption);
                    var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Width = 180, HorizontalAlignment = HorizontalAlignment.Left };
                    X20Ctl.Zone.StickHeading[] order = [X20Ctl.Zone.StickHeading.UpLeft, X20Ctl.Zone.StickHeading.Up, X20Ctl.Zone.StickHeading.UpRight, X20Ctl.Zone.StickHeading.Left, X20Ctl.Zone.StickHeading.Neutral, X20Ctl.Zone.StickHeading.Right, X20Ctl.Zone.StickHeading.DownLeft, X20Ctl.Zone.StickHeading.Down, X20Ctl.Zone.StickHeading.DownRight];
                    foreach (var direction in order) { var b = HybridButton(RebelArrow(direction), "hybrid:macro:stick:" + label + ":" + direction, (left ? ev.Left : ev.Right) == direction, 54, 34); b.IsEnabled = edit; b.Click += (_, _) => Set(left ? ev with { Left = direction } : ev with { Right = direction }); grid.Children.Add(b); }
                    panel.Children.Add(grid);
                }
                break;
            case "timing":
                foreach (var (label, pause) in new[] { ("Hold", false), ("Pause", true) })
                {
                    int value = pause ? ev.PauseMs : ev.HoldMs; var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 14) };
                    var labelText = HText(label, 16, "#f5f1fa", FontWeights.Bold); labelText.Width = 90; line.Children.Add(labelText);
                    foreach (int delta in new[] { -5, 5 })
                    {
                        int next = Math.Clamp(value + delta, 0, 327675); var b = HybridButton(delta < 0 ? "− 5" : "+ 5", "hybrid:macro:time:" + label + ":" + delta, false, 68, 38); b.IsEnabled = edit;
                        b.Click += (_, _) => Set(pause ? ev with { PauseMs = next } : ev with { HoldMs = next }); line.Children.Add(b);
                        if (delta < 0) { var text = HText($"{value} ms", 16, "#d5bff6", FontWeights.Bold); text.Width = 100; text.TextAlignment = TextAlignment.Center; line.Children.Add(text); }
                    }
                    panel.Children.Add(line);
                }
                break;
            case "actions":
                var actions = new WrapPanel();
                foreach (var (label, action, enabled) in new (string, Action, bool)[] { ("Earlier", () => m.Move(-1), selected > 0), ("Later", () => m.Move(1), selected < events.Count - 1), ("Duplicate", m.Duplicate, m.Draft.EntryCount(m.Slot) < 47), ("Delete", m.Remove, true), ("Clear slot", m.Clear, true) })
                { var b = HybridButton(label, "hybrid:macro:action:" + label, false, 120, 38); b.IsEnabled = edit && enabled; b.Click += (_, _) => action(); actions.Children.Add(b); }
                panel.Children.Add(actions); break;
            default:
                var inputs = new WrapPanel();
                foreach (string key in X20Ctl.Zone.MacroDraft.Inputs)
                { var b = HybridButton(RebelInputGlyph(key), "hybrid:macro:input:" + key, ev.Buttons.Contains(key), 65, 40); b.IsEnabled = edit; b.Click += (_, _) => Set(ev with { Buttons = ev.Buttons.Contains(key) ? ev.Buttons.Where(x => x != key).ToArray() : ev.Buttons.Append(key).ToArray() }); inputs.Children.Add(b); }
                panel.Children.Add(inputs); break;
        }
    }

    private void HybridMacroDrawTools(MacroWorkbench m)
    {
        if (hybridMacroActions == null) return;
        var root = hybridMacroActions; root.Children.Clear();
        var title = HText(m.Draft.PcMode ? "Played by · PC mode" : "Played by", 16, "#f5f1fa", FontWeights.Bold); root.Children.Add(title);
        if (m.Draft.PcMode)
        {
            var choices = new[] { "Not used" }.Concat(X20Ctl.Zone.MacroDraft.TriggerInputs).ToArray();
            var trigger = new ComboBox { ItemsSource = choices, Width = 260, Height = 40, Margin = new(0, 8, 0, 12), HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = m.Draft.CanEdit, Tag = "hybrid:macro:trigger" };
            trigger.SelectedItem = m.Draft.Trigger(m.Slot) ?? "Not used";
            trigger.SelectionChanged += (_, _) => { if (trigger.SelectedItem is not string picked) return; m.SetTrigger(picked == "Not used" ? null : picked); };
            root.Children.Add(trigger);
        }
        else { var label = HText(m.Draft.PhysicalSlots.Contains(m.Slot) ? "The " + m.Slot + " paddle" : "Not used", 15, "#bdb4c8", FontWeights.Normal); label.Margin = new(0, 8, 0, 12); root.Children.Add(label); }
        var actions = new WrapPanel(); root.Children.Add(actions);
        var record = HybridButton(m.IsRecording ? "Stop recording" : "Record", "hybrid:macro:record", false, 150, 40);
        record.SetBinding(IsEnabledProperty, new Binding(nameof(IsEnabled)) { Source = m.Record });
        if (m.Record.Content is StackPanel { Children.Count: 2 } nativeRecord && nativeRecord.Children[1] is TextBlock nativeRecordLabel)
            record.SetBinding(ContentControl.ContentProperty, new Binding(nameof(TextBlock.Text)) { Source = nativeRecordLabel });
        record.Click += (_, _) => { m.ToggleRecording(); HybridMacroUpdate(); }; actions.Children.Add(record);
        var preview = HybridButton("Preview timeline", "hybrid:macro:preview", false, 180, 40);
        preview.SetBinding(ContentControl.ContentProperty, new Binding(nameof(ContentControl.Content)) { Source = m.PreviewButton });
        preview.SetBinding(IsEnabledProperty, new Binding(nameof(IsEnabled)) { Source = m.PreviewButton });
        preview.Click += (_, _) =>
        {
            m.Preview();
            if (!animate || !m.IsPreviewing) { hybridMacroTimer?.Stop(); HybridMacroDrawTimeline(m); return; }
            if (hybridMacroTimer == null)
            {
                hybridMacroTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(32) };
                hybridMacroTimer.Tick += (_, _) =>
                {
                    if (Macros is not { } active || !active.IsPreviewing || !IsMacros) { hybridMacroTimer.Stop(); if (Macros is { } current) HybridMacroDrawTimeline(current); return; }
                    HybridMacroDrawTimeline(active);
                };
            }
            hybridMacroTimer.Start();
        }; actions.Children.Add(preview);
        var status = HText("", 13, "#bdb4c8", FontWeights.Normal); status.TextWrapping = TextWrapping.Wrap; status.Margin = new(0, 12, 0, 0);
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = m.StatusText }); root.Children.Add(status);
    }

}

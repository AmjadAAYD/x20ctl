using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool hybridAllAssignments;
    private void HybridButtonsPage()
    {
        if (hybridButtons == null) return;
        string? focused = Keyboard.FocusedElement is FrameworkElement { Tag: string tag } element && hybridButtons.IsAncestorOf(element) ? tag : null;
        var groups = RebelAvailableGroups(); if (groups.Length == 0) return;
        if (SelectedControl.Length == 0 || !layout.Controls.Any(control => control.Key == SelectedControl)) { SelectControl(groups[0].keys[0]); return; }
        string selected = SelectedControl; var group = groups.FirstOrDefault(g => g.keys.Contains(selected)); if (group.keys == null) group = groups[0];
        bool editable = Draft.CanEdit(selected); string target = Draft.Target(selected);
        hybridButtons.Children.Clear(); var board = new Canvas { Width = 1600, Height = 848, ClipToBounds = true, Background = HBrush("#17151c") }; hybridButtons.Children.Add(board);
        HAt(board, new Rectangle { Width = 156, Height = 848, Fill = HBrush("#cbb5e4") }, 0, 0);
        HAt(board, new Rectangle { Width = 140, Height = 848, Fill = HBrush("#29262f") }, 1460, 0);
        HAt(board, new Rectangle { Width = 3, Height = 690, Fill = HBrush("#52dcff") }, 846, 108);
        HAt(board, HText("Buttons", 38, "#f5f1fa", FontWeights.ExtraBold), 216, 14);
        HAt(board, HText(Draft.PcMode ? "LOCAL DRAFT · Applies on this PC." : "LOCAL DRAFT · Not written to controller memory.", 14, "#bdb4c8", FontWeights.SemiBold), 218, 64);
        HAt(board, HText("Press a control to select it · Esc Back", 13, "#c7bdd2", FontWeights.SemiBold), 216, 812);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++)
        {
            var (label, key) = pages[i]; var nav = HybridButton(label, "hybrid:nav:" + key, key == "buttons", 120, 50);
            nav.Click += (_, _) => HybridGo(key); HAt(board, nav, 18, 88 + i * 67);
        }
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (name, keys) in groups)
        {
            var tab = HybridButton(name, "hybrid:group:" + name, !hybridAllAssignments && name == group.name, 112, 38); tab.Margin = new(0, 0, 4, 0);
            tab.Click += (_, _) => { hybridAllAssignments = false; SelectControl(keys[0]); HybridLater(); }; tabs.Children.Add(tab);
        }
        HAt(board, tabs, 786, 5);
        var rows = new StackPanel { Width = 588 };
        var keysShown = hybridAllAssignments ? ButtonDraft.Sources.Where(k => layout.Controls.Any(c => c.Key == k) && Draft.Target(k) != k).ToArray() : group.keys;
        if (keysShown.Length == 0) rows.Children.Add(HText("No changed assignments", 16, "#c8bdd1", FontWeights.SemiBold));
        foreach (string key in keysShown)
        {
            string caption = Draft.CanEdit(key) ? $"{RebelGlyph(key)}    sends    {RebelGlyph(Draft.Target(key))}" : $"{RebelGlyph(key)}    ·    Not remappable";
            var row = HybridButton(caption, "hybrid:row:" + key, key == selected, 588, 57); row.HorizontalContentAlignment = HorizontalAlignment.Left; row.Padding = new(20, 0, 0, 0); row.Margin = new(0, 0, 0, 8);
            row.Click += (_, _) => { if (!OpenMacroSlot(key)) SelectControl(key); }; row.GotKeyboardFocus += (_, _) => { if (SelectedControl != key) SelectControl(key); }; rows.Children.Add(row);
        }
        var reset = HybridButton("Reset this control", "hybrid:reset", false, 588, 54); reset.IsEnabled = editable; reset.Click += (_, _) => { Draft.Reset(SelectedControl); RefreshEditor(); }; rows.Children.Add(reset);
        var test = HybridButton("Test mapping", "hybrid:test", false, 588, 54); test.Click += (_, _) => StartTry(); rows.Children.Add(test);
        var all = HybridButton(hybridAllAssignments ? "Show selected group" : "All changed assignments", "hybrid:all", hybridAllAssignments, 588, 54); all.Click += (_, _) => { hybridAllAssignments = !hybridAllAssignments; HybridLater(); }; rows.Children.Add(all);
        var rowScroll = new ScrollViewer { Content = rows, Width = 588, Height = 640, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; HAt(board, rowScroll, 216, 116);
        HAt(board, Pieces.Pad(Model.Id, PadColours.For(Player, Model.Id), 470), 900, 108);
        HAt(board, HText(editable ? $"{RebelGlyph(selected)} sends {RebelGlyph(target)}" : $"{RebelGlyph(selected)} · read-only", 17, "#f7f3fc", FontWeights.Bold), 900, 430);
        HAt(board, HText("Output", 14, "#c8bdd1", FontWeights.Bold), 900, 468);
        var outputs = Draft.PcMode ? ButtonDraft.Targets.Append("NONE").ToArray() : ButtonDraft.Targets;
        var cells = new WrapPanel { Width = 484, ItemWidth = 96, ItemHeight = 80 };
        foreach (string key in outputs)
        {
            var cell = HybridButton(RebelGlyph(key), "hybrid:out:" + key, key == target, 72, 70); cell.IsEnabled = editable; cell.Margin = new(4); cell.Click += (_, _) => SetDraftTarget(key); System.Windows.Automation.AutomationProperties.SetName(cell, "Send " + Label(key)); cells.Children.Add(cell);
        }
        var outputScroll = new ScrollViewer { Content = cells, Width = 484, Height = 244, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; HAt(board, outputScroll, 900, 494);
        string? want = focused is { } f && f.StartsWith("hybrid:row:") && f != "hybrid:row:" + selected ? "hybrid:row:" + selected : focused;
        if (want != null) Dispatcher.BeginInvoke(() => FindTagged(board, want)?.Focus(), DispatcherPriority.Input);
    }
    private Button HybridButton(string text, string tag, bool selected, double width, double height)
    {
        var label = new TextBlock { Text = text, Foreground = HBrush(selected ? "#1b1622" : "#f4f1f8"), TextTrimming = TextTrimming.CharacterEllipsis };
        var button = new Button { Content = label, Tag = tag, Width = width, Height = height, MinHeight = 0, Padding = new Thickness(8, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = HBrush(selected ? "#1b1622" : "#f4f1f8"), Background = HBrush(selected ? "#d8c6ed" : "#2d2933"), BorderBrush = HBrush(selected ? "#52dcff" : "#514a58"), BorderThickness = new(selected ? 3 : 1), Cursor = Cursors.Hand };
        var scale = new ScaleTransform(1, 1); button.RenderTransform = scale; button.RenderTransformOrigin = new(.5, .5);
        void Paint() { bool focused = button.IsKeyboardFocused || button.IsMouseOver; button.BorderBrush = HBrush(focused || selected ? "#52dcff" : "#514a58"); button.BorderThickness = new(focused || selected ? 3 : 1); if (animate && SystemParameters.ClientAreaAnimation) { var motion = new DoubleAnimation(focused ? 1.04 : 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new BackEase { Amplitude = .3, EasingMode = EasingMode.EaseOut } }; scale.BeginAnimation(ScaleTransform.ScaleXProperty, motion); scale.BeginAnimation(ScaleTransform.ScaleYProperty, motion); } }
        button.GotKeyboardFocus += (_, _) => Paint(); button.LostKeyboardFocus += (_, _) => Paint(); button.MouseEnter += (_, _) => Paint(); button.MouseLeave += (_, _) => Paint();
        return button;
    }
}

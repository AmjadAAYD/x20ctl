using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product;

/// <summary>The Rebel My setups page (owner board setups.png): every saved setup is a wanted poster; the one chosen is pinned large with
/// its facts beside it. Every action is the Setups workbench's own button.</summary>
public partial class StudioScene
{
    private Grid? rebelSetups; private SetupsWorkbench? rebelSetupsBench;

    /// <summary>A poster (.pc): a paper frame round a red field, a black stripe, halftone dots, the pad and a star.</summary>
    private FrameworkElement RebelPoster(double w, double h, double padScale = .92)
    {
        var root = new Grid { Width = w, Height = h, Effect = Ink.Shadow(6, 9, 14, Colors.Black, .55) };
        root.Children.Add(new Border { Background = Ink.B(Ink.White) });
        double b = Math.Max(4, w * .02); var c = new Canvas { Width = w - 2 * b, Height = h - 2 * b, ClipToBounds = true, Margin = new(b) }; var iw = w - 2 * b; var ih = h - 2 * b;
        c.Children.Add(new Rectangle { Width = iw, Height = ih, Fill = Ink.B(Ink.Red) });
        c.Children.Add(RPoly(Ink.B(Ink.Black), iw * .38, ih, iw * .56, ih, iw * 1.02, 0, iw * .84, 0));
        c.Children.Add(RPoly(Ink.B(Ink.White), iw * .86, 0, iw * .88, 0, iw * .44, ih, iw * .42, ih).Also(p => p.Opacity = .9));
        c.Children.Add(Dots.Field(0, ih * .55, iw, ih * .45, Ink.A(Ink.Black, .45), Math.Max(6, w / 28), 1.5, Dots.Fade(0, .9)));
        var pad = Pieces.Pad(Model.Id, PadColours.For(Player, Model.Id), iw * padScale); pad.Effect = Ink.Shadow(5, 6, 0, Colors.Black, 1); Canvas.SetLeft(pad, iw * (1 - padScale) / 2); Canvas.SetTop(pad, ih * .16); c.Children.Add(pad);
        c.Children.Add(Star.Make(iw - w * .13, ih - w * .13, w * .07, 0, 1, Ink.White, Ink.Black));
        root.Children.Add(c); root.IsHitTestVisible = false; return root;
    }
    private static Button RebelFlat(FrameworkElement content, string tag, Action click)
    {
        var b = new Button { Content = content, Tag = tag, Template = RebelBare(), FocusVisualStyle = null, Cursor = Cursors.Hand }; b.Click += (_, _) => click(); return b;
    }

    private void RebelSetupsPage()
    {
        var w = Setups; var g = rebelSetups!; g.Children.Clear();
        if (w == null) return;
        if (!ReferenceEquals(w, rebelSetupsBench)) { rebelSetupsBench = w; w.Watch(() => { if (rebelPage == "setups" && ReferenceEquals(rebelSetupsBench, Setups)) RebelLater(); }); }
        string? focused = (Keyboard.FocusedElement as FrameworkElement) is { Tag: string t } fe && g.IsAncestorOf(fe) ? t : null;
        var cards = w.Cards; string selId = w.SelectedId ?? ""; bool has = cards.Any(c => c.Id == selId);

        if (has)
        {
            var sel = cards.First(c => c.Id == selId);
            // the poster pinned large, with its name cut out of the papers
            var big = RebelPoster(358, 462, .94); big.RenderTransformOrigin = new(.5, .5); big.RenderTransform = new RotateTransform(-2.4);
            var shown = new Grid { Width = 358, Height = 462 }; shown.Children.Add(big);
            var name = Ransom.Make(sel.Name, 30); var tape = new Viewbox { Child = name, MaxWidth = 322, StretchDirection = StretchDirection.DownOnly, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(24, 0, 0, 86), RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-2), IsHitTestVisible = false };
            shown.Children.Add(tape); g.Children.Add(Place(shown, 262, 160));

            // its facts
            var info = new StackPanel { Width = 330 }; info.Children.Add(Ink.Label("Selected setup", 12, Ink.B(Ink.Hot)));
            if (w.RenameActive)
            {
                var box = w.TakeNameField(); box.FontFamily = Ink.Heavy; box.FontSize = 26; box.Height = 48; box.Width = 330; box.Foreground = Ink.B(Ink.White); box.Background = Ink.B(Ink.Panel); box.BorderBrush = Ink.B(Ink.Red); box.BorderThickness = new(2); box.CaretBrush = Ink.B(Ink.White); box.Padding = new(10, 2, 10, 2); box.Margin = new(0, 8, 0, 0); box.VerticalContentAlignment = VerticalAlignment.Center; box.Tag = "su:name";
                info.Children.Add(box);
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 12, 0, 0) };
                row.Children.Add(RebelBt("Save name", null, () => w.Click("saveName"), "su:savename", 14, 38));
                info.Children.Add(row);
            }
            else
            {
                info.Children.Add(Ink.Text(w.DetailName, 36).Also(t => { t.Margin = new(0, 6, 0, 0); t.TextTrimming = TextTrimming.CharacterEllipsis; t.MaxWidth = 330; }));
                var state = Pieces.Tag(w.DetailState); state.HorizontalAlignment = HorizontalAlignment.Left; state.Margin = new(0, 10, 0, 0); info.Children.Add(state);
                info.Children.Add(Ink.Text(w.DetailModel, 15, Ink.B(Ink.C("#9d9598")), false).Also(t => { t.Margin = new(0, 12, 0, 0); t.TextWrapping = TextWrapping.Wrap; }));
            }
            g.Children.Add(Place(info, 650, 176));
            var facts = new StackPanel { Width = 320 }; int fi = 0;
            foreach (var (label, value) in w.Metrics)
            {
                var row = new RowSlab(fi, 38) { Margin = new(fi * 4, 0, 0, 6), IsHitTestVisible = false, Focusable = false }; fi++;
                row.Left.Children.Add(Ink.Text(label, 16)); row.Right.Children.Add(Ink.Text(value, 14, Ink.B(Ink.Hot), false, FontWeights.SemiBold)); facts.Children.Add(row);
            }
            g.Children.Add(Place(facts, 650, 330));

            // its actions
            var acts = new StackPanel { Orientation = Orientation.Horizontal };
            acts.Children.Add(RebelPrimary("Open", () => w.Click("open"), "su:open", false).Also(b => { b.IsEnabled = w.Can("open"); b.Margin = new(0, 0, 14, 0); }));
            acts.Children.Add(RebelBt("Duplicate", null, () => w.Click("duplicate"), "su:duplicate", 15, 44, false, false, w.Can("duplicate")));
            acts.Children.Add(RebelBt("Rename", null, () => w.Click("rename"), "su:rename", 15, 44, false, false, w.Can("rename")));
            acts.Children.Add(RebelBt("Export Setup", null, () => w.Click("export"), "su:export", 15, 44, false, false, w.Can("export")));
            acts.Children.Add(RebelBt("Delete setup…", null, () => w.Click("delete"), "su:delete", 15, 44, false, false, w.Can("delete")));
            g.Children.Add(Place(acts, 276, 668));
        }
        else
        {
            var empty = RebelPoster(358, 462, .94); empty.RenderTransformOrigin = new(.5, .5); empty.RenderTransform = new RotateTransform(-2.4); empty.Opacity = .85; g.Children.Add(Place(empty, 262, 160));
            var col = new StackPanel { Width = 360 }; col.Children.Add(Ransom.Make("Make it yours", 40, 5));
            col.Children.Add(Ink.Text("Keep your controller preferences together. Create a setup, save your draft or import a preserved copy.", 16, Ink.B(Ink.C("#cfc7c9")), false).Also(t => { t.Margin = new(0, 22, 0, 0); t.TextWrapping = TextWrapping.Wrap; t.LineHeight = 24; }));
            if (!w.Healthy) col.Children.Add(Ink.Text(w.LibraryText, 15, Ink.B(Ink.Hot), false, FontWeights.SemiBold).Also(t => { t.Margin = new(0, 16, 0, 0); t.TextWrapping = TextWrapping.Wrap; }));
            g.Children.Add(Place(col, 650, 220));
        }

        // the wall of the others: posters, then dashed places for the setups to come
        var wall = new Canvas { Width = 540, Height = 520 };
        int slots = Math.Max(6, (cards.Count + 2) / 3 * 3);
        for (int i = 0; i < slots; i++)
        {
            double x = i % 3 * 178 + (i % 3 == 1 ? 0 : 0), y = i / 3 * 259;
            if (i < cards.Count)
            {
                var (id, nm, st) = cards[i]; bool on = id == selId;
                var poster = RebelPoster(150, 205, .96); poster.RenderTransformOrigin = new(.5, .5); poster.RenderTransform = new RotateTransform(i % 3 == 0 ? -2 : i % 3 == 1 ? 1 : 2); poster.VerticalAlignment = VerticalAlignment.Top; poster.HorizontalAlignment = HorizontalAlignment.Center; poster.Margin = new(0, 2, 0, 0);
                var label = new StackPanel { Width = 160, Margin = new(0, 216, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
                label.Children.Add(Ink.Text(nm, 16, Ink.B(on ? Ink.Hot : Ink.White)).Also(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.MaxWidth = 160; t.HorizontalAlignment = HorizontalAlignment.Center; }));
                label.Children.Add(Ink.Text(st, 11.5, Ink.B(Ink.C("#b8b0b3")), false).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.TextTrimming = TextTrimming.CharacterEllipsis; t.MaxWidth = 160; }));
                var cell = new Grid { Width = 168, Height = 252, Background = Brushes.Transparent }; cell.Children.Add(poster); cell.Children.Add(label);
                string key = id; var frame = new Border { BorderThickness = new(2), BorderBrush = Brushes.Transparent, Child = cell };
                var b = RebelFlat(frame, "su:c:" + id, () => w.Select(key)); Canvas.SetLeft(b, x); Canvas.SetTop(b, y);
                void Paint() { frame.BorderBrush = b.IsKeyboardFocused || b.IsMouseOver ? Ink.B(Ink.A(Ink.White, .7)) : Brushes.Transparent; }
                b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint();
                wall.Children.Add(b);
            }
            else
            {
                var dash = new Slab { Width = 146, Height = 206, Seed = 40 + i, Fill = null, Line = Ink.B(Ink.A(Ink.White, .3)), LineWidth = 1.6, Dash = new DoubleCollection { 5, 4 }, Amp = 3, Rot = i % 3 == 0 ? -1.4 : i % 3 == 1 ? .8 : 1.2, IsHitTestVisible = false };
                Canvas.SetLeft(dash, x + 8); Canvas.SetTop(dash, y + 6); wall.Children.Add(dash);
            }
        }
        wall.Height = slots / 3 * 259;
        g.Children.Add(Place(new ScrollViewer { Content = wall, Width = 540, Height = 500, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Padding = new(0, 4, 0, 0) }, 1030, 190));

        var low = new StackPanel { Orientation = Orientation.Horizontal };
        low.Children.Add(RebelBt("New setup", "", () => w.Click("new"), "su:new", 14, 40, false, false, w.Can("new")));
        low.Children.Add(RebelBt("Save current draft", "", () => w.Click("save"), "su:save", 14, 40, false, false, w.Can("save")));
        low.Children.Add(RebelBt("Import Setup", "", () => w.Click("import"), "su:import", 14, 40, false, false, w.Can("import")));
        if (w.HasLegacy) low.Children.Add(RebelBt("Copy legacy setups", null, () => w.Click("legacy"), "su:legacy", 14, 40));
        g.Children.Add(Place(low, 1040, 740));

        var note = new StackPanel { Orientation = Orientation.Horizontal }; note.Children.Add(new Canvas { Width = 22, Height = 22, Children = { Star.Make(11, 11, 8, 0, 1) } });
        note.Children.Add(Ink.Text(w.NoticeText, 14, Ink.B(Ink.C("#9d9598")), false).Also(x => { x.Margin = new(6, 0, 0, 0); x.RenderTransform = Ink.Slant; x.MaxWidth = 760; x.TextTrimming = TextTrimming.CharacterEllipsis; }));
        g.Children.Add(Place(note, 262, 842));

        if (w.DeletePending)
        {
            g.Children.Add(new Rectangle { Width = 1600, Height = 900, Fill = Ink.B(Ink.A(Colors.Black, .78)) });
            var paper = new Grid { Width = 560, Height = 250, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-1) }; paper.Children.Add(new TornPaper { Seed = 31 });
            var body = new StackPanel { Margin = new(36, 32, 36, 20) }; paper.Children.Add(body);
            body.Children.Add(Ink.Text(w.DeleteQuestion, 28, Ink.B(Ink.Black)).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.CharacterEllipsis; }));
            body.Children.Add(RebelBody("Only the local library entry is removed. Backups and original legacy files remain.", 15).Also(t => t.Margin = new(0, 8, 0, 0)));
            var yes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 22, 0, 0) };
            yes.Children.Add(RebelPrimary("Keep setup", () => w.Click("keep"), "su:keep"));
            yes.Children.Add(RebelBt("Remove setup", null, () => w.Click("remove"), "su:remove", 15, 44, true).Also(b => b.Margin = new(16, 0, 0, 0)));
            body.Children.Add(yes); g.Children.Add(Place(paper, 520, 300));
            focused = "su:keep";
        }
        string want = focused ?? (has ? "su:c:" + selId : "su:new");
        if (w.RenameActive && !w.DeletePending && focused == null) want = "su:name";
        Dispatcher.BeginInvoke(() => FindTagged(g, want)?.Focus(), DispatcherPriority.Input);
    }
}

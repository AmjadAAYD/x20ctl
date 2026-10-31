using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product.Agent;

public sealed partial class AgentPanel
{
    private void BuildRebelAgent()
    {
        var board = new Canvas { Name = "RebelAgent", Width = 1600, Height = 900, ClipToBounds = true, Background = Ink.B(Ink.Black) };
        var stripe = new PointCollection([new(1390, 0), new(1600, 0), new(1600, 900), new(1050, 900)]);
        board.Children.Add(new Polygon { Points = stripe, Fill = Ink.B(Ink.Red), IsHitTestVisible = false });
        board.Children.Add(new Polygon { Points = stripe, Fill = Dots.Tile(Ink.A(Ink.Black, .3), 10, 1.7), IsHitTestVisible = false });
        board.Children.Add(new Polygon { Points = new PointCollection([new(1550, 0), new(1570, 0), new(1220, 900), new(1200, 900)]), Fill = Ink.B(Ink.White), IsHitTestVisible = false });
        foreach (var (x, y, r) in new[] { (1220d, 282d, 7d), (1420d, 630d, 6d), (1515d, 270d, 9d) }) board.Children.Add(Star.Make(x, y, r));
        RebelToolFrame.At(board, Pieces.Logo(96), 36, 24);
        RebelToolFrame.At(board, Ransom.Make("Agent XCTL", 40), 146, 26);
        var settingsLabel = Ink.Text("", 15, Ink.B(Ink.Dim), false); settingsLabel.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = settingsToggle });
        var settingsButton = new Button { Content = settingsLabel, FocusVisualStyle = null, Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) } };
        System.Windows.Automation.AutomationProperties.SetName(settingsButton, "Agent settings"); settingsButton.Click += (_, _) => ShowSettingsPage(!SettingsOpen); register(settingsButton); RebelToolFrame.At(board, settingsButton, 146, 78);
        RebelToolFrame.Detach(status); status.Foreground = Ink.B(Ink.White); status.FontSize = 15; status.Margin = new(0); RebelToolFrame.At(board, new Border { Background = Ink.B(Ink.Red), Padding = new(12, 3, 12, 3), RenderTransform = new SkewTransform(-12, 0), Child = status }, 570, 50);
        var actions = new WrapPanel();
        foreach (var (caption, action) in new (string, Action)[] { ("LB  Undo", UndoLast), ("RB  Exit", LeaveAgent), ("X  New", Restart) })
        {
            var button = new SlabButton(caption, 132, 40, 906, -2, 16, Ink.White); button.Click += (_, _) => action(); register(button); actions.Children.Add(button);
        }
        RebelToolFrame.At(board, actions, 1136, 30);
        foreach (var (caption, query, index) in new[] { ("Aim & sticks", "sticks", 0), ("Triggers", "triggers", 1), ("Buttons", "buttons", 2), ("PC mode", "PC mode", 3) })
        {
            var button = new SlabButton(caption, 214, 48, 180 + index, -3, 17, Ink.White, index == 0);
            button.Click += (_, _) => { Search.Text = query; ShowView(View.Ideas); }; register(button); RebelToolFrame.At(board, button, 146 + index * 240, 130);
        }
        RebelToolFrame.Detach(scroller); RebelToolFrame.Detach(ideasView); RebelToolFrame.Detach(checkupView); RebelToolFrame.Detach(settingsBox);
        transcript.LayoutTransform = Transform.Identity; transcript.Margin = new(0, 20, 0, 14);
        var views = new Grid { Width = 1440, Height = 468, Children = { scroller, ideasView, checkupView } };
        RebelToolFrame.At(board, views, 70, 208);
        settingsBox.Width = 1320; settingsBox.Height = 562; settingsBox.CornerRadius = new(0); settingsBox.Padding = new(0); settingsBox.Background = Brushes.Transparent;
        var settingsContent = settingsBox.Child; settingsBox.Child = null; var settingsPaper = new Grid(); settingsPaper.Children.Add(new TornPaper { Seed = 901 });
        if (settingsContent is FrameworkElement content) { content.Margin = new(32, 24, 32, 24); settingsPaper.Children.Add(content); }
        settingsBox.Child = settingsPaper; RebelToolFrame.At(board, settingsBox, 140, 144); Panel.SetZIndex(settingsBox, 5);
        RebelToolFrame.Detach(Input); Input.FontFamily = Ink.Body; Input.FontSize = 19; Input.Width = 1080; Input.MinHeight = 56; Input.MaxHeight = 56; Input.Margin = new(24, 0, 0, 0); Input.Foreground = Ink.B(Ink.White); Input.CaretBrush = Ink.B(Ink.White);
        System.Windows.Automation.AutomationProperties.SetName(Input, "Tell Agent XCTL what feels off");
        var bar = new Slab { Width = 1320, Height = 68, Seed = 190, Fill = Ink.B("#0e0d0f"), Line = Ink.B(Ink.White), LineWidth = 2, RenderTransform = new RotateTransform(-.5), RenderTransformOrigin = new(.5, .5) };
        bar.Children.Add(Input);
        var hint = Ink.Text("Tell me what feels off…", 19, Ink.B(Ink.Grey), false); hint.Margin = new(30, 0, 0, 0); hint.VerticalAlignment = VerticalAlignment.Center; hint.IsHitTestVisible = false; bar.Children.Add(hint);
        Input.TextChanged += (_, _) => hint.Visibility = Input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        RebelToolFrame.Detach(sendRound);
        var sendLabel = Ink.Text("Send", 19, Ink.B(Ink.White)); sendLabel.VerticalAlignment = VerticalAlignment.Center;
        var sendBadge = new Border { Width = 27, Height = 27, CornerRadius = new(14), Background = Ink.B(Ink.White), Margin = new(0, 0, 10, 0), Child = Ink.Text("A", 14, Ink.B(Ink.Red)).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }) };
        var sendFace = new Border { Background = Ink.B(Ink.Red), Padding = new(14, 8, 14, 8), BorderBrush = Ink.B(Ink.White), BorderThickness = new(1), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { sendBadge, sendLabel } } };
        void PaintSend() { bool focused = sendRound.IsKeyboardFocused || sendRound.IsMouseOver; sendFace.Background = Ink.B(focused ? Ink.White : Ink.Red); sendLabel.Foreground = Ink.B(focused ? Ink.Black : Ink.White); }
        sendRound.GotKeyboardFocus += (_, _) => PaintSend(); sendRound.LostKeyboardFocus += (_, _) => PaintSend(); sendRound.MouseEnter += (_, _) => PaintSend(); sendRound.MouseLeave += (_, _) => PaintSend(); sendRound.Content = sendFace; sendRound.Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; sendRound.Background = Ink.B(Ink.White); sendRound.Width = 144; sendRound.Height = 48; sendRound.Margin = new(0, 0, 16, 0); sendRound.HorizontalAlignment = HorizontalAlignment.Right; sendRound.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(sendRound);
        RebelToolFrame.At(board, bar, 140, 722);
        RebelToolFrame.At(board, Parts.Hints(("A", "Send"), ("B", "Back"), ("X", "New"), ("Y", "Search"), ("▲▼", "Scroll")), 808, 836);
        Body.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        Body.Background = Ink.B(Ink.Black); Body.RenderTransformOrigin = new(.5, .5);
    }

    private void StyleRebelAgentSettings(DependencyObject item)
    {
        if (item is Button button) { RebelCommands.Dress(button); return; }
        if (item is TextBlock text) { text.Foreground = Ink.B(Ink.Black); text.FontFamily = Ink.Body; text.FontSize = Math.Max(15, text.FontSize); }
        foreach (object child in LogicalTreeHelper.GetChildren(item)) if (child is DependencyObject nested) StyleRebelAgentSettings(nested);
    }

    private Border RebelBubble(string text, bool mine)
    {
        var bubble = new RebelSpeech(mine)
        {
            Width = mine ? 440 : transcript.Children.Count == 0 ? 700 : 880, MaxWidth = mine ? 440 : 880, Padding = new(24, 18, 24, 18), Margin = mine ? new(0, 34, 210, 10) : new(90, 0, 0, 28),
            HorizontalAlignment = mine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = Ink.B(mine ? Ink.Red : Ink.White), BorderBrush = Ink.B(Ink.Black), BorderThickness = new(3),
            RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(mine ? 1.2 : -.6),
            Child = Ink.Text(text, mine ? 21 : 17, Ink.B(mine ? Ink.White : Ink.Black), mine)
        };
        if (bubble.Child is TextBlock body) body.TextWrapping = TextWrapping.Wrap;
        return bubble;
    }
    private void RebelNoChangesFooter(StackPanel stack)
    {
        if (stack.Children.OfType<Border>().FirstOrDefault() is { BorderThickness.Left: 2 } recap) { recap.BorderThickness = new(0); recap.Padding = new(0); }
        if (stack.Children.OfType<DockPanel>().LastOrDefault() is not { } footer) return;
        stack.Children.Remove(footer); RebelToolFrame.InkText(footer, Ink.Dim); footer.Margin = new(0);
        var tag = new Border { BorderBrush = Ink.B(Ink.Grey), BorderThickness = new(1), Padding = new(12, 5, 12, 5), Margin = new(106, 0, 0, 16), HorizontalAlignment = HorizontalAlignment.Left, Child = footer };
        transcript.Children.Add(tag);
    }
    private sealed class RebelSpeech(bool mine) : Border
    {
        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
            if (visualAdded != null)
            {
                RebelToolFrame.InkText(visualAdded, mine ? Ink.White : Ink.Black);
                if (visualAdded is StackPanel stack)
                    {
                        foreach (var text in Texts(stack)) text.FontSize = Math.Max(16, text.FontSize);
                        if (stack.Children.OfType<TextBlock>().FirstOrDefault() is { } title) { title.FontSize = 22; title.FontFamily = Ink.Heavy; }
                    }
            }
        }
        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight; if (w <= 0 || h <= 0) return;
            if (Background is SolidColorBrush { Color.A: 0 }) return;
            var fill = Background ?? Ink.B(mine ? Ink.Red : Ink.White); var edge = new Pen(Ink.B(Ink.Black), 3);
            dc.DrawGeometry(fill, edge, Slab.Poly(mine ? [new(w - 30, 8), new(w + 34, -6), new(w - 6, 40)] : [new(30, 8), new(-34, -6), new(6, 40)]));
            dc.DrawGeometry(fill, edge, Slab.Poly([new(0, 4), new(w, 0), new(w * .994, h), new(w * .006, h - 5)]));
            if (!mine) { dc.DrawEllipse(Ink.B(Ink.Red), new Pen(Ink.B(Ink.White), 3), new(-60, 16), 30, 30); dc.DrawGeometry(Ink.B(Ink.Black), new Pen(Ink.B(Ink.White), 1), Star.Shape(-60, 16, 17)); }
        }
        private static IEnumerable<TextBlock> Texts(DependencyObject root)
        {
            if (root is TextBlock text) yield return text;
            foreach (object child in LogicalTreeHelper.GetChildren(root)) if (child is DependencyObject nested) foreach (var leaf in Texts(nested)) yield return leaf;
        }
    }
}

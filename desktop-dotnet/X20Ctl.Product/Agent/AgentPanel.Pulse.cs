using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;

namespace X20Ctl.Product.Agent;

public sealed partial class AgentPanel
{
    private void BuildPulseAgent()
    {
        var board = new Canvas { Name = "PulseAgent", Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(PulseKit.VoidBackdrop());

        // Ambient ripples behind the voice area
        board.Children.Add(PulseKit.Ripples(118, 300, n: 6, r0: 60, step: 65, op: 0.08));

        // Top Logo & Header
        var logo = PulseParts.LogoSmall(44);
        Canvas.SetLeft(logo, 60);
        Canvas.SetTop(logo, 30);
        board.Children.Add(logo);

        var titleBlock = new StackPanel();
        titleBlock.Children.Add(PulseParts.Disp("Agent XCTL", 34, PulseParts.TextPrimary, FontWeights.Bold));

        var settingsLabel = PulseParts.Body("Settings & Voice Options ▾", 13.5, PulseParts.CyanBright);
        settingsLabel.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = settingsToggle });
        var settingsBtn = new Button
        {
            Content = settingsLabel,
            FocusVisualStyle = null,
            Template = PulseParts.BareTemplate(),
            Cursor = Cursors.Hand
        };
        settingsBtn.Click += (_, _) => ShowSettingsPage(!SettingsOpen);
        register(settingsBtn);
        titleBlock.Children.Add(settingsBtn);

        Canvas.SetLeft(titleBlock, 120);
        Canvas.SetTop(titleBlock, 26);
        board.Children.Add(titleBlock);

        // Status chip
        if (status.Parent is Panel sp) sp.Children.Remove(status);
        status.Foreground = PulseInk.B(PulseParts.TextPrimary);
        status.FontSize = 13;
        var statusChip = PulseParts.Chip("Ready · Harmonic AI", "on");
        Canvas.SetLeft(statusChip, 390);
        Canvas.SetTop(statusChip, 38);
        board.Children.Add(statusChip);

        // Header Actions: LB Undo · RB Exit · X New
        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (caption, action) in new (string, Action)[] { ("Undo", UndoLast), ("Exit", LeaveAgent), ("New Chat", Restart) })
        {
            var btn = PulseParts.PillButton(caption, primary: false, onPearl: false, small: true, onClick: action);
            register(btn);
            btn.Margin = new(8, 0, 0, 0);
            actionsRow.Children.Add(btn);
        }
        Canvas.SetLeft(actionsRow, 1200);
        Canvas.SetTop(actionsRow, 32);
        board.Children.Add(actionsRow);

        // Shortcut Idea Chips
        var ideaChips = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (caption, query, index) in new[] { ("Aim & sticks", "sticks", 0), ("Triggers", "triggers", 1), ("Buttons", "buttons", 2), ("PC mode", "PC mode", 3) })
        {
            var btn = PulseParts.ChipButton(caption, index == 0, () => { Search.Text = query; ShowView(View.Ideas); });
            register(btn);
            ideaChips.Children.Add(btn);
        }
        Canvas.SetLeft(ideaChips, 120);
        Canvas.SetTop(ideaChips, 105);
        board.Children.Add(ideaChips);

        // Views container: scroller, ideasView, checkupView
        if (scroller.Parent is Panel p1) p1.Children.Remove(scroller);
        if (ideasView.Parent is Panel p2) p2.Children.Remove(ideasView);
        if (checkupView.Parent is Panel p3) p3.Children.Remove(checkupView);
        if (settingsBox.Parent is Panel p4) p4.Children.Remove(settingsBox);

        transcript.LayoutTransform = Transform.Identity;
        transcript.Margin = new(0, 10, 0, 10);

        var views = new Grid { Width = 1440, Height = 530, Children = { scroller, ideasView, checkupView } };
        Canvas.SetLeft(views, 80);
        Canvas.SetTop(views, 160);
        board.Children.Add(views);

        // Settings Box Overlay
        settingsBox.Width = 1320;
        settingsBox.Height = 540;
        settingsBox.CornerRadius = new(24);
        settingsBox.Background = PulseInk.B(PulseInk.A(PulseInk.C("#0d1b34"), 0.95));
        settingsBox.BorderBrush = PulseInk.B(PulseParts.CyanBright);
        settingsBox.BorderThickness = new(1);
        settingsBox.Effect = PulseInk.Drop(0, 20, 40, Colors.Black, 0.7);
        Canvas.SetLeft(settingsBox, 140);
        Canvas.SetTop(settingsBox, 160);
        Panel.SetZIndex(settingsBox, 25);
        board.Children.Add(settingsBox);

        // Rising Pearl Input Bar along bottom (x=80, y=715, w=1440, h=76)
        if (Input.Parent is Panel origInputPanel) origInputPanel.Children.Remove(Input);
        Input.FontFamily = PulseParts.BodyFont;
        Input.FontSize = 18;
        Input.Width = 1180;
        Input.MinHeight = 44;
        Input.MaxHeight = 44;
        Input.Margin = new(16, 0, 16, 0);
        Input.Foreground = PulseInk.B(PulseParts.PearlInk);
        Input.CaretBrush = PulseInk.B(PulseParts.CyanColor);
        Input.Background = Brushes.Transparent;
        Input.BorderThickness = new(0);

        var inputDock = new DockPanel { LastChildFill = true };

        // Mic icon
        var micGlyph = PulseParts.Disp("✦", 22, PulseParts.CyanColor, FontWeights.Bold);
        micGlyph.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(micGlyph, Dock.Left);
        inputDock.Children.Add(micGlyph);

        // Send Button
        if (sendRound.Parent is Panel origSend) origSend.Children.Remove(sendRound);
        var sendBtn = PulseParts.PillButton("Send Message", primary: true, onPearl: true, onClick: () => sendRound.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));
        DockPanel.SetDock(sendBtn, Dock.Right);
        inputDock.Children.Add(sendBtn);

        inputDock.Children.Add(Input);

        var pearlBar = PulseParts.RisingPearlCard(inputDock, new Thickness(26, 14, 26, 14));
        pearlBar.Width = 1440;
        pearlBar.Height = 76;
        Canvas.SetLeft(pearlBar, 80);
        Canvas.SetTop(pearlBar, 715);
        board.Children.Add(pearlBar);

        // Bottom Footer hints
        var hintsHost = new StackPanel { Orientation = Orientation.Horizontal };
        PulseParts.PopulateFooterHints(hintsHost, new[] { ("Enter", "Send"), ("Esc", "Exit"), ("X", "New"), ("▲▼", "Scroll") });
        Canvas.SetLeft(hintsHost, 1080);
        Canvas.SetTop(hintsHost, 850);
        board.Children.Add(hintsHost);

        Body.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        Body.Background = new SolidColorBrush(PulseParts.VoidColor);
    }

    private void StylePulseAgentSettings(DependencyObject item)
    {
        if (item is Border b)
        {
            b.Background = PulseInk.B(PulseInk.A(PulseInk.C("#0d1b34"), 0.92));
            b.BorderBrush = PulseInk.B(PulseParts.CyanBright);
        }
        if (item is TextBlock text)
        {
            text.Foreground = PulseInk.B(PulseParts.TextPrimary);
            text.FontFamily = PulseParts.BodyFont;
        }
        foreach (object child in LogicalTreeHelper.GetChildren(item))
            if (child is DependencyObject nested) StylePulseAgentSettings(nested);
    }

    private Border PulseBubble(string text, bool mine)
    {
        if (mine)
        {
            // User bubble: floating frosted glass on the right
            var bubble = new Border
            {
                Width = 480,
                Padding = new Thickness(20, 14, 20, 14),
                Margin = new Thickness(0, 18, 120, 10),
                HorizontalAlignment = HorizontalAlignment.Right,
                CornerRadius = new CornerRadius(22, 22, 4, 22),
                Background = new LinearGradientBrush(PulseInk.A(Colors.White, 0.14), PulseInk.A(PulseParts.CyanBright, 0.10), 45),
                BorderBrush = PulseInk.B(PulseInk.A(PulseParts.CyanBright, 0.55)),
                BorderThickness = new(1),
                Effect = PulseInk.Drop(0, 10, 20, Colors.Black, 0.4),
                Child = PulseParts.Body(text, 16, PulseParts.TextPrimary, FontWeights.Medium, wrap: true)
            };
            return bubble;
        }
        else
        {
            // Agent response: voice orb on the left with clean harmonic text
            var dock = new DockPanel { LastChildFill = true };

            var voiceCanvas = new Canvas { Width = 60, Height = 60 };
            voiceCanvas.Children.Add(PulseKit.Resonance(30, 30, 22, pairs: 2, span: 26, op: 0.7));
            var core = new Ellipse
            {
                Width = 24,
                Height = 24,
                Fill = PulseInk.B(PulseParts.CyanBright),
                Effect = PulseInk.Glow(PulseParts.CyanBright, 12, 0.85)
            };
            Canvas.SetLeft(core, 18);
            Canvas.SetTop(core, 18);
            voiceCanvas.Children.Add(core);

            DockPanel.SetDock(voiceCanvas, Dock.Left);
            dock.Children.Add(voiceCanvas);

            var textStack = new StackPanel { Margin = new(16, 0, 0, 0) };
            textStack.Children.Add(PulseParts.Disp(text, 18, PulseParts.TextPrimary, FontWeights.Normal).Also(t =>
            {
                t.TextWrapping = TextWrapping.Wrap;
                t.LineHeight = 26;
            }));
            dock.Children.Add(textStack);

            var bubble = new Border
            {
                MaxWidth = 960,
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(60, 10, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = dock
            };
            return bubble;
        }
    }

    private void PulseNoChangesFooter(StackPanel stack)
    {
        var tag = PulseParts.Chip("✓ Configuration intact · No changes needed", "ok");
        tag.Margin = new(76, 10, 0, 12);
        tag.HorizontalAlignment = HorizontalAlignment.Left;
        transcript.Children.Add(tag);
    }
}

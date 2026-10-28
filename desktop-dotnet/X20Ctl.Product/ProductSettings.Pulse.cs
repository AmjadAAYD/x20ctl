using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;

namespace X20Ctl.Product;

public sealed partial class ProductSettings
{
    internal FrameworkElement? pulseRoot;
    internal readonly List<Button> pulseTiles = [];
    public Action<string>? PulseNavigate { get; set; }
    private string pulseView = "hub";

    private void BuildPulse()
    {
        if (pageGrid != null) pageGrid.Visibility = Visibility.Collapsed;
        hub.Visibility = Visibility.Collapsed;
        about.Visibility = Visibility.Collapsed;
        feedback.Visibility = Visibility.Collapsed;
        pcMode.Visibility = Visibility.Collapsed;
        themes.Visibility = Visibility.Collapsed;
        footer.Visibility = Visibility.Collapsed;

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        pulseRoot = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        Children.Add(pulseRoot);

        var changed = (Action)(() => { if (pulseRoot.IsVisible && pulseView == "hub") PulseSync(); });
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock)).AddValueChanged(updateStatus, (_, _) => changed());
        DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(UIElement)).AddValueChanged(updateGo, (_, _) => changed());
        autoUpdate.Checked += (_, _) => changed();
        autoUpdate.Unchecked += (_, _) => changed();

        PulseSync();
    }

    private void PulseSync()
    {
        if (pulseRoot is not Viewbox box || box.Child is not Canvas board) return;
        board.Children.Clear();
        pulseTiles.Clear();

        pulseView = pcMode.Visibility == Visibility.Visible ? "pcmode" :
                    feedback.Visibility == Visibility.Visible ? "feedback" :
                    about.Visibility == Visibility.Visible ? "about" :
                    themes.Visibility == Visibility.Visible ? "themes" : "hub";

        board.Children.Add(PulseKit.VoidBackdrop());

        if (pulseView != "about")
        {
            // 1. Left Orbit Navigation per t_pulse.py:304
            var orbit = PulseParts.LeftOrbitNav("Tools", target =>
            {
                if (target == "Tools") return;
                PulseNavigate?.Invoke(target);
            }, out _);
            Canvas.SetLeft(orbit, 22);
            Canvas.SetTop(orbit, 22);
            board.Children.Add(orbit);

            // 2. Page Title at (270, 30) per t_pulse.py:325
            string pageTitle = pulseView switch
            {
                "hub" => "Tools",
                "themes" => "Themes",
                "pcmode" => "PC Mode",
                _ => "Tools"
            };
            var titleBlock = PulseParts.Disp(pageTitle, 44, PulseParts.TextPrimary, FontWeights.ExtraLight);
            Canvas.SetLeft(titleBlock, 270);
            Canvas.SetTop(titleBlock, 30);
            board.Children.Add(titleBlock);

            // 3. Lead text at (272, 82)
            string pageLead = pulseView switch
            {
                "hub" => "System utilities, diagnostic probes, and application configuration",
                "themes" => "Changes how X20CTL looks. Your controllers and drafts stay as they are.",
                "pcmode" => "High-performance virtual gamepad engine for legacy and modern Windows games.",
                _ => ""
            };
            if (!string.IsNullOrEmpty(pageLead))
            {
                var leadBlock = PulseParts.Body(pageLead, 15, PulseParts.TextSoft);
                leadBlock.Width = 1000;
                Canvas.SetLeft(leadBlock, 272);
                Canvas.SetTop(leadBlock, 82);
                board.Children.Add(leadBlock);
            }

            // 4. Back / Action Button at (right: 44, top: 32)
            var backBtn = PulseParts.PillButton(pulseView == "hub" ? "Zone" : "Tools", primary: false, onPearl: false, small: true, onClick: () =>
            {
                if (pulseView == "hub") PulseNavigate?.Invoke("Zone");
                else StepBack();
            });
            Canvas.SetRight(backBtn, 44);
            Canvas.SetTop(backBtn, 32);
            board.Children.Add(backBtn);
        }

        // Subpage or Main Hub
        switch (pulseView)
        {
            case "hub":
                BuildPulseHubContent(board);
                break;
            case "about":
                BuildPulseAboutContent(board);
                break;
            case "themes":
                BuildPulseThemesContent(board);
                break;
            case "pcmode":
                BuildPulsePcModeContent(board);
                break;
            case "feedback":
                BuildPulseFeedbackContent(board);
                break;
        }

        // Footer hints per t_pulse.py:328
        var hintsHost = new StackPanel { Orientation = Orientation.Horizontal };
        PulseParts.PopulateFooterHints(hintsHost, new[] { ("A", "Select / Open"), ("B", "Back"), ("▲▼", "Navigate") });
        Canvas.SetRight(hintsHost, 44);
        Canvas.SetTop(hintsHost, 848);
        board.Children.Add(hintsHost);
    }

    private void BuildPulseHubContent(Canvas board)
    {
        // 1. Triage line at (272, 150) and (860, 186) per t_pulse.py:864
        var triageHead = new StackPanel();
        triageHead.Children.Add(PulseParts.Disp("Troubleshooting", 22, PulseParts.TextPrimary, FontWeights.SemiBold));
        triageHead.Children.Add(PulseParts.Body("Step-by-step controller diagnostics", 14.5, PulseParts.TextSoft).Also(t => t.Margin = new(0, 2, 0, 0)));
        Canvas.SetLeft(triageHead, 272);
        Canvas.SetTop(triageHead, 150);
        board.Children.Add(triageHead);

        // Triage pulse wave at (860, 186, w=690) with 3 step orbs per t_pulse.py:867
        var triageCanvas = new Canvas { Width = 690, Height = 80 };
        var waveLine = new Polyline
        {
            Stroke = PulseInk.B(PulseParts.CyanBright),
            StrokeThickness = 1.8,
            Opacity = 0.85
        };
        for (int pt = 0; pt <= 690; pt += 10)
        {
            double py = 20.0 + Math.Sin(pt * 0.04) * 8.0;
            waveLine.Points.Add(new Point(pt, py));
        }
        triageCanvas.Children.Add(waveLine);

        var steps = new[]
        {
            (80.0, "1", "Replay intro", (Action)(() => Replay.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)))),
            (345.0, "2", "PC mode check", (Action)(() => PcMode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)))),
            (610.0, "3", "Read scan", (Action)(() => Check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))))
        };

        foreach (var (ox, num, label, action) in steps)
        {
            bool on = num == "1";
            var orb = new Border
            {
                Width = on ? 40 : 32,
                Height = on ? 40 : 32,
                CornerRadius = new(on ? 20 : 16),
                Background = on ? PulseParts.PearlWhiteBrush : PulseInk.B(PulseInk.A(PulseParts.DeepColor, 0.85)),
                BorderBrush = PulseInk.B(on ? Colors.White : PulseInk.A(Colors.White, 0.22)),
                BorderThickness = new(on ? 2 : 1),
                Effect = on ? PulseInk.Glow(PulseParts.CyanBright, 16, 0.85) : null,
                Cursor = Cursors.Hand,
                Child = PulseParts.Mono(num, 14, on ? PulseParts.PearlInk : PulseParts.TextPrimary, FontWeights.Bold).Also(t =>
                {
                    t.HorizontalAlignment = HorizontalAlignment.Center;
                    t.VerticalAlignment = VerticalAlignment.Center;
                })
            };
            orb.MouseLeftButtonDown += (_, e) => { action(); e.Handled = true; };
            Canvas.SetLeft(orb, ox - (on ? 20 : 16));
            Canvas.SetTop(orb, 20 - (on ? 20 : 16));
            triageCanvas.Children.Add(orb);

            var stepText = PulseParts.Body(label, 14, on ? Colors.White : PulseParts.TextSoft, on ? FontWeights.SemiBold : FontWeights.Normal);
            stepText.Width = 140;
            stepText.TextAlignment = TextAlignment.Center;
            Canvas.SetLeft(stepText, ox - 70);
            Canvas.SetTop(stepText, 44);
            triageCanvas.Children.Add(stepText);
        }

        Canvas.SetLeft(triageCanvas, 860);
        Canvas.SetTop(triageCanvas, 186);
        board.Children.Add(triageCanvas);

        // 2. 8 Glass Tool Cards at (x = 272 + (i%4)*322, y = 290 + (i/4)*196) per t_pulse.py:873
        var tools = new (string Glyph, string Title, string Desc, Button SourceBtn)[]
        {
            ("❖", "Themes", "Changes how X20CTL looks.", Themes),
            ("⊞", "PC Mode", "Remaps, curves and virtual pad.", PcMode),
            ("◈", "Controller Check", "Read scan hardware integrity probe.", Check),
            ("⚙", "Write Scan", "Flash and probe feature configuration.", WriteScan),
            ("✉", "Send Feedback", "Report issues, suggestions or logs.", Feedback),
            ("◎", "About X20CTL", "Version, system status and credits.", About),
            ("▶", "Replay Intro", "Watch console opening animation.", Replay),
            ("★", "Support Project", "Help sustain independent tools.", Support)
        };

        for (int i = 0; i < tools.Length; i++)
        {
            var (glyph, title, desc, srcBtn) = tools[i];
            double tx = 272 + (i % 4) * 322;
            double ty = 290 + (i / 4) * 196;

            var cardStack = new StackPanel();
            var iconOrb = PulseKit.Orb(44, PulseParts.Disp(glyph, 18, PulseParts.CyanBright, FontWeights.Bold).Also(t =>
            {
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.VerticalAlignment = VerticalAlignment.Center;
            }));
            cardStack.Children.Add(iconOrb);

            cardStack.Children.Add(PulseParts.Disp(title, 17, PulseParts.TextPrimary, FontWeights.SemiBold).Also(t => t.Margin = new(0, 12, 0, 4)));
            cardStack.Children.Add(PulseParts.Body(desc, 12, PulseParts.TextSoft, wrap: true));

            var cardBorder = PulseParts.GlassCard(cardStack, new Thickness(22), radius: 24);
            cardBorder.Width = 300;
            cardBorder.Height = 172;

            var cardBtn = new Button
            {
                Content = cardBorder,
                FocusVisualStyle = null,
                Template = PulseParts.BareTemplate(),
                Cursor = Cursors.Hand
            };
            cardBtn.Click += (_, _) => srcBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pulseTiles.Add(cardBtn);

            Canvas.SetLeft(cardBtn, tx);
            Canvas.SetTop(cardBtn, ty);
            board.Children.Add(cardBtn);
        }

        // 3. Bottom Rising Pearl Card for Updates at (x=262, y=700, w=1300, h=145) per t_pulse.py:882
        var bottomStack = new DockPanel { LastChildFill = true };

        var updateIconOrb = PulseKit.Orb(48, PulseParts.Disp("⟳", 22, PulseParts.CyanBright, FontWeights.Bold).Also(t =>
        {
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
        }));
        updateIconOrb.Margin = new(0, 0, 20, 0);
        DockPanel.SetDock(updateIconOrb, Dock.Left);
        bottomStack.Children.Add(updateIconOrb);

        var updActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        updActions.Children.Add(PulseParts.PillButton("Check for updates", primary: true, onPearl: true, small: true, onClick: () => updateCheck.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
        if (updateGo.Visibility == Visibility.Visible)
            updActions.Children.Add(PulseParts.PillButton("Install update", primary: false, onPearl: true, small: true, onClick: () => updateGo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))).Also(b => b.Margin = new(8, 0, 0, 0)));
        DockPanel.SetDock(updActions, Dock.Right);
        bottomStack.Children.Add(updActions);

        var updateCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        updateCol.Children.Add(PulseParts.Disp("Keep X20CTL updated", 21, PulseParts.PearlInk, FontWeights.SemiBold));
        updateCol.Children.Add(PulseParts.Body(updateStatus.Text is { Length: > 0 } ut ? ut : "Up to date · Checked automatically at launch.", 14, PulseParts.PearlSoft));
        bottomStack.Children.Add(updateCol);

        var pearl = PulseParts.RisingPearlCard(bottomStack, new Thickness(36, 22, 36, 22));
        pearl.Width = 1300;
        pearl.Height = 145;
        Canvas.SetLeft(pearl, 262);
        Canvas.SetTop(pearl, 700);
        board.Children.Add(pearl);
    }

    private void BuildPulseAboutContent(Canvas board)
    {
        double cx = 380, cy = 330;

        // Concentric ripples & aura per t_pulse.py:893
        board.Children.Add(PulseKit.Aura(cx, cy, 380, 300, 0.22));
        board.Children.Add(PulseKit.Ripples(cx, cy, 10, 200, 64, 0.09));
        board.Children.Add(PulseParts.ResonanceMark(210).Also(m => { Canvas.SetLeft(m, cx - 210); Canvas.SetTop(m, cy - 40 - 210); }));

        var logo = PulseParts.LogoSmall(380);
        logo.Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 40, ShadowDepth = 0, Opacity = 0.45 };
        Canvas.SetLeft(logo, cx - 190);
        Canvas.SetTop(logo, cy - 190);
        board.Children.Add(logo);

        var verStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Width = 500 };
        var x20Text = PulseParts.Disp("X 2 0 C T L", 44, Colors.White, FontWeights.ExtraLight);
        x20Text.HorizontalAlignment = HorizontalAlignment.Center;
        verStack.Children.Add(x20Text);

        var verText = PulseParts.Mono($"VERSION {Updater.Shown} · PULSE HARMONIC SUITE", 14.5, PulseParts.CyanBright);
        verText.HorizontalAlignment = HorizontalAlignment.Center;
        verText.Margin = new(0, 4, 0, 0);
        verStack.Children.Add(verText);

        Canvas.SetLeft(verStack, cx - 250);
        Canvas.SetTop(verStack, cy + 110);
        board.Children.Add(verStack);

        // Right side: Title & Description per t_pulse.py:898
        var rightTitles = new StackPanel { Width = 780 };
        var aboutHead = PulseParts.Disp("About", 44, Colors.White, FontWeights.Light);
        aboutHead.Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.45 };
        rightTitles.Children.Add(aboutHead);

        rightTitles.Children.Add(PulseParts.Body("X20CTL is a high-performance calibration and remapping utility engineered for PC gamepads, featuring real-time signal synthesis, customizable curve filtering, and deep hardware telemetry.", 15.5, PulseParts.TextPrimary, wrap: true).Also(t => t.Margin = new(0, 16, 0, 10)));
        rightTitles.Children.Add(PulseParts.Body("Built natively with C# and WPF. All controller communication is handled without invasive drivers, telemetry harvesting, or background services.", 15, PulseParts.TextSoft, wrap: true));

        Canvas.SetLeft(rightTitles, 760);
        Canvas.SetTop(rightTitles, 70);
        board.Children.Add(rightTitles);

        // Rising pearl card with contributors & actions at (x=700, y=470, w=860, h=350) per t_pulse.py:902
        var peopleList = new StackPanel();
        var credits = new[]
        {
            ("ARCHITECTURE", "Lead development, system hooks, and protocol synthesis"),
            ("UI / THEMES", "Pulse harmonic interface, shaders, and kinetic controls"),
            ("OPEN SOURCE", "Free software released under the MIT License")
        };
        foreach (var (role, desc) in credits)
        {
            var line = new DockPanel { Margin = new(0, 8, 0, 8) };
            var r = PulseParts.Kicker(role, onPearl: true);
            r.Width = 140;
            DockPanel.SetDock(r, Dock.Left);
            line.Children.Add(r);
            line.Children.Add(PulseParts.Body(desc, 15, PulseParts.PearlInk));
            peopleList.Children.Add(line);
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 20, 0, 14) };
        actions.Children.Add(PulseParts.PillButton("GitHub Repository", primary: true, onPearl: true, onClick: () => Star.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
        actions.Children.Add(PulseParts.PillButton("Support Project", primary: false, onPearl: true, onClick: () => Support.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))).Also(b => b.Margin = new(12, 0, 0, 0)));
        actions.Children.Add(PulseParts.PillButton("Back to Tools", primary: false, onPearl: true, onClick: () => StepBack()).Also(b => b.Margin = new(12, 0, 0, 0)));
        peopleList.Children.Add(actions);

        peopleList.Children.Add(PulseParts.Body("Special thanks to all controller reverse-engineers and contributors.", 13, PulseParts.PearlSoft));

        var pearl = PulseParts.RisingPearlCard(peopleList, new Thickness(36, 26, 36, 26));
        pearl.Width = 860;
        pearl.Height = 350;
        Canvas.SetLeft(pearl, 700);
        Canvas.SetTop(pearl, 470);
        board.Children.Add(pearl);
    }

    private void BuildPulseThemesContent(Canvas board)
    {
        var allThemes = ThemeCatalog.All;
        string currentId = preferences?.Theme ?? "pulse";

        // Turntable center at (ox=912, oy=330, rx=520, ry=92) per t_pulse.py:514
        double ox = 912, oy = 330, rx = 520, ry = 92;
        board.Children.Add(PulseKit.Aura(ox, oy + 40, 520, 180, 0.18));

        var rail = new Ellipse
        {
            Width = rx * 2,
            Height = ry * 2,
            Stroke = PulseInk.B(PulseInk.A(PulseParts.CyanBright, 0.20)),
            StrokeThickness = 1.2
        };
        Canvas.SetLeft(rail, ox - rx);
        Canvas.SetTop(rail, oy - ry + 40);
        board.Children.Add(rail);

        // Place theme cards on the turntable
        for (int i = 0; i < allThemes.Count; i++)
        {
            var th = allThemes[i];
            bool isCurrent = string.Equals(th.Id, currentId, StringComparison.OrdinalIgnoreCase);

            double ang = 90 + i * (360.0 / allThemes.Count);
            double rad = ang * Math.PI / 180.0;
            double depth = (Math.Sin(rad) + 1.0) / 2.0;

            double cardW = isCurrent ? 280 : 130 + depth * 120;
            double cardH = cardW * 9.0 / 16.0;

            double cardX = ox + rx * Math.Cos(rad) - cardW / 2;
            double cardY = oy + ry * Math.Sin(rad) - cardH / 2 + 40;

            var cardBorder = new Border
            {
                Width = cardW,
                Height = cardH,
                CornerRadius = new(isCurrent ? 16 : 10),
                Background = isCurrent
                    ? new LinearGradientBrush(PulseInk.C("#0d1b34"), PulseInk.C("#070e1c"), 90)
                    : PulseInk.B(PulseInk.A(Colors.White, 0.08)),
                BorderBrush = PulseInk.B(isCurrent ? Colors.White : PulseInk.A(Colors.White, 0.22)),
                BorderThickness = new(isCurrent ? 2 : 1),
                Effect = isCurrent ? PulseInk.Glow(PulseParts.CyanBright, 34, 0.6) : null,
                ClipToBounds = true,
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(PulseParts.Disp(th.Name, isCurrent ? 20 : 13, isCurrent ? PulseParts.TextPrimary : PulseParts.TextSoft, FontWeights.Bold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
            if (isCurrent)
                stack.Children.Add(PulseParts.Mono("IN USE", 10.5, PulseParts.CyanBright, FontWeights.Bold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new(0, 4, 0, 0); }));
            cardBorder.Child = stack;

            string pickTheme = th.Id;
            cardBorder.MouseLeftButtonDown += (_, e) =>
            {
                try { preferences?.SetTheme(pickTheme); } catch { }
                PulseSync();
                e.Handled = true;
            };

            Canvas.SetLeft(cardBorder, cardX);
            Canvas.SetTop(cardBorder, cardY);
            Panel.SetZIndex(cardBorder, (int)(depth * 50));
            board.Children.Add(cardBorder);
        }

        // Rising pearl card along bottom at (262, 664, w=1300, h=160) per t_pulse.py:537
        var detailStack = new DockPanel { LastChildFill = true };

        var introOrb = PulseKit.Orb(52, PulseParts.Disp("▶", 20, PulseParts.CyanBright, FontWeights.Bold).Also(t =>
        {
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
        }));
        introOrb.Margin = new(0, 0, 24, 0);
        DockPanel.SetDock(introOrb, Dock.Left);
        detailStack.Children.Add(introOrb);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(PulseParts.PillButton("Apply & Restart", primary: true, onPearl: true, onClick: () =>
        {
            preferences?.SetTheme(currentId);
            RequestThemeRestart();
        }));
        actions.Children.Add(PulseParts.PillButton("Back to Tools", primary: false, onPearl: true, onClick: () => StepBack()).Also(b => b.Margin = new(12, 0, 0, 0)));
        DockPanel.SetDock(actions, Dock.Right);
        detailStack.Children.Add(actions);

        var introInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        introInfo.Children.Add(PulseParts.Disp("Theme & intro animations", 21, PulseParts.PearlInk, FontWeights.SemiBold));
        introInfo.Children.Add(PulseParts.Body("Changes how X20CTL looks and responds. Your controller calibrations and profiles remain intact.", 14, PulseParts.PearlSoft));
        detailStack.Children.Add(introInfo);

        var pearl = PulseParts.RisingPearlCard(detailStack, new Thickness(34, 26, 34, 26));
        pearl.Width = 1300;
        pearl.Height = 160;
        Canvas.SetLeft(pearl, 262);
        Canvas.SetTop(pearl, 664);
        board.Children.Add(pearl);
    }

    private void BuildPulsePcModeContent(Canvas board)
    {
        var pcBoard = PulsePcModeBuilder.BuildBoard(
            link: pcModeService != null && pcModeStore != null ? new PcModeLink(pcModeStore, pcModeService, preferences) : null,
            onEditButtons: () => PulseNavigate?.Invoke("Buttons"),
            onBack: () => StepBack(),
            onRefresh: () => PulseSync()
        );
        Canvas.SetLeft(pcBoard, 0);
        Canvas.SetTop(pcBoard, 0);
        board.Children.Add(pcBoard);
    }

    private void BuildPulseFeedbackContent(Canvas board)
    {
        var form = FeedbackForm;
        if (form.Parent is Panel p) p.Children.Remove(form);
        else if (form.Parent is ContentControl cc) cc.Content = null;
        else if (form.Parent is Decorator dec) dec.Child = null;
        Canvas.SetLeft(form, 0);
        Canvas.SetTop(form, 0);
        board.Children.Add(form);
    }
}

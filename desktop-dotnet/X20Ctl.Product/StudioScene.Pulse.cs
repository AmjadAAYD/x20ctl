using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{

    private bool IsPulse => ThemeCatalog.Current.Id == "pulse";



    private Grid? pulseLayer;
    private Canvas? pulseStage;
    private Canvas? pulsePageHost;
    private FrameworkElement? pulseOrbit;
    private FrameworkElement? pulseHeader;
    private FrameworkElement? pulseFooter;
    private TextBlock? pulseCrumb;
    private TextBlock? pulseStatusPill;
    private StackPanel? pulseFooterHints;
    private Dictionary<string, Button> pulseOrbButtons = new(StringComparer.OrdinalIgnoreCase);
    private bool pulseQueued;
    private bool pulseButtonsOverview;

    private void BuildPulseStudio()
    {
        if (!IsPulse || pulseLayer != null) return;

        RootLayout.Visibility = Visibility.Collapsed;
        StudioBackdrop.Visibility = Visibility.Collapsed;

        pulseStage = PulseParts.StageCanvas(hero: false);
        pulseHeader = PulseParts.TopHeader(
            "Studio · Home",
            PulseStatusString(),
            "Zone",
            () => BackRequested?.Invoke(),
            out pulseCrumb,
            out pulseStatusPill,
            out _,
            showBreadcrumbs: false);
        Canvas.SetLeft(pulseHeader, 124);
        Canvas.SetTop(pulseHeader, 22);
        pulseStage.Children.Add(pulseHeader);

        pulseOrbit = PulseParts.LeftOrbitNav("Dashboard", PulseSelectPage, out pulseOrbButtons);
        pulseStage.Children.Add(pulseOrbit);

        pulsePageHost = new Canvas { Width = 1600, Height = 900 };
        Canvas.SetLeft(pulsePageHost, 0);
        Canvas.SetTop(pulsePageHost, 0);
        pulseStage.Children.Add(pulsePageHost);

        pulseFooter = PulseParts.BottomFooter(
            [("A", "Select"), ("B", "Back"), ("X", "Action"), ("LB/RB", "Orbit")],
            out pulseFooterHints);
        Canvas.SetLeft(pulseFooter, 124);
        Canvas.SetTop(pulseFooter, 850);
        pulseStage.Children.Add(pulseFooter);

        pulseLayer = new Grid
        {
            Background = new SolidColorBrush(PulseParts.VoidColor),
            Children = { new Viewbox { Stretch = Stretch.Uniform, Child = pulseStage } }
        };
        Panel.SetZIndex(pulseLayer, 30);
        StudioShell.Children.Add(pulseLayer);

        PulseShow();
    }

    private string PulseStatusString()
    {
        string conn = currentTransport ?? (gameplayConnected == true ? "2.4 GHz" : "Standby");
        string? batt = currentBatteryLevel switch
        {
            "full" => "Full",
            "medium" => "Medium",
            "low" => "Low",
            "wired" => "Wired",
            _ => null
        };
        return batt == null ? $"{Model.Name} · Player {Player + 1} · {conn}" : $"{Model.Name} · Player {Player + 1} · {conn} · {batt}";
    }

    internal void PulseSelectPage(string target)
    {
        if (AgentOpen && target != "Agent") CloseAgent();
        if (stickToolsLayer != null && target != "Advanced") CloseStickTools();

        switch (target)
        {
            case "Zone":
                BackRequested?.Invoke();
                return;
            case "Tools":
                ToolsRequested?.Invoke();
                return;
            case "Agent":
                if (AgentOpen) CloseAgent();
                else OpenAgent();
                PulseLater();
                return;
            case "Dashboard":
                ShowManagement("Dashboard");
                break;
            case "Buttons":
                ShowButtons();
                break;
            case "Curves":
                ShowCurves(true);
                break;
            case "Advanced":
                if (!IsCurves) ShowCurves(true);
                OpenStickTools();
                break;
            case "Macros":
                ShowMacros(true);
                break;
            case "Vibration":
                ShowVibration(true);
                break;
            case "PcMode":
            case "RGB":
            case "Screen":
            case "Device":
            case "Diagnostics":
            case "Mouse":
            case "Setups":
            case "Tester":
                ShowManagement(target);
                break;
        }
        PulseLater();
    }

    private void PulseLater()
    {
        if (!IsPulse || pulseLayer == null || pulseQueued) return;
        pulseQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            pulseQueued = false;
            PulseShow();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void PulseShow()
    {
        if (!IsPulse || pulsePageHost == null) return;

        bool isDash = CurrentPage == "Dashboard" && !AgentOpen;
        if (pulseOrbit != null) pulseOrbit.Visibility = isDash ? Visibility.Collapsed : Visibility.Visible;
        if (pulseHeader != null) pulseHeader.Visibility = isDash ? Visibility.Collapsed : Visibility.Visible;
        if (pulseFooter != null) pulseFooter.Visibility = isDash ? Visibility.Collapsed : Visibility.Visible;

        string activePage = AgentOpen ? "Agent" : CurrentPage;
        string orbitKey = activePage == "Diagnostics" ? "Device" : activePage;

        foreach (var (key, btn) in pulseOrbButtons)
            PulseParts.StyleOrbButton(btn, string.Equals(key, orbitKey, StringComparison.OrdinalIgnoreCase));

        if (pulseStatusPill != null)
            pulseStatusPill.Text = PulseStatusString();

        if (pulseCrumb != null)
        {
            pulseCrumb.Text = activePage switch
            {
                "Dashboard" => "Studio · Home",
                "Buttons" => "Studio / Button Mapping",
                "Curves" => "Studio / Response Curves",
                "Macros" => "Studio / Macro Sequencer",
                "Vibration" => "Studio / Harmonic Vibration",
                "PcMode" => "Studio / PC Mode Routing",
                "Device" => "Studio / Controller Overview",
                "Diagnostics" => "Studio / Stick & Trigger Diagnostics",
                "Mouse" => "Studio / Controller as Mouse",
                "Setups" => "Studio / Saved Setups Library",
                "Tester" => "Studio / Live Input Tester",
                "Agent" => "Studio / Agent XCTL · Voice & Text",
                _ => "Studio"
            };
        }

        if (pulseFooterHints != null)
        {
            var hints = activePage switch
            {
                "Dashboard" => new[] { ("A", "Open"), ("B", "Zone"), ("X", "Configure"), ("Y", "Flip"), ("LB/RB", "Orbit") },
                "Buttons" => CapturingRemap ? new[] { ("Esc", "Cancel") } : new[] { ("A", "Assign"), ("B", "Clear / Back"), ("X", "Try it"), ("Y", "Overview"), ("LT/RT", "Front / Rear"), ("R3", "Listen"), ("LB/RB", "Orbit") },
                "Curves" => new[] { ("A", "Adjust"), ("B", "Back"), ("X", "Reset"), ("Y", "Preset"), ("LT/RT", "Channel") },
                "Macros" => new[] { ("A", "Edit step"), ("B", "Back"), ("X", "Add event"), ("Y", "Preview"), ("LT/RT", "Slot") },
                "Vibration" => new[] { ("A", "Preset"), ("B", "Back"), ("X", "Reset"), ("LT/RT", "Strength"), ("LB/RB", "Orbit") },
                "Diagnostics" => new[] { ("Esc", "Back to Device"), ("A", "Reset capture"), ("X", "Details") },
                "Tester" => new[] { ("Esc", "Exit Tester"), ("Hold B", "Leave capture") },
                _ => new[] { ("A", "Select"), ("B", "Back"), ("LB/RB", "Orbit") }
            };
            PulseParts.PopulateFooterHints(pulseFooterHints, hints);
        }

        pulsePageHost.Children.Clear();

        switch (CurrentPage)
        {
            case "Dashboard":
                BuildPulseDashboard(pulsePageHost);
                break;
            case "Buttons":
                BuildPulseButtons(pulsePageHost);
                break;
            case "Curves":
                BuildPulseCurves(pulsePageHost);
                break;
            case "Macros":
                BuildPulseMacros(pulsePageHost);
                break;
            case "Vibration":
                BuildPulseVibration(pulsePageHost);
                break;
            case "PcMode":
                BuildPulsePcMode(pulsePageHost);
                break;
            case "Device":
                BuildPulseDevice(pulsePageHost);
                break;
            case "Diagnostics":
                BuildPulseDiagnostics(pulsePageHost);
                break;
            case "Mouse":
                BuildPulseMouse(pulsePageHost);
                break;
            case "Setups":
                BuildPulseSetups(pulsePageHost);
                break;
            case "Tester":
                BuildPulseTester(pulsePageHost);
                break;
        }
    }

    // ========================================================================
    // 3-dashboard: Studio Home (t_pulse.py:334 / dashboard.png)
    // ========================================================================
    private void BuildPulseDashboard(Canvas host)
    {
        Dashboard ??= CreateDashboard();

        // 1. Top left logo + branding (t_pulse.py:366)
        var logo = PulseParts.LogoMark(86);
        Canvas.SetLeft(logo, 40);
        Canvas.SetTop(logo, 28);
        host.Children.Add(logo);

        var title = PulseParts.Disp("X20CTL", 24, Color.FromRgb(238, 242, 248), FontWeights.Light);
        Canvas.SetLeft(title, 140);
        Canvas.SetTop(title, 46);
        host.Children.Add(title);

        // Status at top right (right=44, top=36)
        var topStatus = new StackPanel { Orientation = Orientation.Horizontal };
        var playerDot = new Border
        {
            Width = 30, Height = 30, CornerRadius = new(15),
            Background = new LinearGradientBrush(Colors.White, Color.FromRgb(221, 229, 241), 90),
            Effect = new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.40 },
            Margin = new(0, 0, 10, 0)
        };
        topStatus.Children.Add(playerDot);
        topStatus.Children.Add(PulseParts.Body($"Player {Player + 1}", 17, Color.FromRgb(223, 229, 238)).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        topStatus.Children.Add(PulseParts.Body(ThemeClock.Now, 17, Color.FromRgb(223, 229, 238)).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(22, 0, 0, 0); }));
        topStatus.Children.Add(PulseParts.Mono("⚡", 18, Color.FromRgb(223, 229, 238)).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(18, 0, 0, 0); }));
        Canvas.SetRight(topStatus, 44);
        Canvas.SetTop(topStatus, 36);
        host.Children.Add(topStatus);

        // 2. Left side player hero info (x=80, y=180)
        var leftInfo = new StackPanel { Width = 520 };
        leftInfo.Children.Add(PulseParts.Kicker($"PLAYER {Player + 1}"));
        string modelName = Model.Name.Replace("EasySMX ", "");
        leftInfo.Children.Add(PulseParts.Disp(modelName, 58, Color.FromRgb(238, 242, 248), FontWeights.ExtraLight).Also(t => t.Margin = new(0, 10, 0, 4)));
        string finishName = PadColours.Catalog.For(Model.Id).FirstOrDefault(f => f.Id == PadColours.For(Player, Model.Id))?.Name ?? "Shadow Black";
        leftInfo.Children.Add(PulseParts.Disp(finishName, 26, PulseInk.C("#a8ccff"), FontWeights.Light));
        Canvas.SetLeft(leftInfo, 80);
        Canvas.SetTop(leftInfo, 180);
        host.Children.Add(leftInfo);

        // 4 Status Dials at (x=80, y=352 + i * 60)
        var statusList = new (string Title, string Value, double Pct)[]
        {
            ("CURRENT SETUP", "Current working settings", 1.0),
            ("CONNECTION", currentTransport ?? (gameplayConnected == true ? "2.4 GHz receiver" : "Not detected"), gameplayConnected == true ? 1.0 : 0.0),
            ("BATTERY", currentBatteryLevel switch { "full" => "Full", "medium" => "Medium", "low" => "Low", "wired" => "Wired", _ => "Not reported" }, currentBatteryLevel switch { "full" => 1.0, "medium" => 0.74, "low" => 0.22, _ => 0.0 }),
            ("LOCAL CHANGES", Draft.UnsentChanges > 0 ? $"{Draft.UnsentChanges} unsent remaps" : "No local changes", Draft.UnsentChanges > 0 ? 0.75 : 0.0)
        };

        for (int i = 0; i < statusList.Length; i++)
        {
            var st = statusList[i];
            var row = new StackPanel { Orientation = Orientation.Horizontal, Height = 48 };
            row.Children.Add(PulseParts.DialMini(st.Pct, 30, 2.6, accent: PulseParts.CyanBright));
            var textStack = new StackPanel { Margin = new(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(PulseParts.Kicker(st.Title, overrideColor: Color.FromRgb(127, 138, 160)));
            textStack.Children.Add(PulseParts.Body(st.Value, 17, Color.FromRgb(238, 242, 248)).Also(t => t.Margin = new(0, 3, 0, 0)));
            row.Children.Add(textStack);
            Canvas.SetLeft(row, 80);
            Canvas.SetTop(row, 352 + i * 60);
            host.Children.Add(row);
        }

        // 3. Right side: Controller at (cx=1010, cy=350, w=600) + 3D Orbital Ellipse
        double cx = 1010, cy = 350, w = 600;
        double ox = 1000, oy = 604, rx = 548, ry = 116;

        // Aura and ripples around controller
        host.Children.Add(PulseKit.Aura(cx, cy + 30, 560, 330, 0.24));
        host.Children.Add(PulseKit.Ripples(cx, cy + 20, n: 9, r0: 250, step: 70, op: 0.08));

        // 9 Studio Pages on Orbit
        var pages = new (string Key, string Name, string Glyph)[]
        {
            ("Buttons", "Buttons", "◉"),
            ("Curves", "Curves", "∿"),
            ("Macros", "Macros", "⟡"),
            ("Vibration", "Vibration", "≋"),
            ("PcMode", "PC mode", "⊞"),
            ("Device", "Device", "◈"),
            ("Mouse", "Mouse", "⬡"),
            ("Setups", "Setups", "▤"),
            ("Tools", "Tools", "⚙")
        };

        // Back ellipse arc (for sin(th) < -0.05, top half):
        var backArc = PulseParts.CreateEllipseArc(ox, oy, rx, ry, 180, 360, Color.FromArgb(51, 168, 204, 255), 1.2);
        host.Children.Add(backArc);

        // Render back orbs
        for (int j = 0; j < pages.Length; j++)
        {
            double th = 90 - j * 40.0;
            double rad = th * Math.PI / 180.0;
            if (Math.Sin(rad) < -0.05)
            {
                RenderDashboardOrbNode(host, pages[j], ox, oy, rx, ry, th, j == 0);
            }
        }

        // Controller pedestal in center
        var pedestal = PulseParts.ControllerPedestal(Model.Id, IsRear, width: 560, height: 350, showRipples: false, finish: PadColours.For(Player, Model.Id));
        Canvas.SetLeft(pedestal, cx - 280);
        Canvas.SetTop(pedestal, cy - 175);
        host.Children.Add(pedestal);

        // Front ellipse arc:
        var frontArc = PulseParts.CreateEllipseArc(ox, oy, rx, ry, 0, 180, Color.FromArgb(178, 207, 224, 255), 1.6);
        host.Children.Add(frontArc);

        // Render front orbs
        for (int j = 0; j < pages.Length; j++)
        {
            double th = 90 - j * 40.0;
            double rad = th * Math.PI / 180.0;
            if (Math.Sin(rad) >= -0.05)
            {
                RenderDashboardOrbNode(host, pages[j], ox, oy, rx, ry, th, j == 0);
            }
        }

        // Flipper keys: [LB] at (ox - 175, 780), [RB] at (ox + 147, 780)
        var lbKey = PulseParts.KeyBadge("LB");
        Canvas.SetLeft(lbKey, ox - 175);
        Canvas.SetTop(lbKey, 780);
        host.Children.Add(lbKey);

        var rbKey = PulseParts.KeyBadge("RB");
        Canvas.SetLeft(rbKey, ox + 147);
        Canvas.SetTop(rbKey, 780);
        host.Children.Add(rbKey);

        // Footer hints (hints at right=44, top=848)
        var dashHints = new StackPanel { Orientation = Orientation.Horizontal };
        PulseParts.PopulateFooterHints(dashHints,
        [
            ("A", "Select"),
            ("B", "Controller Zone"),
            ("X", "Configure"),
            ("Y", "Rescan games")
        ]);
        Canvas.SetRight(dashHints, 44);
        Canvas.SetTop(dashHints, 848);
        host.Children.Add(dashHints);
    }

    private void RenderDashboardOrbNode(Canvas host, (string Key, string Name, string Glyph) page, double ox, double oy, double rx, double ry, double th, bool on)
    {
        double rad = th * Math.PI / 180.0;
        double x = ox + rx * Math.Cos(rad);
        double y = oy + ry * Math.Sin(rad);
        double depth = (Math.Sin(rad) + 1.0) / 2.0;

        if (on)
        {
            // Resonance ring around Buttons (r=50)
            var resRing = new Ellipse
            {
                Width = 100,
                Height = 100,
                Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#eef2f8"), 0.40)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(resRing, x - 50);
            Canvas.SetTop(resRing, y - 50);
            host.Children.Add(resRing);

            // Active 84px glowing pearl orb
            var orbFace = new Border
            {
                Width = 84,
                Height = 84,
                CornerRadius = new(42),
                Background = new LinearGradientBrush(Colors.White, Color.FromRgb(221, 229, 241), 90),
                BorderBrush = PulseInk.B(PulseInk.A(Colors.White, 0.85)),
                BorderThickness = new(1.5),
                Effect = new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 34, ShadowDepth = 0, Opacity = 0.60 },
                Child = PulseParts.Mono(page.Glyph, 32, Color.FromRgb(10, 16, 32), FontWeights.Bold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; })
            };
            var orbBtn = new Button
            {
                Content = orbFace,
                Cursor = Cursors.Hand,
                FocusVisualStyle = null,
                Template = PulseParts.BareTemplate(),
                ToolTip = page.Name
            };
            orbBtn.Click += (_, _) => PulseSelectPage(page.Key);
            Canvas.SetLeft(orbBtn, x - 42);
            Canvas.SetTop(orbBtn, y - 42);
            host.Children.Add(orbBtn);

            var lbl = new TextBlock
            {
                Width = 200,
                TextAlignment = TextAlignment.Center,
                Text = page.Name,
                FontFamily = PulseParts.DispFont,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Effect = new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.65 }
            };
            Canvas.SetLeft(lbl, x - 100);
            Canvas.SetTop(lbl, y + 48);
            host.Children.Add(lbl);
        }
        else
        {
            double d = 34 + 30 * depth;
            var orbFace = new Border
            {
                Width = d,
                Height = d,
                CornerRadius = new(d / 2),
                Background = PulseInk.B(PulseInk.A(Colors.White, 0.08)),
                BorderBrush = PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.25)),
                BorderThickness = new(1),
                Opacity = 0.35 + 0.65 * depth,
                Child = PulseParts.Mono(page.Glyph, d * 0.38, Color.FromRgb(195, 204, 218)).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; })
            };
            var orbBtn = new Button
            {
                Content = orbFace,
                Cursor = Cursors.Hand,
                FocusVisualStyle = null,
                Template = PulseParts.BareTemplate(),
                ToolTip = page.Name
            };
            orbBtn.Click += (_, _) => PulseSelectPage(page.Key);
            Canvas.SetLeft(orbBtn, x - d / 2);
            Canvas.SetTop(orbBtn, y - d / 2);
            host.Children.Add(orbBtn);

            if (depth > 0.3)
            {
                var lbl = new TextBlock
                {
                    Width = 140,
                    TextAlignment = TextAlignment.Center,
                    Text = page.Name,
                    FontFamily = PulseParts.BodyFont,
                    FontSize = 13,
                    FontWeight = FontWeights.Normal,
                    Foreground = PulseInk.B(Color.FromRgb(138, 148, 166)),
                    Opacity = 0.40 + 0.60 * depth
                };
                Canvas.SetLeft(lbl, x - 70);
                Canvas.SetTop(lbl, y + d / 2 + 8);
                host.Children.Add(lbl);
            }
        }
    }

    // ========================================================================
    // 4-buttons: Button Mapping (t_pulse.py:445 / buttons.png)
    // ========================================================================
    private void BuildPulseButtons(Canvas host)
    {
        string sel = string.IsNullOrEmpty(SelectedControl) ? "A" : SelectedControl;
        bool editable = Draft.CanEdit(sel);
        string mappedTarget = editable ? Draft.Target(sel) : sel;

        // 1. Left side:
        // Title at (272, 30): Buttons
        var title = PulseParts.Disp("Buttons", 44, Color.FromRgb(238, 242, 248), FontWeights.ExtraLight);
        Canvas.SetLeft(title, 272);
        Canvas.SetTop(title, 30);
        host.Children.Add(title);

        // Tag at (272, 98): [Local draft] Not written to controller memory.
        var tagRow = new StackPanel { Orientation = Orientation.Horizontal };
        tagRow.Children.Add(PulseParts.Chip("LOCAL DRAFT", "on").Also(c => c.Margin = new(0, 0, 14, 0)));
        tagRow.Children.Add(PulseParts.Body("Not written to controller memory.", 16, Color.FromRgb(170, 180, 196)).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        Canvas.SetLeft(tagRow, 272);
        Canvas.SetTop(tagRow, 98);
        host.Children.Add(tagRow);

        // Group Tabs at (272, 152): Face, Shoulders, Sticks, System
        var groups = new[] { "Face", "Shoulders", "Sticks", "System" };
        var tabStack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var grp in groups)
        {
            bool isGrpActive = (grp == "Face" && sel is "A" or "B" or "X" or "Y")
                            || (grp == "Shoulders" && sel is "LB" or "RB" or "LT" or "RT")
                            || (grp == "Sticks" && sel is "L3" or "R3")
                            || (grp == "System" && sel is "SELECT" or "START" or "DPAD_UP" or "DPAD_DOWN" or "DPAD_LEFT" or "DPAD_RIGHT");
            var tabBtn = PulseParts.TabButton(grp, isGrpActive, () =>
            {
                string firstKey = grp switch
                {
                    "Shoulders" => "LB",
                    "Sticks" => "L3",
                    "System" => "START",
                    _ => "A"
                };
                SelectControl(firstKey);
                PulseLater();
            });
            tabStack.Children.Add(tabBtn);
        }
        Canvas.SetLeft(tabStack, 272);
        Canvas.SetTop(tabStack, 152);
        host.Children.Add(tabStack);

        // Assignment Rows at (262, 210, w=560)
        var (grpKeys, _) = (sel is "LB" or "RB" or "LT" or "RT")
            ? (new[] { "LB", "RB", "LT", "RT" }, new[] { "LB", "RB", "LT", "RT" })
            : (sel is "L3" or "R3")
                ? (new[] { "L3", "R3" }, new[] { "L3", "R3" })
                : (sel is "SELECT" or "START" or "DPAD_UP" or "DPAD_DOWN" or "DPAD_LEFT" or "DPAD_RIGHT")
                    ? (new[] { "SELECT", "START", "DPAD_UP", "DPAD_DOWN" }, new[] { "Select", "Start", "D-Pad Up", "D-Pad Down" })
                    : (new[] { "A", "B", "X", "Y" }, new[] { "A", "B", "X", "Y" });

        var rowsContainer = new StackPanel { Width = 560 };
        for (int i = 0; i < grpKeys.Length; i++)
        {
            string k = grpKeys[i];
            bool on = sel == k;
            string target = Draft.CanEdit(k) ? Draft.Target(k) : k;
            bool modified = Draft.CanEdit(k) && target != k;

            var rowFace = new Border
            {
                Height = 64,
                CornerRadius = new(27),
                Margin = new(0, 0, 0, 8),
                Padding = new(22, 0, 22, 0),
                Background = on
                    ? new LinearGradientBrush(Color.FromArgb(38, 255, 255, 255), Color.FromArgb(13, 255, 255, 255), 90)
                    : PulseInk.B(PulseInk.A(Colors.White, 0.05)),
                BorderBrush = on
                    ? PulseInk.B(Color.FromArgb(217, 238, 242, 248))
                    : PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.12)),
                BorderThickness = new(1),
                Effect = on ? new DropShadowEffect { Color = PulseInk.C("#78aaff"), BlurRadius = 28, ShadowDepth = 0, Opacity = 0.35 } : null
            };

            var rowDock = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
            if (modified)
            {
                var modTag = PulseParts.Body("Modified", 15, Color.FromRgb(238, 242, 248), FontWeights.Light);
                DockPanel.SetDock(modTag, Dock.Right);
                rowDock.Children.Add(modTag);
            }

            var leftElements = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (on)
            {
                leftElements.Children.Add(PulseParts.ResonanceMark());
            }
            leftElements.Children.Add(PulseParts.KeyBadge(k, on).Also(b => b.Margin = new(on ? 12 : 0, 0, 16, 0)));
            leftElements.Children.Add(PulseParts.Body("sends", 15, Color.FromRgb(138, 148, 166), FontWeights.Light).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(0, 0, 16, 0); }));
            leftElements.Children.Add(PulseParts.KeyBadge(target));
            rowDock.Children.Add(leftElements);
            rowFace.Child = rowDock;

            string keyCopy = k;
            var rowBtn = new Button
            {
                Content = rowFace,
                Cursor = Cursors.Hand,
                FocusVisualStyle = null,
                Template = PulseParts.BareTemplate()
            };
            rowBtn.Click += (_, _) => { SelectControl(keyCopy); PulseLater(); };
            rowsContainer.Children.Add(rowBtn);
        }
        Canvas.SetLeft(rowsContainer, 262);
        Canvas.SetTop(rowsContainer, 210);
        host.Children.Add(rowsContainer);

        // 2. Right side:
        // Controller stage at (px=900, py=150, w=560)
        double px = 900, py = 150, pw = 560;
        double cx = px + pw / 2; // 1180
        double cy = py + 160;   // 310

        host.Children.Add(PulseKit.Aura(cx, cy, 420, 260, 0.20));
        host.Children.Add(PulseKit.Ripples(cx, cy, n: 8, r0: 200, step: 60, op: 0.08));

        // Pedestal controller
        var pedestal = PulseParts.ControllerPedestal(Model.Id, IsRear, width: pw, height: 360, showRipples: false, finish: PadColours.For(Player, Model.Id));
        Canvas.SetLeft(pedestal, px);
        Canvas.SetTop(pedestal, py - 10);
        host.Children.Add(pedestal);

        // Active button coordinates (ax, ay)
        double ax = 1322, ay = 295;
        if (sel == "B") { ax = 1352; ay = 265; }
        else if (sel == "X") { ax = 1292; ay = 265; }
        else if (sel == "Y") { ax = 1322; ay = 235; }
        else if (sel == "LB") { ax = 1060; ay = 175; }
        else if (sel == "RB") { ax = 1300; ay = 175; }
        else if (sel == "LT") { ax = 1040; ay = 145; }
        else if (sel == "RT") { ax = 1320; ay = 145; }
        else if (sel == "L3") { ax = 1070; ay = 270; }
        else if (sel == "R3") { ax = 1220; ay = 340; }

        // Real control centre: the pad is a 3:2 box inside the pedestal's 90% x 88% viewbox.
        if (!IsRear && TryControlCenter(sel, out var padFx, out var padFy))
        {
            double padW = Math.Min(pw * 0.90, 360 * 0.88 * 1.5), padH = padW / 1.5;
            ax = px + (pw - padW) / 2 + padFx * padW;
            ay = py - 10 + (360 - padH) / 2 - 4 + padFy * padH;
        }

        // Orbital ring of candidate outputs around (ax, ay): radius = 150
        double orbRadius = 150;
        var ringCircle = new Ellipse
        {
            Width = orbRadius * 2,
            Height = orbRadius * 2,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), 0.30)),
            StrokeThickness = 1,
            Fill = PulseInk.B(PulseInk.A(PulseInk.C("#050810"), 0.35))
        };
        Canvas.SetLeft(ringCircle, ax - orbRadius);
        Canvas.SetTop(ringCircle, ay - orbRadius);
        host.Children.Add(ringCircle);

        // Resonance ring around active button (r=30)
        var centerRes = new Ellipse
        {
            Width = 60,
            Height = 60,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), 0.65)),
            StrokeThickness = 1.2
        };
        Canvas.SetLeft(centerRes, ax - 30);
        Canvas.SetTop(centerRes, ay - 30);
        host.Children.Add(centerRes);

        // 10 Candidate output orbs: A, B, X, Y, LB, RB, LT, RT, L3, R3
        string[] candidates = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3"];
        for (int i = 0; i < candidates.Length; i++)
        {
            string cand = candidates[i];
            bool on = mappedTarget == cand;
            double ang = -90 + i * 36.0;
            double rad = ang * Math.PI / 180.0;
            double ox_ = ax + orbRadius * Math.Cos(rad);
            double oy_ = ay + orbRadius * Math.Sin(rad);
            double d = on ? 44 : 36;

            var candFace = new Border
            {
                Width = d,
                Height = d,
                CornerRadius = new(d / 2),
                Background = on
                    ? new LinearGradientBrush(Colors.White, Color.FromRgb(221, 229, 241), 90)
                    : PulseInk.B(Color.FromArgb(184, 8, 12, 24)),
                BorderBrush = on
                    ? PulseInk.B(PulseInk.A(Colors.White, 0.70))
                    : PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.30)),
                BorderThickness = new(1),
                Effect = on ? new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 24, ShadowDepth = 0, Opacity = 0.60 } : null,
                Child = PulseParts.Mono(cand, on ? 15 : (cand.Length > 1 ? 12 : 14), on ? Color.FromRgb(10, 16, 32) : Color.FromRgb(223, 229, 238), on ? FontWeights.Bold : FontWeights.Normal).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; })
            };

            string pickCand = cand;
            var candBtn = new Button
            {
                Content = candFace,
                Cursor = Cursors.Hand,
                FocusVisualStyle = null,
                Template = PulseParts.BareTemplate(),
                ToolTip = $"Assign {cand}"
            };
            candBtn.Click += (_, _) => { SetDraftTarget(pickCand); PulseLater(); };
            Canvas.SetLeft(candBtn, ox_ - d / 2);
            Canvas.SetTop(candBtn, oy_ - d / 2);
            host.Children.Add(candBtn);
        }

        // 3. Bottom rising pearl card at (x=262, y=640, w=1300, h=175)
        var cardGrid = new DockPanel { LastChildFill = true };

        // Left: [A] -> sine wave -> [B]
        var waveLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 40, 0) };
        var srcBadge = PulseParts.PearlKeyBadge(sel, 62, 26);
        waveLeft.Children.Add(srcBadge);

        var arrowCanvas = PulseParts.WaveArrow(110, 30);
        waveLeft.Children.Add(arrowCanvas);

        var tgtBadge = PulseParts.PearlKeyBadge(mappedTarget, 62, 26);
        waveLeft.Children.Add(tgtBadge);
        cardGrid.Children.Add(waveLeft);

        // Right action buttons: [Reset this control] [Test mapping]
        var rightBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new(24, 0, 0, 0) };
        DockPanel.SetDock(rightBtns, Dock.Right);
        rightBtns.Children.Add(PulseParts.PillButton("Reset this control", primary: false, onPearl: true, onClick: () =>
        {
            if (editable) { Draft.Reset(sel); RefreshEditor(); PulseLater(); }
        }).Also(b => b.Margin = new(0, 0, 12, 0)));
        rightBtns.Children.Add(PulseParts.PillButton(IsTrying ? "Stop keyboard test" : "Test mapping", primary: true, onPearl: true, onClick: () =>
        {
            if (IsTrying) StopTry(); else StartTry(); PulseLater();
        }));
        cardGrid.Children.Add(rightBtns);

        // Center info: Title + Subtitle
        var centerInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        centerInfo.Children.Add(PulseParts.Disp($"{sel} sends {mappedTarget}", 30, PulseParts.PearlInk, FontWeights.Light));
        string stateSub = editable && Draft.Target(sel) != sel
            ? "Modified · Not written to controller memory."
            : "Default · Not written to controller memory.";
        centerInfo.Children.Add(PulseParts.Body(stateSub, 16, PulseParts.PearlMid).Also(t => t.Margin = new(0, 4, 0, 0)));
        cardGrid.Children.Add(centerInfo);

        var pearlCard = PulseParts.RisingPearlCard(cardGrid, new Thickness(40, 24, 44, 24));
        pearlCard.Width = 1300;
        pearlCard.Height = 175;
        Canvas.SetLeft(pearlCard, 262);
        Canvas.SetTop(pearlCard, 640);
        host.Children.Add(pearlCard);
    }

}

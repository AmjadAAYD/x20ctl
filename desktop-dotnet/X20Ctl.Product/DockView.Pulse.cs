using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;
namespace X20Ctl.Product;

/// <summary>The overhauled Pulse faces for each player dock:
/// 1) Overview pod on the upper orbital arc with gyroscopic bezels, radial spectrum bars, and channel telemetry.
/// 2) Selected-Controller Hero stage with extruded chrome typography, inline ECG divider, tethered hardware HUD callouts,
///    glowing Open Studio reactor pill, and orbital action modules.</summary>
public partial class DockView
{
    private bool pulse, pulseLit;
    private Canvas? pulseFace, pulseHeroLayer;
    private Button? pulsePrimaryBtn, pulseCrumbBtn;
    private TextBlock? pulsePrimaryLabel;
    private Button[] pulseActionOrbs = [];
    private string pulseFaceKey = "";

    private const double PodCenterX = 140, PodCenterY = 180;

    private void InitPulse()
    {
        if (ThemeCatalog.Current.Id != "pulse") return;
        pulse = true;
        ((Grid)Content).ClipToBounds = false;
        Card.Visibility = Visibility.Collapsed;
        FocusButton.Cursor = System.Windows.Input.Cursors.Hand;

        pulseFace = new Canvas { IsHitTestVisible = false };
        ContentCanvas.Children.Insert(0, pulseFace);

        pulseHeroLayer = new Canvas { Visibility = Visibility.Collapsed };
        ContentCanvas.Children.Add(pulseHeroLayer);

        // Top-left breadcrumb capsule on Hero: "← Controller Zone / Player 1"
        pulseCrumbBtn = new Button
        {
            Template = PulseKit.PlainButtonTemplate(),
            FocusVisualStyle = null,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        System.Windows.Automation.AutomationProperties.SetName(pulseCrumbBtn, "Back to Controller Zone");
        pulseCrumbBtn.Click += (_, _) => BackRequested?.Invoke();
        PulseKit.At(pulseHeroLayer, pulseCrumbBtn, 56, 28);

        // Primary CTA pill on Hero: "A  Open Studio" at (64, 584) inside a glowing reactor housing
        var priRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var aCap = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new(15),
            Background = PulseInk.B(PulseInk.C("#060c18")),
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.65)),
            BorderThickness = new(1.2),
            Child = new TextBlock
            {
                Text = "A",
                FontFamily = PulseInk.Body,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        priRow.Children.Add(aCap);

        pulsePrimaryLabel = PulseInk.Disp("Open Studio", 18.5, PulseInk.B(PulseInk.C("#060c18")), FontWeights.SemiBold);
        pulsePrimaryLabel.Margin = new(12, 0, 0, 0);
        pulsePrimaryLabel.VerticalAlignment = VerticalAlignment.Center;
        priRow.Children.Add(pulsePrimaryLabel);

        var priInner = new Border
        {
            Height = 58,
            CornerRadius = new(29),
            Padding = new(28, 0, 32, 0),
            Background = PulseInk.PrimaryBtn,
            BorderBrush = Brushes.White,
            BorderThickness = new(1.2),
            Effect = PulseInk.Glow(PulseInk.CyanHot, 32, 0.65),
            Child = priRow
        };
        var priOuter = new Border
        {
            Padding = new(4),
            CornerRadius = new(34),
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.45)),
            BorderThickness = new(1.2),
            Background = PulseInk.B(PulseInk.A(PulseInk.Cyan, 0.08)),
            Child = priInner
        };

        pulsePrimaryBtn = new Button
        {
            Content = priOuter,
            Template = PulseKit.PlainButtonTemplate(),
            FocusVisualStyle = null,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        pulsePrimaryBtn.Click += (_, _) => ActionRequested?.Invoke(Index, false);
        PulseKit.At(pulseHeroLayer, pulsePrimaryBtn, 60, 580);

        // Dashed orbital energy rail connecting the 4 secondary action orbs along C0 = (520, 2260), R0 = 1650
        var railGeo = new StreamGeometry();
        using (var ctx = railGeo.Open())
        {
            for (int x = 350; x <= 800; x += 5)
            {
                double y = 2260 - Math.Sqrt(1650 * 1650 - (x - 520.0) * (x - 520.0)) - 10;
                if (x == 350) ctx.BeginFigure(new Point(x, y), false, false);
                else ctx.LineTo(new Point(x, y), true, true);
            }
        }
        railGeo.Freeze();
        pulseHeroLayer.Children.Add(new Path
        {
            Data = railGeo,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.35)),
            StrokeThickness = 1.4,
            StrokeDashArray = new DoubleCollection([5, 5]),
            IsHitTestVisible = false
        });

        // Four secondary action orbs on gentle arc: C0 = (520, 2260), R0 = 1650
        (string label, string sub, string glyph, Action act, bool isRemove)[] specs =
        [
            ("Controller settings", "DEVICE", "\uE713", () => PageRequested?.Invoke("Device"), false),
            ("Test controller", "DIAGNOSTICS", "\uE9D9", () => PageRequested?.Invoke("Tester"), false),
            ("Manage setups", "PROFILES", "\uE8F1", () => PageRequested?.Invoke("Setups"), false),
            ("Remove from slot", "EJECT", "\uE74D", () => RemoveRequested?.Invoke(Index), true)
        ];
        pulseActionOrbs = new Button[specs.Length];
        for (int i = 0; i < specs.Length; i++)
        {
            var (label, sub, glyph, act, isRemove) = specs[i];
            double x = 384 + i * 134;
            double y = 2260 - Math.Sqrt(1650 * 1650 - (x - 520) * (x - 520)) - 10;

            var itemCanvas = new Canvas { Width = 134, Height = 116 };

            // Outer dashed technical ring around each action orb
            var outerRing = new Ellipse
            {
                Width = 74,
                Height = 74,
                Stroke = PulseInk.B(PulseInk.A(isRemove ? PulseInk.Rose : PulseInk.CyanHot, 0.34)),
                StrokeThickness = 1.0,
                StrokeDashArray = new DoubleCollection([3, 4])
            };
            PulseKit.At(itemCanvas, outerRing, 67 - 37, -5);

            var iconEl = PulseInk.Icon(glyph, 22, PulseInk.B(isRemove ? PulseInk.Rose : PulseInk.Pearl));
            var orbBorder = PulseKit.Orb(64, iconEl, on: false, accentBorder: isRemove ? PulseInk.Rose : PulseInk.CyanHot);
            PulseKit.At(itemCanvas, orbBorder, 67 - 32, 0);

            var lbl = PulseInk.Text(label, 13.5, PulseInk.B(isRemove ? PulseInk.RoseText : PulseInk.Pearl), FontWeights.Medium);
            lbl.Width = 134;
            lbl.TextAlignment = TextAlignment.Center;
            PulseKit.At(itemCanvas, lbl, 0, 72);

            var subLbl = PulseInk.MonoText(sub, 9.5, PulseInk.B(isRemove ? PulseInk.A(PulseInk.RoseText, 0.7) : PulseInk.Sec));
            subLbl.Width = 134;
            subLbl.TextAlignment = TextAlignment.Center;
            PulseKit.At(itemCanvas, subLbl, 0, 91);

            var btn = new Button
            {
                Content = itemCanvas,
                Width = 134,
                Height = 116,
                Template = PulseKit.PlainButtonTemplate(),
                FocusVisualStyle = null,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            System.Windows.Automation.AutomationProperties.SetName(btn, label);
            btn.Click += (_, _) => act();
            btn.GotKeyboardFocus += (_, _) => orbBorder.BorderBrush = PulseInk.B(PulseInk.CyanHot);
            btn.LostKeyboardFocus += (_, _) => orbBorder.BorderBrush = PulseInk.B(PulseInk.A(isRemove ? PulseInk.Rose : PulseInk.CyanHot, 0.42));
            PulseKit.At(pulseHeroLayer, btn, x - 67, y - 32);
            pulseActionOrbs[i] = btn;
        }
    }

    internal void SetPulseState(bool lit, bool hero)
    {
        if (!pulse) return;
        bool changed = pulseLit != lit;
        pulseLit = lit;
        if (changed) pulseFaceKey = "";
        if (!double.IsNaN(Width) && !double.IsNaN(Height)) PresentPulse(hero);
    }

    private void PresentPulse(bool hero)
    {
        if (pulseFace == null || pulseHeroLayer == null) return;
        bool empty = Player.Model == null;
        double w = Width, h = Height;

        foreach (UIElement e in new UIElement[] { PlayerPill, Details, Halo, ArtShadow, AddControllerButton, RemoveControllerButton, ActionCanvas, ArrowBadge, PlusBadge, Silhouette })
            e.Visibility = Visibility.Collapsed;

        FocusButton.IsHitTestVisible = !hero;
        pulseHeroLayer.Visibility = hero ? Visibility.Visible : Visibility.Collapsed;

        if (hero)
        {
            const double heroArtW = 861;
            GlideArt(636.3, 104.5, heroArtW);
            Art.Effect = !empty ? PulseInk.Drop(0, 38, 48, Colors.Black, 0.68) : null;
        }
        else
        {
            double d = pulseLit ? 250 : 206;
            double podArtW = d * 0.894;
            GlideArt(PodCenterX - podArtW / 2, PodCenterY + 6 - (podArtW / 1.5) / 2, podArtW);
            Art.Effect = !empty ? PulseInk.Drop(0, 26, 32, Colors.Black, 0.62) : null;
        }

        string key = $"{hero}|{empty}|{pulseLit}|{Player.ModelId}|{Morph.Finish}|{(int)w}x{(int)h}|{Player.Number}|{Connection.Text}";
        if (key == pulseFaceKey) return;
        pulseFaceKey = key;
        pulseFace.Children.Clear();

        if (hero) PulseHeroFace(empty);
        else PulsePodFace(empty);
    }

    private void PulsePodFace(bool empty)
    {
        double d = pulseLit ? 250 : 206;
        double r = d / 2;

        // Top channel telemetry pill above the pod
        var topBadgeRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var led = new Ellipse
        {
            Width = 6,
            Height = 6,
            Margin = new(0, 0, 7, 0),
            Fill = PulseInk.B(empty ? PulseInk.Mute : (pulseLit ? PulseInk.CyanHot : PulseInk.Warm)),
            Effect = !empty ? PulseInk.Glow(pulseLit ? PulseInk.CyanHot : PulseInk.Warm, 8, 0.9) : null,
            VerticalAlignment = VerticalAlignment.Center
        };
        topBadgeRow.Children.Add(led);
        topBadgeRow.Children.Add(PulseInk.MonoText($"CH 0{Player.Number} // PLAYER {Player.Number}", 11,
            PulseInk.B(pulseLit ? Colors.White : (empty ? PulseInk.Sec : PulseInk.Ice))));

        var topContainer = new Border
        {
            Width = 240,
            Child = topBadgeRow
        };
        PulseKit.At(pulseFace!, topContainer, PodCenterX - 120, PodCenterY - r - (pulseLit ? 52 : 40));

        if (empty)
        {
            // Standby 36-tick gyroscopic bezel
            pulseFace!.Children.Add(PulseKit.GyroRings(PodCenterX, PodCenterY, r + 6, active: false));

            var dashed = new Ellipse
            {
                Width = d,
                Height = d,
                Fill = PulseInk.B(PulseInk.A(PulseInk.C("#081224"), 0.55)),
                Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#88b8ff"), 0.30)),
                StrokeThickness = 1.4,
                StrokeDashArray = new DoubleCollection([4, 6]),
                StrokeDashCap = PenLineCap.Round
            };
            PulseKit.At(pulseFace!, dashed, PodCenterX - r, PodCenterY - r);

            // Dark X20 silhouette with cyan-ice top rim highlight
            var sil = MakeDarkSilhouette(d * 0.872);
            PulseKit.At(pulseFace!, sil, PodCenterX - (d * 0.872) / 2, PodCenterY - (d * 0.872 / 1.5) / 2);

            // Flatline ECG indicator across empty slot
            var flatline = new Line
            {
                X1 = PodCenterX - 48,
                Y1 = PodCenterY + 4,
                X2 = PodCenterX + 48,
                Y2 = PodCenterY + 4,
                Stroke = PulseInk.B(PulseInk.A(PulseInk.Cyan, 0.32)),
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection([4, 4])
            };
            pulseFace!.Children.Add(flatline);

            var emptyLbl = PulseInk.Disp("Empty", 21, PulseInk.B(PulseInk.Soft), FontWeights.Light);
            emptyLbl.Width = 240;
            emptyLbl.TextAlignment = TextAlignment.Center;
            PulseKit.At(pulseFace!, emptyLbl, PodCenterX - 120, PodCenterY + r + (pulseLit ? 32 : 18));

            var stdbyTag = PulseInk.MonoText("NO SIGNAL · STDBY", 10, PulseInk.B(PulseInk.Sub));
            stdbyTag.Width = 240;
            stdbyTag.TextAlignment = TextAlignment.Center;
            PulseKit.At(pulseFace!, stdbyTag, PodCenterX - 120, PodCenterY + r + (pulseLit ? 58 : 44));
            return;
        }

        // Unfocused assigned pods get their own 24-bar radial micro-spectrum and 36-tick bezel
        if (!pulseLit)
        {
            pulseFace!.Children.Add(PulseKit.RadialSpectrum(PodCenterX, PodCenterY, r + 4, 28, 4, 14, Player.Number * 0.9));
            pulseFace!.Children.Add(PulseKit.GyroRings(PodCenterX, PodCenterY, r + 10, active: false));
        }

        var podOrb = PulseKit.Orb(d, null, on: pulseLit, customBg: pulseLit ? PulseInk.PodOrbFocused : PulseInk.PodOrb);
        PulseKit.At(pulseFace!, podOrb, PodCenterX - r, PodCenterY - r);

        // Inner specular ring inside the pod
        var innerRing = new Ellipse
        {
            Width = d - 18,
            Height = d - 18,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.CyanHot, pulseLit ? 0.35 : 0.18)),
            StrokeThickness = 1.0,
            StrokeDashArray = new DoubleCollection([8, 6])
        };
        PulseKit.At(pulseFace!, innerRing, PodCenterX - (d - 18) / 2, PodCenterY - (d - 18) / 2);

        var bottomStack = new StackPanel { Width = 280, IsHitTestVisible = false };
        var shortName = PulseInk.Disp(ShortName, pulseLit ? 27 : 22, Brushes.White, FontWeights.Normal);
        shortName.HorizontalAlignment = HorizontalAlignment.Center;
        bottomStack.Children.Add(shortName);

        string finText = FinishName.Length > 0 ? $"{FinishName.ToUpperInvariant()} · SYNC" : "ACTIVE · SYNC";
        var finPill = new Border
        {
            CornerRadius = new(8),
            Padding = new(10, 2, 10, 2),
            Margin = new(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = PulseInk.B(PulseInk.A(PulseInk.Cyan, pulseLit ? 0.20 : 0.12)),
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.CyanHot, pulseLit ? 0.50 : 0.28)),
            BorderThickness = new(1),
            Child = PulseInk.MonoText(finText, 10.5, PulseInk.B(pulseLit ? PulseInk.CyanHot : PulseInk.Ice))
        };
        bottomStack.Children.Add(finPill);

        PulseKit.At(pulseFace!, bottomStack, PodCenterX - 140, PodCenterY + r + (pulseLit ? 22 : 16));
    }

    private void PulseHeroFace(bool empty)
    {
        // 1. Sleek glass breadcrumb capsule at top-left
        var crumbRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var backIcon = PulseInk.Icon("\uE72B", 13, PulseInk.B(PulseInk.CyanHot));
        backIcon.Margin = new(0, 1, 10, 0);
        crumbRow.Children.Add(backIcon);
        crumbRow.Children.Add(PulseInk.Text("Controller Zone", 15.5, PulseInk.B(PulseInk.Plat), FontWeights.Medium));
        var slash = PulseInk.Text("/", 15.5, PulseInk.B(PulseInk.A(PulseInk.Plat, 0.45)));
        slash.Margin = new(10, 0, 10, 0);
        crumbRow.Children.Add(slash);
        crumbRow.Children.Add(PulseInk.Text($"Player {Player.Number}", 15.5, Brushes.White, FontWeights.SemiBold));

        var crumbBorder = new Border
        {
            CornerRadius = new(18),
            Padding = new(16, 7, 18, 7),
            Background = PulseInk.B(PulseInk.A(PulseInk.C("#0b162c"), 0.78)),
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.35)),
            BorderThickness = new(1),
            Child = crumbRow
        };
        pulseCrumbBtn!.Content = crumbBorder;

        // 2. Left Monumental Identity Block + Inline ECG Heartbeat Divider at (58, 102)
        var block = new StackPanel { IsHitTestVisible = false };

        var chPill = new Border
        {
            CornerRadius = new(8),
            Padding = new(10, 3, 12, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(6, 0, 0, 0),
            Background = PulseInk.B(PulseInk.A(PulseInk.Cyan, 0.16)),
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.50)),
            BorderThickness = new(1),
            Child = PulseInk.MonoText($"CH 0{Player.Number} // PLAYER {Player.Number} · HARMONIC RESONANCE LOCK", 11, PulseInk.B(PulseInk.CyanHot))
        };
        block.Children.Add(chPill);

        var litHero = new TextBlock
        {
            Text = empty ? "Empty" : ShortName,
            FontFamily = PulseInk.Display,
            FontSize = 184,
            FontWeight = FontWeights.ExtraLight,
            Foreground = PulseInk.LitTitle,
            Effect = PulseInk.Drop(0, 10, 28, PulseInk.C("#2878ff"), 0.50),
            Margin = new(-6, -28, 0, -24)
        };
        block.Children.Add(litHero);

        // Inline glowing ECG heartbeat divider bar right under X20
        var ecgDivider = new Canvas { Width = 440, Height = 28, Margin = new(6, 2, 0, 4), HorizontalAlignment = HorizontalAlignment.Left };
        var ecgGeo = new StreamGeometry();
        using (var ctx = ecgGeo.Open())
        {
            ctx.BeginFigure(new Point(0, 14), false, false);
            ctx.LineTo(new Point(70, 14), true, true);
            ctx.LineTo(new Point(82, 9), true, true);
            ctx.LineTo(new Point(92, 18), true, true);
            ctx.LineTo(new Point(104, 1), true, true);
            ctx.LineTo(new Point(116, 26), true, true);
            ctx.LineTo(new Point(128, 8), true, true);
            ctx.LineTo(new Point(142, 14), true, true);
            ctx.LineTo(new Point(260, 14), true, true);
            ctx.LineTo(new Point(272, 10), true, true);
            ctx.LineTo(new Point(282, 3), true, true);
            ctx.LineTo(new Point(292, 23), true, true);
            ctx.LineTo(new Point(304, 14), true, true);
            ctx.LineTo(new Point(420, 14), true, true);
        }
        ecgGeo.Freeze();
        ecgDivider.Children.Add(new Path
        {
            Data = ecgGeo,
            Stroke = PulseInk.PrismBar,
            StrokeThickness = 2.0,
            Effect = PulseInk.Glow(PulseInk.CyanHot, 8, 0.85)
        });
        block.Children.Add(ecgDivider);

        var modelLine = new TextBlock
        {
            FontFamily = PulseInk.Display,
            FontSize = 29,
            FontWeight = FontWeights.Normal,
            Foreground = Brushes.White,
            Margin = new(8, 2, 0, 0)
        };
        if (empty)
        {
            modelLine.Text = "Assign a controller to this player";
        }
        else
        {
            modelLine.Inlines.Add(new Run(Player.Model ?? ""));
            if (FinishName.Length > 0)
            {
                modelLine.Inlines.Add(new Run(" · "));
                modelLine.Inlines.Add(new Run(FinishName) { Foreground = PulseInk.B(PulseInk.CyanHot) });
            }
        }
        block.Children.Add(modelLine);

        PulseKit.At(pulseFace!, block, 58, 98);

        // 3. Tethered Hardware Callout HUD Badges around the Hero Controller

        // Update primary CTA and action orbs visibility
        pulsePrimaryLabel!.Text = empty ? "Add controller" : "Open Studio";
        foreach (var b in pulseActionOrbs)
            b.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

        // 4. Bottom-left Prismatic Telemetry Capsules at (64, 788)
        if (!empty)
        {
            var chipRow = new StackPanel { Orientation = Orientation.Horizontal, IsHitTestVisible = false };
            (string text, bool live)[] chips =
            [
                ("Current working settings", true),
                ("Not detected", false),
                ("Not reported", false),
                ("No local changes", true)
            ];
            for (int i = 0; i < chips.Length; i++)
            {
                var ch = PulseKit.Chip(chips[i].text, activeLed: chips[i].live, h: 34, fontSize: 13);
                if (i < chips.Length - 1) ch.Margin = new(0, 0, 10, 0);
                chipRow.Children.Add(ch);
            }
            PulseKit.At(pulseFace!, chipRow, 64, 788);
        }
    }

    /// <summary>Draws a glowing HUD callout tether from a point (px, py) on the controller to a floating glass telemetry badge at (bx, by).</summary>
    private static void AddHeroCallout(Canvas c, double px, double py, double bx, double by, string title, string sub)
    {
        double elbowX = bx < px ? bx + 185 : bx - 12;
        double elbowY = by + 22;

        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(new Point(px, py), false, false);
            ctx.LineTo(new Point(elbowX, elbowY), true, true);
        }
        sg.Freeze();
        c.Children.Add(new Path
        {
            Data = sg,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.55)),
            StrokeThickness = 1.3,
            StrokeDashArray = new DoubleCollection([4, 3])
        });

        var anchor = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = Brushes.White,
            Stroke = PulseInk.B(PulseInk.CyanHot),
            StrokeThickness = 2,
            Effect = PulseInk.Glow(PulseInk.CyanHot, 10, 0.9)
        };
        PulseKit.At(c, anchor, px - 4, py - 4);

        var st = new StackPanel();
        st.Children.Add(PulseInk.MonoText(title, 10.5, PulseInk.B(PulseInk.CyanHot), FontWeights.Bold));
        st.Children.Add(PulseInk.MonoText(sub, 9.5, PulseInk.B(PulseInk.Soft), FontWeights.Normal));

        var badge = new Border
        {
            CornerRadius = new(10),
            Padding = new(12, 6, 12, 6),
            Background = PulseInk.B(PulseInk.A(PulseInk.C("#09152b"), 0.84)),
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.42)),
            BorderThickness = new(1),
            Effect = PulseInk.Drop(0, 8, 20, Colors.Black, 0.55),
            Child = st
        };
        PulseKit.At(c, badge, bx, by);
    }

    private static FrameworkElement MakeDarkSilhouette(double width)
    {
        double height = width / 1.5;
        var src = ControllerArt.Front("x20", X20Ctl.Zone.FinishCatalog.Original);
        var maskBrush = new ImageBrush(src) { Stretch = Stretch.Fill };
        maskBrush.Freeze();

        var g = new Grid
        {
            Width = width,
            Height = height,
            Opacity = 0.94,
            OpacityMask = maskBrush,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect
            {
                Color = PulseInk.CyanHot,
                Direction = 90,
                ShadowDepth = 1.6,
                BlurRadius = 2.5,
                Opacity = 0.70
            }
        };
        var img = new Image { Source = src, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        g.Children.Add(img);
        g.Children.Add(new Rectangle { Fill = PulseInk.B(Color.FromArgb(240, 4, 8, 18)) });
        return g;
    }
}

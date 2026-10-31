using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Pulse;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>The overhauled Pulse Controller Zone: a Prismatic Harmonic & Bio-Resonance Chamber
/// featuring a 3D cymatic perspective floor, volumetric light shafts, a live 1600px Cardiac ECG &
/// Multi-Sine Waveform Ribbon, 48-bar radial spectrum analyzers, gyroscopic HUD rings, and a
/// multi-bay Prismatic Command Deck.</summary>
public partial class ZoneScene
{
    internal bool IsPulse { get; private set; }
    private static readonly double[] PulseXs = [230, 515, 800, 1085, 1370];
    private const double PulsePodBoxW = 280, PulsePodBoxH = 430;

    private Grid? pulseBoard;
    private Canvas? pulseZoneBack, pulseHeroBack, pulseFocusOverlay;
    private FrameworkElement? pulseHeaderBlock, pulsePearlCard, pulseHintsOverview, pulseHintsHero, pulseStatusCluster;
    private Button? pulseAddCard;
    private TextBlock? pulseCaptionChannel, pulseCaptionMain, pulseStatusWho;
    private StackPanel? pulseCaptionStatuses;
    private DispatcherTimer? pulseWaveClock;
    private double pulseWavePhase = 1.45;
    private int pulseOn;

    internal static double PulseArcY(double x) => -1300 + Math.Sqrt(1950 * 1950 - (x - 800) * (x - 800)) - 210;

    private void BuildPulse()
    {
        IsPulse = true;
        var original = Surface.Children.OfType<Grid>().First();
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = PulseInk.B(PulseInk.Void0);
        OuterFrame.BorderBrush = PulseInk.B(PulseInk.Void0);

        pulseBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        // 1. Overview & Hero 3D Harmonic Chamber Backdrops
        pulseZoneBack = PulseKit.VoidBackdrop(hero: false);

        // Dual-rail illuminated orbital trajectory bus
        pulseZoneBack.Children.Add(new Path
        {
            Data = PulseKit.ArcGeo(800, -1300, 1984, 65, 115),
            Stroke = PulseInk.HorizFade,
            StrokeThickness = 1.8,
            Effect = PulseInk.Glow(PulseInk.Cyan, 10, 0.55),
            IsHitTestVisible = false
        });
        pulseZoneBack.Children.Add(new Path
        {
            Data = PulseKit.ArcGeo(800, -1300, 2002, 66, 114),
            Stroke = PulseInk.B(PulseInk.A(PulseInk.Ice, 0.25)),
            StrokeThickness = 1.0,
            StrokeDashArray = new DoubleCollection([6, 6]),
            IsHitTestVisible = false
        });

        pulseFocusOverlay = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        pulseZoneBack.Children.Add(pulseFocusOverlay);

        // Hero Backdrop: 3D chamber + full-scale Harmonic Gyroscope & 72-bar Radial Spectrum behind controller at (1076, 392)
        pulseHeroBack = PulseKit.VoidBackdrop(hero: true);
        pulseHeroBack.Visibility = Visibility.Collapsed;

        pulseBoard.Children.Add(pulseZoneBack);
        pulseBoard.Children.Add(pulseHeroBack);

        // 2. Top-left Brand Emitter (preserves exact (40, 28, 78, 54.25) IIntroHandoff target) + Console Header
        original.Children.Remove(HeaderBar);
        var tile = BrandTile;
        ((Panel)tile.Parent).Children.Remove(tile);
        double logoH = 78.0 * 523.0 / 752.0;
        tile.Width = 78;
        tile.Height = logoH;
        BrandMark.Width = 78;
        BrandMark.Height = logoH;
        BrandMark.Stretch = Stretch.Fill;
        BrandMark.Effect = PulseInk.Glow(PulseInk.CyanHot, 16, 0.65);
        string logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "pulse", "logo.png");
        if (System.IO.File.Exists(logoPath))
            BrandMark.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(logoPath));
        PulseKit.Put(tile, 40, 28);
        pulseBoard.Children.Add(tile);

        var headerStack = new StackPanel { Orientation = Orientation.Vertical, IsHitTestVisible = false };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(PulseInk.Disp("Controller Zone", 40, PulseInk.B(PulseInk.Pearl), FontWeights.Light));
        var liveBadge = new Border
        {
            CornerRadius = new(6),
            Padding = new(8, 2, 8, 2),
            Margin = new(16, 6, 0, 0),
            Background = PulseInk.B(PulseInk.A(PulseInk.Cyan, 0.16)),
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.50)),
            BorderThickness = new(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = PulseInk.MonoText("LOCAL DRAFTS", 10, PulseInk.B(PulseInk.CyanHot))
        };
        titleRow.Children.Add(liveBadge);
        headerStack.Children.Add(titleRow);
        var subHeader = PulseInk.MonoText("HARMONIC INPUT MATRIX  ·  04 CHANNELS  ·  BIO-ACOUSTIC TELEMETRY", 10.5, PulseInk.B(PulseInk.Sec));
        subHeader.Margin = new(2, 1, 0, 0);
        headerStack.Children.Add(subHeader);
        PulseKit.Put(headerStack, 134, 22);
        pulseBoard.Children.Add(headerStack);
        pulseHeaderBlock = headerStack;

        // 3. Move DockStage onto pulseBoard at full 1600x900 stage coordinates
        Workspace.Children.Remove(DockStage);
        DockStage.ClearValue(Grid.RowProperty);
        DockStage.Margin = new(0);
        DockStage.HorizontalAlignment = HorizontalAlignment.Left;
        DockStage.VerticalAlignment = VerticalAlignment.Top;
        DockStage.Width = 1600;
        DockStage.Height = 900;
        Workspace.Children.Remove(FeedbackText);
        pulseBoard.Children.Add(FeedbackText);
        pulseBoard.Children.Add(DockStage);

        // 4. Fifth pod: "Add controller" Acoustic Signal Injector at (1370, 355)
        double ax = PulseXs[4], ay = PulseArcY(ax);
        var addCanvas = new Canvas { Width = 240, Height = 290 };

        // Gyroscopic tick bezel around 5th pod
        addCanvas.Children.Add(PulseKit.GyroRings(120, 105, 108, false));

        var dashedRing = new Ellipse
        {
            Width = 204,
            Height = 204,
            Fill = PulseInk.B(PulseInk.A(PulseInk.C("#0b1830"), 0.55)),
            Stroke = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.38)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection([5, 5]),
            StrokeDashCap = PenLineCap.Round
        };
        PulseKit.At(addCanvas, dashedRing, 120 - 102, 105 - 102);

        var topScanTag = PulseInk.MonoText("SLOT 05 // SCANNER", 10.5, PulseInk.B(PulseInk.Sec));
        topScanTag.Width = 240;
        topScanTag.TextAlignment = TextAlignment.Center;
        PulseKit.At(addCanvas, topScanTag, 0, -28);

        var innerOrb = PulseKit.Orb(76, PulseInk.Icon("\uE710", 28, Brushes.White), on: false, accentBorder: PulseInk.CyanHot);
        PulseKit.At(addCanvas, innerOrb, 120 - 38, 105 - 38);

        var addLabel = PulseInk.Disp("Add controller", 21, PulseInk.B(PulseInk.Pearl), FontWeights.Normal);
        addLabel.Width = 240;
        addLabel.TextAlignment = TextAlignment.Center;
        PulseKit.At(addCanvas, addLabel, 0, 220);

        var addSub = PulseInk.MonoText("INJECT SIGNAL", 10.5, PulseInk.B(PulseInk.Cyan));
        addSub.Width = 240;
        addSub.TextAlignment = TextAlignment.Center;
        PulseKit.At(addCanvas, addSub, 0, 248);

        pulseAddCard = new Button
        {
            Content = addCanvas,
            Width = 240,
            Height = 290,
            Template = PulseKit.PlainButtonTemplate(),
            FocusVisualStyle = null,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        System.Windows.Automation.AutomationProperties.SetName(pulseAddCard, "Add controller");
        pulseAddCard.Click += (_, _) =>
        {
            int slot = Array.FindIndex(State.Players.ToArray(), p => p.Model == null);
            if (slot >= 0) ControllerActionRequested?.Invoke(slot, false);
        };
        pulseAddCard.GotKeyboardFocus += (_, _) => dashedRing.Stroke = PulseInk.B(PulseInk.CyanHot);
        pulseAddCard.LostKeyboardFocus += (_, _) => dashedRing.Stroke = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.38));
        PulseKit.Put(pulseAddCard, ax - 120, ay - 105);
        pulseBoard.Children.Add(pulseAddCard);

        // 5. Bottom Prismatic Telemetry & Waveform Command Deck: (56, 712, w=1488)
        var deckGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        deckGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        deckGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        deckGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left bay: Circular Lissajous Cymatic Widget + Active Channel Readout
        var leftBay = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        leftBay.Children.Add(PulseKit.CymaticWidget());

        var captionStack = new StackPanel { Margin = new(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        pulseCaptionChannel = PulseInk.MonoText("CHANNEL 01 // ACTIVE RESONANCE LOCK", 11, PulseInk.B(PulseInk.CyanHot));
        captionStack.Children.Add(pulseCaptionChannel);

        pulseCaptionMain = PulseInk.Disp("", 23, Brushes.White, FontWeights.Normal);
        pulseCaptionMain.Margin = new(0, 1, 0, 0);
        captionStack.Children.Add(pulseCaptionMain);
        leftBay.Children.Add(captionStack);
        Grid.SetColumn(leftBay, 0);
        deckGrid.Children.Add(leftBay);

        // Center-Right bay: 14-Bar Live Graphic Equalizer + 3 Prismatic Telemetry Capsules
        pulseCaptionStatuses = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(32, 0, 0, 0)
        };

        // Mini graphic equalizer bars inside the command deck
        var eqRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 18, 0) };
        double[] eqHeights = [10, 18, 28, 16, 32, 22, 12, 26, 34, 20, 14, 24, 16, 10];
        for (int i = 0; i < eqHeights.Length; i++)
        {
            Color eqCol = i % 4 == 0 ? PulseInk.Warm : (i % 2 == 0 ? PulseInk.CyanHot : PulseInk.PrismPink);
            eqRow.Children.Add(new Border
            {
                Width = 3,
                Height = eqHeights[i],
                CornerRadius = new(1.5),
                Margin = new(2, 0, 2, 0),
                Background = PulseInk.B(PulseInk.A(eqCol, 0.80)),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        pulseCaptionStatuses.Children.Add(eqRow);

        foreach (var (s, isLive) in new[] { ("Not detected", false), ("Not reported", false), ("No local changes", true) })
        {
            var chip = PulseKit.Chip(s, activeLed: isLive, h: 32, fontSize: 12.5);
            chip.Margin = new(0, 0, 10, 0);
            pulseCaptionStatuses.Children.Add(chip);
        }
        Grid.SetColumn(pulseCaptionStatuses, 1);
        deckGrid.Children.Add(pulseCaptionStatuses);

        pulsePearlCard = PulseKit.PearlCard(56, 712, 1488, deckGrid, new Thickness(28, 18, 28, 18));
        pulseBoard.Children.Add(pulsePearlCard);

        // 6. Top-right Prismatic Telemetry Status Pill
        var clock = HeaderActions.Children.OfType<HeaderClock>().First();
        HeaderActions.Children.Remove(clock);
        clock.FontFamily = PulseInk.Mono;
        clock.FontSize = 15;
        clock.FontWeight = FontWeights.SemiBold;
        clock.Foreground = PulseInk.B(PulseInk.Pearl);
        clock.Margin = new(0);
        clock.MinWidth = 0;
        var statusCluster = PulseKit.StatusBar(clock, out pulseStatusWho);
        pulseStatusCluster = statusCluster;
        pulseBoard.Children.Add(statusCluster);

        // 7. Bottom-right tactile console hint bars
        pulseHintsOverview = PulseKit.Hints(("A", "Select"), ("Y", "Change"), ("B", "Back"));
        pulseHintsHero = PulseKit.Hints(("A", "Open Studio"), ("B", "Back"));
        pulseHintsHero.Visibility = Visibility.Collapsed;
        pulseBoard.Children.Add(pulseHintsOverview);
        pulseBoard.Children.Add(pulseHintsHero);

        // Keep HeaderActions and UpdateNotice in the tree for keyboard/intro contracts while keeping the stage clean
        var strip = HeaderActions;
        ((Panel)strip.Parent).Children.Remove(strip);
        Grid.SetColumn(strip, 0);
        strip.HorizontalAlignment = HorizontalAlignment.Right;
        strip.VerticalAlignment = VerticalAlignment.Top;
        strip.Margin = new(0, -120, 8, 0);
        var notice = UpdateNotice;
        ((Panel)notice.Parent).Children.Remove(notice);
        Grid.SetColumn(notice, 0);
        notice.HorizontalAlignment = HorizontalAlignment.Center;
        notice.VerticalAlignment = VerticalAlignment.Top;
        notice.Margin = new(0, 10, 0, 0);

        original.Visibility = Visibility.Collapsed;
        Surface.Children.Add(pulseBoard);
        pulseBoard.Children.Add(strip);
        pulseBoard.Children.Add(notice);

        foreach (var d in docks) HookPulseDock(d);

        // Live 30fps waveform & radial spectrum animation timer when window is interactive
        pulseWaveClock = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(33) };
        pulseWaveClock.Tick += (_, _) =>
        {
            if (!IsLoaded || !IsVisible) return;
            pulseWavePhase += 0.09;
            RenderPulseDynamicField();
        };
        Loaded += (_, _) => { if (!ReviewSandbox.Active) pulseWaveClock.Start(); };
        Unloaded += (_, _) => pulseWaveClock.Stop();

        PulseSync();
    }

    private void HookPulseDock(DockView d)
    {
        d.FocusButton.GotKeyboardFocus += (_, _) => { pulseOn = d.Index; PulseSync(); };
        d.FocusButton.MouseEnter += (_, _) => { if (!State.FocusedPlayer.HasValue && pulseOn != d.Index) { pulseOn = d.Index; PulseSync(); } };
        d.BackRequested += () => _ = FocusPlayerAsync(null);
        d.PageRequested += page => StudioPageRequested?.Invoke(d.Index, page);
    }

    private void RenderPulseDynamicField()
    {
        if (!IsPulse || pulseBoard == null || pulseFocusOverlay == null || pulseHeroBack == null) return;
        bool hero = State.FocusedPlayer.HasValue;
        int on = hero ? State.FocusedPlayer!.Value : Math.Clamp(pulseOn, 0, 3);

        if (!hero)
        {
            pulseFocusOverlay.Children.Clear();
            double fx = PulseXs[on], fy = PulseArcY(fx);

            // 1. Active slot radial aura + concentric shockwaves + 48-bar Radial Spectrum + 72-tick Gyroscope + Triple Resonance Wings
            pulseFocusOverlay.Children.Add(PulseKit.Aura(fx, fy, 260, 260, 0.34));
            pulseFocusOverlay.Children.Add(PulseKit.Ripples(fx, fy, 6, 158, 52, 0.16));
            pulseFocusOverlay.Children.Add(PulseKit.RadialSpectrum(fx, fy, 130, 48, 8, 34, pulseWavePhase));
            pulseFocusOverlay.Children.Add(PulseKit.GyroRings(fx, fy, 148, active: true, showMandala: true));
            pulseFocusOverlay.Children.Add(PulseKit.Resonance(fx, fy, 162, 3, 28, 0.92));

            // 2. Vertical Energy Conduit connecting the focused pod straight down into the ECG peak & Command Deck
            var conduitBrush = new LinearGradientBrush { StartPoint = new(0.5, 0), EndPoint = new(0.5, 1), GradientStops = { new(PulseInk.A(PulseInk.CyanHot, 0.75), 0.0), new(PulseInk.A(PulseInk.PrismPink, 0.45), 0.55), new(PulseInk.A(PulseInk.CyanHot, 0.85), 1.0) } };
            conduitBrush.Freeze();
            pulseFocusOverlay.Children.Add(new Line
            {
                X1 = fx,
                Y1 = fy + 126,
                X2 = fx,
                Y2 = 712,
                Stroke = conduitBrush,
                StrokeThickness = 2.0,
                StrokeDashArray = new DoubleCollection([6, 4]),
                Effect = PulseInk.Glow(PulseInk.CyanHot, 10, 0.75)
            });

            // 3. Full-width 1600px Cardiac ECG & Multi-Sine Waveform Ribbon across y = 640
            bool[] assigned = new bool[5];
            for (int i = 0; i < 4; i++) assigned[i] = State.Players[i].Model != null || i == on;
            assigned[4] = false;
            pulseFocusOverlay.Children.Add(PulseKit.WaveformRibbon(640, PulseXs, assigned, on, pulseWavePhase));
        }
        else
        {
            // Rebuild Hero dynamic harmonic gyroscope & ECG field
            while (pulseHeroBack.Children.Count > 1)
                pulseHeroBack.Children.RemoveAt(pulseHeroBack.Children.Count - 1);

            var baseHero = PulseKit.VoidBackdrop(hero: true);
            pulseHeroBack.Children.Clear();
            pulseHeroBack.Children.Add(baseHero);

            // Full-scale Harmonic Reactor & 72-Bar Radial Spectrum behind the Hero controller at (1066, 370)
            double hcx = 1066, hcy = 370;
            pulseHeroBack.Children.Add(PulseKit.Aura(hcx, hcy, 440, 380, 0.36));
            pulseHeroBack.Children.Add(PulseKit.Ripples(hcx, hcy, 7, 290, 52, 0.14));
            pulseHeroBack.Children.Add(PulseKit.RadialSpectrum(hcx, hcy, 305, 72, 12, 54, pulseWavePhase));
            pulseHeroBack.Children.Add(PulseKit.GyroRings(hcx, hcy, 332, active: true, showMandala: true));
            pulseHeroBack.Children.Add(PulseKit.Resonance(hcx, hcy, 366, 3, 32, 0.85));

            // Secondary cymatic ripple field around the Open Studio reactor trigger at (190, 614)
            pulseHeroBack.Children.Add(PulseKit.Ripples(190, 614, 7, 74, 62, 0.13));

            // Full-width Hero ECG & Multi-Sine Waveform Ribbon across y = 518
            pulseHeroBack.Children.Add(PulseKit.WaveformRibbon(
                518,
                [220, 640, 1066, 1380],
                [true, true, true, true],
                2,
                pulseWavePhase));
        }
    }

    private void PulseSync()
    {
        if (!IsPulse || pulseBoard == null) return;
        bool hero = State.FocusedPlayer.HasValue;
        int on = hero ? State.FocusedPlayer!.Value : Math.Clamp(pulseOn, 0, 3);

        pulseZoneBack!.Visibility = hero ? Visibility.Collapsed : Visibility.Visible;
        pulseHeroBack!.Visibility = hero ? Visibility.Visible : Visibility.Collapsed;
        BrandTile.Visibility = pulseHeaderBlock!.Visibility = pulsePearlCard!.Visibility = hero ? Visibility.Collapsed : Visibility.Visible;
        pulseHintsOverview!.Visibility = hero ? Visibility.Collapsed : Visibility.Visible;
        pulseHintsHero!.Visibility = hero ? Visibility.Visible : Visibility.Collapsed;
        pulseAddCard!.Visibility = hero ? Visibility.Collapsed : Visibility.Visible;

        RenderPulseDynamicField();

        for (int i = 0; i < docks.Length; i++)
        {
            docks[i].Visibility = hero && i != on ? Visibility.Collapsed : Visibility.Visible;
            docks[i].SetPulseState(i == on, hero && i == on);
        }

        var p = State.Players[on];
        pulseStatusWho!.Text = $"Player {on + 1}";
        string finish = docks[on].FinishName;
        if (p.Model == null)
        {
            pulseCaptionChannel!.Text = $"CHANNEL 0{on + 1} // STANDBY · NO SIGNAL LOCKED";
            pulseCaptionMain!.Text = $"Player {on + 1} · Empty";
            pulseCaptionStatuses!.Visibility = Visibility.Collapsed;
        }
        else
        {
            pulseCaptionChannel!.Text = $"CHANNEL 0{on + 1}";
            pulseCaptionMain!.Text = $"Player {on + 1} · {p.Model}{(finish.Length > 0 ? " · " + finish : "")}";
            pulseCaptionStatuses!.Visibility = Visibility.Visible;
        }

        DockStage.UpdateLayout();
        if (DockStage.ActualWidth > 48) SnapLayout();
    }

    private DockBounds[] PulseBounds()
    {
        var b = new DockBounds[4];
        int? hero = State.FocusedPlayer;
        for (int i = 0; i < 4; i++)
        {
            if (hero == i)
            {
                b[i] = new(0, 0, 1600, 900);
            }
            else
            {
                double cx = PulseXs[i], cy = PulseArcY(cx);
                b[i] = new(cx - PulsePodBoxW / 2, cy - 180, PulsePodBoxW, PulsePodBoxH);
            }
        }
        return b;
    }

    private Task PulseFocus(int? index)
    {
        transition?.Cancel();
        if (index.HasValue) State.Activate(index.Value); else State.Collapse();
        FeedbackText.Text = State.Feedback;
        UpdateOverviewPill();
        if (index.HasValue) pulseOn = index.Value;
        PulseSync();
        if (index.HasValue) docks[index.Value].FocusButton.Focus();
        return Task.CompletedTask;
    }

    // Exact ink box of Brand/themes/pulse/logo.png (752x523, ink 0,24,752,479) inside BrandMark (78 x 54.247)
    private static readonly Rect PulseMarkInk = new(0, 24.0 / 523.0 * (78.0 * 523.0 / 752.0), 78.0, 479.0 / 523.0 * (78.0 * 523.0 / 752.0));

    private Rect? PulseMarkTarget(Visual to) => Within(BrandMark, PulseMarkInk, to);

    private IEnumerable<UIElement> PulseRevealed =>
        new UIElement?[] { pulseZoneBack, pulseHeaderBlock, pulseStatusCluster, pulsePearlCard, pulseHintsOverview, pulseAddCard }
            .Where(e => e != null).Cast<UIElement>().Concat(docks);

    private void PulseHandoffBegin()
    {
        foreach (var e in PulseRevealed) { e.BeginAnimation(OpacityProperty, null); e.Opacity = 0; }
        BrandTile.Opacity = 1;
        BrandMark.Opacity = 0;
        BrandMark.RenderTransformOrigin = new(.5, .5);
        BrandMark.RenderTransform = brandPulse;
        BeginAnimation(OpacityProperty, null); Opacity = 1;
        Workspace.BeginAnimation(OpacityProperty, null); Workspace.Opacity = 1;
    }

    private void PulseHandoff(double reveal, double brand, double controller, double pulse)
    {
        static double In(double t, double a, double b) { double x = Math.Clamp((t - a) / (b - a), 0, 1); return x * x * (3 - 2 * x); }
        if (pulseZoneBack != null) pulseZoneBack.Opacity = In(reveal, .08, .50);
        if (pulseHeaderBlock != null) pulseHeaderBlock.Opacity = In(reveal, .22, .62);
        if (pulseStatusCluster != null) pulseStatusCluster.Opacity = In(reveal, .20, .60);
        for (int i = 0; i < docks.Length; i++) docks[i].Opacity = In(reveal, .28 + .06 * i, .58 + .06 * i);
        if (pulseAddCard != null) pulseAddCard.Opacity = In(reveal, .28 + .06 * 4, .58 + .06 * 4);
        if (pulsePearlCard != null) pulsePearlCard.Opacity = In(reveal, .42, .82);
        if (pulseHintsOverview != null) pulseHintsOverview.Opacity = In(reveal, .48, .88);
        BrandTile.Opacity = 1;
        BrandMark.Opacity = brand;
        brandPulse.ScaleX = brandPulse.ScaleY = pulse;
    }

    private void PulseHandoffEnd()
    {
        foreach (var e in PulseRevealed) e.Opacity = 1;
        BrandTile.Opacity = BrandMark.Opacity = 1;
        BrandMark.RenderTransform = null;
    }
}

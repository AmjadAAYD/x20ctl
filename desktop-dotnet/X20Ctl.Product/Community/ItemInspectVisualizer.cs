using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Zone;
using X20Ctl.Zone.Community;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product.Community;

/// <summary>
/// Rich pure-WPF visual inspector for community setups, analog curves, dual-strip RGB lighting,
/// vibration profiles, macros, and 160x80 Screen Art rendered on a Black or White X20 Pro shell.
/// </summary>
public sealed class ItemInspectVisualizer : Border
{
    private readonly CommunityItem _item;
    private readonly bool _compact;
    private DispatcherTimer? _animTimer;
    private AnimatedMediaView? _mockupMedia;
    private AnimatedMediaView? _rawScreenMedia;
    private Image? _controllerShellImage;
    private double _animPhase;

    public ItemInspectVisualizer(CommunityItem item, bool compact = false)
    {
        _item = item;
        _compact = compact;

        CornerRadius = new CornerRadius(8);
        Background = new SolidColorBrush(CommunityUI.BgInput);
        BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(compact ? 12 : 14);

        Child = BuildContent();
        Unloaded += (_, _) => StopAnimations();
    }

    public void StopAnimations()
    {
        if (_animTimer != null)
        {
            _animTimer.Stop();
            _animTimer = null;
        }
        _mockupMedia?.StopAnimation();
        _rawScreenMedia?.StopAnimation();
    }

    public void SetScreenMedia(byte[]? bytes, bool isGif)
    {
        if (bytes == null || bytes.Length == 0)
        {
            _mockupMedia?.SetPlaceholder("160x80");
            _rawScreenMedia?.SetPlaceholder("160x80");
            return;
        }
        _mockupMedia?.LoadBytes(bytes, isGif ? "GIF" : "PNG");
        _rawScreenMedia?.LoadBytes(bytes, isGif ? "GIF" : "PNG");
    }

    private UIElement BuildContent()
    {
        var cat = (_item.Category ?? "setup").Trim().ToLowerInvariant();
        return cat switch
        {
            "screen" => BuildScreenMockup(),
            "rgb" => BuildRgbStripsView(),
            "curve" => BuildCurveView(),
            "vibration" => BuildVibrationView(),
            "macro" => BuildMacroView(),
            _ => BuildSetupDiffView()
        };
    }

    // ------------------------------------------------------------------
    // 1. Screen Art: Black / White X20 Pro Hardware Display Mockup
    // ------------------------------------------------------------------
    private UIElement BuildScreenMockup()
    {
        var root = new StackPanel();

        var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var shellBtns = new StackPanel { Orientation = Orientation.Horizontal };
        var blackBtn = CommunityUI.RectButton("Black X20 Pro", RectBtnKind.Primary, 26, 11);
        blackBtn.Tag = "inspect:shell:black";
        blackBtn.Margin = new Thickness(0, 0, 6, 0);
        var whiteBtn = CommunityUI.RectButton("White X20 Pro", RectBtnKind.Secondary, 26, 11);
        whiteBtn.Tag = "inspect:shell:white";
        var rawToggleBtn = CommunityUI.RectButton("2x Pixel View", RectBtnKind.Secondary, 26, 11);
        rawToggleBtn.Tag = "inspect:shell:raw";
        rawToggleBtn.Margin = new Thickness(6, 0, 0, 0);

        shellBtns.Children.Add(blackBtn);
        shellBtns.Children.Add(whiteBtn);
        shellBtns.Children.Add(rawToggleBtn);
        DockPanel.SetDock(shellBtns, Dock.Right);
        topBar.Children.Add(shellBtns);

        var fmtLabel = _item.MediaType == "gif" ? "160x80 Animated GIF" : "160x80 Static PNG";
        var sizeKb = Math.Max(1, _item.AssetBytes / 1024);
        var framesPart = _item.FrameCount > 1 ? $" - {_item.FrameCount} frames" : "";
        topBar.Children.Add(Kit.Text($"X20 PRO DISPLAY PREVIEW - {fmtLabel}{framesPart} ({sizeKb} KB)", 10.5, "DS.AccentHi", FontWeights.Bold)
            .Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        root.Children.Add(topBar);

        // Exact ScreenWorkbench geometry: 1536x1024 front photo cropped to 960x510, screen at (627, 113) 280x140
        var padContainer = new Grid { Width = 1536, Height = 1024, IsHitTestVisible = false };
        _controllerShellImage = new Image
        {
            Width = 1536,
            Height = 1024,
            Stretch = Stretch.UniformToFill
        };
        RenderOptions.SetBitmapScalingMode(_controllerShellImage, BitmapScalingMode.HighQuality);
        TryLoadShellArt("original");
        padContainer.Children.Add(_controllerShellImage);

        var padOverlay = new Canvas { Width = 1536, Height = 1024, IsHitTestVisible = false };
        var screenStage = new Grid { Background = Brushes.Black };
        _mockupMedia = new AnimatedMediaView(276, 136, pixelArt: true, cornerRadius: 10);
        _mockupMedia.SetPlaceholder("160x80");
        screenStage.Children.Add(_mockupMedia);
        screenStage.Children.Add(BuildGlassSheen());

        var embeddedLcd = new Border
        {
            Width = 280,
            Height = 140,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Black,
            Background = Brushes.Black,
            ClipToBounds = true,
            Child = new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = screenStage }
        };
        Canvas.SetLeft(embeddedLcd, 627);
        Canvas.SetTop(embeddedLcd, 113);
        padOverlay.Children.Add(embeddedLcd);
        padContainer.Children.Add(padOverlay);

        var cropStage = new Canvas
        {
            Width = 960,
            Height = 510,
            ClipToBounds = true,
            IsHitTestVisible = false,
            OpacityMask = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Colors.White, 0.0),
                    new(Colors.White, 0.80),
                    new(Colors.Transparent, 1.0)
                },
                new Point(0, 0),
                new Point(0, 1))
        };
        Canvas.SetLeft(padContainer, -288);
        Canvas.SetTop(padContainer, -54);
        cropStage.Children.Add(padContainer);

        var mockupHost = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(4, 8, 18)),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Child = new Viewbox
            {
                MaxHeight = _compact ? 180 : 270,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = cropStage
            }
        };

        // Raw 2x Pixel View (320x160) toggleable panel
        _rawScreenMedia = new AnimatedMediaView(320, 160, pixelArt: true, cornerRadius: 6)
        {
            BorderBrush = new SolidColorBrush(CommunityUI.BorderFocus),
            BorderThickness = new Thickness(1)
        };
        _rawScreenMedia.SetPlaceholder("160x80");
        var rawHost = new Border
        {
            Visibility = Visibility.Collapsed,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(4, 8, 18)),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    _rawScreenMedia,
                    Kit.Text("Native 160x80 pixel-accurate stream (2x zoom)", 11, "DS.TextMuted")
                        .Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new Thickness(0, 8, 0, 0); })
                }
            }
        };

        void ApplyButtonStyle(Button b, bool active)
        {
            b.Background = new SolidColorBrush(active ? CommunityUI.PrimaryFill : Color.FromRgb(16, 26, 46));
            b.BorderBrush = new SolidColorBrush(active ? Color.FromRgb(96, 165, 250) : Color.FromRgb(38, 55, 88));
            b.Foreground = new SolidColorBrush(active ? Colors.White : Color.FromRgb(226, 232, 240));
        }

        bool showingRaw = false;
        blackBtn.Click += (_, _) =>
        {
            showingRaw = false;
            mockupHost.Visibility = Visibility.Visible;
            rawHost.Visibility = Visibility.Collapsed;
            TryLoadShellArt("original");
            ApplyButtonStyle(blackBtn, true);
            ApplyButtonStyle(whiteBtn, false);
            ApplyButtonStyle(rawToggleBtn, false);
        };
        whiteBtn.Click += (_, _) =>
        {
            showingRaw = false;
            mockupHost.Visibility = Visibility.Visible;
            rawHost.Visibility = Visibility.Collapsed;
            TryLoadShellArt("white");
            ApplyButtonStyle(blackBtn, false);
            ApplyButtonStyle(whiteBtn, true);
            ApplyButtonStyle(rawToggleBtn, false);
        };
        rawToggleBtn.Click += (_, _) =>
        {
            showingRaw = !showingRaw;
            mockupHost.Visibility = showingRaw ? Visibility.Collapsed : Visibility.Visible;
            rawHost.Visibility = showingRaw ? Visibility.Visible : Visibility.Collapsed;
            ApplyButtonStyle(rawToggleBtn, showingRaw);
            if (showingRaw)
            {
                ApplyButtonStyle(blackBtn, false);
                ApplyButtonStyle(whiteBtn, false);
            }
            else
            {
                TryLoadShellArt("original");
                ApplyButtonStyle(blackBtn, true);
                ApplyButtonStyle(whiteBtn, false);
            }
        };

        root.Children.Add(mockupHost);
        root.Children.Add(rawHost);
        return root;
    }

    private void TryLoadShellArt(string finish)
    {
        if (_controllerShellImage == null) return;
        try
        {
            _controllerShellImage.Source = ControllerArt.Front("x20_pro", finish);
        }
        catch
        {
            _controllerShellImage.Source = null;
        }
    }

    private static Grid BuildGlassSheen()
    {
        var sheen = new Grid { IsHitTestVisible = false };
        sheen.Children.Add(new Border
        {
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(46, 255, 255, 255), 0.0),
                    new(Color.FromArgb(12, 255, 255, 255), 0.35),
                    new(Color.FromArgb(0, 255, 255, 255), 0.65)
                },
                new Point(0, 0),
                new Point(1, 1))
        });
        return sheen;
    }

    // ------------------------------------------------------------------
    // 2. RGB Lighting: Animated Dual Moving LED Strips
    // ------------------------------------------------------------------
    private UIElement BuildRgbStripsView()
    {
        Color primary = Color.FromRgb(56, 189, 248);
        string hexLabel = "#38BDF8";
        int modeIdx = 0;
        byte brightnessByte = 0x50;

        if (_item.Payload is { ValueKind: JsonValueKind.Object } obj)
        {
            if (obj.TryGetProperty("rgb", out var rp) && rp.ValueKind == JsonValueKind.String)
            {
                hexLabel = rp.GetString() ?? hexLabel;
                primary = ParseHexColor(hexLabel, primary);
            }
            if (obj.TryGetProperty("mode", out var mp) && mp.TryGetInt32(out int m))
                modeIdx = m;
            if (obj.TryGetProperty("brightness", out var bp) && bp.TryGetInt32(out int b))
                brightnessByte = (byte)Math.Clamp(b, 0, 255);
        }

        return BuildDualStripBox(primary, hexLabel, modeIdx, brightnessByte, _compact ? 110 : 148);
    }

    private UIElement BuildDualStripBox(Color baseColor, string hexLabel, int modeIdx, byte brightnessByte, double height)
    {
        var root = new StackPanel();
        string modeName = modeIdx switch
        {
            0 => "Solid",
            1 => "Breathing",
            2 => "Rainbow Wave",
            3 => "Spectrum Cycle",
            _ => $"Mode {modeIdx}"
        };
        int brightPct = (int)Math.Round(brightnessByte * 100.0 / 255.0);

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var metaTags = new StackPanel { Orientation = Orientation.Horizontal };
        metaTags.Children.Add(CommunityUI.SquareTag(modeName.ToUpperInvariant()));
        metaTags.Children.Add(CommunityUI.SquareTag(hexLabel.ToUpperInvariant(), baseColor));
        metaTags.Children.Add(CommunityUI.SquareTag($"BRIGHTNESS {brightPct}%"));
        DockPanel.SetDock(metaTags, Dock.Right);
        header.Children.Add(metaTags);
        header.Children.Add(Kit.Text("X20 PRO DUAL RGB STRIPS", 10.5, "DS.AccentHi", FontWeights.Bold)
            .Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        root.Children.Add(header);

        var chamber = new Border
        {
            Height = height,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(5, 9, 19)),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 16, 18, 16)
        };

        var stripGrid = new Grid();
        stripGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stripGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        stripGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Border MakeStripHousing(string sideLabel, out Border glowLayer, out Border coreBar)
        {
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(Kit.Text(sideLabel, 10, "DS.TextMuted", FontWeights.SemiBold)
                .Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new Thickness(0, 0, 0, 8); }));

            var track = new Grid { Height = 30 };
            glowLayer = new Border
            {
                Height = 26,
                CornerRadius = new CornerRadius(6),
                Opacity = 0.45,
                Margin = new Thickness(2, 0, 2, 0)
            };
            coreBar = new Border
            {
                Height = 14,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center
            };
            track.Children.Add(glowLayer);
            track.Children.Add(coreBar);

            // LED diffuser segment lines
            var segGrid = new UniformGrid { Columns = 12, IsHitTestVisible = false, Height = 14 };
            for (int i = 0; i < 12; i++)
            {
                segGrid.Children.Add(new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromArgb(85, 4, 8, 18)),
                    BorderThickness = new Thickness(1, 0, 1, 0)
                });
            }
            track.Children.Add(segGrid);

            stack.Children.Add(track);
            return new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(10, 16, 30)),
                BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 12),
                Child = stack
            };
        }

        var leftHost = MakeStripHousing("LEFT GRIP STRIP", out var leftGlow, out var leftCore);
        var rightHost = MakeStripHousing("RIGHT GRIP STRIP", out var rightGlow, out var rightCore);
        Grid.SetColumn(rightHost, 2);
        stripGrid.Children.Add(leftHost);
        stripGrid.Children.Add(rightHost);
        chamber.Child = stripGrid;
        root.Children.Add(chamber);

        void RenderFrame(double phase)
        {
            double brightScale = Math.Clamp(0.25 + (brightnessByte / 255.0) * 0.75, 0.2, 1.0);
            if (modeIdx == 1) // Breathing
            {
                double wave = 0.25 + 0.75 * (0.5 + 0.5 * Math.Sin(phase * 2.4));
                brightScale *= wave;
            }

            Brush MakeBrush(double offset)
            {
                var stops = new GradientStopCollection();
                for (int s = 0; s <= 4; s++)
                {
                    double pos = s / 4.0;
                    Color c;
                    if (modeIdx is 2 or 3)
                    {
                        double hue = ((phase * 0.35) + pos * 0.55 + offset) % 1.0;
                        c = HsvToColor(hue < 0 ? hue + 1.0 : hue, 0.88, brightScale);
                    }
                    else
                    {
                        double shimmer = 0.72 + 0.28 * Math.Sin((phase * 2.8) + pos * Math.PI * 2 + offset * Math.PI);
                        c = ScaleColor(baseColor, Math.Clamp(brightScale * shimmer, 0.15, 1.0));
                    }
                    stops.Add(new GradientStop(c, pos));
                }
                return new LinearGradientBrush(stops, new Point(0, 0.5), new Point(1, 0.5));
            }

            var leftBrush = MakeBrush(0.0);
            var rightBrush = MakeBrush(modeIdx is 2 or 3 ? 0.35 : 0.6);
            leftCore.Background = leftBrush;
            leftGlow.Background = leftBrush;
            rightCore.Background = rightBrush;
            rightGlow.Background = rightBrush;
        }

        _animPhase = 0.8;
        RenderFrame(_animPhase);

        if (!ReviewSandbox.Active)
        {
            _animTimer?.Stop();
            _animTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _animTimer.Tick += (_, _) =>
            {
                _animPhase += 0.05;
                RenderFrame(_animPhase);
            };
            _animTimer.Start();
        }

        return root;
    }

    // ------------------------------------------------------------------
    // 3. Analog Curve & Stick Tuning Inspector
    // ------------------------------------------------------------------
    private UIElement BuildCurveView()
    {
        StickLabShareCode.Payload? decoded = null;
        if (!string.IsNullOrWhiteSpace(_item.ShareCode))
            StickLabShareCode.TryDecode(_item.ShareCode, out decoded, out _);

        string channelName = decoded?.Channel ?? "LeftStick";
        string presetName = decoded?.Curve.Preset ?? "custom";
        int inner = decoded?.Curve.Inner ?? 0;
        int outer = decoded?.Curve.Outer ?? 100;
        SetupCurve curve = decoded?.Curve ?? new SetupCurve("default", "default", 0, 100, 85, 85, 170, 170);
        StickTuning? tuning = decoded?.Tuning;
        byte flags = decoded?.Flags ?? 0;

        if (decoded == null && _item.Payload is { ValueKind: JsonValueKind.Object } obj)
        {
            if (obj.TryGetProperty("preset", out var p) && p.ValueKind == JsonValueKind.String) presetName = p.GetString() ?? presetName;
            else if (obj.TryGetProperty("leftPreset", out var lp) && lp.ValueKind == JsonValueKind.String) presetName = lp.GetString() ?? presetName;
            if (obj.TryGetProperty("innerDeadzone", out var idz) && idz.TryGetInt32(out int iv)) inner = iv;
            else if (obj.TryGetProperty("leftInner", out var lid) && lid.TryGetInt32(out int liv)) inner = liv;
            if (obj.TryGetProperty("outerLimit", out var ol) && ol.TryGetInt32(out int ov)) outer = ov;
            else if (obj.TryGetProperty("leftOuter", out var lol) && lol.TryGetInt32(out int lov)) outer = lov;
            if (obj.TryGetProperty("flags", out var fl) && fl.TryGetInt32(out int fv)) flags = (byte)fv;

            var (p1, p2) = CurveDraft.Presets.TryGetValue(presetName.ToLowerInvariant(), out var pts)
                ? pts
                : ((85, 85), (170, 170));
            curve = new SetupCurve(presetName, presetName, Math.Clamp(inner, 0, 99), Math.Clamp(outer, inner + 1, 100), p1.Item1, p1.Item2, p2.Item1, p2.Item2);
        }

        var root = new StackPanel();
        root.Children.Add(Kit.Text("ANALOG RESPONSE & STICK GEOMETRY", 10.5, "DS.AccentHi", FontWeights.Bold)
            .Also(t => t.Margin = new Thickness(0, 0, 0, 10)));

        var split = new Grid();
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var graphBox = BuildCurveGraph(curve, tuning, _compact ? 190 : 228, _compact ? 140 : 168);
        split.Children.Add(graphBox);

        var right = new StackPanel();

        // Deadzone ring preview + specs side by side
        var ringAndSpecs = new DockPanel();
        var ringCanvas = BuildDeadzoneRing(inner, outer, _compact ? 88 : 104);
        ringCanvas.Margin = new Thickness(0, 0, 14, 0);
        DockPanel.SetDock(ringCanvas, Dock.Left);
        ringAndSpecs.Children.Add(ringCanvas);

        var specs = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        specs.Children.Add(SpecTile("CHANNEL", FormatChannelName(channelName)));
        specs.Children.Add(SpecTile("PRESET", presetName.ToUpperInvariant()));
        specs.Children.Add(SpecTile("INNER DZ", $"{inner}%"));
        specs.Children.Add(SpecTile("OUTER CAP", $"{outer}%"));

        double anti = tuning != null ? (tuning.Lab is { RadialAntiDeadzone: > 0 } ? tuning.Lab.RadialAntiDeadzone : tuning.AntiDeadzone) : 0;
        if (anti > 0) specs.Children.Add(SpecTile("ANTI-DZ", $"{anti:0}%"));
        if (decoded != null && decoded.DPadSocd != SocdMode.Off) specs.Children.Add(SpecTile("DPAD SOCD", decoded.DPadSocd.ToString()));
        if (decoded != null && decoded.LeftStickSocd != SocdMode.Off) specs.Children.Add(SpecTile("STICK SOCD", decoded.LeftStickSocd.ToString()));
        if ((flags & (byte)CurveHardwareFlags.InvertX) != 0) specs.Children.Add(SpecTile("AXIS X", "INVERTED"));
        if ((flags & (byte)CurveHardwareFlags.InvertY) != 0) specs.Children.Add(SpecTile("AXIS Y", "INVERTED"));
        if ((flags & (byte)CurveHardwareFlags.SwapSticks) != 0) specs.Children.Add(SpecTile("STICKS", "SWAPPED"));

        ringAndSpecs.Children.Add(specs);
        right.Children.Add(ringAndSpecs);

        Grid.SetColumn(right, 2);
        split.Children.Add(right);
        root.Children.Add(split);
        return root;
    }

    // ------------------------------------------------------------------
    // 4. Full Setup / Pack: Sticks, Triggers, Remaps, Vibration, Macros, RGB
    // ------------------------------------------------------------------
    private UIElement BuildSetupDiffView()
    {
        SetupSnapshot? snap = null;
        if (_item.Payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            try
            {
                snap = JsonSerializer.Deserialize<SetupSnapshot>(el.GetRawText(), SetupStore.JsonOptions);
            }
            catch
            {
                snap = null;
            }
        }

        if (snap == null)
            return Kit.Text("Setup configuration ready to inspect and install.", 12.5, "DS.TextSoft");

        var root = new StackPanel();
        root.Children.Add(Kit.Text("SETUP BREAKDOWN - WHAT WAS CHANGED", 10.5, "DS.AccentHi", FontWeights.Bold)
            .Also(t => t.Margin = new Thickness(0, 0, 0, 10)));

        // A. Sticks & Triggers 4-column grid
        var channelsGrid = new UniformGrid { Columns = _compact ? 2 : 4, Margin = new Thickness(-4, 0, -4, 10) };
        foreach (var ch in new[] { CurveChannel.LeftStick, CurveChannel.RightStick, CurveChannel.LeftTrigger, CurveChannel.RightTrigger })
        {
            SetupCurve c = snap.Curves != null && snap.Curves.TryGetValue(ch.ToString(), out var found) && found != null
                ? found
                : new SetupCurve("default", "default", 0, 100, 85, 85, 170, 170);
            bool modified = !string.Equals(c.Preset, "default", StringComparison.OrdinalIgnoreCase)
                || c.Inner != 0 || c.Outer != 100 || c.X1 != 85 || c.Y1 != 85 || c.X2 != 170 || c.Y2 != 170;

            var card = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(10, 17, 32)),
                BorderBrush = new SolidColorBrush(modified ? CommunityUI.BorderFocus : CommunityUI.BorderSubtle),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Margin = new Thickness(4)
            };
            var s = new StackPanel();
            s.Children.Add(new DockPanel
            {
                Margin = new Thickness(0, 0, 0, 4),
                Children =
                {
                    Kit.Text(modified ? "TUNED" : "STOCK", 9, modified ? "DS.AccentHi" : "DS.TextMuted", FontWeights.Bold)
                        .Also(t => DockPanel.SetDock(t, Dock.Right)),
                    Kit.Text(FormatChannelName(ch.ToString()), 11, "DS.Text", FontWeights.Bold)
                }
            });
            s.Children.Add(BuildCurveGraph(c, null, 132, 68));
            s.Children.Add(Kit.Text($"{c.Preset.ToUpperInvariant()}  |  DZ {c.Inner}% - {c.Outer}%", 10, modified ? "DS.Text" : "DS.TextMuted")
                .Also(t => t.Margin = new Thickness(0, 5, 0, 0)));
            card.Child = s;
            channelsGrid.Children.Add(card);
        }
        root.Children.Add(channelsGrid);

        // B. Button Remaps
        var remapped = snap.Mappings?
            .Where(kv => !string.Equals(kv.Key, kv.Value, StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];

        var remapBox = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(10, 17, 32)),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var remapStack = new StackPanel();
        remapStack.Children.Add(Kit.Text($"BUTTON REMAPS ({remapped.Count})", 10, "DS.TextMuted", FontWeights.Bold)
            .Also(t => t.Margin = new Thickness(0, 0, 0, 5)));
        if (remapped.Count == 0)
        {
            remapStack.Children.Add(Kit.Text("Default factory button layout (0 remaps)", 11.5, "DS.TextSoft"));
        }
        else
        {
            var wrap = new WrapPanel();
            foreach (var kv in remapped)
                wrap.Children.Add(CommunityUI.SquareTag($"{kv.Key} -> {kv.Value}", Colors.White, Color.FromArgb(65, 56, 189, 248)));
            remapStack.Children.Add(wrap);
        }
        remapBox.Child = remapStack;
        root.Children.Add(remapBox);

        // C. Bottom split: Vibration Motors + Macros
        var bottomRow = new Grid();
        bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int leftM = snap.Motors is { Length: >= 2 } ? snap.Motors[0] : snap.Vibration;
        int rightM = snap.Motors is { Length: >= 2 } ? snap.Motors[1] : snap.Vibration;
        int floorM = snap.Motors is { Length: >= 3 } ? snap.Motors[2] : 0;

        var vibCard = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(10, 17, 32)),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Child = new StackPanel
            {
                Children =
                {
                    Kit.Text("VIBRATION MOTORS", 10, "DS.TextMuted", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 0, 0, 5)),
                    BuildMeterBar("Overall", snap.Vibration, 200, Color.FromRgb(74, 222, 128)),
                    BuildMeterBar("Left / Right", (leftM + rightM) / 2, 200, Color.FromRgb(56, 189, 248)),
                    Kit.Text($"L {leftM}%  -  R {rightM}%  -  Floor {floorM}%", 10.5, "DS.TextSoft").Also(t => t.Margin = new Thickness(0, 3, 0, 0))
                }
            }
        };
        bottomRow.Children.Add(vibCard);

        var macroWrap = new WrapPanel();
        int activeMacroCount = 0;
        if (snap.Macros != null)
        {
            foreach (var (slot, steps) in snap.Macros)
            {
                if (steps is not { Length: > 0 }) continue;
                activeMacroCount++;
                string trig = snap.Triggers != null && snap.Triggers.TryGetValue(slot, out var t) && !string.IsNullOrEmpty(t) ? t : "Paddle";
                macroWrap.Children.Add(CommunityUI.SquareTag($"{slot} ({trig}): {steps.Length} {(steps.Length == 1 ? "step" : "steps")}"));
            }
        }
        var macroCard = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(10, 17, 32)),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Child = new StackPanel
            {
                Children =
                {
                    Kit.Text($"MACROS ({activeMacroCount} ACTIVE)", 10, "DS.TextMuted", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 0, 0, 6)),
                    activeMacroCount > 0 ? (UIElement)macroWrap : Kit.Text("No macro sequences attached", 11.5, "DS.TextMuted")
                }
            }
        };
        Grid.SetColumn(macroCard, 2);
        bottomRow.Children.Add(macroCard);
        root.Children.Add(bottomRow);

        // D. Optional RGB snapshot lighting
        if (snap.Lighting != null)
        {
            string hex = snap.Lighting.Palette is { Length: > 0 } ? snap.Lighting.Palette[0] : "#38BDF8";
            var stripBox = BuildDualStripBox(ParseHexColor(hex, Color.FromRgb(56, 189, 248)), hex, 2, snap.Lighting.MaxBrightness, 92);
            if (stripBox is FrameworkElement fe) fe.Margin = new Thickness(0, 10, 0, 0);
            root.Children.Add(stripBox);
        }

        return root;
    }

    // ------------------------------------------------------------------
    // 5. Vibration Motors & Waveform Preview
    // ------------------------------------------------------------------
    private UIElement BuildVibrationView()
    {
        int strength = 70, left = 70, right = 70, floor = 0;
        if (_item.Payload is { ValueKind: JsonValueKind.Object } obj)
        {
            if (obj.TryGetProperty("strength", out var s) && s.TryGetInt32(out int sv)) strength = Math.Clamp(sv, 0, 200);
            if (obj.TryGetProperty("left", out var l) && l.TryGetInt32(out int lv)) left = Math.Clamp(lv, 0, 200);
            else left = strength;
            if (obj.TryGetProperty("right", out var r) && r.TryGetInt32(out int rv)) right = Math.Clamp(rv, 0, 200);
            else right = strength;
            if (obj.TryGetProperty("floor", out var f) && f.TryGetInt32(out int fv)) floor = Math.Clamp(fv, 0, 50);
        }

        var root = new StackPanel();
        root.Children.Add(Kit.Text("VIBRATION MOTOR PROFILE", 10.5, "DS.AccentHi", FontWeights.Bold)
            .Also(t => t.Margin = new Thickness(0, 0, 0, 10)));
        root.Children.Add(BuildMeterBar($"Overall Strength ({strength}%)", strength, 200, Color.FromRgb(74, 222, 128)));
        root.Children.Add(BuildMeterBar($"Heavy Left Motor ({left}%)", left, 200, Color.FromRgb(56, 189, 248)));
        root.Children.Add(BuildMeterBar($"Fine Right Motor ({right}%)", right, 200, Color.FromRgb(192, 132, 252)));
        root.Children.Add(BuildMeterBar($"Minimum Rumble Floor ({floor}%)", floor, 50, Color.FromRgb(251, 191, 36)));

        // Waveform envelope canvas
        double w = 480, h = _compact ? 58 : 76;
        var waveCanvas = new Canvas { Width = w, Height = h, ClipToBounds = true };
        var leftPts = new PointCollection();
        var rightPts = new PointCollection();
        double ampL = Math.Clamp(left / 120.0, 0.12, 1.0) * (h * 0.36);
        double ampR = Math.Clamp(right / 120.0, 0.12, 1.0) * (h * 0.36);
        for (int i = 0; i <= 48; i++)
        {
            double t = i / 48.0;
            double env = Math.Sin(t * Math.PI);
            leftPts.Add(new Point(t * w, h * 0.5 - Math.Sin(t * Math.PI * 6) * ampL * env));
            rightPts.Add(new Point(t * w, h * 0.5 - Math.Sin(t * Math.PI * 12) * ampR * env));
        }
        waveCanvas.Children.Add(new Line { X1 = 0, X2 = w, Y1 = h * 0.5, Y2 = h * 0.5, Stroke = new SolidColorBrush(Color.FromArgb(60, 148, 163, 184)), StrokeThickness = 1 });
        waveCanvas.Children.Add(new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)), StrokeThickness = 2.0, Points = leftPts });
        waveCanvas.Children.Add(new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(192, 132, 252)), StrokeThickness = 1.6, Points = rightPts });

        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(6, 10, 20)),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(8),
            Child = new Viewbox { Stretch = Stretch.Uniform, Child = waveCanvas }
        });
        return root;
    }

    // ------------------------------------------------------------------
    // 6. Macro Sequence Timeline
    // ------------------------------------------------------------------
    private UIElement BuildMacroView()
    {
        string slot = "M1", trigger = "Paddle", mode = "Once";
        int repeat = 1;
        var steps = new List<(string Buttons, int HoldMs, int PauseMs)>();

        if (_item.Payload is { ValueKind: JsonValueKind.Object } obj)
        {
            if (obj.TryGetProperty("slot", out var sl) && sl.ValueKind == JsonValueKind.String) slot = sl.GetString() ?? slot;
            if (obj.TryGetProperty("trigger", out var tr) && tr.ValueKind == JsonValueKind.String) trigger = tr.GetString() ?? trigger;
            if (obj.TryGetProperty("mode", out var md) && md.ValueKind == JsonValueKind.String) mode = md.GetString() ?? mode;
            if (obj.TryGetProperty("repeat", out var rp) && rp.TryGetInt32(out int rv)) repeat = rv;
            if (obj.TryGetProperty("events", out var ev) && ev.ValueKind == JsonValueKind.Array)
            {
                foreach (var st in ev.EnumerateArray().Take(12))
                {
                    var btns = st.TryGetProperty("buttons", out var bp) && bp.ValueKind == JsonValueKind.Array
                        ? string.Join(" + ", bp.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)))
                        : "";
                    if (string.IsNullOrEmpty(btns)) btns = "Stick";
                    int hold = st.TryGetProperty("holdMs", out var hm) && hm.TryGetInt32(out int hv) ? hv : 50;
                    int pause = st.TryGetProperty("pauseMs", out var pm) && pm.TryGetInt32(out int pv) ? pv : 0;
                    steps.Add((btns, hold, pause));
                }
            }
        }

        var root = new StackPanel();
        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        top.Children.Add(Kit.Text("MACRO SEQUENCE TIMELINE", 10.5, "DS.AccentHi", FontWeights.Bold)
            .Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new Thickness(0, 0, 10, 0); }));
        top.Children.Add(CommunityUI.SquareTag($"SLOT {slot}"));
        top.Children.Add(CommunityUI.SquareTag($"TRIGGER: {trigger}"));
        top.Children.Add(CommunityUI.SquareTag($"{mode.ToUpperInvariant()} x{repeat}"));
        root.Children.Add(top);

        var timeline = new WrapPanel();
        if (steps.Count == 0)
        {
            timeline.Children.Add(Kit.Text("No recorded macro steps in payload.", 12, "DS.TextMuted"));
        }
        else
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var st = steps[i];
                var block = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromRgb(14, 24, 46)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(9, 6, 9, 6),
                    Margin = new Thickness(0, 0, 6, 6),
                    Child = new StackPanel
                    {
                        Children =
                        {
                            Kit.Text($"#{i + 1}  {st.Buttons}", 11.5, "DS.Text", FontWeights.Bold),
                            Kit.Text($"Hold {st.HoldMs}ms  |  Wait {st.PauseMs}ms", 10, "DS.TextMuted")
                        }
                    }
                };
                timeline.Children.Add(block);
            }
        }
        root.Children.Add(timeline);
        return root;
    }

    // ------------------------------------------------------------------
    // Shared Curve & Geometry Helpers
    // ------------------------------------------------------------------
    private static Border BuildCurveGraph(SetupCurve curve, StickTuning? tuning, double width, double height)
    {
        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
        double pad = 8;
        double gw = Math.Max(20, width - pad * 2);
        double gh = Math.Max(20, height - pad * 2);

        // Grid
        for (int g = 1; g < 4; g++)
        {
            double gx = pad + gw * g / 4.0;
            double gy = pad + gh * g / 4.0;
            canvas.Children.Add(new Line { X1 = gx, X2 = gx, Y1 = pad, Y2 = pad + gh, Stroke = new SolidColorBrush(Color.FromArgb(35, 148, 163, 184)), StrokeThickness = 1 });
            canvas.Children.Add(new Line { X1 = pad, X2 = pad + gw, Y1 = gy, Y2 = gy, Stroke = new SolidColorBrush(Color.FromArgb(35, 148, 163, 184)), StrokeThickness = 1 });
        }

        // Inner deadzone shaded band
        double innerFrac = Math.Clamp(curve.Inner / 100.0, 0, 0.95);
        if (innerFrac > 0.005)
        {
            var dzBand = new Rectangle
            {
                Width = gw * innerFrac,
                Height = gh,
                Fill = new SolidColorBrush(Color.FromArgb(38, 245, 158, 11))
            };
            Canvas.SetLeft(dzBand, pad);
            Canvas.SetTop(dzBand, pad);
            canvas.Children.Add(dzBand);
        }

        // Outer limit marker
        double outerFrac = Math.Clamp(curve.Outer / 100.0, innerFrac + 0.05, 1.0);
        if (outerFrac < 0.995)
        {
            double ox = pad + gw * outerFrac;
            canvas.Children.Add(new Line
            {
                X1 = ox, X2 = ox, Y1 = pad, Y2 = pad + gh,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 248, 113, 113)),
                StrokeDashArray = [2, 2],
                StrokeThickness = 1
            });
        }

        // 1:1 linear diagonal
        canvas.Children.Add(new Line
        {
            X1 = pad, Y1 = pad + gh, X2 = pad + gw, Y2 = pad,
            Stroke = new SolidColorBrush(Color.FromArgb(75, 148, 163, 184)),
            StrokeDashArray = [3, 3],
            StrokeThickness = 1
        });

        // Sampled response curve
        var pts = new PointCollection();
        for (int i = 0; i <= 24; i++)
        {
            double t = i / 24.0;
            double yNorm = Math.Clamp(PadProcessor.Response(curve, tuning, t), 0, 1);
            pts.Add(new Point(pad + t * gw, pad + gh - yNorm * gh));
        }
        canvas.Children.Add(new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            StrokeThickness = 2.4,
            Points = pts
        });

        // Control point dots (scaled into active inner..outer range)
        foreach (var (kx, ky) in new[] { (curve.X1, curve.Y1), (curve.X2, curve.Y2) })
        {
            double xNorm = innerFrac + (kx / 255.0) * (outerFrac - innerFrac);
            double yNorm = ky / 255.0;
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(dot, pad + xNorm * gw - 3);
            Canvas.SetTop(dot, pad + gh - yNorm * gh - 3);
            canvas.Children.Add(dot);
        }

        return new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(6, 10, 20)),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Child = canvas
        };
    }

    private static Border BuildDeadzoneRing(int innerPct, int outerPct, double size)
    {
        var canvas = new Canvas { Width = size, Height = size };
        double cx = size / 2.0, cy = size / 2.0;
        double maxR = (size - 12) / 2.0;

        // Outer full gate
        var fullGate = new Ellipse
        {
            Width = maxR * 2,
            Height = maxR * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(90, 148, 163, 184)),
            StrokeThickness = 1.2,
            Fill = new SolidColorBrush(Color.FromRgb(9, 15, 29))
        };
        Canvas.SetLeft(fullGate, cx - maxR);
        Canvas.SetTop(fullGate, cy - maxR);
        canvas.Children.Add(fullGate);

        // Active outer threshold ring
        double outerR = Math.Max(6, maxR * Math.Clamp(outerPct / 100.0, 0.1, 1.0));
        var outerRing = new Ellipse
        {
            Width = outerR * 2,
            Height = outerR * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Color.FromArgb(30, 56, 189, 248))
        };
        Canvas.SetLeft(outerRing, cx - outerR);
        Canvas.SetTop(outerRing, cy - outerR);
        canvas.Children.Add(outerRing);

        // Inner deadzone circle
        double innerR = Math.Max(3, maxR * Math.Clamp(innerPct / 100.0, 0.0, 0.95));
        var innerRing = new Ellipse
        {
            Width = innerR * 2,
            Height = innerR * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Color.FromArgb(75, 245, 158, 11))
        };
        Canvas.SetLeft(innerRing, cx - innerR);
        Canvas.SetTop(innerRing, cy - innerR);
        canvas.Children.Add(innerRing);

        // Crosshairs
        canvas.Children.Add(new Line { X1 = cx, X2 = cx, Y1 = 6, Y2 = size - 6, Stroke = new SolidColorBrush(Color.FromArgb(45, 148, 163, 184)), StrokeThickness = 1 });
        canvas.Children.Add(new Line { X1 = 6, X2 = size - 6, Y1 = cy, Y2 = cy, Stroke = new SolidColorBrush(Color.FromArgb(45, 148, 163, 184)), StrokeThickness = 1 });

        return new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(6, 10, 20)),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Child = canvas
        };
    }

    private static Border SpecTile(string label, string value) => new()
    {
        CornerRadius = new CornerRadius(6),
        Background = new SolidColorBrush(Color.FromRgb(12, 20, 38)),
        BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(8, 5, 8, 5),
        Margin = new Thickness(0, 0, 6, 6),
        Child = new StackPanel
        {
            Children =
            {
                Kit.Text(label, 9, "DS.TextMuted", FontWeights.Bold),
                Kit.Text(value, 12, "DS.Text", FontWeights.SemiBold)
            }
        }
    };

    private static StackPanel BuildMeterBar(string label, int val, int maxVal, Color fill)
    {
        double pct = Math.Clamp(val / (double)Math.Max(1, maxVal), 0, 1);
        var track = new Grid { Height = 7, Margin = new Thickness(0, 3, 0, 6) };
        track.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromRgb(18, 28, 50))
        });
        track.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = Math.Max(6, 220 * pct),
            Background = new SolidColorBrush(fill)
        });
        return new StackPanel
        {
            Children =
            {
                Kit.Text(label, 11, "DS.TextSoft"),
                track
            }
        };
    }

    private static string FormatChannelName(string ch) => ch switch
    {
        "LeftStick" => "Left Stick",
        "RightStick" => "Right Stick",
        "LeftTrigger" => "Left Trigger",
        "RightTrigger" => "Right Trigger",
        _ => ch
    };

    private static Color ParseHexColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        string s = hex.Trim().TrimStart('#');
        if (s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            return Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        return fallback;
    }

    private static Color ScaleColor(Color c, double factor) => Color.FromRgb(
        (byte)Math.Clamp(c.R * factor, 0, 255),
        (byte)Math.Clamp(c.G * factor, 0, 255),
        (byte)Math.Clamp(c.B * factor, 0, 255));

    private static Color HsvToColor(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h * 6) % 2 - 1));
        double m = v - c;
        (double r, double g, double b) = (h * 6) switch
        {
            < 1 => (c, x, 0d),
            < 2 => (x, c, 0d),
            < 3 => (0d, c, x),
            < 4 => (0d, x, c),
            < 5 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        return Color.FromRgb(
            (byte)Math.Clamp((r + m) * 255, 0, 255),
            (byte)Math.Clamp((g + m) * 255, 0, 255),
            (byte)Math.Clamp((b + m) * 255, 0, 255));
    }
}

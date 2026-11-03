using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

/// <summary>
/// PC Mode workbench presentation for the Yozakura (Blossom) theme:
/// "The route is a sacred path of stone lanterns.
/// When PC mode is enabled, 1000Hz holy fire illuminates their carved apertures."
/// Strictly implements t_blossom.py:pcmode().
/// </summary>
public partial class StudioScene
{
    private PcModePage? blossomPcSource;
    private bool blossomPcWired;
    private Action? blossomPcRefresh; // always the newest render's controls
    private string blossomPcSelectedRow = "switch"; // "switch", "profile", "turbo", "games", "share", "reset", "notice"

    private void RenderBlossomPcMode(Canvas host)
    {
        var original = ManagementBodyHost.Children.OfType<ScrollViewer>().FirstOrDefault();
        var source = original?.Content as PcModePage ?? blossomPcSource;
        if (source == null) return;

        if (original?.Content is PcModePage pmp)
        {
            blossomPcSource = pmp;
            original.Content = null;
        }

        bool pcOn = source.Switch.IsChecked == true;

        // 1. Sky & Backdrop
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(240, 160, 1320, 200), 8, 7));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "pcmode");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "PC Mode",
            FontFamily = BlossomKit.Display,
            FontSize = 52,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        host.Children.Add(titleTb);

        var leadTb = new TextBlock
        {
            Text = "Software processing on this PC. Hardware configuration stays unchanged.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        host.Children.Add(leadTb);

        // 3. Sacred Path of Stone Lanterns (Tōrō) at (230, 160, width 1330, height 160)
        double px0 = 230, py0 = 160, pw = 1330, ph = 160;

        var pathBack = new Border
        {
            Width = pw,
            Height = ph,
            CornerRadius = new CornerRadius(6),
            Background = BlossomKit.B("rgba(10,4,10,0.35)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.1)"),
            BorderThickness = new Thickness(1)
        };
        Canvas.SetLeft(pathBack, px0);
        Canvas.SetTop(pathBack, py0);
        host.Children.Add(pathBack);

        var pathCanvas = new Canvas { Width = pw, Height = ph };

        // Stone stepping stones between lanterns
        double[] xs = [170, 500, 830, 1160];
        for (int i = 0; i < 3; i++)
        {
            foreach (double f in new[] { 0.22, 0.40, 0.58, 0.76 })
            {
                double sx = xs[i] + (xs[i + 1] - xs[i]) * f;
                double sy = 126 + 4 * Math.Sin(f * 9);
                double rx = 22 - 6 * Math.Abs(f - 0.5);

                var stone = new Ellipse
                {
                    Width = rx * 2,
                    Height = 14,
                    Fill = BlossomKit.B("#3a2b34")
                };
                Canvas.SetLeft(stone, sx - rx);
                Canvas.SetTop(stone, sy - 7);
                pathCanvas.Children.Add(stone);
            }
        }

        // 4 Carved Granite Stone Lanterns along the sacred path
        var routeStages = new[]
        {
            ($"Physical XInput {PcModePage.Player + 1}", true),
            ("PC Profile", pcOn),
            ("Virtual Xbox", pcOn),
            ("Game Engine", true)
        };

        for (int i = 0; i < 4; i++)
        {
            double lx = xs[i];
            var toro = BlossomKit.StoneLanternToro(0.9, routeStages[i].Item2);
            Canvas.SetLeft(toro, lx - toro.Width / 2.0);
            Canvas.SetTop(toro, 4);
            pathCanvas.Children.Add(toro);

            var stLabel = new TextBlock
            {
                Text = routeStages[i].Item1,
                FontFamily = BlossomKit.Body,
                FontSize = 16,
                Foreground = routeStages[i].Item2 ? BlossomKit.TextWhiteBrush : BlossomKit.TextMuteBrush,
                TextAlignment = TextAlignment.Center,
                Width = 240
            };
            Canvas.SetLeft(stLabel, lx - 120);
            Canvas.SetTop(stLabel, 134);
            pathCanvas.Children.Add(stLabel);
        }
        pathBack.Child = pathCanvas;

        // Path Subtitle
        var pathSubTb = new TextBlock
        {
            Text = source.Pipeline.Length > 0 ? source.Pipeline.Replace("\n", " · ") : "Direct Kernel Pipeline · Sub-millisecond Hardware Frame Dispatch",
            FontFamily = BlossomKit.Body,
            FontSize = 14,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(pathSubTb, 236);
        Canvas.SetTop(pathSubTb, 332);
        host.Children.Add(pathSubTb);

        // 4. Bottom Left: Configuration Rows at (236, 360, width 640)
        var leftRows = new StackPanel { Width = 640 };

        // Live ViGEm status row
        var statusTb = new TextBlock
        {
            Text = source.StatusText,
            Foreground = BlossomKit.TextWhiteBrush,
            FontFamily = BlossomKit.Display,
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        leftRows.Children.Add(statusTb);

        // Right Side: Illuminated Washi Parchment Detail Panel at (920, 360, width 624, height 470)
        var litCard = BlossomKit.LitPaperCard(624, 470, new Thickness(28, 22, 28, 20));
        var cardStack = new StackPanel();

        var cardTitleTb = new TextBlock
        {
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.B(BlossomKit.LitPaperCrimson),
            Margin = new Thickness(0, 0, 0, 2)
        };
        cardStack.Children.Add(cardTitleTb);

        var cardMainTb = new TextBlock
        {
            FontFamily = BlossomKit.Display,
            FontSize = 26,
            Foreground = BlossomKit.B(BlossomKit.LitPaperInk),
            Margin = new Thickness(0, 0, 0, 6)
        };
        cardStack.Children.Add(cardMainTb);

        var cardBodyPanel = new StackPanel();
        cardStack.Children.Add(cardBodyPanel);
        litCard.Child = new ScrollViewer { Content = cardStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Canvas.SetLeft(litCard, 920);
        Canvas.SetTop(litCard, 360);
        host.Children.Add(litCard);

        var rowBorders = new Dictionary<string, Border>();

        void RebuildBlossomDetail()
        {
            foreach (var (k, b) in rowBorders)
            {
                bool active = k == blossomPcSelectedRow;
                b.Background = active ? BlossomKit.B("rgba(255,220,230,0.12)") : Brushes.Transparent;
            }

            cardBodyPanel.Children.Clear();

            switch (blossomPcSelectedRow)
            {
                case "profile":
                    cardTitleTb.Text = "ACTIVE PROFILE";
                    cardMainTb.Text = "Settings profile";
                    cardBodyPanel.Children.Add(new TextBlock
                    {
                        Text = $"Player {PcModePage.Player + 1} Profile · Input index {PcModePage.Player + 1}. Game-specific profiles activate automatically when their executable runs in foreground.",
                        FontFamily = BlossomKit.Body,
                        FontSize = 13.5,
                        Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 12)
                    });
                    var profWrap = new WrapPanel();
                    foreach (var (id, label) in source.ProfileChoices)
                    {
                        string pKey = id;
                        bool on = id == PcModePage.ProfileKey;
                        var pBorder = new Border
                        {
                            Height = 34,
                            Padding = new Thickness(14, 0, 14, 0),
                            CornerRadius = new CornerRadius(17),
                            Background = on ? BlossomKit.B(BlossomKit.LitPaperInk) : BlossomKit.B("rgba(59,23,18,0.1)"),
                            Margin = new Thickness(0, 0, 8, 8),
                            Cursor = Cursors.Hand,
                            Child = new TextBlock
                            {
                                Text = label.Split("  ·  ")[0],
                                FontFamily = BlossomKit.Body,
                                FontSize = 13,
                                Foreground = on ? BlossomKit.B("#ffe2b8") : BlossomKit.B(BlossomKit.LitPaperInk),
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        };
                        pBorder.MouseLeftButtonDown += (_, _) =>
                        {
                            source.PickProfile(pKey);
                            RebuildBlossomDetail();
                        };
                        profWrap.Children.Add(pBorder);
                    }
                    cardBodyPanel.Children.Add(profWrap);
                    break;

                case "turbo":
                    cardTitleTb.Text = "RAPID REPETITION";
                    cardMainTb.Text = "Turbo bindings";
                    cardBodyPanel.Children.Add(new TextBlock
                    {
                        Text = "Holding a button with turbo enabled repeats presses at the chosen rate. Press a button to step its frequency.",
                        FontFamily = BlossomKit.Body,
                        FontSize = 13.5,
                        Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 10)
                    });
                    var turboGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
                    foreach (var (id, label, rate) in source.TurboChoices)
                    {
                        string btnId = id;
                        bool hasTurbo = rate != "No turbo";
                        var tCell = new Border
                        {
                            Height = 34,
                            Margin = new Thickness(0, 0, 6, 6),
                            Padding = new Thickness(8, 0, 8, 0),
                            CornerRadius = new CornerRadius(17),
                            Background = hasTurbo ? BlossomKit.B(BlossomKit.LitPaperInk) : BlossomKit.B("rgba(59,23,18,0.08)"),
                            Cursor = Cursors.Hand
                        };
                        var tGrid = new Grid();
                        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        tGrid.Children.Add(new TextBlock { Text = label, FontFamily = BlossomKit.Body, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = hasTurbo ? BlossomKit.B("#ffe2b8") : BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center });
                        var rateTb = new TextBlock { Text = hasTurbo ? rate : "Off", FontFamily = BlossomKit.Body, FontSize = 11, Foreground = hasTurbo ? BlossomKit.B("#ffd3a8") : BlossomKit.B(BlossomKit.LitPaperSub), VerticalAlignment = VerticalAlignment.Center };
                        Grid.SetColumn(rateTb, 1);
                        tGrid.Children.Add(rateTb);
                        tCell.Child = tGrid;
                        tCell.MouseLeftButtonDown += (_, _) =>
                        {
                            source.CycleTurbo(btnId, 1);
                            RebuildBlossomDetail();
                        };
                        turboGrid.Children.Add(tCell);
                    }
                    cardBodyPanel.Children.Add(turboGrid);
                    break;

                case "games":
                    cardTitleTb.Text = "TITLES";
                    cardMainTb.Text = "Give a game its own settings";
                    cardBodyPanel.Children.Add(new TextBlock
                    {
                        Text = "Create game-specific profiles that automatically activate when that title is in the foreground.",
                        FontFamily = BlossomKit.Body,
                        FontSize = 13.5,
                        Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 10)
                    });
                    if (source.Picking)
                    {
                        var appWrap = new WrapPanel();
                        foreach (var (id, label) in source.AppChoices.Take(8))
                        {
                            string appId = id;
                            var appBtn = new Border
                            {
                                Height = 32,
                                Padding = new Thickness(12, 0, 12, 0),
                                CornerRadius = new CornerRadius(16),
                                Background = BlossomKit.B("rgba(59,23,18,0.12)"),
                                Margin = new Thickness(0, 0, 6, 6),
                                Cursor = Cursors.Hand,
                                Child = new TextBlock { Text = label.Split("  ·  ")[0], FontFamily = BlossomKit.Body, FontSize = 12, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center }
                            };
                            appBtn.MouseLeftButtonDown += (_, _) => { source.AddGameById(appId); RebuildBlossomDetail(); };
                            appWrap.Children.Add(appBtn);
                        }
                        var cancelAppBtn = new Border
                        {
                            Height = 32,
                            Padding = new Thickness(12, 0, 12, 0),
                            CornerRadius = new CornerRadius(16),
                            Background = BlossomKit.B("rgba(59,23,18,0.2)"),
                            Cursor = Cursors.Hand,
                            Child = new TextBlock { Text = "Cancel", FontFamily = BlossomKit.Body, FontSize = 12, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center }
                        };
                        cancelAppBtn.MouseLeftButtonDown += (_, _) => { source.CancelPicker(); RebuildBlossomDetail(); };
                        appWrap.Children.Add(cancelAppBtn);
                        cardBodyPanel.Children.Add(appWrap);
                    }
                    else
                    {
                        var findBtn = new Border
                        {
                            Height = 40,
                            Padding = new Thickness(20, 0, 20, 0),
                            CornerRadius = new CornerRadius(20),
                            Background = BlossomKit.CreateWarmLanternBrush(),
                            Cursor = Cursors.Hand,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Child = new TextBlock { Text = "Find open games", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B("#4a140f"), VerticalAlignment = VerticalAlignment.Center }
                        };
                        cardBodyPanel.Children.Add(ThemeAction(findBtn, "theme:pc:find", () => { source.FindGames(); RebuildBlossomDetail(); }));
                    }
                    if (source.IsGame)
                    {
                        var remBtn = new Border
                        {
                            Height = 36,
                            Padding = new Thickness(16, 0, 16, 0),
                            CornerRadius = new CornerRadius(18),
                            Background = BlossomKit.B("rgba(180,40,40,0.15)"),
                            Margin = new Thickness(0, 10, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Cursor = Cursors.Hand,
                            Child = new TextBlock { Text = "Remove this game profile", FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.B("#8a1f18"), VerticalAlignment = VerticalAlignment.Center }
                        };
                        cardBodyPanel.Children.Add(ThemeAction(remBtn, "theme:pc:remove-game", () => { source.RemoveThisGame(); RebuildBlossomDetail(); }));
                    }
                    break;

                case "share":
                    cardTitleTb.Text = "PRESERVATION";
                    cardMainTb.Text = "Import / export PC profile";
                    cardBodyPanel.Children.Add(new TextBlock
                    {
                        Text = "Share these settings as a file or a code, or bring someone else's in. What comes in replaces the settings being edited.",
                        FontFamily = BlossomKit.Body,
                        FontSize = 13.5,
                        Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 12)
                    });
                    var shareWrap = new WrapPanel();
                    foreach (var (lbl, act) in new (string, Action)[] { ("Save to a file", source.SaveFile), ("Open a file", source.OpenFile), ("Copy as a code", source.CopyAsCode), ("Paste a code", source.PasteAsCode) })
                    {
                        Action action = act;
                        var shBtn = new Border
                        {
                            Height = 36,
                            Padding = new Thickness(16, 0, 16, 0),
                            CornerRadius = new CornerRadius(18),
                            Background = BlossomKit.B("rgba(59,23,18,0.1)"),
                            Margin = new Thickness(0, 0, 10, 10),
                            Cursor = Cursors.Hand,
                            Child = new TextBlock { Text = lbl, FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center }
                        };
                        shareWrap.Children.Add(ThemeAction(shBtn, "theme:pc:share:" + lbl, action));
                    }
                    cardBodyPanel.Children.Add(shareWrap);
                    break;

                case "reset":
                    cardTitleTb.Text = "PURIFICATION";
                    cardMainTb.Text = $"Reset Player {PcModePage.Player + 1} profile";
                    cardBodyPanel.Children.Add(new TextBlock
                    {
                        Text = "Reset the selected player's current PC profile? This clears its buttons, curves, stick tools, macros and turbo.",
                        FontFamily = BlossomKit.Body,
                        FontSize = 13.5,
                        Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 14)
                    });
                    var resetFace = new Border
                    {
                        Height = 40,
                        Padding = new Thickness(20, 0, 20, 0),
                        CornerRadius = new CornerRadius(20),
                        Background = BlossomKit.B("rgba(180,40,40,0.18)"),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Cursor = Cursors.Hand,
                        Child = new TextBlock { Text = "Reset selected profile", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B("#8a1f18"), VerticalAlignment = VerticalAlignment.Center }
                    };
                    cardBodyPanel.Children.Add(ThemeAction(resetFace, "theme:pc:reset", () =>
                    {
                        source.ResetProfile();
                        RebuildBlossomDetail();
                    }));
                    break;

                case "notice":
                default:
                    cardTitleTb.Text = "SAFETY PREREQUISITES";
                    cardMainTb.Text = "Before PC mode starts";
                    for (int i = 0; i < PcModePage.NoticeLines.Length; i++)
                    {
                        string line = PcModePage.NoticeLines[i];
                        if (line.Contains(". ")) line = line[(line.IndexOf(". ") + 2)..];
                        var nRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                        var badge = new Border
                        {
                            Width = 24,
                            Height = 24,
                            CornerRadius = new CornerRadius(12),
                            Background = BlossomKit.B(BlossomKit.LitPaperCrimson),
                            VerticalAlignment = VerticalAlignment.Top,
                            Child = new TextBlock { Text = (i + 1).ToString(), FontFamily = BlossomKit.Body, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = BlossomKit.B("#ffffff"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                        };
                        DockPanel.SetDock(badge, Dock.Left);
                        nRow.Children.Add(badge);
                        nRow.Children.Add(new TextBlock
                        {
                            Text = line,
                            FontFamily = BlossomKit.Body,
                            FontSize = 12.5,
                            Foreground = BlossomKit.B(BlossomKit.LitPaperInk),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(10, 0, 0, 0)
                        });
                        cardBodyPanel.Children.Add(nRow);
                    }
                    var agreeFace = new Border
                    {
                        Height = 40,
                        Padding = new Thickness(22, 0, 22, 0),
                        CornerRadius = new CornerRadius(20),
                        Background = BlossomKit.CreateWarmLanternBrush(),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Cursor = Cursors.Hand,
                        Child = new TextBlock { Text = "I understand · Accept", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B("#4a140f"), VerticalAlignment = VerticalAlignment.Center }
                    };
                    cardBodyPanel.Children.Add(ThemeAction(agreeFace, "theme:pc:agree", () =>
                    {
                        source.Accept();
                        blossomPcSelectedRow = "switch";
                        RebuildBlossomDetail();
                    }));
                    break;
            }
        }

        // Left rows
        // Row 0: Switch
        var swBorder = new Border { Height = 50, BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 0, 12, 0), Cursor = Cursors.Hand };
        var swGrid = new Grid();
        swGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        swGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        swGrid.Children.Add(new TextBlock { Text = "PC mode pipeline", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        var swToggle = new CheckBox { Content = "Enabled", Foreground = BlossomKit.TextWhiteBrush, FontFamily = BlossomKit.Body, FontSize = 14, Tag = "theme:pc:switch", VerticalAlignment = VerticalAlignment.Center };
        swToggle.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = source.Switch, Mode = BindingMode.TwoWay });
        Grid.SetColumn(swToggle, 1);
        swGrid.Children.Add(swToggle);
        swBorder.Child = swGrid;
        rowBorders["switch"] = swBorder;
        leftRows.Children.Add(ThemeAction(swBorder, "theme:pc:row:switch", () => { blossomPcSelectedRow = "switch"; RebuildBlossomDetail(); }));

        // Row 1: Profile
        var profBorder = new Border { Height = 50, BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 0, 12, 0), Cursor = Cursors.Hand };
        var profGrid = new Grid();
        profGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        profGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        profGrid.Children.Add(new TextBlock { Text = "Settings profile", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        var profCurTb = new TextBlock { Text = source.ProfileChoices.FirstOrDefault(c => c.Id == PcModePage.ProfileKey).Label?.Split("  ·  ")[0] ?? "Default", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextAmberBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(profCurTb, 1);
        profGrid.Children.Add(profCurTb);
        profBorder.Child = profGrid;
        rowBorders["profile"] = profBorder;
        leftRows.Children.Add(ThemeAction(profBorder, "theme:pc:row:profile", () => { blossomPcSelectedRow = "profile"; RebuildBlossomDetail(); }));

        // Row 2: Turbo
        var turboBorder = new Border { Height = 50, BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 0, 12, 0), Cursor = Cursors.Hand };
        var turboGrid_ = new Grid();
        turboGrid_.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        turboGrid_.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        turboGrid_.Children.Add(new TextBlock { Text = "Turbo bindings", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        var turboCurTb = new TextBlock { Text = source.TurboCount == 0 ? "No turbo" : $"{source.TurboCount} active", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextMuteBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(turboCurTb, 1);
        turboGrid_.Children.Add(turboCurTb);
        turboBorder.Child = turboGrid_;
        rowBorders["turbo"] = turboBorder;
        leftRows.Children.Add(ThemeAction(turboBorder, "theme:pc:row:turbo", () => { blossomPcSelectedRow = "turbo"; RebuildBlossomDetail(); }));

        // Row 3: Games
        var gamesBorder = new Border { Height = 50, BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 0, 12, 0), Cursor = Cursors.Hand };
        var gamesGrid = new Grid();
        gamesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gamesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        gamesGrid.Children.Add(new TextBlock { Text = "Give a game its own settings", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        var gamesChev = new TextBlock { Text = "›", FontFamily = BlossomKit.Display, FontSize = 18, Foreground = BlossomKit.TextMuteBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(gamesChev, 1);
        gamesGrid.Children.Add(gamesChev);
        gamesBorder.Child = gamesGrid;
        rowBorders["games"] = gamesBorder;
        leftRows.Children.Add(ThemeAction(gamesBorder, "theme:pc:row:games", () => { blossomPcSelectedRow = "games"; RebuildBlossomDetail(); }));

        // Row 4: Share
        var shareBorder = new Border { Height = 50, BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 0, 12, 0), Cursor = Cursors.Hand };
        var shareGrid = new Grid();
        shareGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        shareGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        shareGrid.Children.Add(new TextBlock { Text = "Import / export PC profile", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        var shareChev = new TextBlock { Text = "›", FontFamily = BlossomKit.Display, FontSize = 18, Foreground = BlossomKit.TextMuteBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(shareChev, 1);
        shareGrid.Children.Add(shareChev);
        shareBorder.Child = shareGrid;
        rowBorders["share"] = shareBorder;
        leftRows.Children.Add(ThemeAction(shareBorder, "theme:pc:row:share", () => { blossomPcSelectedRow = "share"; RebuildBlossomDetail(); }));

        // Row 5: Reset
        var resetBorder = new Border { Height = 50, BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 0, 12, 0), Cursor = Cursors.Hand };
        var resetGrid = new Grid();
        resetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        resetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        resetGrid.Children.Add(new TextBlock { Text = $"Reset Player {PcModePage.Player + 1} profile", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        var resetChev = new TextBlock { Text = "›", FontFamily = BlossomKit.Display, FontSize = 18, Foreground = BlossomKit.TextMuteBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(resetChev, 1);
        resetGrid.Children.Add(resetChev);
        resetBorder.Child = resetGrid;
        rowBorders["reset"] = resetBorder;
        leftRows.Children.Add(ThemeAction(resetBorder, "theme:pc:row:reset", () => { blossomPcSelectedRow = "reset"; RebuildBlossomDetail(); }));

        // Row 6: Notice
        var noticeBorder = new Border { Height = 50, BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 0, 12, 0), Cursor = Cursors.Hand };
        var noticeGrid = new Grid();
        noticeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        noticeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        noticeGrid.Children.Add(new TextBlock { Text = "Before PC mode starts", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.TextSakuraBrush, VerticalAlignment = VerticalAlignment.Center });
        var noticeChev = new TextBlock { Text = "›", FontFamily = BlossomKit.Display, FontSize = 18, Foreground = BlossomKit.TextSakuraBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(noticeChev, 1);
        noticeGrid.Children.Add(noticeChev);
        noticeBorder.Child = noticeGrid;
        rowBorders["notice"] = noticeBorder;
        leftRows.Children.Add(ThemeAction(noticeBorder, "theme:pc:row:notice", () => { blossomPcSelectedRow = "notice"; RebuildBlossomDetail(); }));

        // Navigation links
        var navStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        foreach (var (pKey, pLabel) in new[] { ("buttons", "Edit Buttons"), ("curves", "Edit Curves"), ("macros", "Edit Macros") })
        {
            string pk = pKey;
            var navBtn = new Border
            {
                Height = 34,
                Padding = new Thickness(14, 0, 14, 0),
                CornerRadius = new CornerRadius(17),
                Background = BlossomKit.B("rgba(255,220,232,0.06)"),
                BorderBrush = BlossomKit.B("rgba(255,220,232,0.2)"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = pLabel, FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            navStack.Children.Add(ThemeAction(navBtn, "theme:pc:edit:" + pk, () => BlossomSelectPage(pk)));
        }
        leftRows.Children.Add(navStack);

        Canvas.SetLeft(leftRows, 236);
        Canvas.SetTop(leftRows, 360);
        host.Children.Add(leftRows);

        if (source.NoticeShown) blossomPcSelectedRow = "notice";
        RebuildBlossomDetail();

        blossomPcRefresh = () =>
            {
                statusTb.Text = source.StatusText;
                profCurTb.Text = source.ProfileChoices.FirstOrDefault(c => c.Id == PcModePage.ProfileKey).Label?.Split("  ·  ")[0] ?? "Default";
                turboCurTb.Text = source.TurboCount == 0 ? "No turbo" : $"{source.TurboCount} active";
                pathSubTb.Text = source.Pipeline.Length > 0 ? source.Pipeline.Replace("\n", " · ") : "Direct Kernel Pipeline";
                if (source.NoticeShown && blossomPcSelectedRow != "notice")
                {
                    blossomPcSelectedRow = "notice";
                    RebuildBlossomDetail();
                }
            };
        // One subscription for the page's life; it reaches whichever render is current.
        if (!blossomPcWired) { blossomPcWired = true; source.Watch(() => blossomPcRefresh?.Invoke()); }
    }
}

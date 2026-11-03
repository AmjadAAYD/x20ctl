using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private PcModePage? voltPcSource;
    private bool voltPcWired;
    private Action? voltPcRefresh; // always the newest render's controls
    private string voltPcSelectedRow = "switch"; // "switch", "profile", "turbo", "games", "share", "reset", "notice"

    /// <summary>
    /// PC Mode workbench for Volt: Protocol routing, ViGEmBus status, and virtual peripheral management.
    /// Strictly implements t_volt.py:253 Volt and models.py:pcmode.
    /// </summary>
    private UIElement BuildVoltPcMode()
    {
        var original = ManagementBodyHost.Children.OfType<ScrollViewer>().FirstOrDefault();
        var source = original?.Content as PcModePage ?? voltPcSource;
        if (source == null)
        {
            var empty = new Canvas { Width = 970, Height = 560 };
            empty.Children.Add(new TextBlock { Text = "PC Mode is unavailable.", Foreground = VoltKit.MuteBrush, FontSize = 18, Margin = new Thickness(40) });
            return VoltKit.TwoPaneShell("pcmode", "PC Mode", "Desktop virtualization, ViGEmBus routing, and software controller profiles.", empty, VoltSelectPage);
        }

        if (original?.Content is PcModePage pmp)
        {
            voltPcSource = pmp;
            original.Content = null;
        }

        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Top Route Flow Diagram (width: 950, height: 60 at y = 0)
        var routeBox = new Border
        {
            Width = 950,
            Height = 60,
            Background = VoltKit.B("#121212"),
            BorderBrush = VoltKit.B("#262626"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 8, 16, 8)
        };
        var routeGrid = new Grid();
        for (int i = 0; i < 7; i++)
        {
            routeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = (i % 2 == 0) ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        }

        string[] stopNames = [$"Physical XInput {PcModePage.Player + 1}", "PC Profile", "Virtual Xbox", "Game Engine"];
        for (int i = 0; i < 4; i++)
        {
            var stopPill = new Border
            {
                Height = 36,
                CornerRadius = new CornerRadius(6),
                Background = (source.RouteActive && i > 0) ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                BorderBrush = (source.RouteActive && i > 0) ? VoltKit.AmberHotBrush : VoltKit.B("#333333"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = stopNames[i],
                    FontFamily = VoltKit.Display,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (source.RouteActive && i > 0) ? VoltKit.B("#111111") : VoltKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(stopPill, i * 2);
            routeGrid.Children.Add(stopPill);

            if (i < 3)
            {
                var arrow = new TextBlock
                {
                    Text = "→",
                    FontFamily = VoltKit.Display,
                    FontSize = 18,
                    Foreground = source.RouteActive ? VoltKit.AmberHotBrush : VoltKit.B("#555555"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 8, 0)
                };
                Grid.SetColumn(arrow, i * 2 + 1);
                routeGrid.Children.Add(arrow);
            }
        }
        routeBox.Child = routeGrid;
        Canvas.SetLeft(routeBox, 0);
        Canvas.SetTop(routeBox, 0);
        content.Children.Add(routeBox);

        // 2. Left Rows Stack (width: 520, height: 480 at y = 70)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        // ViGEm live status text
        var stateTb = new TextBlock
        {
            Text = source.StatusText,
            Foreground = VoltKit.WhiteBrush,
            FontFamily = VoltKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(4, 4, 4, 8)
        };
        leftStack.Children.Add(stateTb);

        // Right details elements references
        var card = new Border
        {
            Width = 410,
            Background = VoltKit.SheetDarkBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24, 20, 24, 20)
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };
        card.Child = cStack;

        var detailTitleTb = new TextBlock
        {
            FontFamily = VoltKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 2)
        };
        cStack.Children.Add(detailTitleTb);

        var detailMainTb = new TextBlock
        {
            FontFamily = VoltKit.Display,
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush,
            LineHeight = 30
        };
        cStack.Children.Add(detailMainTb);

        var detailBodyPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        cStack.Children.Add(detailBodyPanel);

        var rowBorders = new Dictionary<string, Border>();

        void RebuildDetailPanel()
        {
            foreach (var (k, b) in rowBorders)
            {
                bool active = k == voltPcSelectedRow;
                b.Background = active ? VoltKit.B("#2a2a2a") : Brushes.Transparent;
                b.BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#222222");
                b.BorderThickness = new Thickness(active ? 2 : 0, active ? 2 : 0, active ? 2 : 0, active ? 2 : 1);
                b.CornerRadius = active ? new CornerRadius(8) : new CornerRadius(0);
            }

            detailBodyPanel.Children.Clear();

            switch (voltPcSelectedRow)
            {
                case "profile":
                    detailTitleTb.Text = "ACTIVE PROFILE";
                    detailMainTb.Text = "Settings Profile";
                    detailBodyPanel.Children.Add(new TextBlock
                    {
                        Text = $"Player {PcModePage.Player + 1} Profile · Input index {PcModePage.Player + 1}. Game-specific profiles activate automatically when their executable runs.",
                        FontFamily = VoltKit.Body,
                        FontSize = 13,
                        Foreground = VoltKit.B("#bdbdbd"),
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
                            CornerRadius = new CornerRadius(6),
                            Background = on ? VoltKit.AmberHotBrush : VoltKit.B("#262626"),
                            Margin = new Thickness(0, 0, 8, 8),
                            Cursor = Cursors.Hand,
                            Child = new TextBlock
                            {
                                Text = label.Split("  ·  ")[0],
                                FontFamily = VoltKit.Display,
                                FontSize = 13,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = on ? VoltKit.B("#111111") : VoltKit.WhiteBrush,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        };
                        pBorder.MouseLeftButtonDown += (_, _) =>
                        {
                            source.PickProfile(pKey);
                            RebuildDetailPanel();
                        };
                        profWrap.Children.Add(pBorder);
                    }
                    detailBodyPanel.Children.Add(profWrap);
                    break;

                case "turbo":
                    detailTitleTb.Text = "RAPID FIRE";
                    detailMainTb.Text = "Turbo Bindings";
                    detailBodyPanel.Children.Add(new TextBlock
                    {
                        Text = "Hold a button with turbo enabled to repeat clicks at the chosen frequency. Click a button to step rate.",
                        FontFamily = VoltKit.Body,
                        FontSize = 13,
                        Foreground = VoltKit.B("#bdbdbd"),
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
                            CornerRadius = new CornerRadius(6),
                            Background = hasTurbo ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                            Cursor = Cursors.Hand
                        };
                        var tGrid = new Grid();
                        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        tGrid.Children.Add(new TextBlock { Text = label, FontFamily = VoltKit.Display, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = hasTurbo ? VoltKit.B("#111111") : VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
                        var rateTb = new TextBlock { Text = hasTurbo ? rate : "Off", FontFamily = VoltKit.Display, FontSize = 11, Foreground = hasTurbo ? VoltKit.B("#111111") : VoltKit.MuteBrush, VerticalAlignment = VerticalAlignment.Center };
                        Grid.SetColumn(rateTb, 1);
                        tGrid.Children.Add(rateTb);
                        tCell.Child = tGrid;
                        tCell.MouseLeftButtonDown += (_, _) =>
                        {
                            source.CycleTurbo(btnId, 1);
                            RebuildDetailPanel();
                        };
                        turboGrid.Children.Add(tCell);
                    }
                    detailBodyPanel.Children.Add(turboGrid);
                    break;

                case "games":
                    detailTitleTb.Text = "AUTO SWITCHING";
                    detailMainTb.Text = "Per-Game Profiles";
                    detailBodyPanel.Children.Add(new TextBlock
                    {
                        Text = "Create game-specific profiles that automatically activate when that title is running in foreground.",
                        FontFamily = VoltKit.Body,
                        FontSize = 13,
                        Foreground = VoltKit.B("#bdbdbd"),
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
                                CornerRadius = new CornerRadius(6),
                                Background = VoltKit.B("#262626"),
                                Margin = new Thickness(0, 0, 6, 6),
                                Cursor = Cursors.Hand,
                                Child = new TextBlock { Text = label.Split("  ·  ")[0], FontFamily = VoltKit.Display, FontSize = 12, Foreground = VoltKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center }
                            };
                            appBtn.MouseLeftButtonDown += (_, _) => { source.AddGameById(appId); RebuildDetailPanel(); };
                            appWrap.Children.Add(appBtn);
                        }
                        var cancelAppBtn = new Border
                        {
                            Height = 32,
                            Padding = new Thickness(12, 0, 12, 0),
                            CornerRadius = new CornerRadius(6),
                            Background = VoltKit.B("#333333"),
                            Cursor = Cursors.Hand,
                            Child = new TextBlock { Text = "Cancel", FontFamily = VoltKit.Display, FontSize = 12, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center }
                        };
                        cancelAppBtn.MouseLeftButtonDown += (_, _) => { source.CancelPicker(); RebuildDetailPanel(); };
                        appWrap.Children.Add(cancelAppBtn);
                        detailBodyPanel.Children.Add(appWrap);
                    }
                    else
                    {
                        var findBtn = VoltKit.OrangeActionButton("Find Open Games", 362, 42, null);
                        detailBodyPanel.Children.Add(ThemeAction(findBtn, "theme:pc:find", () => { source.FindGames(); RebuildDetailPanel(); }));
                    }
                    if (source.IsGame)
                    {
                        var remBtn = new Border
                        {
                            Height = 36,
                            CornerRadius = new CornerRadius(6),
                            Background = VoltKit.B("#3a1515"),
                            Margin = new Thickness(0, 10, 0, 0),
                            Cursor = Cursors.Hand,
                            Child = new TextBlock { Text = "Remove This Game Profile", FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(VoltKit.C("#ff6b6b")), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                        };
                        detailBodyPanel.Children.Add(ThemeAction(remBtn, "theme:pc:remove-game", () => { source.RemoveThisGame(); RebuildDetailPanel(); }));
                    }
                    break;

                case "share":
                    detailTitleTb.Text = "IMPORT & EXPORT";
                    detailMainTb.Text = "Profile Sharing";
                    detailBodyPanel.Children.Add(new TextBlock
                    {
                        Text = "Export your complete PC Mode profile to a file or text code, or import profiles shared by other players.",
                        FontFamily = VoltKit.Body,
                        FontSize = 13,
                        Foreground = VoltKit.B("#bdbdbd"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 12)
                    });
                    var shareStack = new StackPanel();
                    var shRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                    var saveFileBtn = new Border { Width = 175, Height = 38, CornerRadius = new CornerRadius(6), Background = VoltKit.B("#262626"), Margin = new Thickness(0, 0, 12, 0), Cursor = Cursors.Hand, Child = new TextBlock { Text = "Save to File", FontFamily = VoltKit.Display, FontSize = 13, Foreground = VoltKit.WhiteBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                    shRow1.Children.Add(ThemeAction(saveFileBtn, "theme:pc:savefile", () => source.SaveFile()));
                    var openFileBtn = new Border { Width = 175, Height = 38, CornerRadius = new CornerRadius(6), Background = VoltKit.B("#262626"), Cursor = Cursors.Hand, Child = new TextBlock { Text = "Open File", FontFamily = VoltKit.Display, FontSize = 13, Foreground = VoltKit.WhiteBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                    shRow1.Children.Add(ThemeAction(openFileBtn, "theme:pc:openfile", () => source.OpenFile()));
                    shareStack.Children.Add(shRow1);

                    var shRow2 = new StackPanel { Orientation = Orientation.Horizontal };
                    var copyCodeBtn = new Border { Width = 175, Height = 38, CornerRadius = new CornerRadius(6), Background = VoltKit.B("#262626"), Margin = new Thickness(0, 0, 12, 0), Cursor = Cursors.Hand, Child = new TextBlock { Text = "Copy as Code", FontFamily = VoltKit.Display, FontSize = 13, Foreground = VoltKit.WhiteBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                    shRow2.Children.Add(ThemeAction(copyCodeBtn, "theme:pc:copycode", () => source.CopyAsCode()));
                    var pasteCodeBtn = new Border { Width = 175, Height = 38, CornerRadius = new CornerRadius(6), Background = VoltKit.B("#262626"), Cursor = Cursors.Hand, Child = new TextBlock { Text = "Paste Code", FontFamily = VoltKit.Display, FontSize = 13, Foreground = VoltKit.WhiteBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                    shRow2.Children.Add(ThemeAction(pasteCodeBtn, "theme:pc:pastecode", () => source.PasteAsCode()));
                    shareStack.Children.Add(shRow2);
                    detailBodyPanel.Children.Add(shareStack);
                    break;

                case "reset":
                    detailTitleTb.Text = "RESET CONFIGURATION";
                    detailMainTb.Text = "Reset Profile";
                    detailBodyPanel.Children.Add(new TextBlock
                    {
                        Text = $"Reset all PC Mode settings for Player {PcModePage.Player + 1}? Buttons, curves, stick tools, macros and turbo will return to defaults.",
                        FontFamily = VoltKit.Body,
                        FontSize = 13,
                        Foreground = VoltKit.B("#bdbdbd"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 14)
                    });
                    var resetFace = VoltKit.OrangeActionButton("Reset Selected Profile", 362, 42, null);
                    detailBodyPanel.Children.Add(ThemeAction(resetFace, "theme:pc:reset", () =>
                    {
                        source.ResetProfile();
                        RebuildDetailPanel();
                    }));
                    break;

                case "notice":
                default:
                    detailTitleTb.Text = "SAFETY PREREQUISITES";
                    detailMainTb.Text = "Before PC Mode Starts";
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
                            Background = VoltKit.AmberHotBrush,
                            VerticalAlignment = VerticalAlignment.Top,
                            Child = new TextBlock { Text = (i + 1).ToString(), FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = VoltKit.B("#111111"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                        };
                        DockPanel.SetDock(badge, Dock.Left);
                        nRow.Children.Add(badge);
                        nRow.Children.Add(new TextBlock
                        {
                            Text = line,
                            FontFamily = VoltKit.Body,
                            FontSize = 12.5,
                            Foreground = VoltKit.LightBrush,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(10, 0, 0, 0)
                        });
                        detailBodyPanel.Children.Add(nRow);
                    }
                    var agreeFace = VoltKit.OrangeActionButton("I Understand · Accept", 362, 42, null);
                    detailBodyPanel.Children.Add(ThemeAction(agreeFace, "theme:pc:agree", () =>
                    {
                        source.Accept();
                        voltPcSelectedRow = "switch";
                        RebuildDetailPanel();
                    }));
                    break;
            }
        }

        // Row 0: PC Mode switch row
        var swRow = new Border { MinHeight = 54, Padding = new Thickness(14, 4, 14, 4), Cursor = Cursors.Hand };
        var swGrid = new Grid();
        swGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        swGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var swLabels = new StackPanel();
        swLabels.Children.Add(new TextBlock { Text = "Virtual Controller Routing", FontFamily = VoltKit.Display, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.LightBrush });
        swLabels.Children.Add(new TextBlock { Text = "Emulate XInput through ViGEmBus", FontFamily = VoltKit.Body, FontSize = 12, Foreground = VoltKit.MuteBrush });
        swGrid.Children.Add(swLabels);
        var swToggle = new CheckBox { Content = "Enabled", Foreground = VoltKit.WhiteBrush, FontFamily = VoltKit.Display, FontSize = 14, Tag = "theme:pc:switch", VerticalAlignment = VerticalAlignment.Center };
        swToggle.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Source = source.Switch, Mode = BindingMode.TwoWay });
        Grid.SetColumn(swToggle, 1);
        swGrid.Children.Add(swToggle);
        swRow.Child = swGrid;
        rowBorders["switch"] = swRow;
        leftStack.Children.Add(ThemeAction(swRow, "theme:pc:row:switch", () => { voltPcSelectedRow = "switch"; RebuildDetailPanel(); }));

        // Row 1: Profile row
        var profRow = new Border { MinHeight = 50, Padding = new Thickness(14, 4, 14, 4), Cursor = Cursors.Hand };
        var profGrid = new Grid();
        profGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        profGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        profGrid.Children.Add(new TextBlock { Text = "Settings Profile", FontFamily = VoltKit.Display, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
        var profCurTb = new TextBlock { Text = source.ProfileChoices.FirstOrDefault(c => c.Id == PcModePage.ProfileKey).Label?.Split("  ·  ")[0] ?? "Default", FontFamily = VoltKit.Display, FontSize = 14, Foreground = VoltKit.AmberHotBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(profCurTb, 1);
        profGrid.Children.Add(profCurTb);
        profRow.Child = profGrid;
        rowBorders["profile"] = profRow;
        leftStack.Children.Add(ThemeAction(profRow, "theme:pc:row:profile", () => { voltPcSelectedRow = "profile"; RebuildDetailPanel(); }));

        // Row 2: Turbo bindings
        var turboRow = new Border { MinHeight = 50, Padding = new Thickness(14, 4, 14, 4), Cursor = Cursors.Hand };
        var turboGrid_ = new Grid();
        turboGrid_.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        turboGrid_.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        turboGrid_.Children.Add(new TextBlock { Text = "Turbo Bindings", FontFamily = VoltKit.Display, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
        var turboCurTb = new TextBlock { Text = source.TurboCount == 0 ? "No turbo" : $"{source.TurboCount} active", FontFamily = VoltKit.Display, FontSize = 14, Foreground = VoltKit.MuteBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(turboCurTb, 1);
        turboGrid_.Children.Add(turboCurTb);
        turboRow.Child = turboGrid_;
        rowBorders["turbo"] = turboRow;
        leftStack.Children.Add(ThemeAction(turboRow, "theme:pc:row:turbo", () => { voltPcSelectedRow = "turbo"; RebuildDetailPanel(); }));

        // Row 3: Per-game profile
        var gameRow = new Border { MinHeight = 50, Padding = new Thickness(14, 4, 14, 4), Cursor = Cursors.Hand };
        var gameGrid = new Grid();
        gameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        gameGrid.Children.Add(new TextBlock { Text = "Per-Game Profiles", FontFamily = VoltKit.Display, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
        var gameChev = new TextBlock { Text = "\uE76C", FontFamily = VoltKit.Icons, FontSize = 13, Foreground = VoltKit.MuteBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(gameChev, 1);
        gameGrid.Children.Add(gameChev);
        gameRow.Child = gameGrid;
        rowBorders["games"] = gameRow;
        leftStack.Children.Add(ThemeAction(gameRow, "theme:pc:row:games", () => { voltPcSelectedRow = "games"; RebuildDetailPanel(); }));

        // Row 4: Share & Backup
        var shareRow = new Border { MinHeight = 50, Padding = new Thickness(14, 4, 14, 4), Cursor = Cursors.Hand };
        var shareGrid = new Grid();
        shareGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        shareGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        shareGrid.Children.Add(new TextBlock { Text = "Import / Export PC Profile", FontFamily = VoltKit.Display, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
        var shareChev = new TextBlock { Text = "\uE76C", FontFamily = VoltKit.Icons, FontSize = 13, Foreground = VoltKit.MuteBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(shareChev, 1);
        shareGrid.Children.Add(shareChev);
        shareRow.Child = shareGrid;
        rowBorders["share"] = shareRow;
        leftStack.Children.Add(ThemeAction(shareRow, "theme:pc:row:share", () => { voltPcSelectedRow = "share"; RebuildDetailPanel(); }));

        // Row 5: Reset profile
        var resetRow = new Border { MinHeight = 50, Padding = new Thickness(14, 4, 14, 4), Cursor = Cursors.Hand };
        var resetGrid = new Grid();
        resetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        resetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        resetGrid.Children.Add(new TextBlock { Text = $"Reset Player {PcModePage.Player + 1} Profile", FontFamily = VoltKit.Display, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });
        var resetChev = new TextBlock { Text = "\uE76C", FontFamily = VoltKit.Icons, FontSize = 13, Foreground = VoltKit.MuteBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(resetChev, 1);
        resetGrid.Children.Add(resetChev);
        resetRow.Child = resetGrid;
        rowBorders["reset"] = resetRow;
        leftStack.Children.Add(ThemeAction(resetRow, "theme:pc:row:reset", () => { voltPcSelectedRow = "reset"; RebuildDetailPanel(); }));

        // Row 6: Safety Rules
        var noticeRow = new Border { MinHeight = 50, Padding = new Thickness(14, 4, 14, 4), Cursor = Cursors.Hand };
        var noticeGrid = new Grid();
        noticeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        noticeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        noticeGrid.Children.Add(new TextBlock { Text = "Before PC Mode Starts", FontFamily = VoltKit.Display, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.AmberHotBrush, VerticalAlignment = VerticalAlignment.Center });
        var noticeChev = new TextBlock { Text = "\uE76C", FontFamily = VoltKit.Icons, FontSize = 13, Foreground = VoltKit.AmberHotBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(noticeChev, 1);
        noticeGrid.Children.Add(noticeChev);
        noticeRow.Child = noticeGrid;
        rowBorders["notice"] = noticeRow;
        leftStack.Children.Add(ThemeAction(noticeRow, "theme:pc:row:notice", () => { voltPcSelectedRow = "notice"; RebuildDetailPanel(); }));

        // Navigation links: Edit Buttons, Edit Curves, Edit Macros
        var navBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        foreach (var (pk, pl) in new[] { ("buttons", "Edit Buttons"), ("curves", "Edit Curves"), ("macros", "Edit Macros") })
        {
            string target = pk;
            var nBtn = new Border
            {
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                CornerRadius = new CornerRadius(6),
                Background = VoltKit.B("#262626"),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = pl, FontFamily = VoltKit.Display, FontSize = 12, Foreground = VoltKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            navBar.Children.Add(ThemeAction(nBtn, "theme:pc:edit:" + target, () => VoltSelectPage(target)));
        }
        leftStack.Children.Add(navBar);

        var leftScroll = new ScrollViewer { Width = 530, Height = 480, Content = leftStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Canvas.SetLeft(leftScroll, 0);
        Canvas.SetTop(leftScroll, 70);
        content.Children.Add(leftScroll);

        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };
        rightStack.Children.Add(card);

        // Stick Tools shortcut button below card
        var stickToolsFace = VoltKit.OrangeActionButton("Open Advanced Stick Tools", 410, 44, null);
        stickToolsFace.Margin = new Thickness(0, 10, 0, 0);
        rightStack.Children.Add(ThemeAction(stickToolsFace, "theme:pc:stick-tools", OpenStickTools));

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 70);
        content.Children.Add(rightStack);

        if (source.NoticeShown) voltPcSelectedRow = "notice";
        RebuildDetailPanel();

        voltPcRefresh = () =>
            {
                stateTb.Text = source.StatusText;
                profCurTb.Text = source.ProfileChoices.FirstOrDefault(c => c.Id == PcModePage.ProfileKey).Label?.Split("  ·  ")[0] ?? "Default";
                turboCurTb.Text = source.TurboCount == 0 ? "No turbo" : $"{source.TurboCount} active";
                if (source.NoticeShown && voltPcSelectedRow != "notice")
                {
                    voltPcSelectedRow = "notice";
                    RebuildDetailPanel();
                }
            };
        // One subscription for the page's life; it reaches whichever render is current.
        if (!voltPcWired) { voltPcWired = true; source.Watch(() => voltPcRefresh?.Invoke()); }

        return VoltKit.TwoPaneShell("pcmode", "PC Mode", "Desktop virtualization, ViGEmBus routing, and software controller profiles.", content, VoltSelectPage);
    }
}

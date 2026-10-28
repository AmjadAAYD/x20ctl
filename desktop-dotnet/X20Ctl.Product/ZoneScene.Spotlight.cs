using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Controller Zone and Hero presentation for the Spotlight theme:
/// "The cast on their marks: the chosen controller bathed in the radiant follow spot, the rest waiting in the half-light."
/// Implements dynamic spotlight tracking across all controllers (0-3) and Add Controller (4).
/// </summary>
public partial class ZoneScene
{
    internal bool IsSpotlight { get; private set; }
    private static readonly double[] SpotlightXs = [370, 650, 910, 1160, 1400];

    private Grid? spotlightBoard;
    private Canvas? spotlightZoneBack, spotlightHeroBack, spotlightFocusOverlay;
    private FrameworkElement? spotlightHeaderBlock, spotlightHeroDetails, spotlightHintsOverview, spotlightHintsHero;
    private Button? spotlightAddCard;
    private Border? spotlightAddCardBorder;
    private TextBlock? spotlightLeadSubtitle;

    // Dynamic slot tracking
    private int spotlightChosenSlot = 0;
    private readonly UIElement[] spotlightMarks = new UIElement[SpotlightXs.Length];

    // Dynamic Hero elements
    private TextBlock? heroTitleShort;
    private TextBlock? heroModelLine;
    private TextBlock? heroStatusLine;
    private TextBlock? heroCrumbPlayer;
    private TextBlock? heroPlayerLabel;
    private Canvas? heroStarContainer;
    private Button? heroOpenStudioBtn;
    private Grid? heroActionsGrid;
    private int heroCurrentPlayer = 0;

    private void BuildSpotlight()
    {
        IsSpotlight = true;
        var original = Surface.Children.OfType<Grid>().First();
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = SpotlightKit.HouseBrush;
        OuterFrame.BorderBrush = SpotlightKit.HouseBrush;

        spotlightBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        // 1. Zone Overview Stage (Floor at y = 600, left leg curtain)
        spotlightZoneBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        spotlightZoneBack.Children.Add(SpotlightKit.TheatricalStage(floorY: 600, legs: false, valance: true, leftLeg: true, leftLegW: 150));

        // Follow spot overlay on overview
        spotlightFocusOverlay = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        spotlightZoneBack.Children.Add(spotlightFocusOverlay);

        // Stage marks across floor at y = 650 for each slot
        for (int i = 0; i < SpotlightXs.Length; i++)
        {
            var mark = SpotlightKit.StageMark(SpotlightXs[i], 650, on: i == 0, size: 20);
            spotlightMarks[i] = mark;
            spotlightZoneBack.Children.Add(mark);
        }

        // Hero Stage (Floor at y = 690, left leg curtain, full spotlight on star)
        spotlightHeroBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Visibility = Visibility.Collapsed };
        spotlightHeroBack.Children.Add(SpotlightKit.TheatricalStage(floorY: 690, legs: false, valance: true, leftLeg: true, leftLegW: 150));

        spotlightBoard.Children.Add(spotlightZoneBack);
        spotlightBoard.Children.Add(spotlightHeroBack);

        // 2. Header for Overview: Logo (width 84) + "Controller Zone" at (300, 100)
        var headerStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var logoImg = SpotlightParts.GetThemeLogo();
        if (logoImg != null)
        {
            headerStack.Children.Add(new Image
            {
                Source = logoImg,
                Width = 84,
                Margin = new Thickness(0, 0, 22, 0),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 10, ShadowDepth = 6, Opacity = 0.7 }
            });
        }
        headerStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER ZONE",
            FontFamily = SpotlightKit.Display,
            FontSize = 40,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.B("#f3e7d3")
        });
        Canvas.SetLeft(headerStack, 300);
        Canvas.SetTop(headerStack, 100);
        spotlightZoneBack.Children.Add(headerStack);

        // Lead subtitle at (300, 196)
        spotlightLeadSubtitle = new TextBlock
        {
            Text = "Player 1 · X20 Pro · Shadow · Not detected · Battery not reported · No local changes",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 17,
            Foreground = SpotlightKit.B("#b3a48e"),
            Width = 1000
        };
        Canvas.SetLeft(spotlightLeadSubtitle, 300);
        Canvas.SetTop(spotlightLeadSubtitle, 196);
        spotlightZoneBack.Children.Add(spotlightLeadSubtitle);

        // Status at top-right (right: 60, top: 82)
        var statusEl = SpotlightParts.StatusHeader();
        Canvas.SetRight(statusEl, 60);
        Canvas.SetTop(statusEl, 82);
        spotlightZoneBack.Children.Add(statusEl);

        // Bottom hints: A Select · Y Change · B Back
        spotlightHintsOverview = SpotlightParts.HintsBar(new[] { ("A", "Select"), ("Y", "Change"), ("B", "Back") });
        Canvas.SetRight(spotlightHintsOverview, 60);
        Canvas.SetTop(spotlightHintsOverview, 852);
        spotlightZoneBack.Children.Add(spotlightHintsOverview);

        // 3. Add Controller Slot at xs[4] = 1400
        spotlightAddCard = new Button
        {
            Width = 68,
            Height = 68,
            Cursor = System.Windows.Input.Cursors.Hand,
            FocusVisualStyle = null
        };
        spotlightAddCardBorder = new Border
        {
            Width = 68, Height = 68,
            CornerRadius = new CornerRadius(34),
            BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.50)),
            BorderThickness = new Thickness(1.4),
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = "\uE710",
                FontFamily = SpotlightKit.Icons,
                FontSize = 24,
                Foreground = SpotlightKit.GoldBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        spotlightAddCard.Content = spotlightAddCardBorder;
        spotlightAddCard.Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        spotlightAddCard.Click += (_, _) =>
        {
            int nextEmpty = Array.FindIndex(State.Players.ToArray(), p => p.Model == null);
            if (nextEmpty >= 0) ControllerActionRequested?.Invoke(nextEmpty, false);
        };
        spotlightAddCard.MouseEnter += (_, _) => SetSpotlightSlot(4);
        spotlightAddCard.GotKeyboardFocus += (_, _) => SetSpotlightSlot(4);

        Canvas.SetLeft(spotlightAddCard, SpotlightXs[4] - 34);
        Canvas.SetTop(spotlightAddCard, 520);
        spotlightZoneBack.Children.Add(spotlightAddCard);

        var addText = new TextBlock
        {
            Text = "Add controller",
            FontFamily = SpotlightKit.Body,
            FontSize = 21,
            Foreground = SpotlightKit.ChalkBrush,
            Width = 260,
            TextAlignment = TextAlignment.Center
        };
        Canvas.SetLeft(addText, SpotlightXs[4] - 130);
        Canvas.SetTop(addText, 690);
        spotlightZoneBack.Children.Add(addText);

        // 4. Hero Detail Overlay in spotlightHeroBack
        BuildSpotlightHeroUI();

        // Move DockStage onto spotlightBoard at full 1600x900 stage coordinates
        Workspace.Children.Remove(DockStage);
        DockStage.ClearValue(Grid.RowProperty);
        DockStage.Margin = new(0);
        DockStage.HorizontalAlignment = HorizontalAlignment.Left;
        DockStage.VerticalAlignment = VerticalAlignment.Top;
        DockStage.Width = 1600;
        DockStage.Height = 900;
        Workspace.Children.Remove(FeedbackText);
        spotlightBoard.Children.Add(FeedbackText);
        spotlightBoard.Children.Add(DockStage);

        foreach (var d in docks) HookSpotlightDock(d);

        original.Visibility = Visibility.Collapsed;
        Surface.Children.Insert(0, spotlightBoard);
        SnapSpotlightLayout();
        SetSpotlightSlot(0);
    }

    private void HookSpotlightDock(DockView d)
    {
        d.MouseEnter += (_, _) => SetSpotlightSlot(d.Index);
        d.FocusButton.MouseEnter += (_, _) => SetSpotlightSlot(d.Index);
        d.FocusButton.GotKeyboardFocus += (_, _) => SetSpotlightSlot(d.Index);
        d.Selected += index => SetSpotlightSlot(index);
        d.BackRequested += () => _ = FocusPlayerAsync(null);
        d.PageRequested += page => StudioPageRequested?.Invoke(d.Index, page);
    }

    public void SetSpotlightSlot(int slot)
    {
        if (slot < 0 || slot >= SpotlightXs.Length) return;
        spotlightChosenSlot = slot;

        for (int i = 0; i < docks.Length; i++)
        {
            docks[i].IsSpotlightLit = (i == spotlightChosenSlot);
            docks[i].PresentSpotlightDirect();
        }

        if (spotlightAddCardBorder != null)
        {
            bool isAddChosen = (spotlightChosenSlot == 4);
            spotlightAddCardBorder.BorderBrush = SpotlightKit.B(isAddChosen ? SpotlightKit.GoldHi : SpotlightKit.A(SpotlightKit.Gold, 0.50));
            spotlightAddCardBorder.Effect = isAddChosen
                ? new DropShadowEffect { Color = SpotlightKit.C("#ffb860"), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.85 }
                : null;
        }

        SpotlightSync();
    }

    private void BuildSpotlightHeroUI()
    {
        if (spotlightHeroBack == null) return;

        // Top-left breadcrumb: "← Controller Zone / Player 1"
        var crumbStack = new StackPanel { Orientation = Orientation.Horizontal, Cursor = System.Windows.Input.Cursors.Hand };
        crumbStack.Children.Add(new TextBlock
        {
            Text = "\uE72B",
            FontFamily = SpotlightKit.Icons,
            FontSize = 12,
            Foreground = SpotlightKit.GoldBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });
        crumbStack.Children.Add(new TextBlock
        {
            Text = "Controller Zone",
            FontFamily = SpotlightKit.Body,
            FontSize = 17,
            Foreground = SpotlightKit.B("#b3a48e"),
            VerticalAlignment = VerticalAlignment.Center
        });
        crumbStack.Children.Add(new TextBlock
        {
            Text = " / ",
            FontFamily = SpotlightKit.Body,
            FontSize = 17,
            Foreground = SpotlightKit.B(SpotlightKit.A(SpotlightKit.C("#b3a48e"), 0.5)),
            VerticalAlignment = VerticalAlignment.Center
        });
        heroCrumbPlayer = new TextBlock
        {
            Text = "Player 1",
            FontFamily = SpotlightKit.Body,
            FontSize = 17,
            Foreground = SpotlightKit.GoldHiBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        crumbStack.Children.Add(heroCrumbPlayer);
        crumbStack.MouseLeftButtonDown += (_, _) => FocusPlayerAsync(null);
        Canvas.SetLeft(crumbStack, 150);
        Canvas.SetTop(crumbStack, 104);
        spotlightHeroBack.Children.Add(crumbStack);

        // Status at top-right
        var statusEl = SpotlightParts.StatusHeader();
        Canvas.SetRight(statusEl, 60);
        Canvas.SetTop(statusEl, 82);
        spotlightHeroBack.Children.Add(statusEl);

        // Main Hero Details Stack on Left at (150, 168)
        var heroStack = new StackPanel { Width = 560 };

        heroPlayerLabel = new TextBlock
        {
            Text = "PLAYER 1",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        };
        heroStack.Children.Add(heroPlayerLabel);

        // Monumental gilded short name (150px Perpetua Titling MT)
        heroTitleShort = new TextBlock
        {
            Text = "X20",
            FontFamily = SpotlightKit.Display,
            FontSize = 150,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GildBrush,
            Margin = new Thickness(0, -10, 0, 0)
        };
        heroStack.Children.Add(heroTitleShort);

        // Full model name + finish
        heroModelLine = new TextBlock
        {
            Text = "X20 Pro · Shadow",
            FontFamily = SpotlightKit.Body,
            FontSize = 30,
            Foreground = SpotlightKit.ChalkBrush,
            Margin = new Thickness(0, 10, 0, 0)
        };
        heroStack.Children.Add(heroModelLine);

        // Subtitle status line
        heroStatusLine = new TextBlock
        {
            Text = "Not detected · Battery not reported · No local changes",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(0, 6, 0, 0)
        };
        heroStack.Children.Add(heroStatusLine);

        // Gold divider rule at (150, 520)
        var rule = new Rectangle
        {
            Width = 420, Height = 1,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(1, 0),
                GradientStops = { new(SpotlightKit.Gold, 0.0), new(Colors.Transparent, 1.0) }
            },
            Margin = new Thickness(0, 24, 0, 24),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        heroStack.Children.Add(rule);

        // Primary Button: Open Studio
        heroOpenStudioBtn = SpotlightKit.TheatricalButton("Open Studio", primary: true, key: "A", onClick: () =>
        {
            int player = heroCurrentPlayer;
            ControllerActionRequested?.Invoke(player, false);
        }, height: 56, fontSize: 14);
        heroOpenStudioBtn.HorizontalAlignment = HorizontalAlignment.Left;
        heroStack.Children.Add(heroOpenStudioBtn);

        // Secondary Actions Grid
        heroActionsGrid = new Grid { Margin = new Thickness(0, 24, 0, 0), Width = 480, HorizontalAlignment = HorizontalAlignment.Left };
        heroActionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        heroActionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        heroActionsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        heroActionsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        heroStack.Children.Add(heroActionsGrid);

        // Status chips row at bottom
        var chipsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 0) };
        chipsRow.Children.Add(SpotlightKit.Chip("Current working settings", false));
        chipsRow.Children.Add(new Border { Width = 10 });
        chipsRow.Children.Add(SpotlightKit.Chip("Not detected", false));
        chipsRow.Children.Add(new Border { Width = 10 });
        chipsRow.Children.Add(SpotlightKit.Chip("Battery not reported", false));
        chipsRow.Children.Add(new Border { Width = 10 });
        chipsRow.Children.Add(SpotlightKit.Chip("No local changes", false));
        heroStack.Children.Add(chipsRow);

        Canvas.SetLeft(heroStack, 150);
        Canvas.SetTop(heroStack, 168);
        spotlightHeroBack.Children.Add(heroStack);

        // Dynamic star artwork container
        heroStarContainer = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        spotlightHeroBack.Children.Add(heroStarContainer);

        // Follow spot on hero star
        var heroSpot = SpotlightKit.FollowSpot(cx: 1080, fy: 730, rx: 360, top: -60, opacity: 0.30);
        spotlightHeroBack.Children.Add(heroSpot);

        // Bottom hints for hero: A Open Studio · B Back
        spotlightHintsHero = SpotlightParts.HintsBar(new[] { ("A", "Open Studio"), ("B", "Back") });
        Canvas.SetRight(spotlightHintsHero, 60);
        Canvas.SetTop(spotlightHintsHero, 852);
        spotlightHeroBack.Children.Add(spotlightHintsHero);

        UpdateSpotlightHeroUI(0);
    }

    private void UpdateSpotlightHeroUI(int playerIndex)
    {
        heroCurrentPlayer = playerIndex;
        if (heroCrumbPlayer == null || playerIndex >= State.Players.Count) return;

        var p = State.Players[playerIndex];
        string shortName = p.ModelId?.ToUpperInvariant() switch
        {
            "X20_PRO" or "X20PRO" => "X20 PRO",
            "X15" => "X15",
            "X05_PRO" or "X05PRO" or "X05" => "X05 PRO",
            "X10" => "X10",
            "D10" => "D10",
            "DUNE" => "DUNE",
            _ => (p.Model?.Replace("EasySMX ", "") ?? "X20").ToUpperInvariant()
        };

        string modelFullName = p.Model ?? "EasySMX Controller";
        string finish = PadColours.For(playerIndex, p.ModelId ?? "x20");
        string conn = p.IsMockConnected ? "Connected · Mock" : "Not detected";

        heroCrumbPlayer.Text = $"Player {playerIndex + 1}";
        if (heroPlayerLabel != null) heroPlayerLabel.Text = $"PLAYER {playerIndex + 1}";
        if (heroTitleShort != null) heroTitleShort.Text = shortName;
        if (heroModelLine != null) heroModelLine.Text = $"{modelFullName} · {finish}";
        if (heroStatusLine != null) heroStatusLine.Text = $"{conn} · Battery not reported · No local changes";

        // Rebuild secondary actions wired to playerIndex
        if (heroActionsGrid != null)
        {
            heroActionsGrid.Children.Clear();
            var (actSettings, actTest, actSetups, actRemove) = CreateHeroActionsForPlayer(playerIndex);
            Grid.SetColumn(actSettings, 0); Grid.SetRow(actSettings, 0); heroActionsGrid.Children.Add(actSettings);
            Grid.SetColumn(actTest, 1); Grid.SetRow(actTest, 0); heroActionsGrid.Children.Add(actTest);
            Grid.SetColumn(actSetups, 0); Grid.SetRow(actSetups, 1); heroActionsGrid.Children.Add(actSetups);
            Grid.SetColumn(actRemove, 1); Grid.SetRow(actRemove, 1); heroActionsGrid.Children.Add(actRemove);
        }

        // Rebuild hero star artwork with chosen model
        if (heroStarContainer != null)
        {
            heroStarContainer.Children.Clear();
            string modelId = p.ModelId ?? (p.Model switch
            {
                "EasySMX X15" => "x15",
                "EasySMX X05 Pro" => "x05pro",
                "EasySMX X10" => "x10",
                "EasySMX D10" => "d10",
                "EasySMX Dune" => "dune",
                _ => "x20"
            });
            var heroStar = SpotlightParts.SpotlightStar(modelId, 1080, 690, 700, reflection: 0.18);
            heroStarContainer.Children.Add(heroStar);
        }
    }

    private (UIElement Settings, UIElement Test, UIElement Setups, UIElement Remove) CreateHeroActionsForPlayer(int playerIndex)
    {
        UIElement Link(string icon, string label, Color color, Action onClick)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            p.Children.Add(new TextBlock
            {
                Text = icon,
                FontFamily = SpotlightKit.Icons,
                FontSize = 16,
                Foreground = SpotlightKit.B(color),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            p.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = SpotlightKit.Body,
                FontSize = 18,
                Foreground = SpotlightKit.B(color),
                VerticalAlignment = VerticalAlignment.Center
            });
            p.MouseLeftButtonDown += (_, _) => onClick();
            return p;
        }

        var col = SpotlightKit.ChalkDim;
        var s = Link("\uE713", "Controller settings", col, () => StudioPageRequested?.Invoke(playerIndex, "device"));
        var t = Link("\uE768", "Test controller", col, () => StudioPageRequested?.Invoke(playerIndex, "tester"));
        var u = Link("\uE8B5", "Manage setups", col, () => StudioPageRequested?.Invoke(playerIndex, "setups"));
        var r = Link("\uE74D", "Remove from slot", SpotlightKit.Coral, () =>
        {
            RemoveController(playerIndex);
            _ = FocusPlayerAsync(null);
        });
        return (s, t, u, r);
    }

    private void SnapSpotlightLayout()
    {
        if (!IsSpotlight) return;
        var bounds = SpotlightBounds();
        for (int i = 0; i < docks.Length; i++)
        {
            var b = bounds[i];
            Canvas.SetLeft(docks[i], b.X);
            Canvas.SetTop(docks[i], b.Y);
            docks[i].Width = b.Width;
            docks[i].Height = b.Height;
        }
    }

    private DockBounds[] SpotlightBounds()
    {
        var result = new DockBounds[4];
        for (int i = 0; i < 4; i++)
        {
            double cx = SpotlightXs[i];
            result[i] = new(cx - 140, 360, 280, 420);
        }
        return result;
    }

    private void SpotlightSync()
    {
        if (!IsSpotlight || spotlightFocusOverlay == null) return;
        spotlightFocusOverlay.Children.Clear();

        int? focused = State.FocusedPlayer;
        if (focused.HasValue)
        {
            // Hero active
            spotlightZoneBack!.Visibility = Visibility.Collapsed;
            spotlightHeroBack!.Visibility = Visibility.Visible;
            UpdateSpotlightHeroUI(focused.Value);
        }
        else
        {
            spotlightHeroBack!.Visibility = Visibility.Collapsed;
            spotlightZoneBack!.Visibility = Visibility.Visible;

            // Overview active: put follow spot dynamically on spotlightChosenSlot (0..4)
            int targetSlot = Math.Clamp(spotlightChosenSlot, 0, SpotlightXs.Length - 1);
            double cx = SpotlightXs[targetSlot];
            var spot = SpotlightKit.FollowSpot(cx, 650, 240, -40, 0.36);
            spotlightFocusOverlay.Children.Add(spot);

            // Update stage marks
            for (int i = 0; i < spotlightMarks.Length; i++)
            {
                if (spotlightMarks[i] != null)
                {
                    spotlightZoneBack!.Children.Remove(spotlightMarks[i]);
                    spotlightMarks[i] = SpotlightKit.StageMark(SpotlightXs[i], 650, on: i == targetSlot, size: 20);
                    spotlightZoneBack!.Children.Add(spotlightMarks[i]);
                }
            }

            // Update lead subtitle
            if (spotlightLeadSubtitle != null)
            {
                if (targetSlot < 4 && targetSlot < State.Players.Count)
                {
                    var p = State.Players[targetSlot];
                    if (p.Model != null)
                    {
                        string finish = PadColours.For(targetSlot, p.ModelId ?? "x20");
                        string conn = p.IsMockConnected ? "Connected · Mock" : "Not detected";
                        spotlightLeadSubtitle.Text = $"Player {targetSlot + 1} · {p.Model} · {finish} · {conn} · Battery not reported · No local changes";
                    }
                    else
                    {
                        spotlightLeadSubtitle.Text = $"Player {targetSlot + 1} · Empty slot · Ready for audition · Select to assign controller";
                    }
                }
                else
                {
                    spotlightLeadSubtitle.Text = "Audition Stage · Connect or assign a new controller to join the performance";
                }
            }
        }
    }

    private async Task SpotlightFocus(int? index)
    {
        if (index.HasValue)
        {
            spotlightChosenSlot = index.Value;
            State.Activate(index.Value);
        }
        else
        {
            State.Collapse();
        }
        SpotlightSync();
        for (int i = 0; i < docks.Length; i++)
        {
            docks[i].Visibility = index.HasValue ? Visibility.Collapsed : Visibility.Visible;
        }
        await Task.CompletedTask;
    }
}

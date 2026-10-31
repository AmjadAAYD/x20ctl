using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Controller Zone and Hero presentation for the Tide theme:
/// Redesigned to Section 5.13:
/// - Top-left header with time / connection in a glass pill
/// - Horizontal row of 4 large glass "player pad" cards (P1-P4) centered at 45% height (280x330)
/// - Neutral dashed silhouettes for empty slots
/// - Three round action buttons (Add pad, Change model, Tools/themes) centered below
/// - Dedicated ocean-glass hero stage with truthful telemetry (no fake Hz or battery)
/// </summary>
public partial class ZoneScene
{
    internal bool IsTide { get; private set; }

    private Grid? tideBoard;
    private Canvas? tideZoneBack, tideHeroBack;
    private int selectedZonePlayer = 0;

    private void BuildTide()
    {
        IsTide = true;
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = new SolidColorBrush(TideKit.OceanDark);
        OuterFrame.BorderBrush = new SolidColorBrush(TideKit.OceanDark);

        tideBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        OuterFrame.Child = tideBoard;
        TideRenderZone();
    }

    private void TideRenderZone()
    {
        if (tideBoard == null) return;
        tideBoard.Children.Clear();

        bool isHero = State.FocusedPlayer.HasValue;
        if (isHero)
        {
            TideRenderHero();
            return;
        }

        // ==========================================
        // 1. Zone Overview Stage (Section 5.13)
        // ==========================================
        tideZoneBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Single source of truth: SeaBackground (no ghost watermarks)
        tideZoneBack.Children.Add(TideKit.SeaBackground(7));

        // Top-left header: Time & Status glass pill
        var headerPill = new Border
        {
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x06, 0x16, 0x44)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(16, 6, 20, 6)
        };
        var hpStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        
        var moonOrb = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = new RadialGradientBrush
            {
                Center = new Point(0.4, 0.4),
                GradientStops =
                {
                    new(Colors.White, 0.0),
                    new(TideKit.CyanFoam, 0.7),
                    new(TideKit.CyanWave, 1.0)
                }
            },
            Margin = new Thickness(0, 0, 10, 0)
        };
        hpStack.Children.Add(moonOrb);

        hpStack.Children.Add(new TextBlock
        {
            Text = "TIDE // CONTROLLER ZONE",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        int connectedCount = State.Players.Count(p => p.Model != null);
        hpStack.Children.Add(new TextBlock
        {
            Text = $" · {connectedCount}/4 CONNECTED",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = TideKit.CyanFoamBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        });

        headerPill.Child = hpStack;
        Canvas.SetLeft(headerPill, 48);
        Canvas.SetTop(headerPill, 28);
        tideZoneBack.Children.Add(headerPill);

        // Horizontal Row of 4 large glass "player pad" cards (P1-P4) centered at 45% height
        // Card size: 280 x 330. Total row width: 4 * 280 + 3 * 32 = 1216. Left = (1600 - 1216)/2 = 192.
        double startX = 192;
        double cardY = 240; // 45% height center

        for (int i = 0; i < 4; i++)
        {
            int pIdx = i;
            var player = State.Players[i];
            bool hasModel = player.Model != null;
            string modelId = player.ModelId ?? "x20";
            string modelName = hasModel ? player.Model.Replace("EasySMX ", "") : "EMPTY SLOT";
            bool isSelected = (pIdx == selectedZonePlayer);

            var card = new Border
            {
                Width = 280,
                Height = 330,
                CornerRadius = new CornerRadius(14),
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops =
                    {
                        new(Color.FromArgb(isSelected ? (byte)0xef : (byte)0xd0, 0x08, 0x1a, 0x4c), 0.0),
                        new(Color.FromArgb(isSelected ? (byte)0xf8 : (byte)0xe0, 0x04, 0x10, 0x32), 1.0)
                    }
                },
                BorderBrush = isSelected
                    ? new SolidColorBrush(TideKit.CyanFoam)
                    : new SolidColorBrush(Color.FromArgb(0x55, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(isSelected ? 2.0 : 1.5),
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = isSelected ? Color.FromRgb(0x1a, 0x60, 0xf0) : Color.FromArgb(0xb0, 0x00, 0x0c, 0x28),
                    BlurRadius = isSelected ? 28 : 18,
                    ShadowDepth = isSelected ? 8 : 4,
                    Direction = 270,
                    Opacity = 0.8
                }
            };

            var cardInner = new StackPanel
            {
                Margin = new Thickness(16, 16, 16, 16),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Selected marker dot top-left
            var topHeaderRow = new Grid { Width = 248, Margin = new Thickness(0, 0, 0, 8) };
            topHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (isSelected)
            {
                var selDot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = TideKit.HarpoonRedBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Effect = new DropShadowEffect { Color = TideKit.HarpoonRed, BlurRadius = 8, ShadowDepth = 0, Opacity = 1.0 }
                };
                topHeaderRow.Children.Add(selDot);
                TideKit.AnimatePulse(selDot, 0.8, 1.4, 1.2);
            }

            var pNum = new TextBlock
            {
                Text = $"PLAYER {i + 1}",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 13,
                Foreground = isSelected ? TideKit.CyanFoamBrush : new SolidColorBrush(Color.FromArgb(0x99, 0xa0, 0xc8, 0xf0)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(isSelected ? 6 : 0, 0, 0, 0)
            };
            Grid.SetColumn(pNum, 1);
            topHeaderRow.Children.Add(pNum);

            cardInner.Children.Add(topHeaderRow);

            // Art Area (Height: 160)
            var artHost = new Grid
            {
                Width = 248,
                Height = 160,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (hasModel)
            {
                var ripples = TideKit.PedestalRipples(230, 60);
                Canvas.SetLeft(ripples, 9);
                Canvas.SetTop(ripples, 95);
                artHost.Children.Add(ripples);

                var padArt = TideKit.ControllerArt(modelId, 210);
                padArt.HorizontalAlignment = HorizontalAlignment.Center;
                padArt.VerticalAlignment = VerticalAlignment.Center;
                artHost.Children.Add(padArt);
            }
            else
            {
                var emptyArt = TideKit.NeutralControllerSilhouette(170, 110);
                emptyArt.HorizontalAlignment = HorizontalAlignment.Center;
                emptyArt.VerticalAlignment = VerticalAlignment.Center;
                artHost.Children.Add(emptyArt);
            }

            cardInner.Children.Add(artHost);

            // Model name
            var nameBlock = new TextBlock
            {
                Text = modelName.ToUpperInvariant(),
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 16,
                Foreground = TideKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 10, 0, 6)
            };
            cardInner.Children.Add(nameBlock);

            // State Pill
            var statePill = new Border
            {
                Padding = new Thickness(14, 5, 14, 5),
                CornerRadius = new CornerRadius(12),
                Background = hasModel
                    ? new SolidColorBrush(Color.FromArgb(0x40, 0x18, 0x5a, 0xe0))
                    : new SolidColorBrush(Color.FromArgb(0x20, 0xff, 0xff, 0xff)),
                BorderBrush = hasModel
                    ? new SolidColorBrush(Color.FromArgb(0x80, 0x9f, 0xf6, 0xff))
                    : new SolidColorBrush(Color.FromArgb(0x40, 0xff, 0xff, 0xff)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var stateStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (hasModel)
            {
                var dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = player.IsMockConnected ? new SolidColorBrush(Color.FromRgb(0x4a, 0xde, 0x80)) : TideKit.CyanFoamBrush,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                stateStack.Children.Add(dot);
            }

            stateStack.Children.Add(new TextBlock
            {
                Text = hasModel ? (player.IsMockConnected ? "CONNECTED" : "STANDBY") : "EMPTY SLOT",
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = hasModel ? TideKit.IceLightBrush : new SolidColorBrush(Color.FromArgb(0x80, 0xf2, 0xfd, 0xff)),
                VerticalAlignment = VerticalAlignment.Center
            });

            statePill.Child = stateStack;
            cardInner.Children.Add(statePill);

            card.Child = cardInner;
            Canvas.SetLeft(card, startX + i * (280 + 32));
            Canvas.SetTop(card, isSelected ? cardY - 6 : cardY);

            // Click handling
            card.MouseLeftButtonUp += (_, _) =>
            {
                selectedZonePlayer = pIdx;
                if (hasModel)
                {
                    _ = FocusPlayerAsync(pIdx);
                }
                else
                {
                    ControllerActionRequested?.Invoke(pIdx, true);
                }
            };

            tideZoneBack.Children.Add(card);
        }

        // Below the cards: Three round action buttons centered horizontally (Add pad, Change model, Tools/themes)
        var actionsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // 1. Add Pad button
        actionsRow.Children.Add(BuildZoneActionButton("ADD PAD", "\uE710", () =>
        {
            int empty = Array.FindIndex(State.Players.ToArray(), p => p.Model == null);
            if (empty >= 0) ControllerActionRequested?.Invoke(empty, true);
            else ControllerActionRequested?.Invoke(selectedZonePlayer, true);
        }));

        // 2. Change Model button
        actionsRow.Children.Add(BuildZoneActionButton("CHANGE MODEL", "\uE76C", () =>
        {
            ControllerActionRequested?.Invoke(selectedZonePlayer, true);
        }));

        // 3. Tools / Themes button
        actionsRow.Children.Add(BuildZoneActionButton("TOOLS & THEMES", "\uE713", () =>
        {
            SettingsRequested?.Invoke();
        }));

        Canvas.SetLeft(actionsRow, 560);
        Canvas.SetTop(actionsRow, 630);
        tideZoneBack.Children.Add(actionsRow);

        // Quiet Footer Hints above bottom edge
        var hintsText = new TextBlock
        {
            Text = "\u24B7 Back   \u24B6 Select Player / Open Studio   \u24BE Add / Change Controller",
            FontFamily = TideKit.Body,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xf2, 0xfd, 0xff)),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Canvas.SetRight(hintsText, 48);
        Canvas.SetBottom(hintsText, 24);
        tideZoneBack.Children.Add(hintsText);

        TideKit.AnimateEntrance(tideZoneBack);
        tideBoard.Children.Add(tideZoneBack);
    }

    private FrameworkElement BuildZoneActionButton(string label, string icon, Action onClick)
    {
        var sp = new StackPanel
        {
            Width = 140,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0)
        };

        var btn = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(28),
            Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x06, 0x16, 0x44)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x9f, 0xf6, 0xff)),
            BorderThickness = new Thickness(1.5),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new DropShadowEffect
            {
                Color = Color.FromArgb(0x90, 0x00, 0x0a, 0x24),
                BlurRadius = 14,
                ShadowDepth = 4,
                Direction = 270,
                Opacity = 0.7
            }
        };

        var iconBlock = new TextBlock
        {
            Text = icon,
            FontFamily = TideKit.Icons,
            FontSize = 20,
            Foreground = TideKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        btn.Child = iconBlock;

        var transforms = TideKit.EnsureTransforms(btn, new Point(0.5, 0.5));
        btn.MouseEnter += (_, _) =>
        {
            btn.BorderBrush = TideKit.CyanFoamBrush;
            btn.Background = new SolidColorBrush(Color.FromArgb(0xee, 0x10, 0x38, 0x8a));
            transforms.scale.ScaleX = 1.08;
            transforms.scale.ScaleY = 1.08;
        };
        btn.MouseLeave += (_, _) =>
        {
            btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x9f, 0xf6, 0xff));
            btn.Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x06, 0x16, 0x44));
            transforms.scale.ScaleX = 1.0;
            transforms.scale.ScaleY = 1.0;
        };
        btn.MouseLeftButtonUp += (_, _) => onClick?.Invoke();

        sp.Children.Add(btn);

        var lbl = new TextBlock
        {
            Text = label,
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 11,
            Foreground = TideKit.IceLightBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        sp.Children.Add(lbl);

        return sp;
    }

    private void TideRenderHero()
    {
        if (tideBoard == null) return;

        int pIdx = State.FocusedPlayer ?? 0;
        var player = State.Players[pIdx];
        string modelId = player.ModelId ?? "x20";
        string modelName = player.Model?.ToUpperInvariant() ?? "UNASSIGNED CONTROLLER";
        bool hasModel = player.Model != null;

        // ==========================================
        // 2. Hero Stage (Focused Player View)
        // ==========================================
        tideHeroBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        tideHeroBack.Children.Add(TideKit.SeaBackground(5));

        // Header Bar
        var topBar = TideKit.TideHeader(
            $"PLAYER {pIdx + 1} // {modelName}",
            onBack: () => _ = FocusPlayerAsync(null)
        );
        tideHeroBack.Children.Add(topBar);

        // Left Side: Controller Art on Pedestal
        var artCanvas = new Canvas { Width = 700, Height = 560 };
        Canvas.SetLeft(artCanvas, 80);
        Canvas.SetTop(artCanvas, 160);

        var ripples = TideKit.PedestalRipples(560, 140);
        Canvas.SetLeft(ripples, 70);
        Canvas.SetTop(ripples, 400);
        artCanvas.Children.Add(ripples);

        var padArt = TideKit.ControllerArt(modelId, 540);
        Canvas.SetLeft(padArt, 80);
        Canvas.SetTop(padArt, 60);
        artCanvas.Children.Add(padArt);

        tideHeroBack.Children.Add(artCanvas);

        // Right Side: Focused Inspector Card
        var heroCard = TideKit.OceanGlassCard(w: 620, h: 480);
        Canvas.SetLeft(heroCard, 880);
        Canvas.SetTop(heroCard, 180);

        var cardStack = new StackPanel();

        cardStack.Children.Add(new TextBlock
        {
            Text = $"PLAYER {pIdx + 1} ASSIGNMENT",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        cardStack.Children.Add(new TextBlock
        {
            Text = modelName,
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 32,
            Foreground = TideKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 16)
        });

        // Honest telemetry summary (Rule 6.0.C: No fake 1000 Hz or 100% battery)
        var telemetryCard = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0x06, 0x16, 0x44)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x9f, 0xf6, 0xff)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 24)
        };
        var telemStack = new StackPanel();
        telemStack.Children.Add(new TextBlock
        {
            Text = hasModel ? "STATUS: CONFIGURED · READY FOR LOCAL EDITING" : "STATUS: NO CONTROLLER ASSIGNED",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 12,
            Foreground = hasModel ? TideKit.CyanFoamBrush : new SolidColorBrush(Color.FromArgb(0x80, 0xf2, 0xfd, 0xff))
        });
        telemStack.Children.Add(new TextBlock
        {
            Text = hasModel ? $"Physical slot: Player {pIdx + 1} · Model ID: {modelId}" : "Click 'Assign Controller' to select a hardware profile.",
            FontFamily = TideKit.Body,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8)),
            Margin = new Thickness(0, 4, 0, 0)
        });
        telemetryCard.Child = telemStack;
        cardStack.Children.Add(telemetryCard);

        // Actions: Open Studio (HarpoonRed primary), Change Controller, Remove, Back
        var actStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };

        var openBtn = TideKit.SparkButton("OPEN REMAP STUDIO ➔", icon: "\uE7FC", onClick: () =>
        {
            if (hasModel) ControllerActionRequested?.Invoke(pIdx, false);
            else ControllerActionRequested?.Invoke(pIdx, true);
        });
        openBtn.Margin = new Thickness(0, 0, 0, 12);
        actStack.Children.Add(openBtn);

        var rowActs = new StackPanel { Orientation = Orientation.Horizontal };

        var changeBtn = TideKit.OceanButton("CHANGE CONTROLLER", icon: "\uE76C", onClick: () =>
        {
            ControllerActionRequested?.Invoke(pIdx, true);
        });
        changeBtn.Margin = new Thickness(0, 0, 10, 0);
        rowActs.Children.Add(changeBtn);

        var removeBtn = TideKit.OceanButton("REMOVE", icon: "\uE74D", onClick: () =>
        {
            RemoveController(pIdx);
            _ = FocusPlayerAsync(null);
        });
        removeBtn.Margin = new Thickness(0, 0, 10, 0);
        rowActs.Children.Add(removeBtn);

        var backBtn = TideKit.OceanButton("BACK TO ZONE", icon: "\uE72B", onClick: () =>
        {
            _ = FocusPlayerAsync(null);
        });
        rowActs.Children.Add(backBtn);

        actStack.Children.Add(rowActs);
        cardStack.Children.Add(actStack);

        heroCard.Child = cardStack;
        tideHeroBack.Children.Add(heroCard);

        // Quiet Footer hints
        var heroHints = new TextBlock
        {
            Text = "\u24B7 Back to Zone   \u24B6 Open Remap Studio   \u24BE Change Controller",
            FontFamily = TideKit.Body,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xf2, 0xfd, 0xff))
        };
        Canvas.SetRight(heroHints, 48);
        Canvas.SetBottom(heroHints, 24);
        tideHeroBack.Children.Add(heroHints);

        TideKit.AnimateEntrance(tideHeroBack);
        tideBoard.Children.Add(tideHeroBack);
    }

    private void TideSync()
    {
        if (!IsTide || tideBoard == null) return;
        TideRenderZone();
    }

    private async Task TideFocus(int? index)
    {
        if (index.HasValue) State.Activate(index.Value); else State.Collapse();
        TideSync();
        await Task.CompletedTask;
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class ZoneScene
{
    internal bool IsNocturne { get; private set; }

    private Grid? nocturneBoard;
    private Canvas? nocturneZoneBack, nocturneHeroBack;
    private int nocturneSelectedPlayer = 0;

    private void BuildNocturne()
    {
        IsNocturne = true;
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = NocturneKit.VoidBrush;
        OuterFrame.BorderBrush = NocturneKit.VoidBrush;

        nocturneBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        OuterFrame.Child = nocturneBoard;
        NocturneRenderZone();
    }

    public void NocturneSync()
    {
        if (!IsNocturne || nocturneBoard == null) return;
        NocturneRenderZone();
    }

    public async Task NocturneFocus(int? index)
    {
        if (!IsNocturne) return;
        if (index.HasValue)
        {
            nocturneSelectedPlayer = index.Value;
            State.Activate(index.Value);
        }
        else
        {
            State.Collapse();
        }
        NocturneRenderZone();
        await Task.CompletedTask;
    }

    private void NocturneRenderZone()
    {
        if (nocturneBoard == null) return;
        nocturneBoard.Children.Clear();

        bool isHero = State.FocusedPlayer.HasValue;
        if (isHero)
        {
            NocturneRenderHero();
            return;
        }

        // Zone Overview Stage
        nocturneZoneBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        nocturneZoneBack.Children.Add(NocturneKit.CreateCityBackdrop());

        // Header Pill
        var headerPill = new Border
        {
            Height = 44,
            Background = NocturneKit.NightBrush,
            BorderBrush = NocturneKit.RuleBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 6, 20, 6),
            RenderTransform = NocturneKit.CreateSlant(-12),
            RenderTransformOrigin = new Point(0, 0.5)
        };
        var hpStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hpStack.Children.Add(new Rectangle { Width = 8, Height = 8, Fill = NocturneKit.AccentBrush, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
        hpStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER ZONE",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = NocturneKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        int connectedCount = State.Players.Count(p => p.Model != null);
        hpStack.Children.Add(new TextBlock
        {
            Text = $"  ·  {connectedCount} / 4 CONNECTED",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyles.Italic,
            FontSize = 12,
            Foreground = NocturneKit.SoftBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        headerPill.Child = hpStack;
        Canvas.SetLeft(headerPill, 48);
        Canvas.SetTop(headerPill, 28);
        nocturneZoneBack.Children.Add(headerPill);

        // 4 Slanted Player Cards (P1 to P4)
        double startX = 140;
        double cardY = 180;

        for (int i = 0; i < 4; i++)
        {
            int pIdx = i;
            var player = State.Players[i];
            bool hasModel = player.Model != null;
            string modelName = hasModel ? player.Model.Replace("EasySMX ", "") : "EMPTY SLOT";
            bool isSelected = (pIdx == nocturneSelectedPlayer);

            var playerCard = new Border
            {
                Width = 290,
                Height = 390,
                Background = isSelected ? NocturneKit.WhiteBrush : NocturneKit.NightBrush,
                BorderBrush = isSelected ? NocturneKit.AccentBrush : NocturneKit.RuleBrush,
                BorderThickness = new Thickness(isSelected ? 2.5 : 1.0),
                Cursor = System.Windows.Input.Cursors.Hand,
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Effect = new DropShadowEffect
                {
                    Color = isSelected ? Color.FromRgb(0x00, 0x56, 0xfb) : Colors.Black,
                    BlurRadius = isSelected ? 24 : 10,
                    ShadowDepth = 6,
                    Opacity = isSelected ? 0.6 : 0.4
                }
            };

            var innerSp = new StackPanel { Margin = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Center };

            // Player Title
            var pHeader = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 14) };
            if (isSelected)
            {
                pHeader.Children.Add(new Rectangle { Width = 8, Height = 8, Fill = NocturneKit.AccentBrush, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            }
            pHeader.Children.Add(new TextBlock
            {
                Text = $"PLAYER {pIdx + 1}",
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 14,
                Foreground = isSelected ? NocturneKit.VoidTextBrush : NocturneKit.WhiteBrush,
            });
            innerSp.Children.Add(pHeader);

            // Controller Picture or Silhouette
            var imgBox = new Border
            {
                Width = 240,
                Height = 200,
                Background = isSelected ? new SolidColorBrush(Color.FromArgb(0x10, 0x01, 0x12, 0x3e)) : NocturneKit.DeepBrush,
                BorderBrush = isSelected ? NocturneKit.RuleBrush : NocturneKit.RuleBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 14)
            };

            if (hasModel)
            {
                var padArt = NocturneKit.ControllerArt(player.Model ?? "x20", 220);
                imgBox.Child = padArt;
            }
            else
            {
                var emptySp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                emptySp.Children.Add(new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 36, Foreground = NocturneKit.SoftBrush, HorizontalAlignment = HorizontalAlignment.Center });
                emptySp.Children.Add(new TextBlock { Text = "NO CONTROLLER", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, FontSize = 12, Foreground = NocturneKit.SoftBrush, Margin = new Thickness(0, 8, 0, 0) });
                imgBox.Child = emptySp;
            }
            innerSp.Children.Add(imgBox);

            // Model Name
            innerSp.Children.Add(new TextBlock
            {
                Text = modelName.ToUpperInvariant(),
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 14,
                Foreground = isSelected ? NocturneKit.VoidTextBrush : NocturneKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            playerCard.Child = innerSp;
            Canvas.SetLeft(playerCard, startX + pIdx * (290 + 40));
            Canvas.SetTop(playerCard, cardY);

            playerCard.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                nocturneSelectedPlayer = pIdx;
                _ = NocturneFocus(pIdx);
            };

            nocturneZoneBack.Children.Add(playerCard);
        }

        // Bottom Action Ribbon: Add Controller, Choose Model, Tools
        var ribbonSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var addBtn = NocturneKit.NocturneButton("+ Add Controller", () => { ControllerActionRequested?.Invoke(nocturneSelectedPlayer, true); }, isPrimary: false, width: 180, height: 40);
        addBtn.Margin = new Thickness(0, 0, 16, 0);

        var chooseBtn = NocturneKit.NocturneButton("Choose Model", () => { ControllerActionRequested?.Invoke(nocturneSelectedPlayer, true); }, isPrimary: false, width: 180, height: 40);
        chooseBtn.Margin = new Thickness(0, 0, 16, 0);

        var toolsBtn = NocturneKit.NocturneButton("Tools & Themes", () => { SettingsRequested?.Invoke(); }, isPrimary: false, width: 180, height: 40);

        ribbonSp.Children.Add(addBtn);
        ribbonSp.Children.Add(chooseBtn);
        ribbonSp.Children.Add(toolsBtn);

        Canvas.SetLeft(ribbonSp, 500);
        Canvas.SetTop(ribbonSp, 670);
        nocturneZoneBack.Children.Add(ribbonSp);

        nocturneBoard.Children.Add(nocturneZoneBack);
    }

    private void NocturneRenderHero()
    {
        if (nocturneBoard == null) return;
        nocturneHeroBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        nocturneHeroBack.Children.Add(NocturneKit.CreateCityBackdrop());

        int pIdx = State.FocusedPlayer ?? 0;
        var player = State.Players[pIdx];
        string modelName = player.Model ?? "EasySMX X20";

        // Top Back Button
        var backBtn = NocturneKit.NocturneButton("← Back to Zone", () => { _ = NocturneFocus(null); }, isPrimary: false, width: 180, height: 38);
        Canvas.SetLeft(backBtn, 48);
        Canvas.SetTop(backBtn, 28);
        nocturneHeroBack.Children.Add(backBtn);

        // Center Hero Stage (Large slanted card)
        var heroSp = new StackPanel { Width = 1000 };

        var heroHeader = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        heroHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heroHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

        var titleBlock = new StackPanel();
        titleBlock.Children.Add(new TextBlock
        {
            Text = $"PLAYER {pIdx + 1}",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 32,
            Foreground = NocturneKit.VoidTextBrush
        });
        titleBlock.Children.Add(new TextBlock
        {
            Text = modelName.ToUpperInvariant() + "  ·  LOCAL PROFILE ACTIVE",
            FontFamily = NocturneKit.Body,
            FontSize = 13,
            Foreground = NocturneKit.SoftBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(titleBlock, 0);
        heroHeader.Children.Add(titleBlock);

        var statusChip = NocturneKit.NocturneChip(player.IsMockConnected ? "Connected · mock" : (player.Model != null ? "Assigned" : "Empty"), isSelected: player.Model != null);
        statusChip.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(statusChip, 1);
        heroHeader.Children.Add(statusChip);
        heroSp.Children.Add(heroHeader);

        // Controller Hero Art
        var artContainer = new Border
        {
            Width = 960,
            Height = 360,
            Background = NocturneKit.NightBrush,
            BorderBrush = NocturneKit.RuleBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 24)
        };
        var heroArt = NocturneKit.ControllerArt(player.Model ?? "x20", 520);
        artContainer.Child = heroArt;
        heroSp.Children.Add(artContainer);

        // Action Buttons Row
        var actRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var openStudioBtn = NocturneKit.NocturneButton("Open Studio", () =>
        {
            ControllerActionRequested?.Invoke(pIdx, false);
        }, isPrimary: true, width: 200, height: 44);
        openStudioBtn.Margin = new Thickness(0, 0, 12, 0);

        var switchBtn = NocturneKit.NocturneButton("Switch controller", () =>
        {
            ControllerActionRequested?.Invoke(pIdx, true);
        }, isPrimary: false, width: 190, height: 44);
        switchBtn.Margin = new Thickness(0, 0, 12, 0);

        var settingsBtn = NocturneKit.NocturneButton("Settings", () =>
        {
            SettingsRequested?.Invoke();
        }, isPrimary: false, width: 150, height: 44);

        actRow.Children.Add(openStudioBtn);
        actRow.Children.Add(switchBtn);
        actRow.Children.Add(settingsBtn);
        heroSp.Children.Add(actRow);

        var heroCard = NocturneKit.NocturneWhiteCard(heroSp, 1060, 600);
        Canvas.SetLeft(heroCard, 270);
        Canvas.SetTop(heroCard, 130);
        nocturneHeroBack.Children.Add(heroCard);

        nocturneBoard.Children.Add(nocturneHeroBack);
    }
}

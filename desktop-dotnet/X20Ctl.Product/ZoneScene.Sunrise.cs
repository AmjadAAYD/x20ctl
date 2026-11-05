using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class ZoneScene
{
    internal bool IsSunrise { get; private set; }

    private Grid? sunriseBoard;
    private Canvas? sunriseZoneBack, sunriseHeroBack;
    private int sunriseSelectedPlayer = 0;

    private void BuildSunrise()
    {
        IsSunrise = true;
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = SunriseKit.YellowBrush;
        OuterFrame.BorderBrush = SunriseKit.YellowBrush;

        sunriseBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        OuterFrame.Child = sunriseBoard;
        SunriseRenderZone();
    }

    public void SunriseSync()
    {
        if (!IsSunrise || sunriseBoard == null) return;
        SunriseRenderZone();
    }

    public async Task SunriseFocus(int? index)
    {
        if (!IsSunrise) return;
        if (index.HasValue)
        {
            sunriseSelectedPlayer = index.Value;
            State.Activate(index.Value);
        }
        else
        {
            State.Collapse();
        }
        SunriseRenderZone();
        await Task.CompletedTask;
    }

    private void SunriseRenderZone()
    {
        if (sunriseBoard == null) return;
        sunriseBoard.Children.Clear();

        bool isHero = State.FocusedPlayer.HasValue;
        if (isHero)
        {
            SunriseRenderHero();
            return;
        }

        // Zone Overview Stage
        sunriseZoneBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        sunriseZoneBack.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Header Pill
        var headerPill = new Border
        {
            Height = 44,
            CornerRadius = new CornerRadius(14),
            Background = SunriseKit.InkBrush,
            Padding = new Thickness(18, 6, 20, 6)
        };
        var hpStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hpStack.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = SunriseKit.OrangeBrush, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
        hpStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER ZONE  ·  CHANNEL BROADCAST",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 14,
            Foreground = SunriseKit.CreamBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        int connectedCount = State.Players.Count(p => p.Model != null);
        hpStack.Children.Add(new TextBlock
        {
            Text = $"  ·  {connectedCount} / 4 CONNECTED",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = SunriseKit.YellowBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        headerPill.Child = hpStack;
        Canvas.SetLeft(headerPill, 48);
        Canvas.SetTop(headerPill, 28);
        sunriseZoneBack.Children.Add(headerPill);

        // 4 Player Channel Sticker Cards (CH 1 to CH 4)
        double startX = 140;
        double cardY = 180;

        for (int i = 0; i < 4; i++)
        {
            int pIdx = i;
            var player = State.Players[i];
            bool hasModel = player.Model != null;
            string modelName = hasModel ? player.Model.Replace("EasySMX ", "") : "EMPTY CHANNEL";
            bool isSelected = (pIdx == sunriseSelectedPlayer);

            var playerCard = new Border
            {
                Width = 290,
                Height = 390,
                CornerRadius = new CornerRadius(22),
                Background = isSelected ? SunriseKit.CreamBrush : SunriseKit.CreamBrush,
                BorderBrush = isSelected ? SunriseKit.OrangeBrush : SunriseKit.InkBrush,
                BorderThickness = new Thickness(isSelected ? 3.5 : 2.0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = isSelected ? Color.FromArgb(0xbb, 0xF8, 0x7B, 0x09) : Color.FromArgb(0x60, 0x1E, 0x1D, 0x1A),
                    BlurRadius = isSelected ? 20 : 10,
                    ShadowDepth = 6,
                    Direction = 315,
                    Opacity = isSelected ? 0.6 : 0.35
                }
            };

            var innerSp = new StackPanel { Margin = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Center };

            // Channel Header (CH 1..4)
            var pHeader = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
            if (isSelected)
            {
                pHeader.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = SunriseKit.OrangeBrush, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            }
            pHeader.Children.Add(new TextBlock
            {
                Text = $"CH {pIdx + 1}",
                FontFamily = SunriseKit.Display,
                FontWeight = FontWeights.Black,
                FontSize = 16,
                Foreground = SunriseKit.InkBrush,
            });
            innerSp.Children.Add(pHeader);

            // Controller Picture or Empty Icon
            var imgBox = new Border
            {
                Width = 240,
                Height = 200,
                Background = SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 14)
            };

            if (hasModel)
            {
                var padArt = SunriseKit.ControllerArt(player.Model ?? "x20", 220);
                imgBox.Child = padArt;
            }
            else
            {
                var emptySp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                emptySp.Children.Add(new TextBlock { Text = "📺", FontSize = 36, HorizontalAlignment = HorizontalAlignment.Center });
                emptySp.Children.Add(new TextBlock { Text = "NO SIGNAL", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.OliveBrush, Margin = new Thickness(0, 8, 0, 0) });
                imgBox.Child = emptySp;
            }
            innerSp.Children.Add(imgBox);

            // Model Name
            innerSp.Children.Add(new TextBlock
            {
                Text = modelName.ToUpperInvariant(),
                FontFamily = SunriseKit.Display,
                FontWeight = FontWeights.Black,
                FontSize = 13,
                Foreground = hasModel ? SunriseKit.InkBrush : SunriseKit.OliveBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            playerCard.Child = innerSp;
            Canvas.SetLeft(playerCard, startX + pIdx * (290 + 40));
            Canvas.SetTop(playerCard, cardY);

            playerCard.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                sunriseSelectedPlayer = pIdx;
                _ = SunriseFocus(pIdx);
            };

            sunriseZoneBack.Children.Add(playerCard);
        }

        // Bottom Action Ribbon
        var ribbonSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var addBtn = SunriseKit.SunriseButton("+ Add Controller", () => { ControllerActionRequested?.Invoke(sunriseSelectedPlayer, true); }, isPrimary: false, width: 180, height: 42);
        addBtn.Margin = new Thickness(0, 0, 16, 0);

        var chooseBtn = SunriseKit.SunriseButton("Choose Model", () => { ControllerActionRequested?.Invoke(sunriseSelectedPlayer, true); }, isPrimary: false, width: 180, height: 42);
        chooseBtn.Margin = new Thickness(0, 0, 16, 0);

        var toolsBtn = SunriseKit.SunriseButton("Tools & Themes", () => { SettingsRequested?.Invoke(); }, isPrimary: true, width: 180, height: 42);

        ribbonSp.Children.Add(addBtn);
        ribbonSp.Children.Add(chooseBtn);
        ribbonSp.Children.Add(toolsBtn);

        Canvas.SetLeft(ribbonSp, 500);
        Canvas.SetTop(ribbonSp, 670);
        sunriseZoneBack.Children.Add(ribbonSp);

        sunriseBoard.Children.Add(sunriseZoneBack);
    }

    private void SunriseRenderHero()
    {
        if (sunriseBoard == null) return;
        sunriseHeroBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        sunriseHeroBack.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        int pIdx = State.FocusedPlayer ?? 0;
        var player = State.Players[pIdx];
        string modelName = player.Model ?? "EasySMX X20";

        // Top Back Button
        var backBtn = SunriseKit.SunriseButton("← Back to Zone", () => { _ = SunriseFocus(null); }, isPrimary: false, width: 180, height: 38);
        Canvas.SetLeft(backBtn, 48);
        Canvas.SetTop(backBtn, 28);
        sunriseHeroBack.Children.Add(backBtn);

        // Center Hero Stage (Large TV Screen Panel with Antennae)
        var heroSp = new StackPanel { Width = 960 };

        var heroHeader = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        heroHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heroHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

        var titleBlock = new StackPanel();
        titleBlock.Children.Add(new TextBlock
        {
            Text = $"CHANNEL {pIdx + 1}  ·  PLAYER {pIdx + 1}",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 30,
            Foreground = SunriseKit.InkBrush
        });
        titleBlock.Children.Add(new TextBlock
        {
            Text = modelName.ToUpperInvariant() + "  ·  BROADCAST READY",
            FontFamily = SunriseKit.Body,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = SunriseKit.OliveBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(titleBlock, 0);
        heroHeader.Children.Add(titleBlock);

        var statusChip = SunriseKit.SunriseChip(player.IsMockConnected ? "Connected · mock" : (player.Model != null ? "Assigned" : "Empty"), isSelected: player.Model != null);
        statusChip.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(statusChip, 1);
        heroHeader.Children.Add(statusChip);
        heroSp.Children.Add(heroHeader);

        // Controller Hero Art with concentric ripples
        var artContainer = new Border
        {
            Width = 920,
            Height = 340,
            Background = SunriseKit.CreamBrush,
            BorderBrush = SunriseKit.InkBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(16),
            Margin = new Thickness(0, 0, 0, 20)
        };
        var heroArt = SunriseKit.ControllerArt(player.Model ?? "x20", 500);
        artContainer.Child = heroArt;
        heroSp.Children.Add(artContainer);

        // Action Buttons Row
        var actRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var openStudioBtn = SunriseKit.SunriseButton("Open Studio", () =>
        {
            ControllerActionRequested?.Invoke(pIdx, false);
        }, isPrimary: true, width: 200, height: 44);
        openStudioBtn.Margin = new Thickness(0, 0, 12, 0);

        var switchBtn = SunriseKit.SunriseButton("Switch controller", () =>
        {
            ControllerActionRequested?.Invoke(pIdx, true);
        }, isPrimary: false, width: 190, height: 44);
        switchBtn.Margin = new Thickness(0, 0, 12, 0);

        var settingsBtn = SunriseKit.SunriseButton("Settings", () =>
        {
            SettingsRequested?.Invoke();
        }, isPrimary: false, width: 150, height: 44);

        actRow.Children.Add(openStudioBtn);
        actRow.Children.Add(switchBtn);
        actRow.Children.Add(settingsBtn);
        heroSp.Children.Add(actRow);

        var heroCard = SunriseKit.CreateTvScreenCard(heroSp, 1020, 580, withAntennae: true);
        Canvas.SetLeft(heroCard, 290);
        Canvas.SetTop(heroCard, 110);
        sunriseHeroBack.Children.Add(heroCard);

        sunriseBoard.Children.Add(sunriseHeroBack);
    }
}

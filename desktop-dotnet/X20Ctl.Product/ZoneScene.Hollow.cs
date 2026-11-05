using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class ZoneScene
{
    internal bool IsHollow { get; private set; }

    private Grid? hollowBoard;
    private Canvas? hollowZoneBack, hollowHeroBack;
    private int hollowSelectedPlayer = 0;

    private void BuildHollow()
    {
        IsHollow = true;
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = HollowKit.VoidBrush;
        OuterFrame.BorderBrush = HollowKit.VoidBrush;

        hollowBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        OuterFrame.Child = hollowBoard;
        HollowRenderZone();
    }

    public void HollowSync()
    {
        if (!IsHollow || hollowBoard == null) return;
        HollowRenderZone();
    }

    public async Task HollowFocus(int? index)
    {
        if (!IsHollow) return;
        if (index.HasValue)
        {
            hollowSelectedPlayer = index.Value;
            State.Activate(index.Value);
        }
        else
        {
            State.Collapse();
        }
        HollowRenderZone();
        await Task.CompletedTask;
    }

    private void HollowRenderZone()
    {
        if (hollowBoard == null) return;
        hollowBoard.Children.Clear();

        bool isHero = State.FocusedPlayer.HasValue;
        if (isHero)
        {
            HollowRenderHero();
            return;
        }

        // 1. Zone Overview Stage
        hollowZoneBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        hollowZoneBack.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header
        var headerPill = new Border
        {
            Height = 44,
            Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x05, 0x0c, 0x14)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.5)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(18, 6, 20, 6)
        };
        var hpStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hpStack.Children.Add(new Path { Data = HollowKit.CreateDiamond(8), Fill = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
        hpStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER ZONE",
            FontFamily = HollowKit.Display,
            FontSize = 14,
            Foreground = HollowKit.IvoryTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        int connectedCount = State.Players.Count(p => p.Model != null);
        hpStack.Children.Add(new TextBlock
        {
            Text = $"  ·  {connectedCount} / 4 CONNECTED",
            FontFamily = HollowKit.Display,
            FontSize = 12,
            Foreground = HollowKit.SoftTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        headerPill.Child = hpStack;
        Canvas.SetLeft(headerPill, 48);
        Canvas.SetTop(headerPill, 28);
        hollowZoneBack.Children.Add(headerPill);

        // 4 Arched Alcove Cards (P1 to P4)
        double startX = 192;
        double cardY = 220;

        for (int i = 0; i < 4; i++)
        {
            int pIdx = i;
            var player = State.Players[i];
            bool hasModel = player.Model != null;
            string modelName = hasModel ? player.Model.Replace("EasySMX ", "") : "EMPTY ALCOVE";
            bool isSelected = (pIdx == hollowSelectedPlayer);

            var alcoveCard = new Border
            {
                Width = 280,
                Height = 360,
                CornerRadius = new CornerRadius(4),
                Background = isSelected ? new SolidColorBrush(Color.FromArgb(0xf0, 0x14, 0x22, 0x32)) : new SolidColorBrush(Color.FromArgb(0xd0, 0x07, 0x10, 0x18)),
                BorderBrush = isSelected ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.45)),
                BorderThickness = new Thickness(isSelected ? 1.5 : 1.0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = isSelected ? 24 : 12,
                    ShadowDepth = 6,
                    Opacity = 0.7
                }
            };

            var innerSp = new StackPanel { Margin = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Center };

            // Player Title & Diamond
            var pHeader = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 14) };
            if (isSelected)
            {
                pHeader.Children.Add(new Path { Data = HollowKit.CreateDiamond(6), Fill = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            }
            pHeader.Children.Add(new TextBlock
            {
                Text = $"PLAYER {pIdx + 1}",
                FontFamily = HollowKit.Display,
                FontSize = 13,
                Foreground = isSelected ? HollowKit.IvoryTextBrush : HollowKit.SoftTextBrush,
            });
            innerSp.Children.Add(pHeader);

            // Controller Picture or Silhouette
            var imgBox = new Border
            {
                Width = 240,
                Height = 180,
                Background = new SolidColorBrush(Color.FromArgb(0x60, 0x02, 0x05, 0x0a)),
                BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.3)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 0, 14)
            };

            if (hasModel)
            {
                var padArt = HollowKit.ControllerArt(player.Model ?? "x20", 200);
                imgBox.Child = padArt;
            }
            else
            {
                var emptySp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                emptySp.Children.Add(new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 32, Foreground = HollowKit.MutedTextBrush, HorizontalAlignment = HorizontalAlignment.Center });
                emptySp.Children.Add(new TextBlock { Text = "NO CONTROLLER", FontFamily = HollowKit.Display, FontSize = 11, Foreground = HollowKit.MutedTextBrush, Margin = new Thickness(0, 6, 0, 0) });
                imgBox.Child = emptySp;
            }
            innerSp.Children.Add(imgBox);

            // Model Name
            innerSp.Children.Add(new TextBlock
            {
                Text = modelName.ToUpperInvariant(),
                FontFamily = HollowKit.Display,
                FontSize = 13,
                Foreground = hasModel ? HollowKit.IvoryTextBrush : HollowKit.MutedTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            alcoveCard.Child = innerSp;
            Canvas.SetLeft(alcoveCard, startX + pIdx * (280 + 32));
            Canvas.SetTop(alcoveCard, cardY);

            alcoveCard.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                hollowSelectedPlayer = pIdx;
                _ = HollowFocus(pIdx);
            };

            hollowZoneBack.Children.Add(alcoveCard);
        }

        // Bottom Action Ribbon: Add Controller, Choose Model, Tools
        var ribbonSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var addBtn = HollowKit.HollowButton("+ Add Controller", () => { ControllerActionRequested?.Invoke(hollowSelectedPlayer, true); }, isPrimary: false, width: 160, height: 38, tag: "theme:zone:add");
        addBtn.Margin = new Thickness(0, 0, 16, 0);

        var chooseBtn = HollowKit.HollowButton("Choose Model", () => { ControllerActionRequested?.Invoke(hollowSelectedPlayer, true); }, isPrimary: false, width: 160, height: 38, tag: "theme:zone:choose");
        chooseBtn.Margin = new Thickness(0, 0, 16, 0);

        var toolsBtn = HollowKit.HollowButton("Tools & Themes", () => { SettingsRequested?.Invoke(); }, isPrimary: false, width: 160, height: 38, tag: "theme:zone:tools");

        ribbonSp.Children.Add(addBtn);
        ribbonSp.Children.Add(chooseBtn);
        ribbonSp.Children.Add(toolsBtn);

        Canvas.SetLeft(ribbonSp, 520);
        Canvas.SetTop(ribbonSp, 640);
        hollowZoneBack.Children.Add(ribbonSp);

        // Bottom Dock
        var dock = HollowKit.HollowDock("Zone");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        hollowZoneBack.Children.Add(dock);

        hollowBoard.Children.Add(hollowZoneBack);
    }

    private void HollowRenderHero()
    {
        if (hollowBoard == null) return;
        hollowHeroBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        hollowHeroBack.Children.Add(HollowKit.CreateCavernBackdrop());

        int pIdx = State.FocusedPlayer ?? 0;
        var player = State.Players[pIdx];
        string modelName = player.Model ?? "EasySMX X20";

        // Top Back Button
        var backBtn = HollowKit.HollowButton("← Back to Alcoves", () => { _ = HollowFocus(null); }, isPrimary: false, width: 170, height: 36, tag: "theme:hero:back");
        Canvas.SetLeft(backBtn, 48);
        Canvas.SetTop(backBtn, 28);
        hollowHeroBack.Children.Add(backBtn);

        // Center Hero Stage (x=200, y=120, w=1200, h=660)
        var heroCard = new Canvas { Width = 1200, Height = 660 };

        // Controller Visual with Lantern Glow
        var lanternPedestal = new Ellipse
        {
            Width = 600,
            Height = 180,
            Fill = new RadialGradientBrush(Color.FromArgb(0x40, 0xb8, 0xcc, 0xe0), Colors.Transparent)
        };
        Canvas.SetLeft(lanternPedestal, 300);
        Canvas.SetTop(lanternPedestal, 240);
        heroCard.Children.Add(lanternPedestal);

        var heroArt = HollowKit.ControllerArt(player.Model ?? "x20", 480);
        Canvas.SetLeft(heroArt, 360);
        Canvas.SetTop(heroArt, 50);
        heroCard.Children.Add(heroArt);

        // Controller Title & Identity
        var titleStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Width = 1200 };
        titleStack.Children.Add(new TextBlock
        {
            Text = $"PLAYER {pIdx + 1}  ·  {modelName.ToUpperInvariant()}",
            FontFamily = HollowKit.Display,
            FontSize = 24,
            Foreground = HollowKit.IvoryTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = "Connected over Bluetooth  ·  Ready for Studio calibration",
            FontFamily = HollowKit.Body,
            FontSize = 13.5,
            Foreground = HollowKit.MutedTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Canvas.SetLeft(titleStack, 0);
        Canvas.SetTop(titleStack, 380);
        heroCard.Children.Add(titleStack);

        // Action Buttons Row: Open Studio, Switch controller, Settings
        var actSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

        var openStudioBtn = HollowKit.HollowButton("Open Studio", () =>
        {
            ControllerActionRequested?.Invoke(pIdx, false);
        }, isPrimary: true, width: 180, height: 42, tag: "theme:hero:studio");
        openStudioBtn.Margin = new Thickness(0, 0, 16, 0);

        var switchBtn = HollowKit.HollowButton("Switch controller", () =>
        {
            ControllerActionRequested?.Invoke(pIdx, true);
        }, isPrimary: false, width: 180, height: 42, tag: "theme:hero:switch");
        switchBtn.Margin = new Thickness(0, 0, 16, 0);

        var settingsBtn = HollowKit.HollowButton("Settings", () =>
        {
            SettingsRequested?.Invoke();
        }, isPrimary: false, width: 160, height: 42, tag: "theme:hero:settings");

        actSp.Children.Add(openStudioBtn);
        actSp.Children.Add(switchBtn);
        actSp.Children.Add(settingsBtn);

        Canvas.SetLeft(actSp, 330);
        Canvas.SetTop(actSp, 480);
        heroCard.Children.Add(actSp);

        var framedHero = HollowKit.FiligreeCard(heroCard, padding: 24, cornerSize: 18);
        framedHero.Width = 1248;
        framedHero.Height = 708;
        Canvas.SetLeft(framedHero, 176);
        Canvas.SetTop(framedHero, 90);
        hollowHeroBack.Children.Add(framedHero);

        // Bottom Dock
        var dock = HollowKit.HollowDock("Hero");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        hollowHeroBack.Children.Add(dock);

        hollowBoard.Children.Add(hollowHeroBack);
    }
}

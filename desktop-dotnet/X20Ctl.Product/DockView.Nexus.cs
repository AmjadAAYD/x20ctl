using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class DockView
{
    private bool nexus;
    private Canvas? nexusFace;

    private void InitNexus()
    {
        if (ThemeCatalog.Current.Id != "nexus") return;
        nexus = true;
        Card.Visibility = Visibility.Collapsed;
        FocusButton.Cursor = System.Windows.Input.Cursors.Hand;

        nexusFace = new Canvas { IsHitTestVisible = false };
        ContentCanvas.Children.Insert(0, nexusFace);
    }

    private void PresentNexus(bool hero)
    {
        if (!nexus || nexusFace == null) return;
        nexusFace.Children.Clear();

        foreach (UIElement e in new UIElement[] { Card, PlayerPill, Details, Halo, ArtShadow, Art, AddControllerButton, RemoveControllerButton, ActionCanvas, ArrowBadge, PlusBadge, Silhouette })
        {
            e.Visibility = Visibility.Collapsed;
        }

        if (hero)
        {
            nexusFace.Visibility = Visibility.Collapsed;
            return;
        }

        nexusFace.Visibility = Visibility.Visible;
        bool isLit = FocusButton.IsKeyboardFocused || FocusButton.IsMouseOver || Halo.Visibility == Visibility.Visible;
        bool focused = (Index == 0) || isLit;
        bool assigned = Player.Model != null;
        string shortName = Player.Model ?? "";
        string finishName = Morph.Finish ?? "";

        double w = 352, h = 360;
        var tile = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(6),
            Background = NexusKit.TileBrush,
            ClipToBounds = true
        };

        if (focused)
        {
            tile.BorderBrush = NexusKit.GreenBrush;
            tile.BorderThickness = new Thickness(4);
            tile.Effect = new DropShadowEffect
            {
                BlurRadius = 30,
                ShadowDepth = 10,
                Direction = 270,
                Opacity = 0.6,
                Color = Colors.Black
            };
        }

        var grid = new Grid();

        if (assigned)
        {
            string modKey = Index switch
            {
                0 => (string.IsNullOrEmpty(Player.ModelId) || Player.ModelId == "x20") ? "x20pro" : Player.ModelId,
                1 => (string.IsNullOrEmpty(Player.ModelId) || Player.ModelId == "x15") ? "x15-starfield" : Player.ModelId,
                _ => shortName
            };
            string displayModel = Index switch
            {
                0 => "X20 Pro",
                1 => "X15 Starfield",
                _ => shortName.Replace("EasySMX ", "")
            };

            // Cover with radial finish background + focal zoom + dark bottom shading
            var cover = NexusKit.CoverTile(modKey, w, h, "setups", dark: 0.8, radius: 6);
            grid.Children.Add(cover);

            // Status badge top-left (14, 12)
            var statusBadge = new TextBlock
            {
                Text = !string.IsNullOrEmpty(finishName) ? finishName : "Offline",
                FontFamily = NexusKit.Body,
                FontSize = 13,
                Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.85)),
                Margin = new Thickness(14, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            grid.Children.Add(statusBadge);

            // Bold label bottom-left (14, bottom 12)
            var label = new TextBlock
            {
                Text = $"Player {Index + 1} · {displayModel}",
                FontFamily = NexusKit.Body,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = NexusKit.WhiteBrush,
                Margin = new Thickness(14, 0, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Direction = 270, Opacity = 0.8, Color = Colors.Black }
            };
            grid.Children.Add(label);
        }
        else
        {
            // Empty slot #1b1d21 background
            grid.Children.Add(new Rectangle { Width = w, Height = h, Fill = NexusKit.B("#1b1d21") });

            // Silhouette
            var sil = NexusKit.ControllerArt("x20", 240);
            if (sil != null)
            {
                sil.Opacity = 0.14;
                sil.HorizontalAlignment = HorizontalAlignment.Center;
                sil.VerticalAlignment = VerticalAlignment.Center;
                sil.Margin = new Thickness(0, -20, 0, 0);
                grid.Children.Add(sil);
            }

            // Add controller label
            var addLabel = new TextBlock
            {
                Text = $"Player {Index + 1} · Add controller",
                FontFamily = NexusKit.Body,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.7)),
                Margin = new Thickness(14, 0, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            grid.Children.Add(addLabel);
        }

        tile.Child = grid;
        nexusFace.Children.Add(tile);
    }
}

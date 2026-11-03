using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// DockView implementation for Spotlight theme:
/// Renders player card on stage mark in Zone overview, and handles focus presentation.
/// </summary>
public partial class DockView
{
    private bool spotlight;
    private Canvas? spotlightFace;
    public bool IsSpotlightLit { get; set; }

    public void PresentSpotlightDirect() => PresentSpotlight(false);

    private void InitSpotlight()
    {
        if (ThemeCatalog.Current.Id != "spotlight") return;
        spotlight = true;
        Card.Visibility = Visibility.Collapsed;
        FocusButton.Cursor = System.Windows.Input.Cursors.Hand;

        spotlightFace = new Canvas { IsHitTestVisible = false };
        ContentCanvas.Children.Insert(0, spotlightFace);
    }

    private void PresentSpotlight(bool hero)
    {
        if (!spotlight || spotlightFace == null) return;

        // Completely collapse ALL default theme elements so there is zero overlap
        foreach (UIElement e in new UIElement[] { Card, PlayerPill, Details, Halo, ArtShadow, Art, AddControllerButton, RemoveControllerButton, ActionCanvas, ArrowBadge, PlusBadge, Silhouette })
        {
            e.Visibility = Visibility.Collapsed;
        }

        spotlightFace.Children.Clear();

        if (hero)
        {
            // Hero details are managed directly by ZoneScene.Spotlight
            spotlightFace.Visibility = Visibility.Collapsed;
            return;
        }

        spotlightFace.Visibility = Visibility.Visible;
        bool isLit = IsSpotlightLit || FocusButton.IsKeyboardFocused || FocusButton.IsMouseOver || Halo.Visibility == Visibility.Visible;
        var assigned = Player.Model != null;
        string shortName = Player.Model ?? "";

        double dockW = double.IsNaN(Width) || Width <= 0 ? 280 : Width;
        double dockH = double.IsNaN(Height) || Height <= 0 ? 420 : Height;

        if (assigned)
        {
            string outlineModel = Player.ModelId ?? (Player.Model switch
            {
                "EasySMX X15" => "x15",
                "EasySMX X05 Pro" => "x05pro",
                "EasySMX X10" => "x10",
                "EasySMX D10" => "d10",
                "EasySMX Dune" => "dune",
                _ => "x20"
            });
            string finishName = !string.IsNullOrEmpty(Morph.Finish) ? Morph.Finish : PadColours.For(Index, outlineModel);

            // 1. Stage pedestal floor ring under pad
            double pedW = 220;
            var pedestal = new Ellipse
            {
                Width = pedW,
                Height = 24,
                Stroke = SpotlightKit.B(isLit ? SpotlightKit.GoldHi : SpotlightKit.A(SpotlightKit.Gold, 0.40)),
                StrokeThickness = isLit ? 1.6 : 1.0,
                Fill = new RadialGradientBrush
                {
                    Center = new(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
                    GradientStops =
                    {
                        new(SpotlightKit.A(SpotlightKit.C("#ffe6c4"), isLit ? 0.35 : 0.12), 0.0),
                        new(SpotlightKit.A(SpotlightKit.C("#d9b77a"), isLit ? 0.18 : 0.05), 0.60),
                        new(Colors.Transparent, 1.0)
                    }
                }
            };
            Canvas.SetLeft(pedestal, (dockW - pedW) / 2);
            Canvas.SetTop(pedestal, 160);
            spotlightFace.Children.Add(pedestal);

            // 2. Contact floor shadow
            double shdW = 170;
            var contactShadow = new Ellipse
            {
                Width = shdW,
                Height = 12,
                Fill = new RadialGradientBrush
                {
                    Center = new(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
                    GradientStops = { new(SpotlightKit.A(Colors.Black, 0.75), 0.0), new(Colors.Transparent, 1.0) }
                }
            };
            Canvas.SetLeft(contactShadow, (dockW - shdW) / 2);
            Canvas.SetTop(contactShadow, 166);
            spotlightFace.Children.Add(contactShadow);

            // 3. Controller artwork image container
            double artW = 220;
            double artH = artW * 0.65;
            double artX = (dockW - artW) / 2;
            double artY = 36;

            var padGrid = new Grid { Width = artW, Height = artH };

            // Dedicated rim highlight if lit
            if (isLit)
            {
                var rimBmp = SpotlightParts.GetControllerRimBitmap(outlineModel);
                if (rimBmp != null)
                {
                    var rimImg = new Image
                    {
                        Source = rimBmp,
                        Width = artW,
                        Height = artH,
                        Stretch = Stretch.Uniform,
                        Opacity = 0.85,
                        Effect = new DropShadowEffect
                        {
                            Color = SpotlightKit.C("#ffd27a"),
                            BlurRadius = 14,
                            ShadowDepth = 0,
                            Opacity = 0.80
                        }
                    };
                    RenderOptions.SetBitmapScalingMode(rimImg, BitmapScalingMode.HighQuality);
                    padGrid.Children.Add(rimImg);
                }
            }

            var bmp = SpotlightParts.GetControllerBitmap(outlineModel);
            if (bmp != null)
            {
                var padImg = new Image
                {
                    Source = bmp,
                    Width = artW,
                    Height = artH,
                    Stretch = Stretch.Uniform,
                    Opacity = isLit ? 1.0 : 0.62,
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = isLit ? 20 : 12,
                        ShadowDepth = isLit ? 14 : 8,
                        Opacity = 0.65
                    }
                };
                RenderOptions.SetBitmapScalingMode(padImg, BitmapScalingMode.HighQuality);
                padGrid.Children.Add(padImg);
            }

            Canvas.SetLeft(padGrid, artX);
            Canvas.SetTop(padGrid, artY);
            spotlightFace.Children.Add(padGrid);

            // 4. Text below controller at y = 236
            var textStack = new StackPanel { Width = 260, HorizontalAlignment = HorizontalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = $"PLAYER {Index + 1}",
                FontFamily = SpotlightKit.Display,
                FontSize = 11,
                FontWeight = FontWeights.Light,
                Foreground = isLit ? SpotlightKit.GoldHiBrush : SpotlightKit.MuteBrush,
                TextAlignment = TextAlignment.Center
            });

            textStack.Children.Add(new TextBlock
            {
                Text = shortName,
                FontFamily = SpotlightKit.Body,
                FontWeight = isLit ? FontWeights.SemiBold : FontWeights.Normal,
                FontSize = isLit ? 23 : 20,
                Foreground = isLit ? SpotlightKit.B("#fff6e6") : SpotlightKit.B("#b3a48e"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            });

            textStack.Children.Add(new TextBlock
            {
                Text = !string.IsNullOrEmpty(finishName) ? finishName : "Standard Edition",
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 14.5,
                Foreground = isLit ? SpotlightKit.RoseBrush : SpotlightKit.B("#7a6c5a"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            });

            var statusRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            statusRow.Children.Add(new Ellipse
            {
                Width = 7, Height = 7,
                Fill = Player.IsMockConnected ? SpotlightKit.B("#2ecc71") : SpotlightKit.B("#7a6c5a"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            statusRow.Children.Add(new TextBlock
            {
                Text = Player.IsMockConnected ? "Connected · Mock" : "Standby",
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Foreground = isLit ? SpotlightKit.B("#fff2d6") : SpotlightKit.B("#7a6c5a"),
                VerticalAlignment = VerticalAlignment.Center
            });
            textStack.Children.Add(statusRow);

            Canvas.SetLeft(textStack, (dockW - 260) / 2);
            Canvas.SetTop(textStack, 186);
            spotlightFace.Children.Add(textStack);
        }
        else
        {
            // Empty Audition slot
            var emptyGrid = new Grid { Width = 160, Height = 100 };
            var border = new Border
            {
                Width = 140, Height = 90,
                CornerRadius = new CornerRadius(14),
                BorderBrush = SpotlightKit.B(isLit ? SpotlightKit.Gold : SpotlightKit.A(SpotlightKit.Gold, 0.30)),
                BorderThickness = new Thickness(1.2),
                Background = Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = "+",
                    FontFamily = SpotlightKit.Display,
                    FontSize = 32,
                    Foreground = isLit ? SpotlightKit.GoldHiBrush : SpotlightKit.B("#7a6c5a"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            emptyGrid.Children.Add(border);

            Canvas.SetLeft(emptyGrid, (dockW - 160) / 2);
            Canvas.SetTop(emptyGrid, 76);
            spotlightFace.Children.Add(emptyGrid);

            var textStack = new StackPanel { Width = 260, HorizontalAlignment = HorizontalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = $"PLAYER {Index + 1}",
                FontFamily = SpotlightKit.Display,
                FontSize = 11,
                FontWeight = FontWeights.Light,
                Foreground = isLit ? SpotlightKit.GoldHiBrush : SpotlightKit.MuteBrush,
                TextAlignment = TextAlignment.Center
            });

            textStack.Children.Add(new TextBlock
            {
                Text = "Empty Slot",
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 21,
                Foreground = isLit ? SpotlightKit.B("#fff6e6") : SpotlightKit.B("#7a6c5a"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            textStack.Children.Add(new TextBlock
            {
                Text = "Press A or Click to Audition",
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 13,
                Foreground = isLit ? SpotlightKit.RoseBrush : SpotlightKit.B("#5a4d3d"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            });

            Canvas.SetLeft(textStack, (dockW - 260) / 2);
            Canvas.SetTop(textStack, 186);
            spotlightFace.Children.Add(textStack);
        }
    }
}

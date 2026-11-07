using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public sealed partial class ModelSelectorView
{
    private void BuildNexusSelector(ControllerCatalog catalog, Dictionary<string, Facts> facts)
    {
        var models = catalog.Models.ToDictionary(model => model.Id);

        var chooseItems = new (string Key, string ArtKey, string Name, string Specs, string Status)[]
        {
            ("x20", "x20", "EasySMX X20", "Hall sticks · Hall triggers · RGB · 4 macro paddles", "Full configuration"),
            ("x20_pro", "x20pro", "EasySMX X20 Pro", "TMR sticks · Hall / microswitch triggers · RGB · screen · trigger haptics · 6 macro paddles", "Full configuration"),
            ("x05_pro", "x05pro", "EasySMX X05 Pro", "Hall sticks · Hall / microswitch triggers · RGB · trigger haptics · 2 macro paddles", "Full PC Mode Configuration"),
            ("x10", "x10", "EasySMX X10", "Hall sticks · Hall triggers · 2 macro paddles", "Full PC Mode Configuration"),
            ("d10", "d10-purple", "EasySMX D10", "TMR sticks · Hall / microswitch triggers · RGB · 2 macro paddles", "Full PC Mode Configuration"),
            ("x15", "x15-wukong", "EasySMX X15", "Hall sticks · Hall triggers · RGB · 2 macro paddles", "Full PC Mode Configuration"),
            ("dune", "dune", "EasySMX Dune", "TMR sticks · RGB · screen · trigger haptics · 4 macro paddles", "Full PC Mode Configuration")
        };

        string selectedModelId = "x20";
        var board = new Canvas { Name = "NexusChoose", Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Key art band (exact t_nexus.py:226 choose keyband)
        var keyBand = new Canvas { Width = 1600, Height = 560, ClipToBounds = true };
        var bg = new Rectangle
        {
            Width = 1600,
            Height = 560,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.481, -0.12),
                EndPoint = new Point(0.519, 1.12),
                GradientStops =
                {
                    new(Color.FromRgb(0xc9, 0xd0, 0xdc), 0.0),
                    new(Color.FromRgb(0x8f, 0x9a, 0xab), 0.38),
                    new(Color.FromRgb(0x3f, 0x46, 0x52), 0.70),
                    new(Color.FromRgb(0x10, 0x11, 0x13), 1.0)
                }
            }
        };
        keyBand.Children.Add(bg);

        for (int i = 0; i < 14; i++)
        {
            double lineY = 170 + i * 26;
            double lineX = -200 + i * 30;
            double lineH = 2 + (i % 3);
            double lineOp = 0.3 + (i % 4) * 0.1;
            var line = new Rectangle
            {
                Width = 1000,
                Height = lineH,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0.5),
                    EndPoint = new Point(1, 0.5),
                    GradientStops =
                    {
                        new(Colors.Transparent, 0.0),
                        new(Color.FromArgb((byte)(255 * 0.55), 255, 255, 255), 0.5),
                        new(Colors.Transparent, 1.0)
                    }
                },
                Opacity = lineOp,
                RenderTransform = new RotateTransform(-8)
            };
            Canvas.SetLeft(line, lineX);
            Canvas.SetTop(line, lineY);
            keyBand.Children.Add(line);
        }

        var padImg = NexusKit.ControllerArt("x20", 760);
        if (padImg != null)
        {
            padImg.RenderTransformOrigin = new Point(0.5, 0.5);
            padImg.RenderTransform = new RotateTransform(6);
            padImg.Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 40, Direction = 270, Opacity = 0.55, Color = Colors.Black };
            Canvas.SetLeft(padImg, 760);
            Canvas.SetTop(padImg, 40);
            keyBand.Children.Add(padImg);
        }

        var btmFade = new Rectangle
        {
            Width = 1600,
            Height = 240,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 1),
                EndPoint = new Point(0.5, 0),
                GradientStops =
                {
                    new(Color.FromRgb(0x10, 0x11, 0x13), 0.0),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        Canvas.SetTop(btmFade, 320);
        keyBand.Children.Add(btmFade);

        var leftFade = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(0.45, 0.5),
                GradientStops =
                {
                    new(Color.FromArgb((byte)(255 * 0.75), 0x10, 0x11, 0x13), 0.0),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        keyBand.Children.Add(leftFade);
        board.Children.Add(keyBand);

        // 2. Top bar
        var topBar = NexusKit.TopBar(Player, onTools: () => Cancelled?.Invoke());
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        board.Children.Add(topBar);

        // 3. Header text at (68, 170)
        var headStack = new StackPanel();
        headStack.Children.Add(new TextBlock
        {
            Text = $"PLAYER {Player + 1}",
            FontFamily = NexusKit.Body,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.GreenBrush
        });
        headStack.Children.Add(new TextBlock
        {
            Text = "Which controller is this?",
            FontFamily = NexusKit.Display,
            FontSize = 40,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush,
            Margin = new Thickness(0, 6, 0, 0)
        });
        headStack.Children.Add(new TextBlock
        {
            Text = "Pick the model in your hands. This sets its layout and tools; connecting the hardware is a separate step.",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 8, 0, 0),
            MaxWidth = 560,
            TextWrapping = TextWrapping.Wrap
        });
        Canvas.SetLeft(headStack, 68);
        Canvas.SetTop(headStack, 170);
        board.Children.Add(headStack);

        // 4. Tiles Canvas Host
        var tilesCanvas = new Canvas { Width = 1600, Height = 900 };
        board.Children.Add(tilesCanvas);

        // 5. Bottom Panel at (68, 676, 1464x150)
        var bottomPanel = new Border
        {
            Width = 1464,
            Height = 150,
            Padding = new Thickness(30, 24, 30, 24),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        Canvas.SetLeft(bottomPanel, 68);
        Canvas.SetTop(bottomPanel, 676);
        board.Children.Add(bottomPanel);

        void RenderTiles(string activeId)
        {
            selectedModelId = activeId;
            tilesCanvas.Children.Clear();

            var activeItem = chooseItems.FirstOrDefault(item => item.Key == activeId);
            if (activeItem.Key == null) activeItem = chooseItems[0];

            double x = 68;
            for (int i = 0; i < chooseItems.Length; i++)
            {
                var item = chooseItems[i];
                bool isSel = (item.Key == activeId);

                if (isSel)
                {
                    var cover = NexusKit.CoverTile(item.ArtKey, 212, 174, "dashboard", dark: 0.8, radius: 6);
                    var t = NexusKit.Tile(212, 174, cover, item.Name.Replace("EasySMX ", ""), focused: true, onClick: () =>
                    {
                        if (models.TryGetValue(item.Key, out var mod))
                        {
                            Chosen?.Invoke(mod);
                        }
                    });
                    Canvas.SetLeft(t, x);
                    Canvas.SetTop(t, 475);
                    tilesCanvas.Children.Add(t);
                    x += 230;
                }
                else
                {
                    string pickId = item.Key;
                    var cover = NexusKit.CoverTile(item.ArtKey, 133, 133, "dashboard", radius: 6);
                    var t = NexusKit.Tile(133, 133, cover, null, focused: false, onClick: () =>
                    {
                        RenderTiles(pickId);
                    });
                    Canvas.SetLeft(t, x);
                    Canvas.SetTop(t, 515);
                    tilesCanvas.Children.Add(t);
                    x += 141;
                }
            }

            // Bottom panel details
            var panelGrid = new Grid();
            panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(520) });
            panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = activeItem.Name,
                FontFamily = NexusKit.Display,
                FontSize = 28,
                FontWeight = FontWeights.SemiBold,
                Foreground = NexusKit.WhiteBrush
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = activeItem.Specs,
                FontFamily = NexusKit.Body,
                FontSize = 16,
                Foreground = NexusKit.SubBrush,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(infoStack, 0);
            panelGrid.Children.Add(infoStack);

            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            var badge = new Border
            {
                Padding = new Thickness(16, 8, 16, 8),
                CornerRadius = new CornerRadius(4),
                Background = NexusKit.GreenDeepBrush
            };
            var badgeRow = new StackPanel { Orientation = Orientation.Horizontal };
            badgeRow.Children.Add(new TextBlock
            {
                Text = "\uE73E",
                FontFamily = NexusKit.Icons,
                FontSize = 16,
                Foreground = NexusKit.WhiteBrush,
                Margin = new Thickness(0, 0, 8, 0)
            });
            badgeRow.Children.Add(new TextBlock
            {
                Text = activeItem.Status,
                FontFamily = NexusKit.Body,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = NexusKit.WhiteBrush
            });
            badge.Child = badgeRow;
            rightStack.Children.Add(badge);

            Grid.SetColumn(rightStack, 1);
            panelGrid.Children.Add(rightStack);

            bottomPanel.Child = panelGrid;
        }

        RenderTiles("x20");

        // 6. Hints at bottom right
        var hints = NexusKit.HintsBar(new[] { ("A", "Choose"), ("B", "Back"), ("\u271D", "Move") });
        Canvas.SetRight(hints, 62);
        Canvas.SetBottom(hints, 32);
        board.Children.Add(hints);

        Children.Clear();
        Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = board });
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Hybrid;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Controller Zone and Hero presentation for the Hybrid theme:
/// "Two halves, one machine." Tabletop perspective glass surface with standing cards
/// on floor shadows, cyan top tabs, and split clamshell heroic view.
/// Strictly implements t_hybrid.py:372 zone() and t_hybrid.py:284 hero().
/// </summary>
public partial class ZoneScene
{
    internal bool IsHybrid => ThemeCatalog.Current.Id == "hybrid";

    private Grid? hybridBoard;
    private Canvas? hybridZoneBack, hybridHeroBack;

    private void BuildHybrid()
    {
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = HybridKit.InkBrush;
        OuterFrame.BorderBrush = HybridKit.InkBrush;

        hybridBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        // 1. Zone Overview Stage
        hybridZoneBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        hybridZoneBack.Children.Add(HybridKit.Halves(800, 250));
        hybridZoneBack.Children.Add(HybridKit.Brand(44, 30, true));
        hybridZoneBack.Children.Add(HybridKit.TopRight(false, 0));

        // Title at (150, 120)
        var title = new TextBlock
        {
            Text = "Controller Zone",
            FontFamily = HybridKit.Display,
            FontSize = 46,
            FontWeight = FontWeights.ExtraBold,
            Foreground = HybridKit.InkBrush
        };
        Canvas.SetLeft(title, 150);
        Canvas.SetTop(title, 120);
        hybridZoneBack.Children.Add(title);

        // Perspective glass table at y = 676
        hybridZoneBack.Children.Add(HybridKit.TabletopStage(676));

        // 4 Standing Cards on the table
        double cardW = 236;
        double cardH = 330;
        double gap = 34;
        double x0 = 800 - (4 * cardW + 3 * gap) / 2;

        var slots = new (string? Model, int PlayerNum, string Name, string Finish)[]
        {
            ("d10-purple", 1, "D10", "Farout Purple"),
            ("x20-black", 2, "X20", "Shadow Black"),
            (null, 3, "", ""),
            (null, 4, "", "")
        };

        for (int i = 0; i < slots.Length; i++)
        {
            int pIndex = i;
            var (model, num, name, finish) = slots[i];
            double x = x0 + i * (cardW + gap);
            double y = 676 - cardH + 16;
            bool on = i == 0;

            var inner = new Canvas { Width = cardW, Height = cardH, ClipToBounds = true };
            if (model != null)
            {
                // Gradient card bg
                var bg = new Rectangle
                {
                    Width = cardW,
                    Height = cardH,
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0.5, 0),
                        EndPoint = new Point(0.5, 1),
                        GradientStops = { new(HybridKit.C("#fbf8ff"), 0.0), new(HybridKit.C("#e9dff7"), 1.0) }
                    }
                };
                inner.Children.Add(bg);

                // Player badge top left
                var pBadge = new StackPanel { Orientation = Orientation.Horizontal };
                for (int d = 0; d < 4; d++)
                {
                    var dash = new Border
                    {
                        Width = 14,
                        Height = 4,
                        CornerRadius = new CornerRadius(2),
                        Margin = new Thickness(d == 0 ? 0 : 3, 0, 0, 0),
                        Background = d < num ? HybridKit.InkBrush : HybridKit.B("#ab9ec0")
                    };
                    pBadge.Children.Add(dash);
                }
                pBadge.Children.Add(new TextBlock
                {
                    Text = $"Player {num}",
                    FontFamily = HybridKit.Display,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = HybridKit.InkBrush,
                    Margin = new Thickness(8, -3, 0, 0)
                });
                Canvas.SetLeft(pBadge, 18);
                Canvas.SetTop(pBadge, 18);
                inner.Children.Add(pBadge);

                // Controller art
                var padImg = HybridKit.ControllerArt(model, cardW - 32);
                if (padImg != null)
                {
                    padImg.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 6, Opacity = 0.35 };
                    Canvas.SetLeft(padImg, 16);
                    Canvas.SetTop(padImg, 74);
                    inner.Children.Add(padImg);
                }

                // Name and finish bottom
                var textStack = new StackPanel { Orientation = Orientation.Vertical, Width = cardW - 32 };
                textStack.Children.Add(new TextBlock
                {
                    Text = name,
                    FontFamily = HybridKit.Display,
                    FontSize = 20,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = HybridKit.InkBrush
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = finish,
                    FontFamily = HybridKit.Body,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = HybridKit.B("#6d5786")
                });
                Canvas.SetLeft(textStack, 18);
                Canvas.SetTop(textStack, cardH - 64);
                inner.Children.Add(textStack);
            }
            else
            {
                // Empty slot card
                var bg = new Rectangle
                {
                    Width = cardW,
                    Height = cardH,
                    Fill = HybridKit.B("rgba(30,22,40,.25)")
                };
                inner.Children.Add(bg);

                var emptyText = new TextBlock
                {
                    Text = $"Player {num}\nEmpty Slot",
                    FontFamily = HybridKit.Display,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = HybridKit.B("rgba(255,255,255,.35)"),
                    TextAlignment = TextAlignment.Center,
                    Width = cardW
                };
                Canvas.SetTop(emptyText, cardH / 2 - 20);
                inner.Children.Add(emptyText);
            }

            var standCard = HybridKit.StandCard(x, y, cardW, cardH, inner, on: on, onClick: async () =>
            {
                if (model != null)
                {
                    await FocusPlayerAsync(pIndex);
                }
            });
            hybridZoneBack.Children.Add(standCard);
        }

        // Add hint row at bottom right
        var hintsPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var bHint = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 0) };
        bHint.Children.Add(new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = HybridKit.WhiteBrush,
            Margin = new Thickness(0, 0, 6, 0),
            Child = new TextBlock
            {
                Text = "A",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.InkBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });
        bHint.Children.Add(new TextBlock { Text = "Select Controller", FontFamily = HybridKit.Body, FontSize = 14, Foreground = HybridKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        hintsPanel.Children.Add(bHint);

        Canvas.SetRight(hintsPanel, 60);
        Canvas.SetBottom(hintsPanel, 36);
        hybridZoneBack.Children.Add(hintsPanel);

        hybridBoard.Children.Add(hybridZoneBack);

        // 2. Hero Stage
        BuildHybridHero();
        hybridBoard.Children.Add(hybridHeroBack!);

        OuterFrame.Child = hybridBoard;
    }

    private void BuildHybridHero()
    {
        hybridHeroBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Visibility = Visibility.Collapsed };
        hybridHeroBack.Children.Add(HybridKit.Halves(700, 560));
        hybridHeroBack.Children.Add(HybridKit.TopRight(false, 0));

        // Back link at (64, 44)
        var backLink = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        backLink.Children.Add(new TextBlock
        {
            Text = "\uE72B", // back icon
            FontFamily = HybridKit.Icons,
            FontSize = 14,
            Foreground = HybridKit.B("#5c4075"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        backLink.Children.Add(new TextBlock
        {
            Text = "Controller Zone / Player 1",
            FontFamily = HybridKit.Body,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = HybridKit.B("#5c4075"),
            VerticalAlignment = VerticalAlignment.Center
        });
        backLink.MouseLeftButtonUp += async (_, _) => await FocusPlayerAsync(null);
        Canvas.SetLeft(backLink, 64);
        Canvas.SetTop(backLink, 44);
        hybridHeroBack.Children.Add(backLink);

        // Title and large model typography at (64, 128)
        var infoStack = new StackPanel { Orientation = Orientation.Vertical, Width = 600 };
        infoStack.Children.Add(new TextBlock
        {
            Text = "PLAYER 1",
            FontFamily = HybridKit.Body,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.B("#5c4075"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        // Massive "D10" letters
        infoStack.Children.Add(new TextBlock
        {
            Text = "D10",
            FontFamily = HybridKit.Display,
            FontSize = 180,
            FontWeight = FontWeights.Black,
            Foreground = HybridKit.InkBrush,
            Margin = new Thickness(0, -20, 0, -10)
        });

        infoStack.Children.Add(new TextBlock
        {
            Text = "EasySMX D10 · Farout Purple",
            FontFamily = HybridKit.Display,
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });

        infoStack.Children.Add(new TextBlock
        {
            Text = "Not detected · Battery not reported · No local changes",
            FontFamily = HybridKit.Body,
            FontSize = 15,
            Foreground = HybridKit.B("#5c4075")
        });

        Canvas.SetLeft(infoStack, 64);
        Canvas.SetTop(infoStack, 128);
        hybridHeroBack.Children.Add(infoStack);

        // Action buttons list at (64, 494, w: 430)
        var actionList = new StackPanel { Orientation = Orientation.Vertical, Width = 430 };

        // Primary: Configure in Studio (A)
        var btnStudio = new Border
        {
            Height = 58,
            CornerRadius = new CornerRadius(29),
            Background = HybridKit.InkBrush,
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(14, 0, 20, 0),
            Margin = new Thickness(0, 0, 0, 10),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Border
                    {
                        Width = 32,
                        Height = 32,
                        CornerRadius = new CornerRadius(16),
                        Background = HybridKit.LavenderBrush,
                        Margin = new Thickness(0, 0, 14, 0),
                        Child = new TextBlock
                        {
                            Text = "A",
                            FontFamily = new FontFamily("Segoe UI"),
                            FontSize = 14,
                            FontWeight = FontWeights.Bold,
                            Foreground = HybridKit.InkBrush,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    new TextBlock
                    {
                        Text = "Configure in Studio",
                        FontFamily = HybridKit.Display,
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Foreground = HybridKit.B("#efe4ff"),
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        btnStudio.MouseLeftButtonUp += (_, _) =>
        {
            ControllerActionRequested?.Invoke(0, false);
        };
        actionList.Children.Add(btnStudio);

        // Secondary actions
        var secondary = new (string Icon, string Label, Action? Act)[]
        {
            ("\uE877", "Identify / Vibrate", null),
            ("\uE772", "Assign to Different Player Slot", null),
            ("\uE74D", "Disconnect Device", async () => await FocusPlayerAsync(null))
        };

        foreach (var (ico, lbl, act) in secondary)
        {
            var btnSec = new Border
            {
                Height = 46,
                CornerRadius = new CornerRadius(23),
                Background = new SolidColorBrush(Color.FromArgb(0x20, 92, 64, 117)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(18, 0, 18, 0),
                Margin = new Thickness(0, 0, 0, 8),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = ico,
                            FontFamily = HybridKit.Icons,
                            FontSize = 16,
                            Foreground = HybridKit.InkBrush,
                            Margin = new Thickness(0, 0, 12, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = lbl,
                            FontFamily = HybridKit.Display,
                            FontSize = 16,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = HybridKit.InkBrush,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            if (act != null) btnSec.MouseLeftButtonUp += (_, _) => act();
            actionList.Children.Add(btnSec);
        }

        Canvas.SetLeft(actionList, 64);
        Canvas.SetTop(actionList, 494);
        hybridHeroBack.Children.Add(actionList);

        // Glow behind controller on right at (820, 210, w: 680, h: 520)
        var glow = new Ellipse
        {
            Width = 680,
            Height = 520,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new(Color.FromArgb(0x33, 82, 220, 255), 0.0),
                    new(Color.FromArgb(0x1a, 150, 110, 230), 0.55),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        Canvas.SetLeft(glow, 820);
        Canvas.SetTop(glow, 210);
        hybridHeroBack.Children.Add(glow);

        // Rotated controller graphic at (660, 150, w: 840)
        var padArt = HybridKit.ControllerArt("d10-purple", 840);
        if (padArt != null)
        {
            padArt.RenderTransform = new RotateTransform(-7, 420, 250);
            padArt.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 40,
                ShadowDepth = 30,
                Direction = 270,
                Opacity = 0.6
            };
            Canvas.SetLeft(padArt, 660);
            Canvas.SetTop(padArt, 150);
            hybridHeroBack.Children.Add(padArt);
        }
    }

    private void HybridSync()
    {
        if (!IsHybrid || hybridBoard == null) return;

        bool hero = State.FocusedPlayer.HasValue;
        if (hero)
        {
            hybridZoneBack!.Visibility = Visibility.Collapsed;
            hybridHeroBack!.Visibility = Visibility.Visible;
            for (int i = 0; i < docks.Length; i++) docks[i].Visibility = Visibility.Collapsed;
        }
        else
        {
            hybridZoneBack!.Visibility = Visibility.Visible;
            hybridHeroBack!.Visibility = Visibility.Collapsed;
            for (int i = 0; i < docks.Length; i++) docks[i].Visibility = Visibility.Visible;
        }
    }

    private async Task HybridFocus(int? index)
    {
        if (index.HasValue) State.Activate(index.Value); else State.Collapse();
        HybridSync();
        await Task.CompletedTask;
    }
}

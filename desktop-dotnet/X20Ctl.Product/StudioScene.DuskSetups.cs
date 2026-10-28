using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void DuskShowSetups()
    {
        duskPageHost!.Children.Clear();

        string padKey = Model?.Id?.ToLowerInvariant() ?? "x05pro";
        if (padKey.Contains("05")) padKey = "x05pro";

        string rawModel = !string.IsNullOrEmpty(Model?.Name) ? Model.Name.Trim() : "X05 PRO";
        string shortModel = rawModel.StartsWith("EasySMX ", StringComparison.OrdinalIgnoreCase)
            ? rawModel.Substring("EasySMX ".Length).Trim()
            : (rawModel.StartsWith("EASYSMX ", StringComparison.OrdinalIgnoreCase) ? rawModel.Substring("EASYSMX ".Length).Trim() : rawModel);
        if (string.IsNullOrEmpty(shortModel)) shortModel = "X05 PRO";

        // 1. Backdrop
        var backdrop = DuskKit.Backdrop(padKey, 0.22);
        duskPageHost.Children.Add(backdrop);

        // 2. Top bar
        var top = DuskKit.TopBar($"Player {Player + 1}", "19:24", 100);
        duskPageHost.Children.Add(top);

        // 3. Header at (52, 100)
        var headStack = new StackPanel { Orientation = Orientation.Vertical };
        headStack.Children.Add(new TextBlock
        {
            Text = "Saved Profiles & Setups",
            FontFamily = DuskKit.Display,
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush
        });
        headStack.Children.Add(new TextBlock
        {
            Text = $"3 SAVED SETUPS · EASYSMX {shortModel.ToUpperInvariant()}",
            FontFamily = DuskKit.Display,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Canvas.SetLeft(headStack, 52);
        Canvas.SetTop(headStack, 100);
        duskPageHost.Children.Add(headStack);

        // Top right actions: [+ New Setup], [Import]
        var rightBtns = new StackPanel { Orientation = Orientation.Horizontal };
        var newBtn = new Border
        {
            Height = 44,
            Padding = new Thickness(20, 0, 20, 0),
            CornerRadius = new CornerRadius(4),
            Background = DuskKit.SlateTabOnBrush,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 10, 0)
        };
        newBtn.Child = new TextBlock
        {
            Text = "+ New Setup",
            FontFamily = DuskKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        rightBtns.Children.Add(newBtn);

        var impBtn = new Border
        {
            Height = 44,
            Padding = new Thickness(20, 0, 20, 0),
            CornerRadius = new CornerRadius(4),
            Background = DuskKit.SlateItemBrush,
            Cursor = Cursors.Hand
        };
        impBtn.Child = new TextBlock
        {
            Text = "Import Profile",
            FontFamily = DuskKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.LightBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        rightBtns.Children.Add(impBtn);

        Canvas.SetRight(rightBtns, 44);
        Canvas.SetTop(rightBtns, 104);
        duskPageHost.Children.Add(rightBtns);

        // 4. Setup Cards Row at top = 196
        var setupCards = new (string Name, string Sub, double W, bool Active)[]
        {
            ("DEFAULT FPS", "Linear curves · 0ms turbo", 520, true),
            ("RACING SIM", "50% steering damping", 330, false),
            ("FIGHTING", "Aggressive D-pad shortcuts", 330, false)
        };

        double curX = 52;
        foreach (var (cName, cSub, w, active) in setupCards)
        {
            var card = new Border
            {
                Width = w,
                Height = 300,
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Cursor = Cursors.Hand
            };

            if (active)
            {
                card.BorderBrush = DuskKit.WhiteBrush;
                card.BorderThickness = new Thickness(3);
                card.Effect = new DropShadowEffect { Color = Colors.White, BlurRadius = 30, ShadowDepth = 0, Opacity = 0.35 };
            }
            else
            {
                card.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 8, Opacity = 0.5 };
            }

            var cardCanvas = new Canvas { Width = w, Height = 300 };

            // Background
            var bg = new Rectangle
            {
                Width = w,
                Height = 300,
                Fill = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.2),
                    GradientOrigin = new Point(0.5, 0.2),
                    RadiusX = 1.2,
                    RadiusY = 0.9,
                    GradientStops = { new(DuskKit.C("#26364a"), 0.0), new(DuskKit.C("#0d131a"), 1.0) }
                }
            };
            cardCanvas.Children.Add(bg);

            // Dim pad art
            var padImg = DuskKit.ControllerArt(padKey, w * 0.9);
            if (padImg != null)
            {
                padImg.Opacity = 0.45;
                Canvas.SetLeft(padImg, w * 0.1);
                Canvas.SetTop(padImg, 10);
                cardCanvas.Children.Add(padImg);
            }

            // Dark bottom shade
            var shade = new Rectangle
            {
                Width = w,
                Height = 300,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = { new(Colors.Transparent, 0.4), new(DuskKit.C("rgba(14,18,24,.9)"), 1.0) }
                }
            };
            cardCanvas.Children.Add(shade);

            // Local badge top right
            var badge = new Border
            {
                Padding = new Thickness(9, 3, 9, 3),
                CornerRadius = new CornerRadius(3),
                Background = DuskKit.B("rgba(14,18,24,.8)"),
                Child = new TextBlock
                {
                    Text = "LOCAL",
                    FontFamily = DuskKit.Display,
                    FontSize = 12,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = DuskKit.LightBrush
                }
            };
            Canvas.SetRight(badge, 14);
            Canvas.SetTop(badge, 12);
            cardCanvas.Children.Add(badge);

            // Title & sub bottom left
            var textStack = new StackPanel { Orientation = Orientation.Vertical };
            textStack.Children.Add(new TextBlock
            {
                Text = cName,
                FontFamily = DuskKit.LogoFont,
                FontSize = active ? 46 : 34,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 3, Opacity = 0.6 }
            });
            textStack.Children.Add(new TextBlock
            {
                Text = cSub,
                FontFamily = DuskKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = DuskKit.LightBrush,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Canvas.SetLeft(textStack, 20);
            Canvas.SetBottom(textStack, 18);
            cardCanvas.Children.Add(textStack);

            card.Child = cardCanvas;
            Canvas.SetLeft(card, curX);
            Canvas.SetTop(card, 196);
            duskPageHost.Children.Add(card);

            curX += w + 16;
        }

        // 5. Focused title & meta at (52, 520)
        var fStack = new StackPanel { Orientation = Orientation.Vertical };
        fStack.Children.Add(new TextBlock
        {
            Text = "Default FPS",
            FontFamily = DuskKit.Display,
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush
        });

        var fMeta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        fMeta.Children.Add(new TextBlock
        {
            Text = $"EASYSMX {shortModel.ToUpperInvariant()} · MATCHED TO ACTIVE HARDWARE · ",
            FontFamily = DuskKit.Display,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.MuteBrush
        });
        fMeta.Children.Add(new TextBlock
        {
            Text = "Synchronized to Controller Memory",
            FontFamily = DuskKit.Display,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.GreenOnlineBrush
        });
        fStack.Children.Add(fMeta);

        Canvas.SetLeft(fStack, 52);
        Canvas.SetTop(fStack, 520);
        duskPageHost.Children.Add(fStack);

        // 6. Full-width dark band at (0, 612, h: 118)
        var band = new Border
        {
            Width = 1600,
            Height = 118,
            Background = DuskKit.B("rgba(14,18,24,.84)"),
            BorderThickness = new Thickness(0, 1, 0, 1),
            BorderBrush = DuskKit.B("rgba(255,255,255,.06)")
        };
        Canvas.SetLeft(band, 0);
        Canvas.SetTop(band, 612);
        duskPageHost.Children.Add(band);

        // Green action button at (52, 634, w: 300, h: 74)
        var playBtn = DuskKit.GreenActionButton("Apply Setup", 300, 74, () => DuskSelectPage("buttons"));
        Canvas.SetLeft(playBtn, 52);
        Canvas.SetTop(playBtn, 634);
        duskPageHost.Children.Add(playBtn);

        // Stats at x = 400, 610, 820, 1040
        var stats = new (string Lab, string Val, double X)[]
        {
            ("PROFILE ID", "P-01", 400),
            ("LAST SYNCED", "Today 14:20", 610),
            ("STATUS", "Active in RAM", 820),
            ("POLL RATE", "1000 Hz", 1040)
        };

        foreach (var (sLab, sVal, sX) in stats)
        {
            var sStack = new StackPanel { Orientation = Orientation.Vertical };
            sStack.Children.Add(new TextBlock
            {
                Text = sLab,
                FontFamily = DuskKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DuskKit.MuteBrush,
                Margin = new Thickness(0, 0, 0, 6)
            });
            sStack.Children.Add(new TextBlock
            {
                Text = sVal,
                FontFamily = DuskKit.Display,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = DuskKit.WhiteBrush
            });
            Canvas.SetLeft(sStack, sX);
            Canvas.SetTop(sStack, 642);
            duskPageHost.Children.Add(sStack);
        }

        // 4 tool squares at x = 1240, 1322, 1404, 1486, top: 638
        var tools = new (string Icon, string Tooltip)[]
        {
            ("\uE8C8", "Duplicate"),
            ("\uE8AC", "Rename"),
            ("\uEDE1", "Export"),
            ("\uE712", "More")
        };

        for (int i = 0; i < tools.Length; i++)
        {
            var (icon, tip) = tools[i];
            var sq = new Border
            {
                Width = 68,
                Height = 68,
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.SlateTabOnBrush,
                Cursor = Cursors.Hand,
                ToolTip = tip,
                Child = new TextBlock
                {
                    Text = icon,
                    FontFamily = DuskKit.Icons,
                    FontSize = 28,
                    Foreground = DuskKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(sq, 1240 + i * 82);
            Canvas.SetTop(sq, 638);
            duskPageHost.Children.Add(sq);
        }

        // Labels under tools
        var toolLabels = new TextBlock
        {
            Text = "DUPLICATE · RENAME · EXPORT SETUP · MORE",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = DuskKit.MuteBrush
        };
        Canvas.SetLeft(toolLabels, 1240);
        Canvas.SetTop(toolLabels, 716);
        duskPageHost.Children.Add(toolLabels);

        // Bottom bar
        var bar = DuskKit.BottomBar(new[] { ("A", "APPLY"), ("X", "DUPLICATE"), ("B", "BACK") });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 844);
        duskPageHost.Children.Add(bar);
    }
}

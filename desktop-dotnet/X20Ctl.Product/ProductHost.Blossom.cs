using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;

namespace X20Ctl.Product;

/// <summary>
/// Host notice dialog and modal presentation for the Yozakura (Blossom) theme:
/// "Multi-panel Shoji lattice dialog: illuminated washi paper with shoji grid lattice pattern,
/// vermilion samurai seal stamp, dark void background, and bottom status panels."
/// Strictly implements t_blossom.py:notices().
/// </summary>
public sealed partial class ProductHost
{
    public Canvas? ActiveBlossomNotice { get; private set; }

    public void ShowBlossomThemeRestartNotice(string? themeName = null)
    {
        string chosen = themeName ?? ThemeCatalog.Find(preferencesHealthy ? preferences.Theme : ThemeCatalog.Current.Id).Name;
        DismissBlossomNotice();

        var notice = BuildBlossomModalNotice(
            title: $"Restart X20CTL to switch to {chosen}",
            lines: new[]
            {
                $"X20CTL needs to restart to apply the {chosen} hanami night experience.",
                "Unsaved controller drafts will be lost. Saved setups and PC mode settings stay on this PC.",
                "The app will reopen with the bespoke Japanese samurai night lantern theme."
            },
            yesText: "Restart Now (A)",
            noText: "Cancel (B)",
            onYes: () =>
            {
                DismissBlossomNotice();
                if (!ReviewSandbox.Active) ThemeCatalog.Restart();
            },
            onNo: () => DismissBlossomNotice()
        );

        ShowBlossomNotice(notice);
    }

    public void ShowBlossomDriverNotice(IReadOnlyList<string>? names = null, Action<bool>? decided = null)
    {
        DismissBlossomNotice();
        var list = names is { Count: > 0 } ? string.Join(", ", names) : "ViGEmBus, HidHide";

        var notice = BuildBlossomModalNotice(
            title: "PC Mode Drivers Required",
            lines: new[]
            {
                "PC mode communicates directly with virtual controller emulation drivers:",
                "ViGEmBus: Virtual Xbox & Switch controller bus driver.",
                "HidHide: Hides physical gamepad to prevent double input."
            },
            yesText: "Install Drivers (A)",
            noText: "Later (B)",
            onYes: () =>
            {
                DismissBlossomNotice();
                decided?.Invoke(true);
            },
            onNo: () =>
            {
                DismissBlossomNotice();
                decided?.Invoke(false);
            }
        );

        ShowBlossomNotice(notice);
    }

    private Canvas BuildBlossomModalNotice(
        string title,
        string[] lines,
        string yesText,
        string noText,
        Action onYes,
        Action onNo)
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Dim night backdrop
        var backdrop = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.B("rgba(14,5,12,0.85)") };
        root.Children.Add(backdrop);

        // Main Shoji Lattice Parchment Dialog Card at (330, 90, width 940, height 520)
        var shojiCard = new Border
        {
            Width = 940,
            Height = 520,
            CornerRadius = new CornerRadius(4),
            Background = BlossomKit.CreateLitPaperBrush(),
            BorderBrush = BlossomKit.B("rgba(255,220,180,0.6)"),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0xa0, 0x5a),
                BlurRadius = 44,
                ShadowDepth = 26,
                Opacity = 0.5
            }
        };

        var shojiCanvas = new Canvas { Width = 940, Height = 520 };

        // Subtle Shoji lattice grid lines
        for (int lx = 234; lx < 940; lx += 234)
        {
            shojiCanvas.Children.Add(new Line { X1 = lx, Y1 = 0, X2 = lx, Y2 = 520, Stroke = BlossomKit.B("rgba(91,42,34,0.10)"), StrokeThickness = 1.5 });
        }
        for (int ly = 172; ly < 520; ly += 172)
        {
            shojiCanvas.Children.Add(new Line { X1 = 0, Y1 = ly, X2 = 940, Y2 = ly, Stroke = BlossomKit.B("rgba(91,42,34,0.10)"), StrokeThickness = 1.5 });
        }

        // Blossom Theme Logo
        var logo = BlossomKit.BlossomLogo(110);
        if (logo != null)
        {
            Canvas.SetLeft(logo, 50);
            Canvas.SetTop(logo, 36);
            shojiCanvas.Children.Add(logo);
        }

        // Left Content: Title & Bullet Points
        var leftStack = new StackPanel { Width = 400 };
        leftStack.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = BlossomKit.Display,
            FontSize = 28,
            Foreground = BlossomKit.B(BlossomKit.LitPaperInk)
        });
        leftStack.Children.Add(new TextBlock
        {
            Text = lines[0],
            FontFamily = BlossomKit.Heading,
            FontSize = 19,
            Foreground = BlossomKit.B(BlossomKit.LitPaperInk),
            Margin = new Thickness(0, 14, 0, 14)
        });

        for (int i = 1; i < lines.Length; i++)
        {
            var bRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var lanternDot = new Border
            {
                Width = 10,
                Height = 13,
                CornerRadius = new CornerRadius(5, 5, 5, 5),
                Background = BlossomKit.CreateWarmLanternBrush(),
                Margin = new Thickness(0, 5, 12, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            bRow.Children.Add(lanternDot);
            bRow.Children.Add(new TextBlock
            {
                Text = lines[i],
                FontFamily = BlossomKit.Body,
                FontSize = 15,
                LineHeight = 22,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
                Width = 370
            });
            leftStack.Children.Add(bRow);
        }

        Canvas.SetLeft(leftStack, 50);
        Canvas.SetTop(leftStack, 132);
        shojiCanvas.Children.Add(leftStack);

        // Right Content: Guidance & Action Buttons at x = 512, top = 46
        var rightStack = new StackPanel { Width = 380 };
        rightStack.Children.Add(new TextBlock
        {
            Text = "Unsaved controller drafts will be lost. Saved setups and PC mode settings stay on this PC.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub)
        });
        rightStack.Children.Add(new TextBlock
        {
            Text = "Continue with this action?",
            FontFamily = BlossomKit.Display,
            FontSize = 30,
            Foreground = BlossomKit.B(BlossomKit.LitPaperInk),
            Margin = new Thickness(0, 22, 0, 22)
        });

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
        var noBtn = new Border
        {
            Height = 46,
            Padding = new Thickness(22, 0, 22, 0),
            CornerRadius = new CornerRadius(23),
            BorderBrush = BlossomKit.B("rgba(59,23,18,0.55)"),
            BorderThickness = new Thickness(1.5),
            Margin = new Thickness(0, 0, 12, 0),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = noText, FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center }
        };
        btnRow.Children.Add(StudioScene.ThemeAction(noBtn, "notice:no", onNo));

        var yesBtn = new Border
        {
            Height = 46,
            Padding = new Thickness(24, 0, 24, 0),
            CornerRadius = new CornerRadius(23),
            Background = BlossomKit.B(BlossomKit.LitPaperInk),
            Cursor = Cursors.Hand
        };
        var ySt = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        ySt.Children.Add(new TextBlock { Text = yesText, FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.B("#ffe2b8") });
        yesBtn.Child = ySt;
        btnRow.Children.Add(StudioScene.ThemeAction(yesBtn, "notice:yes", onYes));

        rightStack.Children.Add(btnRow);
        Canvas.SetLeft(rightStack, 512);
        Canvas.SetTop(rightStack, 46);
        shojiCanvas.Children.Add(rightStack);

        shojiCard.Child = shojiCanvas;
        Canvas.SetLeft(shojiCard, 330);
        Canvas.SetTop(shojiCard, 90);
        root.Children.Add(shojiCard);

        // Bottom Status Panels (4 panels: 2x2 grid at x=330, 810; y=644, 748)
        var statusPanels = new (string Label, string Text, bool Lit)[]
        {
            ("UPDATE", "V0.017 · Yozakura Theme Edition", true),
            ("WHILE IT INSTALLS", "Keeps your current profiles and assignments intact", true),
            ("AFTER YES", "Restarts into the authentic hanami night console immediately", true),
            ("AFTER NO", "Continues in your current session without changes", false)
        };

        for (int i = 0; i < statusPanels.Length; i++)
        {
            var sp = statusPanels[i];
            double px = 330 + (i % 2) * 480;
            double py = 644 + (i / 2) * 104;

            var pBorder = new Border
            {
                Width = 460,
                Height = 88,
                CornerRadius = new CornerRadius(6),
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(204, 46, 19, 40), 0.0),
                        new GradientStop(Color.FromArgb(204, 26, 10, 24), 1.0)
                    }
                },
                BorderBrush = BlossomKit.B("rgba(255,210,225,0.10)"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(22, 0, 22, 0)
            };

            var pGrid = new Grid();
            pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var pBeacon = new Border
            {
                Width = 22,
                Height = 28,
                CornerRadius = new CornerRadius(10, 10, 10, 10),
                Background = sp.Lit ? BlossomKit.CreateWarmLanternBrush() : BlossomKit.CreateColdLanternBrush(),
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            if (sp.Lit)
            {
                pBeacon.Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.6 };
            }
            Grid.SetColumn(pBeacon, 0);
            pGrid.Children.Add(pBeacon);

            var pStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            pStack.Children.Add(new TextBlock { Text = sp.Label, FontFamily = BlossomKit.Small, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = BlossomKit.TextSakuraBrush });
            pStack.Children.Add(new TextBlock { Text = sp.Text, FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextWhiteBrush, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(pStack, 1);
            pGrid.Children.Add(pStack);

            pBorder.Child = pGrid;
            Canvas.SetLeft(pBorder, px);
            Canvas.SetTop(pBorder, py);
            root.Children.Add(pBorder);
        }

        return root;
    }

    private void ShowBlossomNotice(Canvas notice)
    {
        ActiveBlossomNotice = notice;
        Panel.SetZIndex(notice, 100);
        Children.Add(notice); ArmThemeNotice(notice);
    }

    public void DismissBlossomNotice()
    {
        if (ActiveBlossomNotice != null)
        {
            ReleaseThemeNotice(ActiveBlossomNotice); Children.Remove(ActiveBlossomNotice);
            ActiveBlossomNotice = null; _ = UpdateNativeContext();
        }
    }
}

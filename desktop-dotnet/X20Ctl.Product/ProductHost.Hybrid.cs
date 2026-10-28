using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Hybrid;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    public Canvas? ActiveHybridNotice { get; private set; }

    public void ShowHybridThemeRestartNotice(string? themeName = null)
    {
        string chosen = themeName ?? "Hybrid";
        DismissHybridNotice();

        var notice = BuildClamshellNotice(
            title: $"Restart X20CTL to switch to {chosen}",
            lines: new[]
            {
                $"X20CTL needs to restart to apply the {chosen} console experience.",
                "Your active controller drafts, curves, and button profiles will be preserved.",
                "The app will reopen with the authentic Nintendo Switch two-halves design."
            },
            driverLead: "THEME SWITCH REQUIREMENTS",
            paras: new[]
            {
                $"Selected theme: {chosen}",
                "The opening intro video, color palettes, and console stage will update upon restart.",
                "Would you like to restart X20CTL now?"
            },
            yesText: "Restart Now (A)",
            noText: "Cancel (B)",
            onYes: () =>
            {
                DismissHybridNotice();
                if (!ReviewSandbox.Active) ThemeCatalog.Restart();
            },
            onNo: () => DismissHybridNotice()
        );

        ShowHybridNotice(notice);
    }

    public void ShowHybridDriverNotice(IReadOnlyList<string>? names = null, Action<bool>? decided = null)
    {
        DismissHybridNotice();
        var list = names is { Count: > 0 } ? string.Join(", ", names) : "ViGEmBus, HidHide";

        var notice = BuildClamshellNotice(
            title: "PC Mode Drivers Required",
            lines: new[]
            {
                "PC mode communicates directly with virtual controller emulation drivers:",
                "ViGEmBus: Virtual Xbox & Switch controller bus driver.",
                "HidHide: Hides the physical hardware gamepad to prevent double input."
            },
            driverLead: "DRIVER PACKAGES & SIGNATURES",
            paras: new[]
            {
                $"Required components: {list}",
                "Drivers are digitally signed by Nefarius Software Solutions. You can uninstall them at any time in Windows Apps.",
                "Install these drivers now to enable complete PC mode functionality?"
            },
            yesText: "Install Drivers (A)",
            noText: "Later (B)",
            onYes: () =>
            {
                DismissHybridNotice();
                decided?.Invoke(true);
            },
            onNo: () =>
            {
                DismissHybridNotice();
                decided?.Invoke(false);
            }
        );

        ShowHybridNotice(notice);
    }

    private Canvas BuildClamshellNotice(
        string title,
        string[] lines,
        string driverLead,
        string[] paras,
        string yesText,
        string noText,
        Action onYes,
        Action onNo)
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Dim backdrop
        var dim = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new SolidColorBrush(Color.FromArgb(0xd1, 14, 11, 18))
        };
        root.Children.Add(dim);

        // Clamshell Dialog: Left Lavender half + Right Charcoal half
        // Left half (240, 150, w: 560, h: 470)
        var leftHalf = new Border
        {
            Width = 560,
            Height = 470,
            CornerRadius = new CornerRadius(36, 0, 0, 36),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = { new(HybridKit.C("#e3d3fb"), 0.0), new(HybridKit.C("#c3a6ef"), 1.0) }
            },
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 30, ShadowDepth = 15, Opacity = 0.5 }
        };
        Canvas.SetLeft(leftHalf, 240);
        Canvas.SetTop(leftHalf, 150);
        root.Children.Add(leftHalf);

        // Right half (800, 150, w: 560, h: 470)
        var rightHalf = new Border
        {
            Width = 560,
            Height = 470,
            CornerRadius = new CornerRadius(0, 36, 36, 0),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = { new(HybridKit.C("#36313d"), 0.0), new(HybridKit.C("#25222b"), 1.0) }
            },
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 30, ShadowDepth = 15, Opacity = 0.5 }
        };
        Canvas.SetLeft(rightHalf, 800);
        Canvas.SetTop(rightHalf, 150);
        root.Children.Add(rightHalf);

        // Seam down dialog center
        root.Children.Add(HybridKit.Seam(800, 150, 620, 385, true, 0.95));

        // Content on Left Half at (284, 190, w: 480)
        var leftStack = new StackPanel { Width = 480, Orientation = Orientation.Vertical };
        string logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "hybrid", "logo.png");
        if (System.IO.File.Exists(logoPath))
        {
            var logo = new Image
            {
                Width = 86,
                Height = 60,
                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(logoPath))),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            leftStack.Children.Add(logo);
        }

        leftStack.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = HybridKit.Display,
            FontSize = 26,
            FontWeight = FontWeights.ExtraBold,
            Foreground = HybridKit.InkBrush,
            Margin = new Thickness(0, 10, 0, 12),
            TextWrapping = TextWrapping.Wrap
        });

        foreach (var line in lines)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock
            {
                Text = "\uE73E", // check icon
                FontFamily = HybridKit.Icons,
                FontSize = 14,
                Foreground = HybridKit.B("#5c4075"),
                Margin = new Thickness(0, 3, 10, 0)
            });
            row.Children.Add(new TextBlock
            {
                Text = line,
                FontFamily = HybridKit.Body,
                FontSize = 15,
                Foreground = HybridKit.InkBrush,
                Width = 450,
                TextWrapping = TextWrapping.Wrap
            });
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 284);
        Canvas.SetTop(leftStack, 190);
        root.Children.Add(leftStack);

        // Content on Right Half at (844, 190, w: 480)
        var rightStack = new StackPanel { Width = 480, Orientation = Orientation.Vertical };
        rightStack.Children.Add(new TextBlock
        {
            Text = driverLead,
            FontFamily = HybridKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.B("#9e97a9"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        foreach (var p in paras)
        {
            rightStack.Children.Add(new TextBlock
            {
                Text = p,
                FontFamily = HybridKit.Body,
                FontSize = 14,
                Foreground = HybridKit.B("#c2bccd"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
        }

        // Action Buttons
        var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };

        var btnYes = new Border
        {
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.LavenderBrush,
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(22, 0, 22, 0),
            Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock
            {
                Text = yesText,
                FontFamily = HybridKit.Body,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.InkBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        btnYes.MouseLeftButtonUp += (_, _) => onYes();
        btns.Children.Add(btnYes);

        var btnNo = new Border
        {
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.B("#34303c"),
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(22, 0, 22, 0),
            Child = new TextBlock
            {
                Text = noText,
                FontFamily = HybridKit.Body,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = HybridKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        btnNo.MouseLeftButtonUp += (_, _) => onNo();
        btns.Children.Add(btnNo);

        rightStack.Children.Add(btns);

        Canvas.SetLeft(rightStack, 844);
        Canvas.SetTop(rightStack, 190);
        root.Children.Add(rightStack);

        // Stacked toasts along bottom right
        var toasts = new string[]
        {
            "Profile Pro Tourney loaded",
            "EasySMX D10 connected · 2.4 GHz",
            "XInput report stream active"
        };

        for (int i = 0; i < toasts.Length; i++)
        {
            var toast = new Border
            {
                Height = 44,
                Padding = new Thickness(18, 0, 18, 0),
                CornerRadius = new CornerRadius(16),
                Background = HybridKit.B("rgba(27,22,34,.94)"),
                BorderBrush = HybridKit.B("#3d3748"),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 4, Opacity = 0.4 },
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = "\uE73E", FontFamily = HybridKit.Icons, FontSize = 14, Foreground = HybridKit.CyanBrush, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock { Text = toasts[i], FontFamily = HybridKit.Body, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = HybridKit.B("#efe9f8"), VerticalAlignment = VerticalAlignment.Center }
                    }
                }
            };
            Canvas.SetRight(toast, 44);
            Canvas.SetBottom(toast, 44 + i * 54);
            root.Children.Add(toast);
        }

        return root;
    }

    private void ShowHybridNotice(Canvas notice)
    {
        ActiveHybridNotice = notice;
        Panel.SetZIndex(notice, 100);
        Children.Add(notice);
    }

    public void DismissHybridNotice()
    {
        if (ActiveHybridNotice != null)
        {
            Children.Remove(ActiveHybridNotice);
            ActiveHybridNotice = null;
        }
    }
}

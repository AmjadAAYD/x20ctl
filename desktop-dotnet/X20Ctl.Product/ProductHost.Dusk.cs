using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    public Canvas? ActiveDuskNotice { get; private set; }

    public void ShowDuskThemeRestartNotice(string? themeName = null)
    {
        string chosen = themeName ?? "Dusk";
        DismissDuskNotice();

        var notice = BuildDeckModalNotice(
            title: $"Restart X20CTL to switch to {chosen}",
            lines: new[]
            {
                $"X20CTL needs to restart to apply the {chosen} console experience.",
                "Active controller drafts, curves, and button profiles will be preserved.",
                "The app will reopen with the authentic Steam Deck dark console home design."
            },
            yesText: "Restart Now (A)",
            noText: "Cancel (B)",
            onYes: () =>
            {
                DismissDuskNotice();
                if (!ReviewSandbox.Active) ThemeCatalog.Restart();
            },
            onNo: () => DismissDuskNotice()
        );

        ShowDuskNotice(notice);
    }

    public void ShowDuskDriverNotice(IReadOnlyList<string>? names = null, Action<bool>? decided = null)
    {
        DismissDuskNotice();
        var list = names is { Count: > 0 } ? string.Join(", ", names) : "ViGEmBus, HidHide";

        var notice = BuildDeckModalNotice(
            title: "PC Mode Drivers Required",
            lines: new[]
            {
                "PC mode communicates directly with virtual controller emulation drivers:",
                "ViGEmBus: Virtual Xbox & Switch controller bus driver.",
                "HidHide: Hides the physical hardware gamepad to prevent double input."
            },
            yesText: "Install Drivers (A)",
            noText: "Later (B)",
            onYes: () =>
            {
                DismissDuskNotice();
                decided?.Invoke(true);
            },
            onNo: () =>
            {
                DismissDuskNotice();
                decided?.Invoke(false);
            }
        );

        ShowDuskNotice(notice);
    }

    /// <summary>
    /// Steam Deck Modal Dialog and Toasts.
    /// Strictly implements t_dusk.py:notices().
    /// </summary>
    private Canvas BuildDeckModalNotice(
        string title,
        string[] lines,
        string yesText,
        string noText,
        Action onYes,
        Action onNo)
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Darkened overlay
        var overlay = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = DuskKit.B("rgba(8,10,14,.75)")
        };
        root.Children.Add(overlay);

        // 2. Green update pill top right at (1050, 20)
        var pill = new Border
        {
            Padding = new Thickness(16, 8, 16, 8),
            CornerRadius = new CornerRadius(22),
            Background = DuskKit.GreenOnlineBrush,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = "\uE777", FontFamily = DuskKit.Icons, FontSize = 16, Foreground = DuskKit.SlateBgBrush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "SYSTEM UPDATE · X20CTL DUSK 0.018", FontFamily = DuskKit.Display, FontSize = 15, FontWeight = FontWeights.ExtraBold, Foreground = DuskKit.SlateBgBrush, VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        Canvas.SetLeft(pill, 1050);
        Canvas.SetTop(pill, 20);
        root.Children.Add(pill);

        // 3. Steam Deck Modal Card (.mod) at (110, 150, w: 790)
        var modal = new Border
        {
            Width = 790,
            CornerRadius = new CornerRadius(8),
            Background = DuskKit.SlateItemBrush,
            ClipToBounds = true,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 60, ShadowDepth = 25, Opacity = 0.65 }
        };

        var mGrid = new Grid();
        mGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        mGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Body
        mGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

        // Header (.mt)
        var headBlock = new TextBlock
        {
            Text = title,
            FontFamily = DuskKit.Display,
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Padding = new Thickness(36, 30, 36, 0)
        };
        Grid.SetRow(headBlock, 0);
        mGrid.Children.Add(headBlock);

        // Body (.mb)
        var bodyStack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(36, 16, 36, 26) };
        bodyStack.Children.Add(new TextBlock
        {
            Text = lines[0],
            FontFamily = DuskKit.Body,
            FontSize = 18,
            Foreground = DuskKit.LightBrush,
            Margin = new Thickness(0, 0, 0, 14),
            TextWrapping = TextWrapping.Wrap
        });

        for (int i = 1; i < lines.Length; i++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock
            {
                Text = "\uE73E",
                FontFamily = DuskKit.Icons,
                FontSize = 16,
                Foreground = DuskKit.GreenOnlineBrush,
                Margin = new Thickness(0, 3, 12, 0),
                VerticalAlignment = VerticalAlignment.Top
            });
            row.Children.Add(new TextBlock
            {
                Text = lines[i],
                FontFamily = DuskKit.Body,
                FontSize = 16,
                Foreground = DuskKit.WhiteBrush,
                Width = 670,
                TextWrapping = TextWrapping.Wrap
            });
            bodyStack.Children.Add(row);
        }

        bodyStack.Children.Add(new TextBlock
        {
            Text = "Would you like to proceed now?",
            FontFamily = DuskKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 10, 0, 0)
        });
        Grid.SetRow(bodyStack, 1);
        mGrid.Children.Add(bodyStack);

        // Footer (.mf)
        var footer = new Border
        {
            Background = DuskKit.SlatePanelBrush,
            Padding = new Thickness(36, 16, 36, 16)
        };
        var fBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var btnYes = new Border
        {
            Height = 46,
            Padding = new Thickness(24, 0, 24, 0),
            CornerRadius = new CornerRadius(3),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops = { new(DuskKit.GreenGrad0, 0.0), new(DuskKit.GreenGrad1, 1.0) }
            },
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 12, 0),
            Effect = new DropShadowEffect { Color = Colors.White, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.25 }
        };
        btnYes.Child = new TextBlock
        {
            Text = yesText,
            FontFamily = DuskKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        btnYes.MouseLeftButtonUp += (_, _) => onYes();
        fBtns.Children.Add(btnYes);

        var btnNo = new Border
        {
            Height = 46,
            Padding = new Thickness(24, 0, 24, 0),
            CornerRadius = new CornerRadius(3),
            Background = DuskKit.SlateTabOnBrush,
            Cursor = Cursors.Hand
        };
        btnNo.Child = new TextBlock
        {
            Text = noText,
            FontFamily = DuskKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.LightBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        btnNo.MouseLeftButtonUp += (_, _) => onNo();
        fBtns.Children.Add(btnNo);

        footer.Child = fBtns;
        Grid.SetRow(footer, 2);
        mGrid.Children.Add(footer);

        modal.Child = mGrid;
        Canvas.SetLeft(modal, 110);
        Canvas.SetTop(modal, 150);
        root.Children.Add(modal);

        // 4. Steam Deck Toasts along right at (960, 150, w: 580)
        var toastStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };

        var toasts = new (string Lab, string Txt, string Icon, Color Col)[]
        {
            ("WHILE IT INSTALLS", "USB handshake maintains active rumble calibration in background", "\uE895", DuskKit.C("#1a9fff")),
            ("AFTER RESTART", "Console home launches with Steam Deck dark slate interface", "\uE73E", DuskKit.GreenOnline),
            ("IF POSTPONED", "Changes remain staged locally in memory without modifying hardware", "\uE711", DuskKit.TextMute)
        };

        foreach (var (lab, txt, icon, col) in toasts)
        {
            var tSec = new TextBlock
            {
                Text = lab,
                FontFamily = DuskKit.Display,
                FontSize = 14,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DuskKit.MuteBrush,
                Margin = new Thickness(0, 14, 0, 6)
            };
            toastStack.Children.Add(tSec);

            var toast = new Border
            {
                Padding = new Thickness(20, 16, 20, 16),
                CornerRadius = new CornerRadius(6),
                Background = DuskKit.B("#2c3440"),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 6, Opacity = 0.4 }
            };
            var tRow = new StackPanel { Orientation = Orientation.Horizontal };
            tRow.Children.Add(new TextBlock
            {
                Text = icon,
                FontFamily = DuskKit.Icons,
                FontSize = 22,
                Foreground = DuskKit.B(col),
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            tRow.Children.Add(new TextBlock
            {
                Text = txt,
                FontFamily = DuskKit.Display,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = DuskKit.WhiteBrush,
                Width = 480,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });
            toast.Child = tRow;
            toastStack.Children.Add(toast);
        }

        Canvas.SetLeft(toastStack, 960);
        Canvas.SetTop(toastStack, 150);
        root.Children.Add(toastStack);

        // 5. Bottom bar: (A) YES, (B) NO
        var bar = DuskKit.BottomBar(new[] { ("A", "YES"), ("B", "NO") });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 844);
        root.Children.Add(bar);

        return root;
    }

    private void ShowDuskNotice(Canvas notice)
    {
        ActiveDuskNotice = notice;
        Panel.SetZIndex(notice, 100);
        Children.Add(notice);
    }

    public void DismissDuskNotice()
    {
        if (ActiveDuskNotice != null)
        {
            Children.Remove(ActiveDuskNotice);
            ActiveDuskNotice = null;
        }
    }
}

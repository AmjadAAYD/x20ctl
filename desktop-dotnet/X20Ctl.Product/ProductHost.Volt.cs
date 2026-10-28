using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    public Canvas? ActiveVoltNotice { get; private set; }

    public void ShowVoltThemeRestartNotice(string? themeName = null)
    {
        string chosen = themeName ?? "Volt";
        DismissVoltNotice();

        var notice = BuildVoltModalNotice(
            title: $"Restart X20CTL to switch to {chosen}",
            lines: new[]
            {
                $"X20CTL needs to restart to apply the {chosen} console experience.",
                "Active controller drafts, curves, and button profiles will be preserved.",
                "The app will reopen with the authentic Steam Deck yellow console home design.",
                "Safe to restart. All local working settings are preserved.",
                "Restart X20CTL now to apply the new theme?"
            },
            yesText: "Restart now",
            noText: "Later",
            onYes: () =>
            {
                DismissVoltNotice();
                if (!ReviewSandbox.Active) ThemeCatalog.Restart();
            },
            onNo: () => DismissVoltNotice()
        );

        ShowVoltNotice(notice);
    }

    public void ShowVoltDriverNotice(IReadOnlyList<string>? names = null, Action<bool>? decided = null)
    {
        DismissVoltNotice();
        var list = names is { Count: > 0 } ? string.Join(", ", names) : "ViGEmBus, HidHide";

        var notice = BuildVoltModalNotice(
            title: "Install required driver",
            lines: new[]
            {
                "X20CTL needs the ViGEmBus and HidHide drivers for PC Mode.",
                "ViGEmBus creates a virtual Xbox or Switch controller for your game.",
                "HidHide hides the real pad so games never see two controllers.",
                "Safe to install. Nothing changes until you turn PC Mode on.",
                "Install drivers and restart X20CTL now?"
            },
            yesText: "Restart now",
            noText: "Later",
            onYes: () =>
            {
                DismissVoltNotice();
                decided?.Invoke(true);
            },
            onNo: () =>
            {
                DismissVoltNotice();
                decided?.Invoke(false);
            }
        );

        ShowVoltNotice(notice);
    }

    /// <summary>
    /// Steam Deck Power Dialog and Toast notifications for Volt.
    /// Strictly implements t_volt.py:361 notices().
    /// </summary>
    private Canvas BuildVoltModalNotice(
        string title,
        string[] lines,
        string yesText,
        string noText,
        Action onYes,
        Action onNo)
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Soft blurred yellow wave backdrop
        root.Children.Add(VoltKit.WaveBackdrop(22));

        // 2. Dimming overlay for modal dialog focus
        var overlay = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = VoltKit.B("rgba(0,0,0,.68)")
        };
        root.Children.Add(overlay);

        // 3. Top bar (right: false)
        root.Children.Add(VoltKit.TopBar(right: false));

        // 4. Centered title pill at (560, 96)
        var titlePill = new Border
        {
            Padding = new Thickness(28, 12, 28, 12),
            CornerRadius = new CornerRadius(16),
            Background = VoltKit.B("#1b1b1b"),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, ShadowDepth = 6, Opacity = 0.5 },
            Child = new TextBlock
            {
                Text = title,
                FontFamily = VoltKit.Display,
                FontSize = 30,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        Canvas.SetLeft(titlePill, 420);
        Canvas.SetTop(titlePill, 96);
        root.Children.Add(titlePill);

        // 5. Left explanation card at (160, 172, w: 800)
        var card = new Border
        {
            Width = 800,
            Background = VoltKit.CardDarkBrush,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(36, 30, 36, 30),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 60, ShadowDepth = 26, Opacity = 0.5 }
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };

        if (lines.Length > 0)
        {
            cStack.Children.Add(new TextBlock
            {
                Text = lines[0],
                FontFamily = VoltKit.Body,
                FontSize = 18,
                Foreground = VoltKit.WhiteBrush,
                LineHeight = 27,
                Margin = new Thickness(0, 0, 0, 12)
            });
        }

        // Bullet points with #ffad0a dots
        for (int i = 1; i < Math.Min(3, lines.Length); i++)
        {
            var bRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            bRow.Children.Add(new TextBlock
            {
                Text = "●",
                FontSize = 14,
                Foreground = VoltKit.AmberHotBrush,
                Margin = new Thickness(0, 4, 12, 0)
            });
            bRow.Children.Add(new TextBlock
            {
                Text = lines[i],
                FontFamily = VoltKit.Body,
                FontSize = 17,
                Foreground = VoltKit.B("#dcdcdc"),
                TextWrapping = TextWrapping.Wrap,
                Width = 700,
                LineHeight = 24
            });
            cStack.Children.Add(bRow);
        }

        if (lines.Length > 3)
        {
            cStack.Children.Add(new TextBlock
            {
                Text = lines[3],
                FontFamily = VoltKit.Body,
                FontSize = 16,
                Foreground = VoltKit.B("#9a9a9a"),
                Margin = new Thickness(0, 8, 0, 10)
            });
        }

        if (lines.Length > 4)
        {
            cStack.Children.Add(new TextBlock
            {
                Text = lines[4],
                FontFamily = VoltKit.Display,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        card.Child = cStack;
        Canvas.SetLeft(card, 160);
        Canvas.SetTop(card, 172);
        root.Children.Add(card);

        // 6. Action choices dialog list at (160, 494, w: 800)
        var dlg = new Border
        {
            Width = 800,
            Background = VoltKit.CardDarkBrush,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 60, ShadowDepth = 26, Opacity = 0.5 }
        };
        var dStack = new StackPanel { Orientation = Orientation.Vertical };

        // [Restart now] solid orange pill (#ffad0a)
        var yesBtn = new Border
        {
            Height = 58,
            CornerRadius = new CornerRadius(12),
            Background = VoltKit.AmberHotBrush,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 8)
        };
        yesBtn.Child = new TextBlock
        {
            Text = yesText,
            FontFamily = VoltKit.Display,
            FontSize = 27,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.B("#111111"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        yesBtn.MouseLeftButtonUp += (_, _) => onYes();
        dStack.Children.Add(yesBtn);

        // [Later] dark button
        var noBtn = new Border
        {
            Height = 58,
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand
        };
        noBtn.Child = new TextBlock
        {
            Text = noText,
            FontFamily = VoltKit.Display,
            FontSize = 27,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        noBtn.MouseLeftButtonUp += (_, _) => onNo();
        dStack.Children.Add(noBtn);

        dlg.Child = dStack;
        Canvas.SetLeft(dlg, 160);
        Canvas.SetTop(dlg, 494);
        root.Children.Add(dlg);

        // 7. Toasts on right: at top = 172 + i * 96, right = 0, width = 560
        var toastData = new (string Lab, string Txt, string Icon, bool Hot)[]
        {
            ("UPDATE", "ViGEmBus driver is ready to install.", "\uE895", true),
            ("WHILE IT INSTALLS", "Controller inputs will temporarily pause.", "\uE895", true),
            ("AFTER YES", "X20CTL reopens with virtual device enabled.", "\uE73E", true),
            ("AFTER NO", "Physical input mode remains unchanged.", "\uE711", false)
        };

        for (int i = 0; i < toastData.Length; i++)
        {
            var (lab, txt, icon, hot) = toastData[i];
            var toast = new Border
            {
                Width = 540,
                Height = 80,
                Background = VoltKit.B("#1c1c1c"),
                CornerRadius = new CornerRadius(4, 0, 0, 4),
                ClipToBounds = true,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 8, Opacity = 0.5 }
            };

            var tGrid = new Grid();
            tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Square icon badge (80x80)
            var iconBox = new Border
            {
                Width = 80,
                Height = 80,
                Background = hot ? VoltKit.AmberHotBrush : VoltKit.B("#3a3a3a"),
                Child = new TextBlock
                {
                    Text = icon,
                    FontFamily = VoltKit.Icons,
                    FontSize = 26,
                    Foreground = hot ? VoltKit.B("#111111") : VoltKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(iconBox, 0);
            tGrid.Children.Add(iconBox);

            var tCol = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 20, 0) };
            tCol.Children.Add(new TextBlock
            {
                Text = lab,
                FontFamily = VoltKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.B("#9a9a9a"),
                Margin = new Thickness(0, 0, 0, 4)
            });
            tCol.Children.Add(new TextBlock
            {
                Text = txt,
                FontFamily = VoltKit.Body,
                FontSize = 15,
                Foreground = VoltKit.WhiteBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            });
            Grid.SetColumn(tCol, 1);
            tGrid.Children.Add(tCol);

            toast.Child = tGrid;
            Canvas.SetRight(toast, 0);
            Canvas.SetTop(toast, 172 + i * 96);
            root.Children.Add(toast);
        }

        // 8. Bottom hint bar
        var bar = VoltKit.BottomBar(new[] { ("A", "SELECT"), ("B", "BACK") });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 850);
        root.Children.Add(bar);

        return root;
    }

    public void ShowVoltNotice(Canvas notice)
    {
        DismissVoltNotice();
        ActiveVoltNotice = notice;
        Panel.SetZIndex(notice, 100);
        Children.Add(notice);
    }

    public void DismissVoltNotice()
    {
        if (ActiveVoltNotice != null)
        {
            Children.Remove(ActiveVoltNotice);
            ActiveVoltNotice = null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;

namespace X20Ctl.Product;

/// <summary>
/// Pulse modal notice boards (<c>13-notice-restart</c> and <c>14-notice-drivers</c> in <c>t_pulse.py:975</c>):
/// Dimmed backdrop with concentric wave ripples at (800, 600), 4 status pills at (y=236),
/// and central rising pearl dialog card at (x=200, y=470, w=1200).
/// </summary>
public sealed class PulseNotice : Grid
{
    public Button Primary { get; }
    public Button Secondary { get; }
    public string Kind { get; }

    private PulseNotice(
        string kind,
        string title,
        string line1,
        string[] bulletLines,
        string explText,
        string questionText,
        (string Kicker, string Text, bool active)[] statusPills,
        string primaryLabel,
        string secondaryLabel,
        Action<bool> decide)
    {
        Kind = kind;
        Background = new SolidColorBrush(Color.FromArgb(220, 5, 7, 17));

        var stage = new Canvas { Width = 1600, Height = 900 };

        // Concentric ripples centered at (800, 600) per t_pulse.py:980
        stage.Children.Add(PulseKit.Ripples(800, 600, n: 9, r0: 260, step: 70, op: 0.08));

        // 1. Top: 4 Glass Status Pills in 2x2 grid (x = 200 + (i%2)*610, y = 236 + (i/2)*86)
        for (int i = 0; i < statusPills.Length && i < 4; i++)
        {
            var (kicker, text, active) = statusPills[i];
            double px = 200 + (i % 2) * 610;
            double py = 236 + (i / 2) * 86;

            var pillStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Mini dial on left (d=30, sw=2.6)
            pillStack.Children.Add(PulseParts.DialMini(active ? 1.0 : 0.0, diameter: 30, strokeWidth: 2.6).Also(d => d.Margin = new(0, 0, 16, 0)));

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(PulseParts.Kicker(kicker).Also(k => k.FontSize = 10));
            textStack.Children.Add(PulseParts.Body(text, 14, Colors.White, FontWeights.Normal).Also(t => t.Margin = new(0, 2, 0, 0)));
            pillStack.Children.Add(textStack);

            var pillBorder = PulseParts.GlassCard(pillStack, new Thickness(24, 14, 24, 14), radius: 36);
            pillBorder.Width = 590;
            pillBorder.Height = 72;

            Canvas.SetLeft(pillBorder, px);
            Canvas.SetTop(pillBorder, py);
            stage.Children.Add(pillBorder);
        }

        // 2. Rising Pearl Card at (x=200, y=470, w=1200)
        Primary = PulseParts.PillButton(primaryLabel, primary: true, onPearl: true, onClick: () => decide(true));
        Secondary = PulseParts.PillButton(secondaryLabel, primary: false, onPearl: true, onClick: () => decide(false));

        var pearlGrid = new Grid { Width = 1104 };
        pearlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(520) });
        pearlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        pearlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Column of Pearl Card
        var leftCol = new StackPanel();
        var headLine = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var logo = PulseParts.LogoSmall(72);
        logo.Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.5 };
        headLine.Children.Add(logo);
        headLine.Children.Add(PulseParts.Disp(title, 26, PulseParts.PearlInk, FontWeights.Bold).Also(t => t.Margin = new(18, 0, 0, 0)));
        leftCol.Children.Add(headLine);

        leftCol.Children.Add(PulseParts.Disp(line1, 18, PulseParts.PearlInk, FontWeights.SemiBold).Also(t => t.Margin = new(0, 16, 0, 8)));

        foreach (string bl in bulletLines)
        {
            var bRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 6, 0, 0) };
            bRow.Children.Add(PulseParts.ResonanceMark().Also(m => m.Margin = new(0, 0, 12, 0)));
            bRow.Children.Add(PulseParts.Body(bl, 14.5, PulseParts.PearlMid));
            leftCol.Children.Add(bRow);
        }
        Grid.SetColumn(leftCol, 0);
        pearlGrid.Children.Add(leftCol);

        // Right Column of Pearl Card
        var rightCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        rightCol.Children.Add(PulseParts.Body(explText, 14.5, PulseParts.PearlMid, wrap: true));

        var qText = new TextBlock
        {
            Text = questionText,
            FontFamily = PulseParts.DispFont,
            FontSize = 32,
            FontWeight = FontWeights.Light,
            Foreground = PulseInk.B(PulseParts.PearlInk),
            Margin = new(0, 16, 0, 18)
        };
        rightCol.Children.Add(qText);

        var actionStack = new StackPanel { Orientation = Orientation.Horizontal };
        actionStack.Children.Add(Secondary.Also(b => b.Margin = new(0, 0, 12, 0)));
        actionStack.Children.Add(Primary);
        rightCol.Children.Add(actionStack);

        Grid.SetColumn(rightCol, 2);
        pearlGrid.Children.Add(rightCol);

        var pearlCard = PulseParts.RisingPearlCard(pearlGrid, padding: new Thickness(44, 38, 44, 38), topRadius: 36, bottomRadius: 28);
        pearlCard.Width = 1200;
        Canvas.SetLeft(pearlCard, 200);
        Canvas.SetTop(pearlCard, 470);
        stage.Children.Add(pearlCard);

        // Footer hints
        var footer = PulseParts.BottomFooter([("A", primaryLabel), ("B", secondaryLabel)], out _);
        Canvas.SetLeft(footer, 124);
        Canvas.SetTop(footer, 850);
        stage.Children.Add(footer);

        Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = stage });
    }

    public static PulseNotice ThemeRestart(string themeName, Action<bool> decide)
    {
        var pills = new (string, string, bool)[]
        {
            ("UPDATE", "Theme configuration updated and validated", true),
            ("WHILE IT INSTALLS", "App assets and shaders switch dynamically", true),
            ("AFTER YES", "Restart loads new theme opening film & layout", true),
            ("AFTER NO", "Active theme applies on next manual launch", false)
        };

        return new PulseNotice(
            "restart",
            "Update available · v2.4.1",
            $"{themeName} starts on next launch",
            new[] { "Signed release package from verified maintainer", "Direct controller hardware bridge improvements" },
            "Your theme preference has been saved to local preferences. Restart X20CTL now to activate the new layout, or continue working with your current session.",
            "Restart X20CTL now?",
            pills,
            "Restart now",
            "Later",
            decide);
    }

    public static PulseNotice Drivers(IReadOnlyList<string> names, Action<bool> decide)
    {
        var pills = new (string, string, bool)[]
        {
            ("UPDATE", "Signed installers · ViGEmBus & HidHide", true),
            ("WHILE IT INSTALLS", "Windows asks for administrator permission once", true),
            ("AFTER YES", "Games see virtual Xbox 360 pad with zero delay", true),
            ("AFTER NO", "Native X20 USB input continues straight to games", false)
        };

        return new PulseNotice(
            "drivers",
            "Driver Setup Required",
            "PC mode needs two small drivers",
            new[] { "ViGEmBus · Virtual Xbox 360 controller driver", "HidHide · Physical input device cloaking driver" },
            "Both drivers are signed installers from Nefarius Software Solutions. They enable zero-lag virtual controller emulation for games while preventing duplicate inputs.",
            "Install drivers now?",
            pills,
            "Install now",
            "Not now",
            decide);
    }
}

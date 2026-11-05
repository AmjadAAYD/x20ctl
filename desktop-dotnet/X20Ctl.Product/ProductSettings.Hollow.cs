using System.Windows.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public sealed partial class ProductSettings
{
    internal FrameworkElement? hollowRoot;
    private string hollowView = "hub";

    private void BuildHollow()
    {
        if (pageGrid != null) pageGrid.Visibility = Visibility.Collapsed;
        hub.Visibility = Visibility.Collapsed;
        about.Visibility = Visibility.Collapsed;
        feedback.Visibility = Visibility.Collapsed;
        pcMode.Visibility = Visibility.Collapsed;
        themes.Visibility = Visibility.Collapsed;
        footer.Visibility = Visibility.Collapsed;

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        hollowRoot = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        Children.Add(hollowRoot);

        HollowSync();
    }

    internal void HollowSync()
    {
        if (hollowRoot is not Viewbox box || box.Child is not Canvas board) return;
        board.Children.Clear();

        hollowView = pcMode.Visibility == Visibility.Visible ? "pcmode" :
                     feedback.Visibility == Visibility.Visible ? "feedback" :
                     about.Visibility == Visibility.Visible ? "about" :
                     themes.Visibility == Visibility.Visible ? "themes" : "hub";

        switch (hollowView)
        {
            case "about":
                RenderHollowAbout(board);
                break;
            case "themes":
                RenderHollowThemes(board);
                break;
            case "feedback":
                RenderHollowFeedback(board);
                break;
            case "pcmode":
                RenderHollowPcMode(board);
                break;
            case "hub":
            default:
                RenderHollowHub(board);
                break;
        }
    }

    private void RenderHollowHub(Canvas board)
    {
        var frame = new HollowToolFrame("TOOLS & CONFIGURATION", "Diagnostics, update checks, themes, and application preferences", () => Back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));
        var canvas = frame.ContentArea;

        var items = new (string Title, string Desc, string ActionLabel, Action Action, string Icon)[]
        {
            ("CONTROLLER CHECK", "Verify button inputs, triggers, and sensor diagnostics over Bluetooth or USB.", "Run Diagnostic Check", () => Check.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)), ""),
            ("WRITE SCAN", "Inspect controller registers and compare stored profiles with local firmware records.", "Inspect Firmware Scan", () => WriteScan.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)), ""),
            ("VISUAL THEMES", "Switch between hand-crafted themes: Hollow, Nocturne, Sunrise, Tide, and classic styles.", "Browse Themes", () => ShowThemes(true), ""),
            ("FEEDBACK & SUPPORT", "Report bugs, propose new feature requests, or view diagnostic logs.", "Open Feedback Form", () => ShowFeedback(true), ""),
            ("SOFTWARE UPDATES", "Check for newer preview builds and driver package updates on GitHub.", "Check for Updates", () => ShowAbout(true), ""),
            ("OPENING CINEMATIC", "Replay the theme opening intro film and brand animation.", "Replay Intro", () => Replay.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)), ""),
            ("APPLICATION SOUNDS", "Configure interface feedback chime volume and audio effects.", "Sound Settings", () => ShowAbout(true), ""),
            ("ABOUT & CREDITS", "License terms, third-party libraries, privacy policies, and release notes.", "View Release Notes", () => ShowAbout(true), "")
        };

        var grid = new UniformGrid { Columns = 4, Rows = 2, Width = 1440, Height = 640 };
        foreach (var item in items)
        {
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = item.Icon, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 28, Foreground = HollowKit.IvoryTextBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) });
            sp.Children.Add(new TextBlock { Text = item.Title, FontFamily = HollowKit.Display, FontSize = 13.5, Foreground = HollowKit.IvoryTextBrush, HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = item.Desc, FontFamily = HollowKit.Body, FontSize = 11, Foreground = HollowKit.MutedTextBrush, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 12), Height = 36 });
            sp.Children.Add(HollowKit.DiamondDivider(180, 10));

            var btn = HollowKit.HollowButton(item.ActionLabel, item.Action, isPrimary: false, height: 32);
            btn.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(btn);

            var card = HollowKit.FiligreeCard(sp, padding: 14, cornerSize: 12);
            card.Margin = new Thickness(8);
            grid.Children.Add(card);
        }

        Canvas.SetLeft(grid, 80);
        Canvas.SetTop(grid, 40);
        canvas.Children.Add(grid);

        board.Children.Add(frame);
    }

    private void RenderHollowThemes(Canvas board)
    {
        RenderCatalogThemes(board);
    }

    private void RenderHollowAbout(Canvas board)
    {
        var frame = new HollowToolFrame("ABOUT X20CTL", "Open-source controller companion software for EasySMX controllers", () => ShowAbout(false));
        var canvas = frame.ContentArea;

        var sp = new StackPanel { Width = 800, HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new TextBlock { Text = "X20CTL · HOLLOW EDITION", FontFamily = HollowKit.Display, FontSize = 22, Foreground = HollowKit.IvoryTextBrush, HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = "Version 4.1.0-preview.7 · Built natively with WPF & .NET 10", FontFamily = HollowKit.Body, FontSize = 13, Foreground = HollowKit.MutedTextBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 16) });
        sp.Children.Add(HollowKit.DiamondDivider(600, 16));

        sp.Children.Add(new TextBlock
        {
            Text = "Created by Amjad AAYD.\nSpecial thanks to Kiin and Team EasySMX for hardware samples and documentation support.\nAll trademarks, logos, and controller hardware properties belong to their respective owners.",
            FontFamily = HollowKit.Body,
            FontSize = 13,
            Foreground = HollowKit.SoftTextBrush,
            TextAlignment = TextAlignment.Center,
            LineHeight = 22,
            Margin = new Thickness(0, 14, 0, 24)
        });

        var closeBtn = HollowKit.HollowButton("Close", () => ShowAbout(false), isPrimary: false, width: 120, height: 34);
        closeBtn.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(closeBtn);

        var card = HollowKit.FiligreeCard(sp, padding: 28, cornerSize: 16);
        card.Width = 860;
        Canvas.SetLeft(card, 370);
        Canvas.SetTop(card, 120);
        canvas.Children.Add(card);

        board.Children.Add(frame);
    }

    private void RenderHollowFeedback(Canvas board)
    {
        var frame = new HollowToolFrame("FEEDBACK & ISSUES", "Submit bug reports, feature suggestions, and telemetry archives", () => ShowFeedback(false));
        var canvas = frame.ContentArea;

        var sp = new StackPanel { Width = 700 };
        sp.Children.Add(new TextBlock { Text = "CONTROLLER FEEDBACK", FontFamily = HollowKit.Display, FontSize = 16, Foreground = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 0, 0, 10) });
        sp.Children.Add(new TextBlock { Text = "Describe what happened or request a calibration feature:", FontFamily = HollowKit.Body, FontSize = 12.5, Foreground = HollowKit.SoftTextBrush, Margin = new Thickness(0, 0, 0, 8) });

        var tb = new TextBox
        {
            Height = 160,
            Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x05, 0x0c, 0x14)),
            Foreground = HollowKit.IvoryTextBrush,
            BorderBrush = HollowKit.IvoryBorderBrush,
            BorderThickness = new Thickness(1),
            FontFamily = HollowKit.Body,
            FontSize = 13,
            Padding = new Thickness(10),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Text = "The stick calibration is smooth, but I would like custom curve deadzones below 3%."
        };
        sp.Children.Add(tb);

        var submitBtn = HollowKit.HollowButton("Save feedback log", () => ShowFeedback(false), isPrimary: true, width: 160, height: 36);
        submitBtn.Margin = new Thickness(0, 14, 0, 0);
        submitBtn.HorizontalAlignment = HorizontalAlignment.Right;
        sp.Children.Add(submitBtn);

        var card = HollowKit.FiligreeCard(sp, padding: 24, cornerSize: 16);
        card.Width = 750;
        Canvas.SetLeft(card, 425);
        Canvas.SetTop(card, 100);
        canvas.Children.Add(card);

        board.Children.Add(frame);
    }

    private void RenderHollowPcMode(Canvas board)
    {
        var frame = new HollowToolFrame("PC MODE SETTINGS", "Global virtual controller routing and gamepad emulation drivers", () => ShowPcMode(false));
        var canvas = frame.ContentArea;

        var sp = new StackPanel { Width = 750 };
        sp.Children.Add(new TextBlock { Text = "SYSTEM DRIVER REVISION", FontFamily = HollowKit.Display, FontSize = 16, Foreground = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 0, 0, 14) });
        sp.Children.Add(HollowKit.DiamondDivider(700, 14));

        sp.Children.Add(new TextBlock { Text = "ViGEmBus: Installed and active  ·  HidHide: Installed and active", FontFamily = HollowKit.Body, FontSize = 13, Foreground = HollowKit.SoftTextBrush, Margin = new Thickness(0, 12, 0, 16) });

        var closeBtn = HollowKit.HollowButton("Back to Studio", () => ShowPcMode(false), isPrimary: false, width: 150, height: 36);
        sp.Children.Add(closeBtn);

        var card = HollowKit.FiligreeCard(sp, padding: 24, cornerSize: 16);
        card.Width = 800;
        Canvas.SetLeft(card, 400);
        Canvas.SetTop(card, 120);
        canvas.Children.Add(card);

        board.Children.Add(frame);
    }
}

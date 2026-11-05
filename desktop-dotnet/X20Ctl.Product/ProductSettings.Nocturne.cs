using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public sealed partial class ProductSettings
{
    internal FrameworkElement? nocturneRoot;
    private string nocturneView = "hub";

    private void BuildNocturne()
    {
        if (pageGrid != null) pageGrid.Visibility = Visibility.Collapsed;
        hub.Visibility = Visibility.Collapsed;
        about.Visibility = Visibility.Collapsed;
        feedback.Visibility = Visibility.Collapsed;
        pcMode.Visibility = Visibility.Collapsed;
        themes.Visibility = Visibility.Collapsed;
        footer.Visibility = Visibility.Collapsed;

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        nocturneRoot = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        Children.Add(nocturneRoot);

        NocturneSync();
    }

    internal void NocturneSync()
    {
        if (nocturneRoot is not Viewbox box || box.Child is not Canvas board) return;
        board.Children.Clear();

        nocturneView = pcMode.Visibility == Visibility.Visible ? "pcmode" :
                       feedback.Visibility == Visibility.Visible ? "feedback" :
                       about.Visibility == Visibility.Visible ? "about" :
                       themes.Visibility == Visibility.Visible ? "themes" : "hub";

        switch (nocturneView)
        {
            case "about":
                RenderNocturneAbout(board);
                break;
            case "themes":
                RenderNocturneThemes(board);
                break;
            case "feedback":
                RenderNocturneFeedback(board);
                break;
            case "pcmode":
                RenderNocturnePcMode(board);
                break;
            case "hub":
            default:
                RenderNocturneHub(board);
                break;
        }
    }

    private void RenderNocturneHub(Canvas board)
    {
        var frame = new NocturneToolFrame("TOOLS & CONFIGURATION", "Diagnostics, update checks, themes, and application preferences", () => Back.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
        var canvas = frame.ContentArea;

        var items = new (string Title, string Desc, string ActionLabel, Action Action, string Icon)[]
        {
            ("CONTROLLER CHECK", "Verify button inputs, triggers, and sensor diagnostics over Bluetooth or USB.", "Run Diagnostic Check", () => Check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)), ""),
            ("WRITE SCAN", "Inspect controller registers and compare stored profiles with local firmware records.", "Inspect Firmware Scan", () => WriteScan.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)), ""),
            ("VISUAL THEMES", "Switch between hand-crafted themes: Hollow, Nocturne, Sunrise, Tide, and classic styles.", "Browse Themes", () => ShowThemes(true), ""),
            ("FEEDBACK & SUPPORT", "Report bugs, propose new feature requests, or view diagnostic logs.", "Open Feedback Form", () => ShowFeedback(true), ""),
            ("SOFTWARE UPDATES", "Check for newer preview builds and driver package updates on GitHub.", "Check for Updates", () => ShowAbout(true), ""),
            ("OPENING CINEMATIC", "Replay the theme opening intro film and brand animation.", "Replay Intro", () => Replay.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)), ""),
            ("APPLICATION SOUNDS", "Configure interface feedback chime volume and audio effects.", "Sound Settings", () => ShowAbout(true), ""),
            ("ABOUT & CREDITS", "License terms, third-party libraries, privacy policies, and release notes.", "View Release Notes", () => ShowAbout(true), "")
        };

        var grid = new UniformGrid { Columns = 4, Rows = 2, Width = 1440, Height = 640 };
        foreach (var item in items)
        {
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = item.Icon, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 28, Foreground = NocturneKit.VoidTextBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) });
            sp.Children.Add(new TextBlock { Text = item.Title, FontFamily = NocturneKit.Display, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic, FontSize = 13.5, Foreground = NocturneKit.VoidTextBrush, HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = item.Desc, FontFamily = NocturneKit.Body, FontSize = 11, Foreground = NocturneKit.SoftBrush, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 10), Height = 34 });

            var btn = NocturneKit.NocturneButton(item.ActionLabel, item.Action, isPrimary: false, height: 32);
            sp.Children.Add(btn);

            var card = NocturneKit.NocturneWhiteCard(sp, 330, 270);
            card.Margin = new Thickness(8);
            grid.Children.Add(card);
        }

        Canvas.SetLeft(grid, 80);
        Canvas.SetTop(grid, 40);
        canvas.Children.Add(grid);

        board.Children.Add(frame);
    }

    private void RenderNocturneThemes(Canvas board)
    {
        RenderCatalogThemes(board);
    }

    private void RenderNocturneAbout(Canvas board)
    {
        var frame = new NocturneToolFrame("ABOUT X20CTL", "Open-source controller companion software for EasySMX controllers", () => ShowAbout(false));
        var canvas = frame.ContentArea;

        var sp = new StackPanel { Width = 800, HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new TextBlock { Text = "X20CTL · NOCTURNE EDITION", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic, FontSize = 22, Foreground = NocturneKit.VoidTextBrush, HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = "Version 4.1.0-preview.7 · Built natively with WPF & .NET 10", FontFamily = NocturneKit.Body, FontSize = 13, Foreground = NocturneKit.SoftBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 16) });

        sp.Children.Add(new TextBlock
        {
            Text = "Created by Amjad AAYD.\nSpecial thanks to Kiin and Team EasySMX for hardware samples and documentation support.\nAll trademarks, logos, and controller hardware properties belong to their respective owners.",
            FontFamily = NocturneKit.Body,
            FontSize = 13,
            Foreground = NocturneKit.VoidTextBrush,
            TextAlignment = TextAlignment.Center,
            LineHeight = 22,
            Margin = new Thickness(0, 14, 0, 24)
        });

        var closeBtn = NocturneKit.NocturneButton("Close", () => ShowAbout(false), isPrimary: false, width: 140, height: 36);
        closeBtn.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(closeBtn);

        var card = NocturneKit.NocturneWhiteCard(sp, 860, 360);
        Canvas.SetLeft(card, 370);
        Canvas.SetTop(card, 120);
        canvas.Children.Add(card);

        board.Children.Add(frame);
    }

    private void RenderNocturneFeedback(Canvas board)
    {
        var frame = new NocturneToolFrame("FEEDBACK & ISSUES", "Submit bug reports, feature suggestions, and telemetry archives", () => ShowFeedback(false));
        var canvas = frame.ContentArea;

        var sp = new StackPanel { Width = 700 };
        sp.Children.Add(new TextBlock { Text = "CONTROLLER FEEDBACK", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic, FontSize = 16, Foreground = NocturneKit.VoidTextBrush, Margin = new Thickness(0, 0, 0, 10) });
        sp.Children.Add(new TextBlock { Text = "Describe what happened or request a calibration feature:", FontFamily = NocturneKit.Body, FontSize = 12.5, Foreground = NocturneKit.SoftBrush, Margin = new Thickness(0, 0, 0, 8) });

        var tb = new TextBox
        {
            Height = 160,
            Background = NocturneKit.NightBrush,
            Foreground = NocturneKit.WhiteBrush,
            BorderBrush = NocturneKit.AccentBrush,
            BorderThickness = new Thickness(1),
            FontFamily = NocturneKit.Body,
            FontSize = 13,
            Padding = new Thickness(10),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Text = "The stick calibration is snappy and responsive on Nocturne."
        };
        sp.Children.Add(tb);

        var submitBtn = NocturneKit.NocturneButton("Save feedback log", () => ShowFeedback(false), isPrimary: true, width: 180, height: 38);
        submitBtn.Margin = new Thickness(0, 14, 0, 0);
        submitBtn.HorizontalAlignment = HorizontalAlignment.Right;
        sp.Children.Add(submitBtn);

        var card = NocturneKit.NocturneWhiteCard(sp, 750, 360);
        Canvas.SetLeft(card, 425);
        Canvas.SetTop(card, 100);
        canvas.Children.Add(card);

        board.Children.Add(frame);
    }

    private void RenderNocturnePcMode(Canvas board)
    {
        var frame = new NocturneToolFrame("PC MODE SETTINGS", "Global virtual controller routing and gamepad emulation drivers", () => ShowPcMode(false));
        var canvas = frame.ContentArea;

        var sp = new StackPanel { Width = 750 };
        sp.Children.Add(new TextBlock { Text = "SYSTEM DRIVER REVISION", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic, FontSize = 16, Foreground = NocturneKit.VoidTextBrush, Margin = new Thickness(0, 0, 0, 14) });

        sp.Children.Add(new TextBlock { Text = "ViGEmBus: Installed and active  ·  HidHide: Installed and active", FontFamily = NocturneKit.Body, FontSize = 13, Foreground = NocturneKit.SoftBrush, Margin = new Thickness(0, 12, 0, 16) });

        var closeBtn = NocturneKit.NocturneButton("Back to Studio", () => ShowPcMode(false), isPrimary: false, width: 160, height: 38);
        sp.Children.Add(closeBtn);

        var card = NocturneKit.NocturneWhiteCard(sp, 800, 300);
        Canvas.SetLeft(card, 400);
        Canvas.SetTop(card, 120);
        canvas.Children.Add(card);

        board.Children.Add(frame);
    }
}

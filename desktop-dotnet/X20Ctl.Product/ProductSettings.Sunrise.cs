using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public sealed partial class ProductSettings
{
    internal FrameworkElement? sunriseRoot;
    private string sunriseView = "hub";

    private void BuildSunrise()
    {
        if (pageGrid != null) pageGrid.Visibility = Visibility.Collapsed;
        hub.Visibility = Visibility.Collapsed;
        about.Visibility = Visibility.Collapsed;
        feedback.Visibility = Visibility.Collapsed;
        pcMode.Visibility = Visibility.Collapsed;
        themes.Visibility = Visibility.Collapsed;
        footer.Visibility = Visibility.Collapsed;

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        sunriseRoot = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        Children.Add(sunriseRoot);

        SunriseSync();
    }

    internal void SunriseSync()
    {
        if (sunriseRoot is not Viewbox box || box.Child is not Canvas board) return;
        board.Children.Clear();

        sunriseView = pcMode.Visibility == Visibility.Visible ? "pcmode" :
                      feedback.Visibility == Visibility.Visible ? "feedback" :
                      about.Visibility == Visibility.Visible ? "about" :
                      themes.Visibility == Visibility.Visible ? "themes" : "hub";

        switch (sunriseView)
        {
            case "about":
                RenderSunriseAbout(board);
                break;
            case "themes":
                RenderSunriseThemes(board);
                break;
            case "feedback":
                RenderSunriseFeedback(board);
                break;
            case "pcmode":
                RenderSunrisePcMode(board);
                break;
            case "hub":
            default:
                RenderSunriseHub(board);
                break;
        }
    }

    private void RenderSunriseHub(Canvas board)
    {
        var frame = new SunriseToolFrame("TOOLS & CONFIGURATION", "Diagnostics, update checks, themes, and application preferences", () => Back.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
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
            sp.Children.Add(new TextBlock { Text = item.Icon, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 28, Foreground = SunriseKit.InkBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) });
            sp.Children.Add(new TextBlock { Text = item.Title, FontFamily = SunriseKit.Display, FontWeight = FontWeights.Black, FontSize = 13.5, Foreground = SunriseKit.InkBrush, HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = item.Desc, FontFamily = SunriseKit.Body, FontSize = 11, Foreground = SunriseKit.OliveBrush, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 10), Height = 34 });

            var btn = SunriseKit.SunriseButton(item.ActionLabel, item.Action, isPrimary: false, height: 32);
            sp.Children.Add(btn);

            var card = SunriseKit.CreateStickerCard(sp, 330, 270, cornerRadius: 20);
            card.Margin = new Thickness(8);
            grid.Children.Add(card);
        }

        Canvas.SetLeft(grid, 80);
        Canvas.SetTop(grid, 40);
        canvas.Children.Add(grid);

        board.Children.Add(frame);
    }

    private void RenderSunriseThemes(Canvas board)
    {
        RenderCatalogThemes(board);
    }

    private void RenderSunriseAbout(Canvas board)
    {
        var frame = new SunriseToolFrame("ABOUT X20CTL", "Open-source controller companion software for EasySMX controllers", () => ShowAbout(false));
        var canvas = frame.ContentArea;

        var sp = new StackPanel { Width = 800, HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new TextBlock { Text = "📺 X20CTL · SUNRISE EDITION", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Black, FontSize = 22, Foreground = SunriseKit.InkBrush, HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = "Version 4.1.0-preview.7 · Built natively with WPF & .NET 10", FontFamily = SunriseKit.Body, FontSize = 13, Foreground = SunriseKit.OliveBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 16) });

        sp.Children.Add(new TextBlock
        {
            Text = "Created by Amjad AAYD.\nSpecial thanks to Kiin and Team EasySMX for hardware samples and documentation support.\nAll trademarks, logos, and controller hardware properties belong to their respective owners.",
            FontFamily = SunriseKit.Body,
            FontSize = 13,
            Foreground = SunriseKit.InkBrush,
            TextAlignment = TextAlignment.Center,
            LineHeight = 22,
            Margin = new Thickness(0, 14, 0, 24)
        });

        var closeBtn = SunriseKit.SunriseButton("Close", () => ShowAbout(false), isPrimary: false, width: 140, height: 36);
        closeBtn.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(closeBtn);

        var card = SunriseKit.CreateStickerCard(sp, 860, 360, cornerRadius: 22);
        Canvas.SetLeft(card, 370);
        Canvas.SetTop(card, 120);
        canvas.Children.Add(card);

        board.Children.Add(frame);
    }

    private void RenderSunriseFeedback(Canvas board)
    {
        var frame = new SunriseToolFrame("FEEDBACK & ISSUES", "Submit bug reports, feature suggestions, and telemetry archives", () => ShowFeedback(false));
        var canvas = frame.ContentArea;

        var sp = new StackPanel { Width = 700 };
        sp.Children.Add(new TextBlock { Text = "CONTROLLER FEEDBACK", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Black, FontSize = 16, Foreground = SunriseKit.InkBrush, Margin = new Thickness(0, 0, 0, 10) });
        sp.Children.Add(new TextBlock { Text = "Describe what happened or request a calibration feature:", FontFamily = SunriseKit.Body, FontSize = 12.5, Foreground = SunriseKit.OliveBrush, Margin = new Thickness(0, 0, 0, 8) });

        var tb = new TextBox
        {
            Height = 160,
            Background = SunriseKit.CreamBrush,
            Foreground = SunriseKit.InkBrush,
            BorderBrush = SunriseKit.InkBrush,
            BorderThickness = new Thickness(2),
            FontFamily = SunriseKit.Body,
            FontSize = 13,
            Padding = new Thickness(10),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Text = "The retro TV theme looks vibrant and the tactile feedback is cheerful."
        };
        sp.Children.Add(tb);

        var submitBtn = SunriseKit.SunriseButton("Save feedback log", () => ShowFeedback(false), isPrimary: true, width: 180, height: 38);
        submitBtn.Margin = new Thickness(0, 14, 0, 0);
        submitBtn.HorizontalAlignment = HorizontalAlignment.Right;
        sp.Children.Add(submitBtn);

        var card = SunriseKit.CreateStickerCard(sp, 750, 360, cornerRadius: 22);
        Canvas.SetLeft(card, 425);
        Canvas.SetTop(card, 100);
        canvas.Children.Add(card);

        board.Children.Add(frame);
    }

    private void RenderSunrisePcMode(Canvas board)
    {
        var frame = new SunriseToolFrame("PC MODE SETTINGS", "Global virtual controller routing and gamepad emulation drivers", () => ShowPcMode(false));
        var canvas = frame.ContentArea;

        var sp = new StackPanel { Width = 750 };
        sp.Children.Add(new TextBlock { Text = "SYSTEM DRIVER REVISION", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Black, FontSize = 16, Foreground = SunriseKit.InkBrush, Margin = new Thickness(0, 0, 0, 14) });

        sp.Children.Add(new TextBlock { Text = "ViGEmBus: Installed and active  ·  HidHide: Installed and active", FontFamily = SunriseKit.Body, FontSize = 13, Foreground = SunriseKit.OliveBrush, Margin = new Thickness(0, 12, 0, 16) });

        var closeBtn = SunriseKit.SunriseButton("Back to Studio", () => ShowPcMode(false), isPrimary: false, width: 160, height: 38);
        sp.Children.Add(closeBtn);

        var card = SunriseKit.CreateStickerCard(sp, 800, 300, cornerRadius: 22);
        Canvas.SetLeft(card, 400);
        Canvas.SetTop(card, 120);
        canvas.Children.Add(card);

        board.Children.Add(frame);
    }
}

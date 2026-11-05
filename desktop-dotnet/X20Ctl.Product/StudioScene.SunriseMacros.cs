using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SunriseShowMacros()
    {
        if (sunrisePageHost == null) return;
        sunrisePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateSunriseHeader("Studio"));
        var sidebar = CreateSunriseSidebar("Macros");
        Canvas.SetLeft(sidebar, 16);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area
        var content = new Canvas { Width = 1300, Height = 770 };
        Canvas.SetLeft(content, 272);
        Canvas.SetTop(content, 80);

        var titleSp = SunriseKit.SunriseTitle("HARDWARE MACROS", "Record multi-key combos, timed sequences, and trigger loops.");
        Canvas.SetLeft(titleSp, 10);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        string curSlot = Macros?.Slot ?? "M1";

        // Slot Selector Pills (M1 - M4)
        var slotSp = new StackPanel { Orientation = Orientation.Horizontal };
        string[] slots = ["M1", "M2", "M3", "M4"];
        foreach (var s in slots)
        {
            bool isCur = (s == curSlot);
            var sChip = SunriseKit.SunriseChip($"SLOT {s}", isCur, () =>
            {
                Macros?.SelectSlot(s);
                SunriseShowMacros();
            }, tag: $"theme:macro:slot:{s.ToLowerInvariant()}");
            slotSp.Children.Add(sChip);
        }
        Canvas.SetLeft(slotSp, 10);
        Canvas.SetTop(slotSp, 75);
        content.Children.Add(slotSp);

        // ==========================================
        // LEFT STICKER CARD: Macro Timeline Ticker
        // ==========================================
        var leftSp = new StackPanel { Width = 560 };

        var timelineHeader = new Grid();
        timelineHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        timelineHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tTitle = new TextBlock
        {
            Text = $"SEQUENCE TIMELINE (M{curSlot})",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12.5,
            Foreground = SunriseKit.InkBrush
        };
        Grid.SetColumn(tTitle, 0);
        timelineHeader.Children.Add(tTitle);

        var tDuration = new TextBlock
        {
            Text = $"{Macros.Draft.Duration(curSlot):F2}s TOTAL",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            Foreground = SunriseKit.OrangeBrush
        };
        Grid.SetColumn(tDuration, 1);
        timelineHeader.Children.Add(tDuration);
        leftSp.Children.Add(timelineHeader);

        // Film-strip ticker of cream blocks
        var tickerScroll = new ScrollViewer
        {
            Width = 540,
            Height = 240,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 12, 0, 16)
        };
        var tickerPanel = new StackPanel { Orientation = Orientation.Horizontal };

        var events = Macros.Draft[curSlot];
        if (events == null || events.Count == 0)
        {
            var emptyBox = new Border
            {
                Width = 520,
                Height = 210,
                Background = SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12)
            };
            var emptySp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            emptySp.Children.Add(new TextBlock { Text = "🎞️", FontSize = 32, HorizontalAlignment = HorizontalAlignment.Center });
            emptySp.Children.Add(new TextBlock { Text = "EMPTY SEQUENCE TICKER", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = SunriseKit.InkBrush, Margin = new Thickness(0, 6, 0, 2) });
            emptySp.Children.Add(new TextBlock { Text = "Press Record or Add Step to compose hardware combo", FontFamily = SunriseKit.Body, FontSize = 11, Foreground = SunriseKit.OliveBrush });
            emptyBox.Child = emptySp;
            tickerPanel.Children.Add(emptyBox);
        }
        else
        {
            foreach (var ev in events)
            {
                var evCard = new Border
                {
                    Width = 90,
                    Height = 200,
                    Background = SunriseKit.CreamBrush,
                    BorderBrush = SunriseKit.InkBrush,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(10),
                    Margin = new Thickness(0, 0, 8, 0),
                    Padding = new Thickness(8)
                };
                var evSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                string btnText = ev.Buttons.Count > 0 ? string.Join("+", ev.Buttons) : "Pause";
                evSp.Children.Add(new TextBlock { Text = btnText, FontFamily = SunriseKit.Display, FontWeight = FontWeights.Black, FontSize = 16, Foreground = SunriseKit.InkBrush, HorizontalAlignment = HorizontalAlignment.Center });
                evSp.Children.Add(new TextBlock { Text = $"{ev.HoldMs}ms", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = SunriseKit.OrangeBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
                evCard.Child = evSp;
                tickerPanel.Children.Add(evCard);
            }
        }
        tickerScroll.Content = tickerPanel;
        leftSp.Children.Add(tickerScroll);

        // Memory capacity gauge
        var capHeader = new Grid();
        capHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        capHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        capHeader.Children.Add(new TextBlock { Text = "ON-PAD MEMORY CAPACITY", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = SunriseKit.InkBrush });
        var capVal = new TextBlock { Text = $"{events?.Count ?? 0} / 64 STEPS", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = SunriseKit.OrangeBrush };
        Grid.SetColumn(capVal, 1);
        capHeader.Children.Add(capVal);
        leftSp.Children.Add(capHeader);

        var capBar = new Border
        {
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = SunriseKit.CreamBrush,
            BorderBrush = SunriseKit.InkBrush,
            BorderThickness = new Thickness(1.5),
            Margin = new Thickness(0, 4, 0, 16),
            ClipToBounds = true
        };
        double pct = Math.Min(1.0, (events?.Count ?? 0) / 64.0);
        var capFill = new Border { Width = 540 * pct, HorizontalAlignment = HorizontalAlignment.Left, Background = SunriseKit.OrangeBrush };
        capBar.Child = capFill;
        leftSp.Children.Add(capBar);

        // Recording & Timeline Actions
        var actSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var recBtn = SunriseKit.SunriseButton(Macros?.IsRecording == true ? "■ Stop Record" : "● Record Macro", () =>
        {
            Macros?.ToggleRecording();
            SunriseShowMacros();
        }, isPrimary: Macros?.IsRecording != true, isDanger: Macros?.IsRecording == true, width: 160, height: 38, tag: "theme:macro:record");
        recBtn.Margin = new Thickness(0, 0, 10, 0);

        var clearBtn = SunriseKit.SunriseButton("Clear Sequence", () =>
        {
            Macros?.Clear();
            SunriseShowMacros();
        }, isPrimary: false, width: 140, height: 38, tag: "theme:macro:clear");
        clearBtn.Margin = new Thickness(0, 0, 10, 0);

        bool isPrev = Macros?.IsPreviewing ?? false;
        var prevBtn = SunriseKit.SunriseButton(isPrev ? "Stop Preview" : "Preview Combo", () =>
        {
            if (isPrev) Macros?.StopPreview(); else Macros?.Preview();
            SunriseShowMacros();
        }, isPrimary: false, width: 140, height: 38, tag: "theme:macro:preview");

        actSp.Children.Add(recBtn);
        actSp.Children.Add(clearBtn);
        actSp.Children.Add(prevBtn);
        leftSp.Children.Add(actSp);

        var leftCard = SunriseKit.CreateStickerCard(leftSp, 590, 520, cornerRadius: 22);
        Canvas.SetLeft(leftCard, 10);
        Canvas.SetTop(leftCard, 130);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT TV SCREEN: Trigger Mapping & Loop Modes
        // ==========================================
        var rightSp = new StackPanel { Width = 580 };

        var tvHeader = new TextBlock
        {
            Text = "PADDLE ASSIGNMENT & PLAYBACK LOOP",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 13,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        rightSp.Children.Add(tvHeader);

        var loopHeader = new TextBlock { Text = "PLAYBACK TRIGGER MODE:", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.InkBrush, Margin = new Thickness(0, 6, 0, 6) };
        rightSp.Children.Add(loopHeader);

        var loopPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
        var loopModes = new[] { "Press Once (Play)", "Hold (Continuous Loop)", "Toggle (Loop until release)" };
        foreach (var lm in loopModes)
        {
            bool isFirst = (lm == loopModes[0]);
            var lChip = SunriseKit.SunriseChip(lm, isFirst, () => { });
            loopPanel.Children.Add(lChip);
        }
        rightSp.Children.Add(loopPanel);

        var padBezel = new Border
        {
            Width = 560,
            Height = 220,
            Background = SunriseKit.CreamBrush,
            BorderBrush = SunriseKit.InkBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(0, 0, 0, 16)
        };
        var padArt = SunriseKit.ControllerArt(Model.Id, 320);
        padBezel.Child = padArt;
        rightSp.Children.Add(padBezel);

        if (ControllerWrites)
        {
            var macroActSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var readBtn = SunriseKit.SunriseButton("READ CONTROLLER", () =>
            {
                _ = LoadFromController(text => { ChangeCount.Text = text; SunriseLater(); });
                SunriseShowMacros();
            }, isPrimary: false, width: 170, height: 40, tag: "theme:macro:read");
            readBtn.Margin = new Thickness(0, 0, 10, 0);
            macroActSp.Children.Add(readBtn);

            var applyBtn = SunriseKit.SunriseButton("APPLY TO CONTROLLER", () =>
            {
                _ = ApplyToController(text => { ChangeCount.Text = text; SunriseLater(); });
                SunriseShowMacros();
            }, isPrimary: true, width: 190, height: 40, tag: "theme:apply:macros");
            macroActSp.Children.Add(applyBtn);
            rightSp.Children.Add(macroActSp);
        }
        else
        {
            var note = new TextBlock
            {
                Text = "Macros edit in local draft · Apply to controller is active on supported devices.",
                FontFamily = SunriseKit.Body,
                FontSize = 11.5,
                Foreground = SunriseKit.OliveBrush,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            rightSp.Children.Add(note);
        }

        var tvCard = SunriseKit.CreateTvScreenCard(rightSp, 610, 520, withAntennae: true);
        Canvas.SetLeft(tvCard, 630);
        Canvas.SetTop(tvCard, 80);
        content.Children.Add(tvCard);

        board.Children.Add(content);

        // Bottom Bar
        var bBar = CreateSunriseBottomBar();
        Canvas.SetLeft(bBar, 0);
        Canvas.SetTop(bBar, 856);
        board.Children.Add(bBar);

        sunrisePageHost.Children.Add(board);
    }
}

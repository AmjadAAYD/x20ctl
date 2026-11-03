using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private bool tideVibrationWired;

    private void TideShowVibration()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();

        if (Vibration == null)
        {
            Vibration = new(vibrationDraft, this, RegisterFocus, animate, Model.Id);
        }

        if (!tideVibrationWired && Vibration != null)
        {
            tideVibrationWired = true;
            Vibration.Changed += () => TideLater();
        }

        var frame = new TideToolFrame(
            "VIBRATION",
            "Linked grip strength & haptic rumble preview · Local draft",
            onBack: () => TideShowDashboard(),
            hints: new[] { "\u24B7 Back", "\u24B6 Adjust Strength", "\u24BD Reset", "\u24BE Test Rumble" },
            seed: 7);

        TideKit.AnimateEntrance(frame);

        // 1. Left List of Controls (230, 160, w=580)
        var listStack = new StackPanel { Width = 580 };

        listStack.Children.Add(TideKit.SectionHeader("LINKED MOTOR INTENSITY"));

        // Strength Slider Row
        var sldBorder = new Border
        {
            Height = 64,
            Background = TideKit.Sea0Brush,
            BorderBrush = TideKit.WhiteBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(0, 0, 0, 12),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 4, Direction = 315, Color = TideKit.Ink }
        };
        var sldGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        sldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        sldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });

        sldGrid.Children.Add(new TextBlock
        {
            Text = "GRIP STRENGTH",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = TideKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var strengthSlider = ThemeSlider(Vibration.Strength, "theme:vibration:strength", 280, true);
        strengthSlider.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(strengthSlider, 1);
        sldGrid.Children.Add(strengthSlider);

        var valBlock = new TextBlock
        {
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 18,
            Foreground = TideKit.LightCyanBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        valBlock.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = Vibration.Strength, StringFormat = "{0:0}%" });
        Grid.SetColumn(valBlock, 2);
        sldGrid.Children.Add(valBlock);

        sldBorder.Child = sldGrid;
        listStack.Children.Add(sldBorder);

        // Preset Pills Row (0%, 25%, 50%, 75%, 100%)
        listStack.Children.Add(TideKit.SectionHeader("QUICK PRESETS"));
        var presetsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        foreach (int percent in new[] { 0, 25, 50, 75, 100 })
        {
            int p = percent;
            var pill = new Border
            {
                Padding = new Thickness(18, 8, 18, 8),
                Background = (Vibration.Strength.Value == p) ? TideKit.SpikeRedBrush : TideKit.Sea0Brush,
                BorderBrush = TideKit.WhiteBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 8, 8),
                Cursor = Cursors.Hand,
                RenderTransform = new SkewTransform(-10, 0),
                Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 3, Direction = 315, Color = TideKit.Ink }
            };
            pill.Child = new TextBlock
            {
                Text = $"{p}%",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 15,
                Foreground = TideKit.WhiteBrush
            };
            presetsWrap.Children.Add(ThemeAction(pill, "theme:vibration:preset:" + p, () =>
            {
                Vibration.Set(p);
                TideShowVibration();
            }));
        }
        listStack.Children.Add(presetsWrap);

        // Region Selection Rows
        listStack.Children.Add(TideKit.SectionHeader("PREVIEW REGION"));
        foreach (string region in new[] { "All", "Both grips", "Left grip", "Right grip" })
        {
            string reg = region;
            var row = TideKit.PersonaRow($"Preview · {reg}", focused: (reg == "All"), onClick: () =>
            {
                Vibration.SelectRegion(reg);
            });
            listStack.Children.Add(ThemeAction(row, "theme:vibration:region:" + reg, () => Vibration.SelectRegion(reg)));
        }

        // Actions Row: [ Reset Draft ]
        var actRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        var resetBtn = TideKit.PersonaButton("RESET STRENGTH", primary: false, onClick: () =>
        {
            Vibration.Set(70);
            TideShowVibration();
        });
        resetBtn.Height = 38;
        actRow.Children.Add(ThemeAction(resetBtn, "theme:vibration:reset", () =>
        {
            Vibration.Set(70);
            TideShowVibration();
        }));
        listStack.Children.Add(actRow);

        var listHost = new Canvas
        {
            Width = 580,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-2)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 160);

        // 2. Right Side: Controller Visual & Expanding Shockwave Canvas (880, 160, w=660, h=540)
        var padCard = TideKit.PersonaCard(660, 540, 2);
        var cardInner = new Canvas { Width = 624, Height = 504 };

        var art = TideKit.Character(Model.Id, 60, 70, 500, -6);
        cardInner.Children.Add(art);

        // Animated Expanding Shockwaves around Left and Right Grips
        for (int i = 0; i < 3; i++)
        {
            var arcL = new Ellipse
            {
                Width = 75 + i * 28,
                Height = 110 + i * 36,
                Stroke = TideKit.LightCyanBrush,
                StrokeThickness = 3,
                Opacity = 0.85 - i * 0.25,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            Canvas.SetLeft(arcL, 75 - i * 14);
            Canvas.SetTop(arcL, 190 - i * 16);
            cardInner.Children.Add(arcL);
            TideKit.AnimatePulse(arcL, 0.9, 1.15 + i * 0.05, 0.8 + i * 0.2);

            var arcR = new Ellipse
            {
                Width = 75 + i * 28,
                Height = 110 + i * 36,
                Stroke = TideKit.LightCyanBrush,
                StrokeThickness = 3,
                Opacity = 0.85 - i * 0.25,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            Canvas.SetLeft(arcR, 440 - i * 6);
            Canvas.SetTop(arcR, 190 - i * 16);
            cardInner.Children.Add(arcR);
            TideKit.AnimatePulse(arcR, 0.9, 1.15 + i * 0.05, 0.8 + i * 0.2);
        }

        // Live Intensity Plaque
        var intPlaque = new Border
        {
            Padding = new Thickness(16, 8, 16, 8),
            Background = TideKit.InkBrush,
            BorderBrush = TideKit.SpikeRedBrush,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(6)
        };
        var intStack = new StackPanel { Orientation = Orientation.Horizontal };
        intStack.Children.Add(new TextBlock
        {
            Text = "INTENSITY: ",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 18,
            Foreground = TideKit.WhiteBrush
        });
        var intVal = new TextBlock
        {
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 18,
            Foreground = TideKit.SpikeRedBrush
        };
        intVal.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = Vibration.Strength, StringFormat = "{0:0}%" });
        intStack.Children.Add(intVal);
        intPlaque.Child = intStack;
        Canvas.SetLeft(intPlaque, 40);
        Canvas.SetTop(intPlaque, 20);
        cardInner.Children.Add(intPlaque);

        var noteSticker = TideKit.NoteSticker("LOCAL DRAFT · NO CONTROLLER MEMORY WRITTEN", -2);
        Canvas.SetLeft(noteSticker, 140);
        Canvas.SetTop(noteSticker, 430);
        cardInner.Children.Add(noteSticker);

        padCard.Child = cardInner;
        Canvas.SetLeft(padCard, 880);
        Canvas.SetTop(padCard, 160);
        frame.ContentArea.Children.Add(padCard);

        ConsoleSurface.Children.Add(frame);
    }
}

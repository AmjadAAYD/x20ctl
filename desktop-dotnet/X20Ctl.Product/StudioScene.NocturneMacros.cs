using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NocturneShowMacros()
    {
        if (nocturnePageHost == null) return;
        nocturnePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(NocturneKit.CreateCityBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateNocturneHeader("Studio"));
        var sidebar = CreateNocturneSidebar("Macros");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 74);
        board.Children.Add(sidebar);

        // Title Block
        var title = NocturneKit.NocturneTitle("MACRO", "SEQUENCES", "Automate complex inputs and chained button combos on rear paddles.");
        Canvas.SetLeft(title, 300);
        Canvas.SetTop(title, 70);
        board.Children.Add(title);

        string[] slots = ["M1", "M2", "M3", "M4"];
        string curSlot = Macros?.Slot ?? "M1";

        // Slot Tabs Row (Skewed Chips)
        var slotSp = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var s in slots)
        {
            bool isCur = (s == curSlot);
            int count = Macros?.Draft[s].Count ?? 0;
            var slotBtn = NocturneKit.NocturneChip($"{s} ({count})", isCur, () =>
            {
                Macros?.SelectSlot(s);
                NocturneShowMacros();
            }, tag: $"theme:macro:slot:{s.ToLowerInvariant()}");
            slotSp.Children.Add(slotBtn);
        }
        Canvas.SetLeft(slotSp, 300);
        Canvas.SetTop(slotSp, 205);
        board.Children.Add(slotSp);

        // ==========================================
        // LEFT STAGE: Timeline on Slanted White Card
        // ==========================================
        var leftSp = new StackPanel { Width = 640 };

        leftSp.Children.Add(new TextBlock
        {
            Text = $"SEQUENCE TIMELINE ({curSlot})",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 22,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Timeline Ruler with a row of skewed blocks
        var timelineCanvas = new Canvas
        {
            Width = 620,
            Height = 120,
            Background = new SolidColorBrush(Color.FromArgb(0x18, 0x01, 0x00, 0x42)),
            ClipToBounds = true
        };

        var events = Macros != null ? Macros.Draft[curSlot] : null;
        int stepCount = Math.Max(4, events?.Count ?? 4);

        for (int i = 0; i < stepCount; i++)
        {
            double stepX = 20 + i * (560.0 / Math.Max(1, stepCount - 1));

            // Skewed block for each macro event
            var block = new Border
            {
                Width = 24,
                Height = 44,
                Background = (i == 0) ? NocturneKit.ActiveBlueBrush : new SolidColorBrush(Color.FromArgb(0xdd, 0x00, 0x56, 0xfb)),
                BorderBrush = NocturneKit.WhiteBrush,
                BorderThickness = new Thickness(1),
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            block.Child = new TextBlock
            {
                Text = $"{i + 1}",
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = NocturneKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            Canvas.SetLeft(block, stepX);
            Canvas.SetTop(block, 38);
            timelineCanvas.Children.Add(block);
        }
        leftSp.Children.Add(timelineCanvas);

        // Capacity Progress Bar
        int entryCount = Macros?.Draft.EntryCount(curSlot) ?? 0;
        var capSp = new StackPanel { Margin = new Thickness(0, 14, 0, 14) };
        capSp.Children.Add(new TextBlock
        {
            Text = $"CAPACITY: {entryCount} / 47 EVENTS",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = NocturneKit.CobaltBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        var capTrack = new Border
        {
            Height = 6,
            Background = new SolidColorBrush(Color.FromArgb(0x25, 0x01, 0x00, 0x42)),
            CornerRadius = new CornerRadius(1)
        };
        var capFill = new Border
        {
            Height = 6,
            Width = Math.Clamp(620 * (entryCount / 47.0), 0, 620),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = NocturneKit.ActiveBlueBrush,
            CornerRadius = new CornerRadius(1)
        };
        capTrack.Child = capFill;
        capSp.Children.Add(capTrack);
        leftSp.Children.Add(capSp);

        // Action Toolbar: Record, Add Event, Preview, Clear
        var toolRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

        bool isRecording = Macros?.IsRecording ?? false;
        var recordBtn = NocturneKit.NocturneButton(isRecording ? "Stop Recording" : "Record", () =>
        {
            Macros?.ToggleRecording();
            NocturneShowMacros();
        }, isPrimary: isRecording, isDanger: isRecording, width: 150, height: 36, tag: "theme:macro:record");
        recordBtn.Margin = new Thickness(0, 0, 10, 0);

        var addEventBtn = NocturneKit.NocturneButton("+ Add Event", () =>
        {
            Macros?.Add();
            NocturneShowMacros();
        }, isPrimary: false, width: 130, height: 36, tag: "theme:macro:add");
        addEventBtn.Margin = new Thickness(0, 0, 10, 0);

        bool isPreviewing = Macros?.IsPreviewing ?? false;
        var prevBtn = NocturneKit.NocturneButton(isPreviewing ? "Stop Preview" : "Preview", () =>
        {
            if (isPreviewing) Macros?.StopPreview(); else Macros?.Preview();
            NocturneShowMacros();
        }, isPrimary: false, width: 130, height: 36, tag: "theme:macro:preview");
        prevBtn.Margin = new Thickness(0, 0, 10, 0);

        var clearBtn = NocturneKit.NocturneButton("Clear Slot", () =>
        {
            Macros?.Clear();
            NocturneShowMacros();
        }, isPrimary: false, width: 130, height: 36, tag: "theme:macro:clear");

        toolRow.Children.Add(recordBtn);
        toolRow.Children.Add(addEventBtn);
        toolRow.Children.Add(prevBtn);
        toolRow.Children.Add(clearBtn);
        leftSp.Children.Add(toolRow);

        var leftWhiteCard = NocturneKit.NocturneWhiteCard(leftSp, width: 690, height: 570);
        Canvas.SetLeft(leftWhiteCard, 280);
        Canvas.SetTop(leftWhiteCard, 260);
        board.Children.Add(leftWhiteCard);

        // ==========================================
        // RIGHT STAGE: Info & Key Mapping Guidance
        // ==========================================
        var rightSp = new StackPanel { Width = 460 };
        rightSp.Children.Add(new TextBlock
        {
            Text = "MACRO ENGINE SPECS",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var specs = new (string K, string V)[]
        {
            ("Profile Capacity", Macros?.CapacityDescription ?? "0 / 47 inputs"),
            ("Step Resolution", "1 millisecond"),
            ("Max Chained Inputs", "47 discrete inputs"),
            ("Trigger Mode", "Hardware loop playback"),
            ("Paddle Target", $"{curSlot} Rear Microswitch")
        };

        foreach (var (k, v) in specs)
        {
            var sRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
            sRow.Children.Add(new TextBlock { Text = k.ToUpperInvariant() + ": ", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = NocturneKit.CobaltBrush, Width = 150 });
            sRow.Children.Add(new TextBlock { Text = v, FontFamily = NocturneKit.Body, FontSize = 12.5, Foreground = NocturneKit.InkBrush });
            rightSp.Children.Add(sRow);
        }

        rightSp.Children.Add(new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 16, 0, 16) });

        if (ControllerWrites)
        {
            var readBtn = NocturneKit.NocturneButton("READ CONTROLLER", () =>
            {
                _ = LoadFromController(text => { ChangeCount.Text = text; NocturneLater(); });
                NocturneShowMacros();
            }, isPrimary: false, width: null, height: 38, tag: "theme:macro:read");
            readBtn.Margin = new Thickness(0, 0, 0, 10);
            rightSp.Children.Add(readBtn);

            var applyMacroBtn = NocturneKit.NocturneButton("APPLY TO CONTROLLER", () =>
            {
                _ = ApplyToController(text => { ChangeCount.Text = text; NocturneLater(); });
                NocturneShowMacros();
            }, isPrimary: true, width: null, height: 42, tag: "theme:apply:macros");
            rightSp.Children.Add(applyMacroBtn);
        }
        else
        {
            var macroNote = new TextBlock
            {
                Text = "Macros edit in local draft · Apply to controller is active on supported devices.",
                FontFamily = NocturneKit.Body,
                FontSize = 12,
                Foreground = NocturneKit.InkBrush,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            rightSp.Children.Add(macroNote);
        }

        var rightWhiteCard = NocturneKit.NocturneWhiteCard(rightSp, width: 510, height: 570);
        Canvas.SetLeft(rightWhiteCard, 1020);
        Canvas.SetTop(rightWhiteCard, 260);
        board.Children.Add(rightWhiteCard);

        // Bottom Bar
        var bottomBar = CreateNocturneBottomBar();
        Canvas.SetLeft(bottomBar, 0);
        Canvas.SetTop(bottomBar, 856);
        board.Children.Add(bottomBar);

        nocturnePageHost.Children.Add(board);
    }
}

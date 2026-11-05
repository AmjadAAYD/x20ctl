using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void HollowShowMacros()
    {
        if (hollowPageHost == null) return;
        hollowPageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateHollowHeader("Studio"));
        var sidebar = CreateHollowSidebar("Macros");
        Canvas.SetLeft(sidebar, 28);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area at x=286, y=80
        var content = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(content, 286);
        Canvas.SetTop(content, 80);

        var titleSp = HollowKit.HollowHeader("MACRO SEQUENCES", "Automate complex inputs and combo sequences on rear paddles.");
        Canvas.SetLeft(titleSp, 16);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        string[] slots = ["M1", "M2", "M3", "M4"];
        string curSlot = Macros?.Slot ?? "M1";

        // Slot Tabs Row
        var slotSp = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var s in slots)
        {
            bool isCur = (s == curSlot);
            var slotBtn = new Border
            {
                Padding = new Thickness(18, 6, 18, 6),
                Background = isCur ? HollowKit.RaisedBrush : Brushes.Transparent,
                BorderBrush = isCur ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:macro:slot:{s.ToLowerInvariant()}"
            };
            var sPanel = new StackPanel { Orientation = Orientation.Horizontal };
            if (isCur)
            {
                sPanel.Children.Add(new Path { Data = HollowKit.CreateDiamond(6), Fill = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            }
            sPanel.Children.Add(new TextBlock { Text = s, FontFamily = HollowKit.Display, FontSize = 13, Foreground = isCur ? HollowKit.IvoryTextBrush : HollowKit.SoftTextBrush });
            slotBtn.Child = sPanel;

            slotBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Macros?.SelectSlot(s);
                HollowShowMacros();
            };
            slotSp.Children.Add(slotBtn);
        }
        Canvas.SetLeft(slotSp, 16);
        Canvas.SetTop(slotSp, 68);
        content.Children.Add(slotSp);

        // ==========================================
        // LEFT PANEL: Carved Timeline Strip & Event List
        // ==========================================
        var leftSp = new StackPanel { Width = 610 };

        var timelineHeader = new TextBlock
        {
            Text = $"SEQUENCE TIMELINE ({curSlot})",
            FontFamily = HollowKit.Display,
            FontSize = 13,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        leftSp.Children.Add(timelineHeader);

        // Timeline Ruler Canvas
        var timelineCanvas = new Canvas
        {
            Width = 590,
            Height = 80,
            Background = new SolidColorBrush(Color.FromArgb(0xbb, 0x07, 0x10, 0x18)),
            ClipToBounds = true
        };
        var rulerLine = new Line { X1 = 10, Y1 = 40, X2 = 580, Y2 = 40, Stroke = HollowKit.IvoryBorderBrush, StrokeThickness = 1 };
        timelineCanvas.Children.Add(rulerLine);

        // Simulated rune/diamond steps
        var events = Macros != null ? Macros.Draft[curSlot] : null;
        int stepCount = Math.Max(4, events?.Count ?? 4);
        for (int i = 0; i < stepCount; i++)
        {
            double stepX = 20 + i * (540.0 / Math.Max(1, stepCount - 1));
            var tick = new Line { X1 = stepX, Y1 = 34, X2 = stepX, Y2 = 46, Stroke = HollowKit.SilverBorderBrush, StrokeThickness = 1 };
            var diamondMark = new Path
            {
                Data = HollowKit.CreateDiamond(8),
                Fill = (i == 0) ? HollowKit.IvoryTextBrush : HollowKit.RaisedBrush,
                Stroke = HollowKit.IvoryBorderBrush,
                StrokeThickness = 0.5
            };
            Canvas.SetLeft(diamondMark, stepX);
            Canvas.SetTop(diamondMark, 40);
            timelineCanvas.Children.Add(tick);
            timelineCanvas.Children.Add(diamondMark);
        }
        leftSp.Children.Add(timelineCanvas);

        // Actions toolbar: Record, Add Event, Preview, Clear
        var toolRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 14) };

        bool isRecording = Macros?.IsRecording ?? false;
        var recordBtn = HollowKit.HollowButton(isRecording ? "Stop Recording" : "Record", () =>
        {
            Macros?.ToggleRecording();
            HollowShowMacros();
        }, isPrimary: isRecording, height: 32, tag: "theme:macro:record");
        recordBtn.Margin = new Thickness(0, 0, 8, 0);

        var addEventBtn = HollowKit.HollowButton("Add event", () =>
        {
            Macros?.Add();
            HollowShowMacros();
        }, isPrimary: false, height: 32, tag: "theme:macro:add");
        addEventBtn.Margin = new Thickness(0, 0, 8, 0);

        bool isPreviewing = Macros?.IsPreviewing ?? false;
        var previewBtn = HollowKit.HollowButton(isPreviewing ? "Stop preview" : "Preview timeline", () =>
        {
            if (isPreviewing) Macros?.StopPreview(); else Macros?.Preview();
            HollowShowMacros();
        }, isPrimary: false, height: 32, tag: "theme:macro:preview");
        previewBtn.Margin = new Thickness(0, 0, 8, 0);

        var clearBtn = HollowKit.HollowButton("Clear this slot", () =>
        {
            Macros?.Clear();
            HollowShowMacros();
        }, isPrimary: false, height: 32, tag: "theme:macro:clear");

        toolRow.Children.Add(recordBtn);
        toolRow.Children.Add(addEventBtn);
        toolRow.Children.Add(previewBtn);
        toolRow.Children.Add(clearBtn);
        leftSp.Children.Add(toolRow);

        // Status Line
        var statusLine = new TextBlock
        {
            Text = "Reading is verified on the X20 Pro only.",
            FontFamily = HollowKit.Body,
            FontSize = 11.5,
            Foreground = HollowKit.MutedTextBrush,
            Margin = new Thickness(0, 0, 0, 8)
        };
        leftSp.Children.Add(statusLine);

        var leftCard = HollowKit.FiligreeCard(leftSp, padding: 18, cornerSize: 14);
        leftCard.Width = 636;
        leftCard.Height = 580;
        Canvas.SetLeft(leftCard, 16);
        Canvas.SetTop(leftCard, 115);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT PANEL: Event Parameters & Sync
        // ==========================================
        var rightSp = new StackPanel { Width = 550 };

        var inspectorTitle = new TextBlock
        {
            Text = "EVENT INSPECTOR",
            FontFamily = HollowKit.Display,
            FontSize = 14,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        rightSp.Children.Add(inspectorTitle);

        var stepDesc = new TextBlock
        {
            Text = "Hold button combo for 120ms then release.",
            FontFamily = HollowKit.Body,
            FontSize = 13,
            Foreground = HollowKit.SoftTextBrush,
            Margin = new Thickness(0, 0, 0, 14)
        };
        rightSp.Children.Add(stepDesc);

        rightSp.Children.Add(HollowKit.DiamondDivider(550, 16));

        // Read & Apply Buttons wired to real controller actions
        if (ControllerWrites)
        {
            var readBtn = HollowKit.HollowButton("Read controller", () =>
            {
                _ = LoadFromController(text => { ChangeCount.Text = text; HollowLater(); });
                HollowShowMacros();
            }, isPrimary: false, height: 36, tag: "theme:macro:read");
            readBtn.Margin = new Thickness(0, 12, 0, 8);
            rightSp.Children.Add(readBtn);

            var applyBtn = HollowKit.HollowPrimaryApplyButton("Apply to controller", () =>
            {
                _ = ApplyToController(text => { ChangeCount.Text = text; HollowLater(); });
                HollowShowMacros();
            }, width: double.NaN, height: 42, tag: "theme:macro:apply");
            rightSp.Children.Add(applyBtn);
        }
        else
        {
            var macroNote = new TextBlock
            {
                Text = "Macros edit in local draft · Apply to controller is active on supported devices.",
                FontFamily = HollowKit.Body,
                FontSize = 12.5,
                Foreground = HollowKit.MutedTextBrush,
                Margin = new Thickness(0, 14, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            rightSp.Children.Add(macroNote);
        }

        var rightCard = HollowKit.FiligreeCard(rightSp, padding: 22, cornerSize: 14);
        rightCard.Width = 590;
        rightCard.Height = 580;
        Canvas.SetLeft(rightCard, 674);
        Canvas.SetTop(rightCard, 115);
        content.Children.Add(rightCard);

        board.Children.Add(content);

        // Dock
        var dock = HollowKit.HollowDock("Macros");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        board.Children.Add(dock);

        hollowPageHost.Children.Add(board);
    }
}

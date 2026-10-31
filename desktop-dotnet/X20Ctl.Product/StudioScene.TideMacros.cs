using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Macro Sequences Studio for Tide theme (Section 5.4):
/// - Rebuilt in luxury ocean glassmorphism
/// - Left: Slot rail (M1-M4) + Timeline card with glowing playhead and duration ruler
/// - Capacity progress bar (n / 47 · Local draft)
/// - Right: Event inspector glass card with button matrix, steppers (-5/+5), and actions
/// - Bottom: OceanDock
/// </summary>
public sealed partial class StudioScene
{
    private bool tideMacrosWired;

    private void TideShowMacros()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();

        if (Macros is not { } macro)
        {
            var emptyBoard = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
            emptyBoard.Children.Add(TideKit.SeaBackground(6));
            emptyBoard.Children.Add(TideKit.TideHeader("MACROS", onBack: () => TideShowDashboard()));
            var msgCard = TideKit.OceanGlassCard(w: 600, h: 200);
            Canvas.SetLeft(msgCard, 500);
            Canvas.SetTop(msgCard, 300);
            var errStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            errStack.Children.Add(new TextBlock
            {
                Text = "MACRO WORKBENCH UNAVAILABLE",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 18,
                Foreground = TideKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            errStack.Children.Add(new TextBlock
            {
                Text = "Connect or select a supported controller to configure paddle macros.",
                FontFamily = TideKit.Body,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8)),
                Margin = new Thickness(0, 8, 0, 0)
            });
            msgCard.Child = errStack;
            emptyBoard.Children.Add(msgCard);
            emptyBoard.Children.Add(TideKit.OceanDock("Macros", TideSelectPage));
            tidePageHost.Children.Add(emptyBoard);
            return;
        }

        if (!tideMacrosWired)
        {
            tideMacrosWired = true;
            macro.Changed += () => TideLater();
        }

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Cinematic Background
        board.Children.Add(TideKit.SeaBackground(6));

        // 2. Global Header
        var topBar = TideKit.TideHeader(
            "MACRO SEQUENCES",
            onBack: () => TideShowDashboard()
        );
        board.Children.Add(topBar);

        // 3. Left Section — Paddle Slot Rail & Event Timeline
        var leftCanvas = new Canvas { Width = 680, Height = 700 };
        Canvas.SetLeft(leftCanvas, 48);
        Canvas.SetTop(leftCanvas, 100);

        // A. Paddle / Slot Selector Tabs across the top
        var slotRow = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string slot in macro.Draft.SlotNames)
        {
            string s = slot;
            bool active = (s == macro.Slot);
            int count = macro.Draft[s].Count;

            var tabBtn = new Border
            {
                Padding = new Thickness(16, 7, 16, 7),
                Background = active
                    ? new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1),
                        GradientStops =
                        {
                            new(Color.FromArgb(0xcc, 0x18, 0x5a, 0xe0), 0.0),
                            new(Color.FromArgb(0xee, 0x0c, 0x34, 0x98), 1.0)
                        }
                    }
                    : new SolidColorBrush(Color.FromArgb(0x80, 0x06, 0x16, 0x44)),
                BorderBrush = active
                    ? new SolidColorBrush(TideKit.CyanFoam)
                    : new SolidColorBrush(Color.FromArgb(0x50, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(active ? 2.0 : 1.0),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };

            var tabContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (active)
            {
                var dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = TideKit.HarpoonRedBrush,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                tabContent.Children.Add(dot);
                TideKit.AnimatePulse(dot, 0.8, 1.4, 1.2);
            }

            tabContent.Children.Add(new TextBlock
            {
                Text = s,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 13,
                Foreground = TideKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Badge count
            var badge = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(0x40, 0x9f, 0xf6, 0xff)),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 0, 0)
            };
            badge.Child = new TextBlock
            {
                Text = $"{count}",
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = TideKit.CyanFoamBrush
            };
            tabContent.Children.Add(badge);

            tabBtn.Child = tabContent;
            slotRow.Children.Add(ThemeAction(tabBtn, "theme:macro:slot:" + s, () =>
            {
                macro.SelectSlot(s);
                TideShowMacros();
            }));
        }
        Canvas.SetLeft(slotRow, 0);
        Canvas.SetTop(slotRow, 0);
        leftCanvas.Children.Add(slotRow);

        // B. Timeline Card
        var timelineCard = TideKit.OceanGlassCard(w: 680, h: 580);
        Canvas.SetLeft(timelineCard, 0);
        Canvas.SetTop(timelineCard, 46);

        var tlStack = new StackPanel();

        // Toolbar: Record, Add, Clear, Preview, Elapsed Time
        var toolRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        toolRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var actBtns = new StackPanel { Orientation = Orientation.Horizontal };

        // Record (Harpoon Red primary)
        var recBtn = TideKit.SparkButton(
            macro.IsRecording ? "STOP RECORDING" : "RECORD",
            icon: "\uE7C8",
            onClick: () =>
            {
                macro.ToggleRecording();
                TideShowMacros();
            }
        );
        actBtns.Children.Add(ThemeAction(recBtn, "theme:macro:record", () =>
        {
            macro.ToggleRecording();
            TideShowMacros();
        }));

        // Add Event
        var addBtn = TideKit.OceanButton("+ ADD EVENT", icon: "\uE710", primary: false, onClick: macro.Add);
        addBtn.Margin = new Thickness(8, 0, 0, 0);
        actBtns.Children.Add(ThemeAction(addBtn, "theme:macro:add", macro.Add, macro.Draft.CanEdit && macro.Draft.EntryCount(macro.Slot) < 47));

        // Clear
        var clearBtn = TideKit.OceanButton("CLEAR", icon: "\uE74D", primary: false, onClick: macro.Clear);
        clearBtn.Margin = new Thickness(8, 0, 0, 0);
        actBtns.Children.Add(ThemeAction(clearBtn, "theme:macro:clear", macro.Clear, macro.Draft.CanEdit));

        // Preview
        var prevBtn = TideKit.OceanButton("PREVIEW", icon: "\uE768", primary: false, onClick: macro.Preview);
        prevBtn.Margin = new Thickness(8, 0, 0, 0);
        actBtns.Children.Add(ThemeAction(prevBtn, "theme:macro:preview", macro.Preview));

        toolRow.Children.Add(actBtns);

        // Elapsed Time Readout (replacing the empty white box!)
        int totalMs = macro.Draft.Duration(macro.Slot);
        var timePill = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0x06, 0x16, 0x44)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6)
        };
        timePill.Child = new TextBlock
        {
            Text = $"TOTAL: {totalMs / 1000.0:0.00}s",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = TideKit.CyanFoamBrush
        };
        Grid.SetColumn(timePill, 2);
        toolRow.Children.Add(timePill);

        tlStack.Children.Add(toolRow);

        // Capacity Progress Bar
        int entryCount = macro.Draft.EntryCount(macro.Slot);
        var capHeader = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        capHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        capHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        capHeader.Children.Add(new TextBlock
        {
            Text = $"{macro.Slot} SEQUENCE · LOCAL DRAFT",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.WhiteBrush
        });

        var capVal = new TextBlock
        {
            Text = $"CAPACITY {entryCount} / 47",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = TideKit.CyanFoamBrush
        };
        Grid.SetColumn(capVal, 1);
        capHeader.Children.Add(capVal);
        tlStack.Children.Add(capHeader);

        // Progress bar track
        var barTrack = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(Color.FromArgb(0x30, 0x9f, 0xf6, 0xff)),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var barFill = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = Math.Clamp(640 * (entryCount / 47.0), 0, 640),
            Background = new SolidColorBrush(entryCount > 40 ? TideKit.HarpoonRed : TideKit.CyanFoam)
        };
        barTrack.Child = barFill;
        tlStack.Children.Add(barTrack);

        // Event List or Empty State
        var events = macro.Draft[macro.Slot];
        if (events.Count == 0)
        {
            var emptyPanel = new StackPanel
            {
                Height = 360,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            emptyPanel.Children.Add(new TextBlock
            {
                Text = "\uE7C3",
                FontFamily = TideKit.Icons,
                FontSize = 36,
                Foreground = new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            emptyPanel.Children.Add(new TextBlock
            {
                Text = "NO MACRO EVENTS IN THIS SLOT",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 16,
                Foreground = TideKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 4)
            });
            emptyPanel.Children.Add(new TextBlock
            {
                Text = "Click '+ ADD EVENT' or 'RECORD' to capture paddle sequence inputs.",
                FontFamily = TideKit.Body,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            });
            var firstAddBtn = TideKit.OceanButton("+ ADD FIRST EVENT", icon: "\uE710", primary: true, onClick: macro.Add);
            firstAddBtn.HorizontalAlignment = HorizontalAlignment.Center;
            emptyPanel.Children.Add(firstAddBtn);
            tlStack.Children.Add(emptyPanel);
        }
        else
        {
            var seqScroll = new ScrollViewer
            {
                Height = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var seqStack = new StackPanel { Width = 640 };

            for (int i = 0; i < events.Count; i++)
            {
                int index = i;
                var entry = events[i];
                bool isSelected = (i == macro.SelectedIndex);

                var rowBorder = new Border
                {
                    Height = 48,
                    CornerRadius = new CornerRadius(8),
                    Background = isSelected
                        ? new SolidColorBrush(Color.FromArgb(0x40, 0x18, 0x5a, 0xe0))
                        : new SolidColorBrush(Color.FromArgb(0x20, 0x06, 0x16, 0x44)),
                    BorderBrush = isSelected
                        ? new SolidColorBrush(TideKit.CyanFoam)
                        : new SolidColorBrush(Color.FromArgb(0x30, 0x9f, 0xf6, 0xff)),
                    BorderThickness = new Thickness(isSelected ? 1.5 : 1.0),
                    Padding = new Thickness(12, 0, 12, 0),
                    Margin = new Thickness(0, 0, 0, 6),
                    Cursor = Cursors.Hand
                };

                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Playhead indicator
                if (isSelected)
                {
                    var playheadBar = new Border
                    {
                        Width = 3,
                        Height = 28,
                        CornerRadius = new CornerRadius(1.5),
                        Background = TideKit.HarpoonRedBrush,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    rowGrid.Children.Add(playheadBar);
                }

                // Step number
                var stepNum = new TextBlock
                {
                    Text = $"{i + 1:00}",
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Foreground = isSelected ? TideKit.CyanFoamBrush : new SolidColorBrush(Color.FromArgb(0x80, 0xc0, 0xdc, 0xf8)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(isSelected ? 8 : 0, 0, 0, 0)
                };
                rowGrid.Children.Add(stepNum);

                // Buttons pressed chip list
                var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(btnPanel, 1);
                if (entry.Buttons.Count > 0)
                {
                    foreach (string b in entry.Buttons)
                    {
                        var bChip = new Border
                        {
                            CornerRadius = new CornerRadius(4),
                            Background = new SolidColorBrush(Color.FromArgb(0x60, 0x08, 0x1a, 0x4c)),
                            BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x9f, 0xf6, 0xff)),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(6, 2, 6, 2),
                            Margin = new Thickness(0, 0, 6, 0)
                        };
                        bChip.Child = new TextBlock
                        {
                            Text = b,
                            FontFamily = TideKit.Display,
                            FontWeight = FontWeights.Black,
                            FontSize = 11,
                            Foreground = TideKit.WhiteBrush
                        };
                        btnPanel.Children.Add(bChip);
                    }
                }
                else
                {
                    btnPanel.Children.Add(new TextBlock
                    {
                        Text = "Stick Deflection Only",
                        FontFamily = TideKit.Body,
                        FontStyle = FontStyles.Italic,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xc0, 0xdc, 0xf8))
                    });
                }
                rowGrid.Children.Add(btnPanel);

                // Hold & Pause Pills (NO clipped text!)
                var pillsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(pillsPanel, 2);

                var holdPill = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromArgb(0x30, 0x3a, 0xa5, 0xff)),
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(0, 0, 6, 0)
                };
                holdPill.Child = new TextBlock
                {
                    Text = $"{entry.HoldMs} ms hold",
                    FontFamily = TideKit.Body,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = TideKit.CyanFoamBrush
                };
                pillsPanel.Children.Add(holdPill);

                var pausePill = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromArgb(0x20, 0xff, 0xff, 0xff)),
                    Padding = new Thickness(8, 3, 8, 3)
                };
                pausePill.Child = new TextBlock
                {
                    Text = $"{entry.PauseMs} ms pause",
                    FontFamily = TideKit.Body,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8))
                };
                pillsPanel.Children.Add(pausePill);

                rowGrid.Children.Add(pillsPanel);

                rowBorder.Child = rowGrid;
                seqStack.Children.Add(ThemeAction(rowBorder, "theme:macro:event:" + i, () =>
                {
                    macro.Select(index);
                    TideShowMacros();
                }));
            }

            seqScroll.Content = seqStack;
            tlStack.Children.Add(seqScroll);
        }

        timelineCard.Child = tlStack;
        leftCanvas.Children.Add(timelineCard);
        board.Children.Add(leftCanvas);

        // 4. Right Section — Event Inspector Glass Card (760 x 626)
        var inspectCard = TideKit.OceanGlassCard(w: 800, h: 626);
        Canvas.SetLeft(inspectCard, 750);
        Canvas.SetTop(inspectCard, 100);

        var cardInner = new StackPanel();

        if (macro.SelectedIndex >= 0 && macro.SelectedIndex < events.Count)
        {
            var curEvent = events[macro.SelectedIndex];

            cardInner.Children.Add(new TextBlock
            {
                Text = $"EVENT {macro.SelectedIndex + 1:00} · TARGET INPUTS & TIMING",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 18,
                Foreground = TideKit.WhiteBrush,
                Margin = new Thickness(0, 0, 0, 14)
            });

            // Target Gamepad Buttons Grid
            cardInner.Children.Add(new TextBlock
            {
                Text = "SELECT TARGET BUTTONS (CHORD MULTIPLE):",
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = TideKit.CyanFoamBrush,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var inWrap = new WrapPanel { Width = 760, Margin = new Thickness(0, 0, 0, 16) };
            foreach (string inKey in MacroDraft.Inputs)
            {
                string ik = inKey;
                bool isPressed = curEvent.Buttons.Contains(ik);

                var inBtn = new Border
                {
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = isPressed
                        ? new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1),
                            GradientStops =
                            {
                                new(Color.FromArgb(0xee, 0x18, 0x5a, 0xe0), 0.0),
                                new(Color.FromArgb(0xff, 0x0c, 0x34, 0x98), 1.0)
                            }
                        }
                        : new SolidColorBrush(Color.FromArgb(0x50, 0x06, 0x16, 0x44)),
                    BorderBrush = isPressed
                        ? new SolidColorBrush(TideKit.CyanFoam)
                        : new SolidColorBrush(Color.FromArgb(0x40, 0x9f, 0xf6, 0xff)),
                    BorderThickness = new Thickness(isPressed ? 1.5 : 1.0),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 6, 6),
                    Cursor = Cursors.Hand
                };

                var inStack = new StackPanel { Orientation = Orientation.Horizontal };
                if (isPressed)
                {
                    var dot = new Ellipse
                    {
                        Width = 5,
                        Height = 5,
                        Fill = TideKit.HarpoonRedBrush,
                        Margin = new Thickness(0, 0, 5, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    inStack.Children.Add(dot);
                }

                inStack.Children.Add(new TextBlock
                {
                    Text = ik,
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = TideKit.WhiteBrush
                });

                inBtn.Child = inStack;
                inWrap.Children.Add(ThemeAction(inBtn, "theme:macro:input:" + ik, () =>
                {
                    var updated = isPressed
                        ? curEvent.Buttons.Where(b => b != ik).ToArray()
                        : curEvent.Buttons.Append(ik).ToArray();
                    macro.Update(curEvent with { Buttons = updated });
                    TideShowMacros();
                }, macro.Draft.CanEdit));
            }
            cardInner.Children.Add(inWrap);

            // Duration Stepper Panels: Hold & Pause
            var durGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            durGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            durGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Hold Stepper
            var holdCard = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0x06, 0x16, 0x44)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var hPanel = new StackPanel();
            hPanel.Children.Add(new TextBlock
            {
                Text = "HOLD DURATION",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Foreground = TideKit.CyanFoamBrush,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var hBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var hMinus = TideKit.OceanButton("−5 MS", onClick: () =>
            {
                macro.Update(curEvent with { HoldMs = Math.Clamp(curEvent.HoldMs - 5, 5, 10000) });
                TideShowMacros();
            });
            hBtns.Children.Add(ThemeAction(hMinus, "theme:macro:hold:-5", () =>
            {
                macro.Update(curEvent with { HoldMs = Math.Clamp(curEvent.HoldMs - 5, 5, 10000) });
                TideShowMacros();
            }));

            var hVal = new TextBlock
            {
                Text = $"{curEvent.HoldMs} ms",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontSize = 18,
                Foreground = TideKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 16, 0)
            };
            hBtns.Children.Add(hVal);

            var hPlus = TideKit.OceanButton("+5 MS", onClick: () =>
            {
                macro.Update(curEvent with { HoldMs = Math.Clamp(curEvent.HoldMs + 5, 5, 10000) });
                TideShowMacros();
            });
            hBtns.Children.Add(ThemeAction(hPlus, "theme:macro:hold:+5", () =>
            {
                macro.Update(curEvent with { HoldMs = Math.Clamp(curEvent.HoldMs + 5, 5, 10000) });
                TideShowMacros();
            }));

            hPanel.Children.Add(hBtns);
            holdCard.Child = hPanel;
            durGrid.Children.Add(holdCard);

            // Pause Stepper
            var pauseCard = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0x06, 0x16, 0x44)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(8, 0, 0, 0)
            };
            var pPanel = new StackPanel();
            pPanel.Children.Add(new TextBlock
            {
                Text = "PAUSE AFTER EVENT",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Foreground = TideKit.CyanFoamBrush,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var pBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var pMinus = TideKit.OceanButton("−5 MS", onClick: () =>
            {
                macro.Update(curEvent with { PauseMs = Math.Clamp(curEvent.PauseMs - 5, 0, 10000) });
                TideShowMacros();
            });
            pBtns.Children.Add(ThemeAction(pMinus, "theme:macro:pause:-5", () =>
            {
                macro.Update(curEvent with { PauseMs = Math.Clamp(curEvent.PauseMs - 5, 0, 10000) });
                TideShowMacros();
            }));

            var pVal = new TextBlock
            {
                Text = $"{curEvent.PauseMs} ms",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontSize = 18,
                Foreground = TideKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 16, 0)
            };
            pBtns.Children.Add(pVal);

            var pPlus = TideKit.OceanButton("+5 MS", onClick: () =>
            {
                macro.Update(curEvent with { PauseMs = Math.Clamp(curEvent.PauseMs + 5, 0, 10000) });
                TideShowMacros();
            });
            pBtns.Children.Add(ThemeAction(pPlus, "theme:macro:pause:+5", () =>
            {
                macro.Update(curEvent with { PauseMs = Math.Clamp(curEvent.PauseMs + 5, 0, 10000) });
                TideShowMacros();
            }));

            pPanel.Children.Add(pBtns);
            pauseCard.Child = pPanel;
            Grid.SetColumn(pauseCard, 1);
            durGrid.Children.Add(pauseCard);

            cardInner.Children.Add(durGrid);

            // Lifecycle actions: Earlier, Later, Duplicate, Remove
            var actRow = new StackPanel { Orientation = Orientation.Horizontal };

            var earlierBtn = TideKit.OceanButton("EARLIER", icon: "\uE76B", onClick: () => macro.Move(-1));
            actRow.Children.Add(ThemeAction(earlierBtn, "theme:macro:earlier", () => macro.Move(-1), macro.Draft.CanEdit));

            var laterBtn = TideKit.OceanButton("LATER", icon: "\uE76C", onClick: () => macro.Move(1));
            laterBtn.Margin = new Thickness(8, 0, 0, 0);
            actRow.Children.Add(ThemeAction(laterBtn, "theme:macro:later", () => macro.Move(1), macro.Draft.CanEdit));

            var dupBtn = TideKit.OceanButton("DUPLICATE", icon: "\uE8C8", onClick: macro.Duplicate);
            dupBtn.Margin = new Thickness(8, 0, 0, 0);
            actRow.Children.Add(ThemeAction(dupBtn, "theme:macro:duplicate", macro.Duplicate, macro.Draft.CanEdit && macro.Draft.EntryCount(macro.Slot) < 47));

            var remBtn = TideKit.OceanButton("REMOVE", icon: "\uE74D", onClick: macro.Remove);
            remBtn.Margin = new Thickness(8, 0, 0, 0);
            actRow.Children.Add(ThemeAction(remBtn, "theme:macro:remove", macro.Remove, macro.Draft.CanEdit));

            cardInner.Children.Add(actRow);
        }
        else
        {
            var noSelPanel = new StackPanel
            {
                Height = 360,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            noSelPanel.Children.Add(new TextBlock
            {
                Text = "\uE7FC",
                FontFamily = TideKit.Icons,
                FontSize = 36,
                Foreground = new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            noSelPanel.Children.Add(new TextBlock
            {
                Text = "NO EVENT SELECTED",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 18,
                Foreground = TideKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 4)
            });
            noSelPanel.Children.Add(new TextBlock
            {
                Text = "Select an event from the sequence list on the left to edit target buttons and timing.",
                FontFamily = TideKit.Body,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            cardInner.Children.Add(noSelPanel);
        }

        inspectCard.Child = cardInner;
        board.Children.Add(inspectCard);

        // 5. Bottom Navigation Dock
        var navDock = TideKit.OceanDock("Macros", TideSelectPage);
        Canvas.SetLeft(navDock, 230);
        Canvas.SetTop(navDock, 820);
        board.Children.Add(navDock);

        tidePageHost.Children.Add(board);
    }
}

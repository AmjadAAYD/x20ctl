using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;

namespace X20Ctl.Product;

/// <summary>
/// Setups library presentation for the Yozakura (Blossom) theme:
/// "The active profile is painted on a large festival fan (Uchiwa);
/// saved profiles hang from a bamboo rack like prayer plaques at a shrine."
/// Strictly implements t_blossom.py:setups().
/// </summary>
public partial class StudioScene
{
    private bool blossomSetupsWired;

    private void RenderBlossomSetups(Canvas host)
    {
        if (Setups is not { } setups) return;

        if (!blossomSetupsWired)
        {
            blossomSetupsWired = true;
            setups.Watch(BlossomLater);
        }

        string rawModel = !string.IsNullOrEmpty(Model?.Name) ? Model.Name.Trim() : "CONTROLLER";
        string shortModel = rawModel.StartsWith("EasySMX ", StringComparison.OrdinalIgnoreCase)
            ? rawModel.Substring("EasySMX ".Length).Trim()
            : (rawModel.StartsWith("EASYSMX ", StringComparison.OrdinalIgnoreCase) ? rawModel.Substring("EASYSMX ".Length).Trim() : rawModel);

        // 1. Sky & Backdrop
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(240, 140, 1320, 240), 10, 8));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "setups");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "Setups",
            FontFamily = BlossomKit.Display,
            FontSize = 52,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        host.Children.Add(titleTb);

        var leadTb = new TextBlock
        {
            Text = $"{setups.Cards.Count} SAVED SETUPS · {shortModel.ToUpperInvariant()}",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        host.Children.Add(leadTb);

        // 3. Left Side: Selected Large Festival Fan (Uchiwa, 300px) at (410, 316)
        var auraGlow = new Ellipse
        {
            Width = 520,
            Height = 520,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(50, 255, 170, 110), 0.0),
                    new GradientStop(Colors.Transparent, 0.7)
                }
            },
            Effect = new BlurEffect { Radius = 20 }
        };
        Canvas.SetLeft(auraGlow, 150);
        Canvas.SetTop(auraGlow, 120);
        host.Children.Add(auraGlow);

        var mainFan = BlossomKit.UchiwaFan(300, Model.Id, true);
        Canvas.SetLeft(mainFan, 260);
        Canvas.SetTop(mainFan, 166);
        host.Children.Add(mainFan);

        var cardList = setups.Cards;
        var (selId, selName, selState) = cardList.FirstOrDefault(c => c.Id == setups.SelectedId);
        if (string.IsNullOrEmpty(selId) && cardList.Count > 0) (selId, selName, selState) = cardList[0];
        if (string.IsNullOrEmpty(selName)) selName = "No Setup Selected";

        // Active Setup Details at (600, 176, width 320)
        var detailStack = new StackPanel { Width = 320 };
        detailStack.Children.Add(new TextBlock
        {
            Text = "SELECTED SETUP",
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.TextSakuraBrush
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = selName,
            FontFamily = BlossomKit.Display,
            FontSize = 32,
            Foreground = BlossomKit.B("#ffe6cc"),
            Margin = new Thickness(0, 6, 0, 0)
        });

        var savedTag = new Border
        {
            Padding = new Thickness(12, 4, 12, 4),
            CornerRadius = new CornerRadius(3),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(BlossomKit.WarmFire1, 0.0),
                    new GradientStop(BlossomKit.WarmFire2, 1.0)
                }
            },
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.5 },
            Child = new TextBlock
            {
                Text = selState ?? "SAVED LOCALLY",
                FontFamily = BlossomKit.Small,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BlossomKit.B("#4a140f")
            }
        };
        detailStack.Children.Add(savedTag);

        detailStack.Children.Add(new TextBlock
        {
            Text = $"Target controller: {shortModel}",
            FontFamily = BlossomKit.Body,
            FontSize = 14,
            Foreground = BlossomKit.TextMuteBrush,
            Margin = new Thickness(0, 10, 0, 16)
        });

        // 2-Column Stats Grid from setups.Metrics
        var metrics = setups.Metrics;
        var statGrid = new Grid();
        statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        statGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < metrics.Count; i++)
        {
            var cell = new Border
            {
                BorderBrush = BlossomKit.B("rgba(255,220,230,0.1)"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 5, 0, 5),
                Margin = new Thickness(0, 0, 14, 6)
            };
            var cg = new Grid();
            cg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cg.Children.Add(new TextBlock { Text = metrics[i].Label, FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextWhiteBrush });
            var vt = new TextBlock { Text = metrics[i].Value, FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextAmberBrush };
            Grid.SetColumn(vt, 1);
            cg.Children.Add(vt);
            cell.Child = cg;

            Grid.SetColumn(cell, i % 2);
            Grid.SetRow(cell, i / 2);
            statGrid.Children.Add(cell);
        }
        detailStack.Children.Add(statGrid);

        // Rename Editor Panel (if rename is active)
        if (setups.RenameActive)
        {
            var renameStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            renameStack.Children.Add(new TextBlock
            {
                Text = "RENAME SETUP",
                FontFamily = BlossomKit.Small,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BlossomKit.TextSakuraBrush,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var nameBox = setups.TakeNameField();
            nameBox.Width = 300;
            nameBox.Height = 36;
            nameBox.FontSize = 14;
            nameBox.Background = BlossomKit.B("rgba(255,220,232,0.1)");
            nameBox.Foreground = BlossomKit.TextWhiteBrush;
            nameBox.BorderBrush = BlossomKit.TextSakuraBrush;
            nameBox.BorderThickness = new Thickness(1);
            nameBox.Margin = new Thickness(0, 0, 0, 8);
            renameStack.Children.Add(nameBox);

            var renameBtns = new StackPanel { Orientation = Orientation.Horizontal };
            var saveNameBorder = new Border
            {
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(18),
                Background = BlossomKit.CreateWarmLanternBrush(),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Save name", FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.B("#4a140f"), VerticalAlignment = VerticalAlignment.Center }
            };
            renameBtns.Children.Add(ThemeAction(saveNameBorder, "theme:setup:save-name", () => setups.Click("saveName")));

            var cancelRenameBorder = new Border
            {
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(18),
                Background = BlossomKit.B("rgba(255,220,232,0.1)"),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Cancel", FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            renameBtns.Children.Add(ThemeAction(cancelRenameBorder, "theme:setup:cancel-rename", () =>
            {
                setups.Click("cancelRename");
                BlossomLater();
            }));
            renameStack.Children.Add(renameBtns);
            detailStack.Children.Add(renameStack);
        }

        Canvas.SetLeft(detailStack, 600);
        Canvas.SetTop(detailStack, 176);
        host.Children.Add(detailStack);

        // Action Buttons at (236, 646)
        var actionStack = new StackPanel { Orientation = Orientation.Horizontal };
        if (setups.DeletePending)
        {
            var delWarn = new TextBlock
            {
                Text = setups.DeleteQuestion,
                FontFamily = BlossomKit.Body,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = BlossomKit.TextSakuraBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            actionStack.Children.Add(delWarn);

            var confirmBtn = new Border
            {
                Height = 44,
                Padding = new Thickness(22, 0, 22, 0),
                CornerRadius = new CornerRadius(22),
                Background = BlossomKit.CreateWarmLanternBrush(),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Delete setup", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.B("#4a140f"), VerticalAlignment = VerticalAlignment.Center }
            };
            actionStack.Children.Add(ThemeAction(confirmBtn, "theme:setup:remove", () => setups.Click("remove")));

            var keepBtn = new Border
            {
                Height = 44,
                Padding = new Thickness(22, 0, 22, 0),
                CornerRadius = new CornerRadius(22),
                Background = BlossomKit.B("rgba(255,220,232,0.1)"),
                BorderBrush = BlossomKit.B("rgba(255,220,232,0.3)"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Keep setup", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            keepBtn.Tag = "theme:macro:setup-keep";
            actionStack.Children.Add(ThemeAction(keepBtn, "theme:setup:keep", () => setups.Click("keep")));
        }
        else
        {
            var applyBtn = new Border
            {
                Height = 46,
                Padding = new Thickness(24, 0, 24, 0),
                CornerRadius = new CornerRadius(23),
                Background = BlossomKit.CreateWarmLanternBrush(),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.6 }
            };
            var aSt = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            aSt.Children.Add(CreateKeyBadge("A", true));
            aSt.Children.Add(new TextBlock { Text = "Open Setup", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.B("#4a140f"), Margin = new Thickness(10, 0, 0, 0) });
            applyBtn.Child = aSt;
            applyBtn.Tag = "theme:setup:open";
            actionStack.Children.Add(ThemeAction(applyBtn, "theme:setup:apply", () => setups.Click("open"), setups.Can("open")));

            var dupBtn = new Border
            {
                Height = 46,
                Padding = new Thickness(20, 0, 20, 0),
                CornerRadius = new CornerRadius(23),
                Background = BlossomKit.B("rgba(255,220,232,0.06)"),
                BorderBrush = BlossomKit.B("rgba(246,233,236,0.5)"),
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Duplicate", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            actionStack.Children.Add(ThemeAction(dupBtn, "theme:setup:duplicate", () => setups.Click("duplicate"), setups.Can("duplicate")));

            var renameBtn = new Border
            {
                Height = 46,
                Padding = new Thickness(20, 0, 20, 0),
                CornerRadius = new CornerRadius(23),
                Background = BlossomKit.B("rgba(255,220,232,0.06)"),
                BorderBrush = BlossomKit.B("rgba(246,233,236,0.5)"),
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Rename", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            actionStack.Children.Add(ThemeAction(renameBtn, "theme:setup:rename", () => setups.Click("rename"), setups.Can("rename")));

            var expBtn = new Border
            {
                Height = 46,
                Padding = new Thickness(20, 0, 20, 0),
                CornerRadius = new CornerRadius(23),
                Background = BlossomKit.B("rgba(255,220,232,0.06)"),
                BorderBrush = BlossomKit.B("rgba(246,233,236,0.5)"),
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Export", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            actionStack.Children.Add(ThemeAction(expBtn, "theme:setup:export", () => setups.Click("export"), setups.Can("export")));

            var delBtn = new Border
            {
                Height = 46,
                Padding = new Thickness(20, 0, 20, 0),
                CornerRadius = new CornerRadius(23),
                Background = BlossomKit.B("rgba(255,220,232,0.06)"),
                BorderBrush = BlossomKit.B("rgba(255,80,80,0.4)"),
                BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Delete", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.B("#ff8080"), VerticalAlignment = VerticalAlignment.Center }
            };
            actionStack.Children.Add(ThemeAction(delBtn, "theme:setup:delete", () => setups.Click("delete"), setups.Can("delete")));
        }

        Canvas.SetLeft(actionStack, 236);
        Canvas.SetTop(actionStack, 646);
        host.Children.Add(actionStack);

        // 4. Right Side: Bamboo Rack with Saved Fan Profiles at (960, 250, width 580)
        var bambooBar = new Border
        {
            Width = 580,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x8a, 0x64, 0x48), 0.0),
                    new GradientStop(Color.FromRgb(0x3a, 0x24, 0x18), 1.0)
                }
            }
        };
        Canvas.SetLeft(bambooBar, 960);
        Canvas.SetTop(bambooBar, 250);
        host.Children.Add(bambooBar);

        // Fans on rack from setups.Cards
        for (int i = 0; i < 6; i++)
        {
            double fx = 1050 + (i % 3) * 196;
            double fy = 270 + (i / 3) * 230;

            if (i < setups.Cards.Count)
            {
                var card = setups.Cards[i];
                bool isSelected = card.Id == setups.SelectedId;
                var fanElem = BlossomKit.UchiwaFan(140, Model.Id, isSelected);
                fanElem.Cursor = Cursors.Hand;
                Canvas.SetLeft(fanElem, fx - 70);
                Canvas.SetTop(fanElem, fy);
                string cardId = card.Id;
                host.Children.Add(ThemeAction(fanElem, "theme:setup:card:" + cardId, () =>
                {
                    setups.Select(cardId);
                    BlossomLater();
                }));

                var fLabelTb = new TextBlock
                {
                    Text = card.Name,
                    FontFamily = BlossomKit.Heading,
                    FontSize = 15,
                    Foreground = isSelected ? BlossomKit.TextAmberBrush : BlossomKit.TextWhiteBrush,
                    TextAlignment = TextAlignment.Center,
                    Width = 180
                };
                Canvas.SetLeft(fLabelTb, fx - 90);
                Canvas.SetTop(fLabelTb, fy + 160);
                host.Children.Add(fLabelTb);
            }
            else
            {
                var emptyFan = BlossomKit.UchiwaFan(140, null, false);
                Canvas.SetLeft(emptyFan, fx - 70);
                Canvas.SetTop(emptyFan, fy);
                host.Children.Add(emptyFan);
            }
        }

        // Bottom Tools: New Setup, Save Draft, Import
        var toolRow = new StackPanel { Orientation = Orientation.Horizontal };
        var newSetupBorder = new Border
        {
            Height = 42,
            Padding = new Thickness(18, 0, 18, 0),
            CornerRadius = new CornerRadius(21),
            Background = BlossomKit.B("rgba(255,220,232,0.06)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.22)"),
            BorderThickness = new Thickness(1.2),
            Margin = new Thickness(0, 0, 12, 0),
            Cursor = Cursors.Hand,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = "\uE710", FontFamily = BlossomKit.Icons, FontSize = 14, Foreground = BlossomKit.B("#e3cfd6"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "+ New setup", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.B("#e3cfd6"), VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        toolRow.Children.Add(ThemeAction(newSetupBorder, "theme:setup:new", () => setups.Click("new"), setups.Can("new")));

        var saveDraftBorder = new Border
        {
            Height = 42,
            Padding = new Thickness(18, 0, 18, 0),
            CornerRadius = new CornerRadius(21),
            Background = BlossomKit.B("rgba(255,220,232,0.06)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.22)"),
            BorderThickness = new Thickness(1.2),
            Margin = new Thickness(0, 0, 12, 0),
            Cursor = Cursors.Hand,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = "\uE74E", FontFamily = BlossomKit.Icons, FontSize = 14, Foreground = BlossomKit.B("#e3cfd6"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "Save draft", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.B("#e3cfd6"), VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        toolRow.Children.Add(ThemeAction(saveDraftBorder, "theme:setup:save", () => setups.Click("save"), setups.Can("save")));

        var impSetupBorder = new Border
        {
            Height = 42,
            Padding = new Thickness(18, 0, 18, 0),
            CornerRadius = new CornerRadius(21),
            Background = BlossomKit.B("rgba(255,220,232,0.06)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.22)"),
            BorderThickness = new Thickness(1.2),
            Cursor = Cursors.Hand,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = "\uE8B7", FontFamily = BlossomKit.Icons, FontSize = 14, Foreground = BlossomKit.B("#e3cfd6"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "Import setup", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.B("#e3cfd6"), VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        toolRow.Children.Add(ThemeAction(impSetupBorder, "theme:setup:import", () => setups.Click("import"), setups.Can("import")));

        Canvas.SetLeft(toolRow, 960);
        Canvas.SetTop(toolRow, 744);
        host.Children.Add(toolRow);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Open"));
        hintsStack.Children.Add(CreateHint("X", "Duplicate"));
        hintsStack.Children.Add(CreateHint("Y", "Delete"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }
}

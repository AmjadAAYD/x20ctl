using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool voltSetupsWired;

    /// <summary>
    /// Setups workbench for Volt: Profile presets and local configuration library.
    /// Strictly implements t_volt.py:253 Volt and models.py:setups.
    /// </summary>
    private UIElement BuildVoltSetups()
    {
        if (Setups is not { } setups)
        {
            var empty = new Canvas { Width = 970, Height = 560 };
            empty.Children.Add(new TextBlock { Text = "Setups library is unavailable.", Foreground = VoltKit.MuteBrush, FontSize = 18, Margin = new Thickness(40) });
            return VoltKit.TwoPaneShell("setups", "Setups", "Saved controller setups in local setup library.", empty, VoltSelectPage);
        }

        if (!voltSetupsWired)
        {
            voltSetupsWired = true;
            setups.Watch(VoltLater);
        }

        string rawModel = !string.IsNullOrEmpty(Model?.Name) ? Model.Name.Trim() : "CONTROLLER";
        string shortModel = rawModel.StartsWith("EasySMX ", StringComparison.OrdinalIgnoreCase)
            ? rawModel.Substring("EasySMX ".Length).Trim()
            : (rawModel.StartsWith("EASYSMX ", StringComparison.OrdinalIgnoreCase) ? rawModel.Substring("EASYSMX ".Length).Trim() : rawModel);

        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Left List: Profile cards (width: 520)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        var headGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new StackPanel();
        titleBlock.Children.Add(new TextBlock
        {
            Text = $"{setups.Cards.Count} SAVED SETUPS · {shortModel.ToUpperInvariant()}",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a")
        });
        headGrid.Children.Add(titleBlock);

        // Header Actions: New Setup, Save Current Draft, Import
        var topBtns = new StackPanel { Orientation = Orientation.Horizontal };

        var newBtn = new Border
        {
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(6),
            Background = VoltKit.AmberHotBrush,
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "+ New Setup", FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = VoltKit.B("#111111"), VerticalAlignment = VerticalAlignment.Center }
        };
        topBtns.Children.Add(ThemeAction(newBtn, "theme:setup:new", () => setups.Click("new"), setups.Can("new")));

        var saveBtn = new Border
        {
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(6),
            Background = VoltKit.B("#262626"),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "Save Draft", FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center }
        };
        topBtns.Children.Add(ThemeAction(saveBtn, "theme:setup:save", () => setups.Click("save"), setups.Can("save")));

        var impBtn = new Border
        {
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(6),
            Background = VoltKit.B("#262626"),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "Import", FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center }
        };
        topBtns.Children.Add(ThemeAction(impBtn, "theme:setup:import", () => setups.Click("import"), setups.Can("import")));

        Grid.SetColumn(topBtns, 1);
        headGrid.Children.Add(topBtns);
        leftStack.Children.Add(headGrid);

        if (setups.Cards.Count == 0)
        {
            var emptyPanel = new StackPanel { Margin = new Thickness(0, 40, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            emptyPanel.Children.Add(new TextBlock
            {
                Text = "No saved setups in library.",
                FontFamily = VoltKit.Display,
                FontSize = 18,
                Foreground = VoltKit.MuteBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            emptyPanel.Children.Add(new TextBlock
            {
                Text = "Create a new setup or save your current editor settings as a setup.",
                FontFamily = VoltKit.Body,
                FontSize = 14,
                Foreground = VoltKit.SubBrush,
                Margin = new Thickness(0, 6, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            leftStack.Children.Add(emptyPanel);
        }
        else
        {
            foreach (var card in setups.Cards)
            {
                bool isSelected = card.Id == setups.SelectedId;
                bool isActiveDraft = card.Id == setups.ActiveId;

                var cBorder = new Border
                {
                    MinHeight = 64,
                    Background = isSelected ? VoltKit.B("#2a2a2a") : VoltKit.B("#181818"),
                    BorderBrush = isSelected ? VoltKit.AmberHotBrush : VoltKit.B("#262626"),
                    BorderThickness = new Thickness(isSelected ? 2 : 1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(16, 10, 16, 10),
                    Margin = new Thickness(0, 0, 0, 8),
                    Cursor = Cursors.Hand
                };

                var cGrid = new Grid();
                cGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                cGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var tStack = new StackPanel();
                tStack.Children.Add(new TextBlock
                {
                    Text = card.Name,
                    FontFamily = VoltKit.Display,
                    FontSize = 19,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = isSelected ? VoltKit.WhiteBrush : VoltKit.LightBrush
                });
                tStack.Children.Add(new TextBlock
                {
                    Text = card.State,
                    FontFamily = VoltKit.Body,
                    FontSize = 13,
                    Foreground = isSelected ? VoltKit.AmberHotBrush : VoltKit.MuteBrush,
                    Margin = new Thickness(0, 2, 0, 0)
                });
                cGrid.Children.Add(tStack);

                if (isActiveDraft)
                {
                    var pill = new Border
                    {
                        Background = VoltKit.AmberHotBrush,
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(10, 4, 10, 4),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock { Text = "CURRENT", FontFamily = VoltKit.Display, FontSize = 11, FontWeight = FontWeights.ExtraBold, Foreground = VoltKit.B("#111111") }
                    };
                    Grid.SetColumn(pill, 1);
                    cGrid.Children.Add(pill);
                }

                cBorder.Child = cGrid;
                string id = card.Id;
                leftStack.Children.Add(ThemeAction(cBorder, "theme:setup:select:" + id, () =>
                {
                    setups.Select(id);
                    VoltLater();
                }));
            }
        }

        var leftScroll = new ScrollViewer { Width = 530, Height = 540, Content = leftStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Canvas.SetLeft(leftScroll, 0);
        Canvas.SetTop(leftScroll, 10);
        content.Children.Add(leftScroll);

        // 2. Right Details Card: Profile information (width: 410 at x = 550)
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };

        var cardBox = new Border
        {
            Width = 410,
            Background = VoltKit.SheetDarkBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24, 20, 24, 20)
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };

        var cardList = setups.Cards;
        var (selId, selName, selState) = cardList.FirstOrDefault(c => c.Id == setups.SelectedId);
        if (string.IsNullOrEmpty(selId) && cardList.Count > 0) (selId, selName, selState) = cardList[0];
        if (string.IsNullOrEmpty(selName)) selName = "No Setup Selected";

        cStack.Children.Add(new TextBlock
        {
            Text = "SELECTED SETUP",
            FontFamily = VoltKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 2)
        });

        cStack.Children.Add(new TextBlock
        {
            Text = selName,
            FontFamily = VoltKit.Display,
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush,
            LineHeight = 32
        });

        cStack.Children.Add(new TextBlock
        {
            Text = $"{selState ?? "SAVED LOCALLY"} · Target controller: {shortModel}",
            FontFamily = VoltKit.Body,
            FontSize = 13,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18,
            Margin = new Thickness(0, 4, 0, 12)
        });

        // Controller Art Preview
        var artBox = new Border
        {
            Width = 362,
            Height = 140,
            Background = VoltKit.B("#0b0b0b"),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 12)
        };
        var artCanvas = new Canvas { Width = 362, Height = 140 };
        var padArt = new Image
        {
            Width = 260,
            Source = ControllerArt.Front(Model.Id, PadColours.For(Player, Model.Id)),
            Stretch = Stretch.Uniform,
            Opacity = 0.95
        };
        Canvas.SetLeft(padArt, 51);
        Canvas.SetTop(padArt, 6);
        artCanvas.Children.Add(padArt);
        artBox.Child = artCanvas;
        cStack.Children.Add(artBox);

        // Rename Editor Panel (when Rename is active)
        if (setups.RenameActive)
        {
            var renamePanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 12) };
            renamePanel.Children.Add(new TextBlock
            {
                Text = "RENAME SETUP",
                FontFamily = VoltKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.AmberHotBrush,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var nameBox = setups.TakeNameField();
            nameBox.Width = 362;
            nameBox.Height = 38;
            nameBox.FontSize = 15;
            nameBox.Background = VoltKit.B("#181818");
            nameBox.Foreground = VoltKit.WhiteBrush;
            nameBox.BorderBrush = VoltKit.AmberHotBrush;
            nameBox.BorderThickness = new Thickness(1.5);
            nameBox.Margin = new Thickness(0, 0, 0, 8);
            renamePanel.Children.Add(nameBox);

            var renameBtns = new StackPanel { Orientation = Orientation.Horizontal };
            var saveNameBtn = new Border
            {
                Width = 175,
                Height = 38,
                CornerRadius = new CornerRadius(6),
                Background = VoltKit.AmberHotBrush,
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Save Name", FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = VoltKit.B("#111111"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            renameBtns.Children.Add(ThemeAction(saveNameBtn, "theme:setup:save-name", () => setups.Click("saveName")));

            var cancelRenameBtn = new Border
            {
                Width = 175,
                Height = 38,
                CornerRadius = new CornerRadius(6),
                Background = VoltKit.B("#262626"),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Cancel", FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.WhiteBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            renameBtns.Children.Add(ThemeAction(cancelRenameBtn, "theme:setup:cancel-rename", () =>
            {
                setups.Click("cancelRename");
                VoltLater();
            }));
            renamePanel.Children.Add(renameBtns);
            cStack.Children.Add(renamePanel);
        }
        else if (setups.DeletePending)
        {
            var delWarn = new TextBlock
            {
                Text = setups.DeleteQuestion,
                FontFamily = VoltKit.Display,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(VoltKit.C("#ff4d4f")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            cStack.Children.Add(delWarn);

            var delRow = new StackPanel { Orientation = Orientation.Horizontal };
            var confirmFace = VoltKit.OrangeActionButton("Delete Setup", 175, 42, null);
            delRow.Children.Add(ThemeAction(confirmFace, "theme:setup:remove", () => setups.Click("remove")));

            var cancelBtn = new Border
            {
                Width = 175,
                Height = 42,
                CornerRadius = new CornerRadius(8),
                Background = VoltKit.B("#262626"),
                Margin = new Thickness(12, 0, 0, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Keep Setup", FontFamily = VoltKit.Display, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }
            };
            delRow.Children.Add(ThemeAction(cancelBtn, "theme:setup:keep", () => setups.Click("keep")));
            cStack.Children.Add(delRow);
        }
        else
        {
            // Primary Action: Open Setup Locally (loads draft)
            var openFace = VoltKit.OrangeActionButton("Open Setup Locally", 362, 44, null);
            var openBtn = ThemeAction(openFace, "theme:setup:open", () => setups.Click("open"), setups.Can("open"));
            openFace.Tag = "theme:setup:apply"; // support both tags for verification suites
            cStack.Children.Add(openBtn);

            // Row 2: Duplicate & Rename
            var row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var dupBtn = new Border
            {
                Width = 175,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                Background = VoltKit.B("#262626"),
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Duplicate", FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }
            };
            row2.Children.Add(ThemeAction(dupBtn, "theme:setup:duplicate", () => setups.Click("duplicate"), setups.Can("duplicate")));

            var renameBtn = new Border
            {
                Width = 175,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                Background = VoltKit.B("#262626"),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Rename", FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }
            };
            row2.Children.Add(ThemeAction(renameBtn, "theme:setup:rename", () => setups.Click("rename"), setups.Can("rename")));
            cStack.Children.Add(row2);

            // Row 3: Export & Delete
            var row3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var expBtn = new Border
            {
                Width = 175,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                Background = VoltKit.B("#262626"),
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Export Setup", FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }
            };
            row3.Children.Add(ThemeAction(expBtn, "theme:setup:export", () => setups.Click("export"), setups.Can("export")));

            var delBtn = new Border
            {
                Width = 175,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                Background = VoltKit.B("#262626"),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Delete", FontFamily = VoltKit.Display, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(VoltKit.C("#ff6b6b")), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }
            };
            delBtn.Tag = "theme:setup:delete";
            row3.Children.Add(ThemeAction(delBtn, "theme:setup:delete", () => setups.Click("delete"), setups.Can("delete")));
            cStack.Children.Add(row3);
        }

        // Library notice
        var noticeTb = new TextBlock
        {
            Text = "Local setup library · Stored on this computer",
            FontFamily = VoltKit.Body,
            FontSize = 12,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        cStack.Children.Add(noticeTb);

        cardBox.Child = cStack;
        rightStack.Children.Add(cardBox);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        return VoltKit.TwoPaneShell("setups", "Setups", $"{setups.Cards.Count} SAVED SETUPS · {shortModel.ToUpperInvariant()}", content, VoltSelectPage);
    }
}

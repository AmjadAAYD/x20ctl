using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowSetups()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Cinematic Sea Background
        board.Children.Add(TideKit.SeaBackground(23, "SETUP LIBRARY"));

        var setups = Setups;
        var cards = setups?.Cards ?? Array.Empty<(string Id, string Name, string State)>();
        string selectedId = setups?.SelectedId ?? "";
        string activeId = setups?.ActiveId ?? "";

        var activeCard = cards.FirstOrDefault(c => c.Id == selectedId);
        if (string.IsNullOrEmpty(activeCard.Id) && cards.Count > 0) activeCard = cards[0];

        // 2. Top Header Bar: Logo, Title, Actions, Back
        var topBar = new Canvas { Width = 1520, Height = 64 };
        Canvas.SetLeft(topBar, 40);
        Canvas.SetTop(topBar, 20);

        // Title
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var moonIcon = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = new RadialGradientBrush
            {
                Center = new Point(0.4, 0.4),
                GradientStops =
                {
                    new(Colors.White, 0.0),
                    new(Color.FromRgb(0x9f, 0xf6, 0xff), 0.7),
                    new(Color.FromRgb(0x3a, 0xa5, 0xff), 1.0)
                }
            },
            Margin = new Thickness(0, 0, 12, 0),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x9f, 0xe0, 0xff), BlurRadius = 14, ShadowDepth = 0 }
        };
        titleStack.Children.Add(moonIcon);
        titleStack.Children.Add(new TextBlock
        {
            Text = "TIDE // LOCAL SETUPS LIBRARY",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 20,
            Foreground = TideKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        topBar.Children.Add(titleStack);
        Canvas.SetLeft(titleStack, 0);
        Canvas.SetTop(titleStack, 14);

        // Header Actions: New, Save Draft, Import, Export
        var hActions = new StackPanel { Orientation = Orientation.Horizontal };

        var newBtn = TideKit.OceanButton("+ NEW SETUP", icon: "\uE710", primary: false, onClick: () =>
        {
            setups?.Click("new");
            TideShowSetups();
        });
        newBtn.Margin = new Thickness(0, 0, 8, 0);
        hActions.Children.Add(newBtn);

        var saveDraftBtn = TideKit.SparkButton("SAVE CURRENT DRAFT", icon: "\uE74E", onClick: () =>
        {
            setups?.Click("save");
            TideShowSetups();
        });
        saveDraftBtn.Margin = new Thickness(0, 0, 8, 0);
        hActions.Children.Add(saveDraftBtn);

        var importBtn = TideKit.OceanButton("IMPORT", icon: "\uE8B5", onClick: () =>
        {
            setups?.Click("import");
            TideShowSetups();
        });
        importBtn.Margin = new Thickness(0, 0, 8, 0);
        hActions.Children.Add(importBtn);

        var exportBtn = TideKit.OceanButton("EXPORT", icon: "\uEDE1", onClick: () =>
        {
            setups?.Click("export");
            TideShowSetups();
        });
        exportBtn.Margin = new Thickness(0, 0, 14, 0);
        hActions.Children.Add(exportBtn);

        var backBtn = TideKit.OceanButton("DASHBOARD", icon: "\uE72B", onClick: () => TideShowDashboard());
        hActions.Children.Add(backBtn);

        topBar.Children.Add(hActions);
        Canvas.SetRight(hActions, 0);
        Canvas.SetTop(hActions, 10);

        board.Children.Add(topBar);

        // 3. Left Section — Setup Cards Grid (Scrollable)
        var libraryCard = TideKit.OceanGlassCard(w: 860, h: 680);
        Canvas.SetLeft(libraryCard, 60);
        Canvas.SetTop(libraryCard, 100);

        var libStack = new StackPanel();

        // Library Header
        var libHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        libHead.Children.Add(new TextBlock
        {
            Text = "\uE8B5",
            FontFamily = TideKit.Icons,
            FontSize = 18,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        libHead.Children.Add(new TextBlock
        {
            Text = "MY SAVED SETUPS",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = TideKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        libHead.Children.Add(new TextBlock
        {
            Text = $" · {cards.Count} PROFILES · LOCAL STORAGE",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = TideKit.CyanFoamBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        });
        libStack.Children.Add(libHead);

        // Scrollable Grid of Cards
        var scroll = new ScrollViewer
        {
            Height = 580,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var cardsWrap = new WrapPanel { Width = 820 };

        if (cards.Count == 0)
        {
            var emptyBox = new Border
            {
                Width = 800,
                Height = 300,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(0x40, 0x06, 0x16, 0x40)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24)
            };
            var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            emptyStack.Children.Add(new TextBlock
            {
                Text = "NO SAVED SETUPS YET",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 18,
                Foreground = TideKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            emptyStack.Children.Add(new TextBlock
            {
                Text = "Save your current controller draft or create a new profile to begin.",
                FontFamily = TideKit.Body,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            });
            emptyBox.Child = emptyStack;
            cardsWrap.Children.Add(emptyBox);
        }
        else
        {
            foreach (var card in cards)
            {
                bool isSel = (card.Id == activeCard.Id);
                bool isAct = (card.Id == activeId);

                var sTile = new Border
                {
                    Width = 260,
                    Height = 175,
                    Margin = new Thickness(6),
                    CornerRadius = new CornerRadius(10),
                    Cursor = Cursors.Hand,
                    Background = isSel
                        ? new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1),
                            GradientStops =
                            {
                                new(Color.FromArgb(0xee, 0x16, 0x54, 0xd8), 0.0),
                                new(Color.FromArgb(0xf6, 0x0a, 0x2e, 0x8a), 1.0)
                            }
                        }
                        : new SolidColorBrush(Color.FromArgb(0xaa, 0x08, 0x1a, 0x44)),
                    BorderBrush = isSel
                        ? TideKit.CyanFoamBrush
                        : new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
                    BorderThickness = new Thickness(isSel ? 2 : 1),
                    Padding = new Thickness(14),
                    Effect = isSel
                        ? new DropShadowEffect { Color = Color.FromRgb(0x1a, 0x5a, 0xd8), BlurRadius = 18, ShadowDepth = 4 }
                        : null
                };

                var sStack = new StackPanel();

                // Top state badge
                var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                if (isAct)
                {
                    var actBadge = new Border
                    {
                        CornerRadius = new CornerRadius(8),
                        Background = TideKit.HarpoonRedBrush,
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    actBadge.Child = new TextBlock
                    {
                        Text = "ACTIVE",
                        FontFamily = TideKit.Display,
                        FontWeight = FontWeights.Black,
                        FontStyle = FontStyles.Italic,
                        FontSize = 9,
                        Foreground = TideKit.WhiteBrush
                    };
                    badgeRow.Children.Add(actBadge);
                }

                badgeRow.Children.Add(new TextBlock
                {
                    Text = card.State,
                    FontFamily = TideKit.Body,
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    Foreground = TideKit.CyanFoamBrush
                });
                sStack.Children.Add(badgeRow);

                // Setup Name
                sStack.Children.Add(new TextBlock
                {
                    Text = card.Name,
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 16,
                    Foreground = TideKit.WhiteBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 0, 0, 6)
                });

                // Description
                sStack.Children.Add(new TextBlock
                {
                    Text = $"Custom mapping, response curves and haptics for {Model.Name}.",
                    FontFamily = TideKit.Body,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8)),
                    TextWrapping = TextWrapping.Wrap,
                    Height = 32
                });

                // Load action hint
                var loadHint = new TextBlock
                {
                    Text = isSel ? "▸ SELECTED" : "CLICK TO SELECT",
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 10,
                    Foreground = isSel ? TideKit.CyanFoamBrush : new SolidColorBrush(Color.FromArgb(0x70, 0xc0, 0xdc, 0xf8)),
                    Margin = new Thickness(0, 10, 0, 0)
                };
                sStack.Children.Add(loadHint);

                sTile.Child = sStack;

                string targetId = card.Id;
                sTile.MouseLeftButtonUp += (_, _) =>
                {
                    setups?.TakeNameField();
                    var cardBtn = setups?.SetupCards.FirstOrDefault(b => b.Tag as string == targetId);
                    cardBtn?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    TideShowSetups();
                };

                cardsWrap.Children.Add(sTile);
            }
        }

        scroll.Content = cardsWrap;
        libStack.Children.Add(scroll);

        libraryCard.Child = libStack;
        board.Children.Add(libraryCard);

        // 4. Right Section — Selected Setup Inspector Deck
        var detailCard = TideKit.OceanGlassCard(w: 580, h: 680);
        Canvas.SetLeft(detailCard, 960);
        Canvas.SetTop(detailCard, 100);

        var detailStack = new StackPanel();

        // Header Plaque
        var plaque = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new(Color.FromArgb(0xcc, 0x18, 0x5a, 0xe0), 0.0),
                    new(Color.FromArgb(0xee, 0x0c, 0x32, 0x96), 1.0)
                }
            },
            BorderBrush = TideKit.CyanFoamBrush,
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 0, 0, 20),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x1a, 0x5a, 0xd8), BlurRadius = 18, ShadowDepth = 4 }
        };

        var plaqueStack = new StackPanel();
        plaqueStack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(activeCard.Name) ? "NO SETUP SELECTED" : activeCard.Name.ToUpperInvariant(),
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = TideKit.WhiteBrush
        });
        plaqueStack.Children.Add(new TextBlock
        {
            Text = activeCard.Id == activeId
                ? "CURRENT ACTIVE SETUP IN LOCAL WORKBENCH"
                : "SAVED LOCAL SETUP ON THIS COMPUTER",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });
        plaque.Child = plaqueStack;
        detailStack.Children.Add(plaque);

        // If Delete Confirmation is pending
        if (setups?.DeletePending == true)
        {
            var delBox = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(0xee, 0x2e, 0x08, 0x14)),
                BorderBrush = TideKit.HarpoonRedBrush,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 20)
            };
            var delStack = new StackPanel();
            delStack.Children.Add(new TextBlock
            {
                Text = "CONFIRM DELETION",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 18,
                Foreground = TideKit.WhiteBrush
            });
            delStack.Children.Add(new TextBlock
            {
                Text = setups.DeleteQuestion,
                FontFamily = TideKit.Body,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xdd, 0xff, 0xc0, 0xcc)),
                Margin = new Thickness(0, 6, 0, 16)
            });

            var delBtnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var keepBtn = TideKit.OceanButton("CANCEL / KEEP", onClick: () =>
            {
                setups.Click("keep");
                TideShowSetups();
            });
            keepBtn.Margin = new Thickness(0, 0, 10, 0);
            delBtnRow.Children.Add(keepBtn);

            var removeBtn = TideKit.SparkButton("CONFIRM REMOVE", icon: "\uE74D", onClick: () =>
            {
                setups.Click("remove");
                TideShowSetups();
            });
            delBtnRow.Children.Add(removeBtn);

            delStack.Children.Add(delBtnRow);
            delBox.Child = delStack;
            detailStack.Children.Add(delBox);
        }
        // If Inline Rename is active
        else if (setups?.RenameActive == true)
        {
            var renameBox = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(0xcc, 0x06, 0x16, 0x40)),
                BorderBrush = TideKit.CyanFoamBrush,
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 20)
            };
            var rStack = new StackPanel();
            rStack.Children.Add(new TextBlock
            {
                Text = "RENAME SETUP",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 16,
                Foreground = TideKit.WhiteBrush,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var nameInput = setups.TakeNameField();
            nameInput.FontSize = 16;
            nameInput.Height = 36;
            nameInput.Margin = new Thickness(0, 0, 0, 14);
            rStack.Children.Add(nameInput);

            var rBtnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var saveNameBtn = TideKit.SparkButton("SAVE NAME", onClick: () =>
            {
                setups.Click("saveName");
                TideShowSetups();
            });
            saveNameBtn.Margin = new Thickness(0, 0, 10, 0);
            rBtnRow.Children.Add(saveNameBtn);

            var cancelRenameBtn = TideKit.OceanButton("CANCEL", onClick: () =>
            {
                setups.Click("cancelRename");
                TideShowSetups();
            });
            rBtnRow.Children.Add(cancelRenameBtn);

            rStack.Children.Add(rBtnRow);
            renameBox.Child = rStack;
            detailStack.Children.Add(renameBox);
        }

        // Metrics Section
        detailStack.Children.Add(new TextBlock
        {
            Text = "CONFIGURATION METRICS",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var metricsList = setups?.Metrics ?? Array.Empty<(string Label, string Value)>();
        foreach (var (mLabel, mVal) in metricsList)
        {
            var mRow = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(0x60, 0x06, 0x16, 0x40)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var mGrid = new Grid();
            mGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lblText = new TextBlock
            {
                Text = mLabel.ToUpperInvariant(),
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Foreground = TideKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lblText, 0);
            mGrid.Children.Add(lblText);

            var valText = new TextBlock
            {
                Text = mVal,
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = TideKit.CyanFoamBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valText, 1);
            mGrid.Children.Add(valText);

            mRow.Child = mGrid;
            detailStack.Children.Add(mRow);
        }

        // Actions Section (Rename, Duplicate, Delete, Open in Local Editor)
        var actionDock = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };

        if (setups?.Can("rename") == true)
        {
            var rnBtn = TideKit.OceanButton("RENAME", icon: "\uE8AC", onClick: () =>
            {
                setups.Click("rename");
                TideShowSetups();
            });
            rnBtn.Margin = new Thickness(0, 0, 8, 0);
            actionDock.Children.Add(rnBtn);
        }

        if (setups?.Can("duplicate") == true)
        {
            var dupBtn = TideKit.OceanButton("DUPLICATE", icon: "\uE8C8", onClick: () =>
            {
                setups.Click("duplicate");
                TideShowSetups();
            });
            dupBtn.Margin = new Thickness(0, 0, 8, 0);
            actionDock.Children.Add(dupBtn);
        }

        if (setups?.Can("delete") == true)
        {
            var delBtn = TideKit.OceanButton("DELETE", icon: "\uE74D", onClick: () =>
            {
                setups.Click("delete");
                TideShowSetups();
            });
            delBtn.Margin = new Thickness(0, 0, 8, 0);
            actionDock.Children.Add(delBtn);
        }

        if (setups?.Can("open") == true)
        {
            var openBtn = TideKit.SparkButton("LOAD IN LOCAL EDITOR ➔", icon: "\uE8A7", onClick: () =>
            {
                setups.Click("open");
                TideShowSetups();
            });
            actionDock.Children.Add(openBtn);
        }

        detailStack.Children.Add(actionDock);

        detailCard.Child = detailStack;
        board.Children.Add(detailCard);

        // 5. Bottom Navigation Dock
        var navDock = TideKit.OceanDock("Setups", TideSelectPage);
        Canvas.SetLeft(navDock, 230);
        Canvas.SetTop(navDock, 820);
        board.Children.Add(navDock);

        tidePageHost.Children.Add(board);
    }
}

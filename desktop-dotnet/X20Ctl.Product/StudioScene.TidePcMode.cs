using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private PcModePage? tidePcSource;
    private string tidePcSelectedRow = "switch";

    private void TideShowPcMode()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();

        var original = ManagementBodyHost.Children.OfType<ScrollViewer>().FirstOrDefault();
        var source = original?.Content as PcModePage ?? tidePcSource;
        if (source == null)
        {
            var emptyFrame = new TideToolFrame(
                "PC MODE",
                "Virtual controller routing unavailable.",
                onBack: () => TideShowDashboard(),
                hints: new[] { "\u24B7 Back" },
                seed: 12);
            ConsoleSurface.Children.Add(emptyFrame);
            return;
        }

        if (original?.Content is PcModePage pmp)
        {
            tidePcSource = pmp;
            original.Content = null;
        }

        var frame = new TideToolFrame(
            "PC MODE",
            "Route controller input through saved PC profile to virtual Xbox device. Nothing written to hardware.",
            onBack: () => TideShowDashboard(),
            hints: new[] { "\u24B7 Back", "\u24B6 Select", "\u24BD Toggle PC Mode", "LB/RB Tabs" },
            seed: 12);

        TideKit.AnimateEntrance(frame);

        // 1. Top Route Flow Bar (230, 116, w=1310, h=52)
        var routeBorder = new Border
        {
            Width = 1310,
            Height = 52,
            Background = TideKit.Sea0Brush,
            BorderBrush = TideKit.WhiteBrush,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 6, 16, 6),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 4, Direction = 315, Color = TideKit.Ink }
        };

        var routeGrid = new Grid();
        for (int i = 0; i < 7; i++)
        {
            routeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = (i % 2 == 0) ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        }

        string[] steps = [$"Physical XInput {PcModePage.Player + 1}", "PC Profile", "Virtual Xbox", "Game Engine"];
        for (int i = 0; i < 4; i++)
        {
            bool stepActive = source.RouteActive && i > 0;
            var pill = new Border
            {
                Height = 34,
                CornerRadius = new CornerRadius(4),
                Background = stepActive ? TideKit.SpikeRedBrush : (i == 0 ? TideKit.InkBrush : TideKit.Sea0Brush),
                BorderBrush = TideKit.WhiteBrush,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(10, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = steps[i],
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 13,
                    Foreground = TideKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(pill, i * 2);
            routeGrid.Children.Add(pill);

            if (i < 3)
            {
                var arr = new TextBlock
                {
                    Text = "▸",
                    FontFamily = TideKit.Display,
                    FontSize = 18,
                    Foreground = source.RouteActive ? TideKit.LightCyanBrush : TideKit.SubBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 10, 0)
                };
                Grid.SetColumn(arr, i * 2 + 1);
                routeGrid.Children.Add(arr);
            }
        }
        routeBorder.Child = routeGrid;
        Canvas.SetLeft(routeBorder, 230);
        Canvas.SetTop(routeBorder, 156);
        frame.ContentArea.Children.Add(routeBorder);

        // 2. Left Staircase List of Sub-views (230, 184, w=580)
        var listStack = new StackPanel { Width = 580 };

        var rows = new (string Key, string Label, string Sub, string Tag)[]
        {
            ("switch", "PC MODE ROUTING", source.RouteActive ? "Routing active to virtual Xbox controller" : "PC mode is off. Controller goes straight to games.", "theme:pcmode:switch"),
            ("profile", "SETTINGS FOR PLAYER", $"Configuring Player {PcModePage.Player + 1} Profile", "theme:pcmode:profile"),
            ("turbo", "TURBO BINDINGS", "Rapid fire repeat mappings", "theme:pcmode:turbo"),
            ("games", "PER-GAME PROFILES", "Automatic profile switching", "theme:pcmode:games"),
            ("share", "IMPORT / EXPORT", "Share profiles via JSON files", "theme:pcmode:share"),
            ("reset", "RESET PROFILE", "Restore factory defaults for player", "theme:pcmode:reset"),
            ("notice", "BEFORE PC MODE STARTS", !source.NoticeShown ? "Driver consent accepted" : "Requires driver authorization", "theme:pcmode:notice")
        };

        Action? updateDetail = null;

        foreach (var (rKey, rLabel, rSub, rTag) in rows)
        {
            string key = rKey;
            bool isFocused = key == tidePcSelectedRow;

            var row = TideKit.PersonaRow(rLabel, sub: rSub, focused: isFocused, onClick: () =>
            {
                tidePcSelectedRow = key;
                TideShowPcMode();
            });

            listStack.Children.Add(ThemeAction(row, rTag, () =>
            {
                tidePcSelectedRow = key;
                TideShowPcMode();
            }));
        }

        var listHost = new Canvas
        {
            Width = 580,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-2)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 222);

        // 3. Right Side: Detail / Action Card (880, 184, w=660, h=520)
        var detailCard = TideKit.PersonaCard(660, 520, 2);
        var cardInner = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };

        switch (tidePcSelectedRow)
        {
            case "notice":
                cardInner.Children.Add(new TextBlock
                {
                    Text = "BEFORE PC MODE STARTS",
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 22,
                    Foreground = TideKit.Sea0Brush,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                string[] notices = [
                    "Your settings only work while X20CTL is running. If it closes, the controller returns to standard XInput.",
                    "Use turbo and macros responsibly in multiplayer games to avoid anti-cheat infractions.",
                    "Virtual controller creation utilizes the ViGEmBus driver. Windows will request driver permission once."
                ];

                foreach (var n in notices)
                {
                    var nb = new Border
                    {
                        Background = TideKit.Sea0Brush,
                        Padding = new Thickness(14, 10, 14, 10),
                        Margin = new Thickness(0, 0, 0, 8),
                        CornerRadius = new CornerRadius(4)
                    };
                    nb.Child = new TextBlock
                    {
                        Text = n,
                        FontFamily = TideKit.Body,
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 13,
                        Foreground = TideKit.WhiteBrush,
                        TextWrapping = TextWrapping.Wrap
                    };
                    cardInner.Children.Add(nb);
                }

                var acceptBtn = TideKit.PersonaButton(!source.NoticeShown ? "CONSENT ACCEPTED" : "I UNDERSTAND", primary: true, onClick: () =>
                {
                    source.Accept();
                    TideShowPcMode();
                });
                acceptBtn.Height = 44;
                acceptBtn.Margin = new Thickness(0, 12, 0, 0);
                cardInner.Children.Add(ThemeAction(acceptBtn, "theme:pcmode:consent", () =>
                {
                    source.Accept();
                    TideShowPcMode();
                }));
                break;

            case "switch":
            default:
                cardInner.Children.Add(new TextBlock
                {
                    Text = "VIRTUAL XBOX ROUTING",
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 22,
                    Foreground = TideKit.Sea0Brush,
                    Margin = new Thickness(0, 0, 0, 6)
                });

                cardInner.Children.Add(new TextBlock
                {
                    Text = "Virtual Xbox 360 controller output for full remapping, custom curves, and macros.",
                    FontFamily = TideKit.Body,
                    FontSize = 14,
                    Foreground = TideKit.SubBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 16)
                });

                var switchBox = new Border
                {
                    Height = 60,
                    Background = TideKit.Sea0Brush,
                    BorderBrush = TideKit.WhiteBrush,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(16, 0, 16, 0),
                    Margin = new Thickness(0, 0, 0, 16)
                };
                var sbGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
                sbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                sbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                sbGrid.Children.Add(new TextBlock
                {
                    Text = "ENABLE PC MODE",
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 16,
                    Foreground = TideKit.WhiteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var swCheck = new CheckBox
                {
                    Content = "ACTIVE",
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 14,
                    Foreground = TideKit.LightCyanBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = "theme:pcmode:toggle"
                };
                swCheck.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsChecked") { Source = source.Switch, Mode = BindingMode.TwoWay });
                Grid.SetColumn(swCheck, 1);
                sbGrid.Children.Add(swCheck);
                switchBox.Child = sbGrid;
                cardInner.Children.Add(switchBox);

                var statusPill = new Border
                {
                    Padding = new Thickness(14, 8, 14, 8),
                    Background = source.RouteActive ? TideKit.SpikeRedBrush : TideKit.InkBrush,
                    CornerRadius = new CornerRadius(4),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                statusPill.Child = new TextBlock
                {
                    Text = source.RouteActive ? "ROUTING ACTIVE · VIRTUAL CONTROLLER CONNECTED" : "ROUTING INACTIVE · DIRECT INPUT ACTIVE",
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 13,
                    Foreground = TideKit.WhiteBrush
                };
                cardInner.Children.Add(statusPill);
                break;
        }

        detailCard.Child = cardInner;
        Canvas.SetLeft(detailCard, 880);
        Canvas.SetTop(detailCard, 222);
        frame.ContentArea.Children.Add(detailCard);

        ConsoleSurface.Children.Add(frame);
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;

namespace X20Ctl.Product;

/// <summary>
/// Setups workbench presentation for the Yozakura (Blossom) theme:
/// "Setups are traditional Japanese festival fans (Uchiwa): the setup's artwork
/// on a round washi paper face with a bamboo rim and carved timber handle."
/// Strictly implements t_blossom.py:setups().
/// </summary>
public partial class StudioScene
{
    private void RenderBlossomSetups(Canvas host)
    {
        // 1. Sky & Corner Canopy
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Corner weeping branch canopy
        host.Children.Add(BlossomKit.CanopyBranch("corner", 11));

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(940, 160, 620, 540), 12, 11));

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
            Text = "Saved controller profiles with dedicated response curves, button remappings, and haptic calibrations.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        host.Children.Add(leadTb);

        // 3. Left Side: Active Large Festival Fan (Uchiwa, 300px) at (410, 316)
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

        // Active Setup Details at (600, 176, width 320)
        var detailStack = new StackPanel { Width = 320 };
        detailStack.Children.Add(new TextBlock
        {
            Text = "ACTIVE PROFILE",
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.TextSakuraBrush
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Pro Mechanical",
            FontFamily = BlossomKit.Display,
            FontSize = 36,
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
                Text = "SAVED ON CONTROLLER",
                FontFamily = BlossomKit.Small,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BlossomKit.B("#4a140f")
            }
        };
        detailStack.Children.Add(savedTag);

        detailStack.Children.Add(new TextBlock
        {
            Text = "Matches active connected hardware model.",
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            Foreground = BlossomKit.TextMuteBrush,
            Margin = new Thickness(0, 10, 0, 16)
        });

        // 2-Column Stats Grid
        var stats = new (string Key, string Val)[]
        {
            ("Sampling", "1000 Hz"),
            ("Curve Profile", "Aggressive Linear"),
            ("Stick Deadzone", "4%"),
            ("Vibration Level", "70%")
        };
        var statGrid = new Grid();
        statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        statGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < stats.Length; i++)
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
            cg.Children.Add(new TextBlock { Text = stats[i].Key, FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextWhiteBrush });
            var vt = new TextBlock { Text = stats[i].Val, FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextAmberBrush };
            Grid.SetColumn(vt, 1);
            cg.Children.Add(vt);
            cell.Child = cg;

            Grid.SetColumn(cell, i % 2);
            Grid.SetRow(cell, i / 2);
            statGrid.Children.Add(cell);
        }
        detailStack.Children.Add(statGrid);

        Canvas.SetLeft(detailStack, 600);
        Canvas.SetTop(detailStack, 176);
        host.Children.Add(detailStack);

        // Action Buttons at (236, 646)
        var actionStack = new StackPanel { Orientation = Orientation.Horizontal };
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
        aSt.Children.Add(new TextBlock { Text = "Apply to controller", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.B("#4a140f"), Margin = new Thickness(10, 0, 0, 0) });
        applyBtn.Child = aSt;
        actionStack.Children.Add(applyBtn);

        foreach (var act in new[] { "Duplicate", "Export profile", "Delete" })
        {
            var btnBorder = new Border
            {
                Height = 46,
                Padding = new Thickness(20, 0, 20, 0),
                CornerRadius = new CornerRadius(23),
                Background = BlossomKit.B("rgba(255,220,232,0.06)"),
                BorderBrush = BlossomKit.B("rgba(246,233,236,0.5)"),
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = act, FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            actionStack.Children.Add(btnBorder);
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

        // 6 Fans on rack
        var fanCards = new (string Name, string ModelKey, bool Active)[]
        {
            ("Pro Mechanical", "x20", true),
            ("Fighting Stance", "x20-black", false),
            ("Racing Sim", "x15-wukong", false),
            ("Empty Slot 4", "", false),
            ("Empty Slot 5", "", false),
            ("Empty Slot 6", "", false)
        };

        for (int i = 0; i < 6; i++)
        {
            var fc = fanCards[i];
            double fx = 1050 + (i % 3) * 196;
            double fy = 270 + (i / 3) * 230;

            if (!string.IsNullOrEmpty(fc.ModelKey))
            {
                var fanElem = BlossomKit.UchiwaFan(140, fc.ModelKey, fc.Active);
                fanElem.Cursor = Cursors.Hand;
                Canvas.SetLeft(fanElem, fx - 70);
                Canvas.SetTop(fanElem, fy);
                host.Children.Add(fanElem);

                var fLabelTb = new TextBlock
                {
                    Text = fc.Name,
                    FontFamily = BlossomKit.Heading,
                    FontSize = 17,
                    Foreground = fc.Active ? BlossomKit.TextAmberBrush : BlossomKit.TextWhiteBrush,
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

        // Bottom Tools: New Setup, Import, Restore
        var toolRow = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (ic, txt) in new[] { ("\uE710", "New setup"), ("\uE74E", "Import profile"), ("\uE8B5", "Restore defaults") })
        {
            var btnBorder = new Border
            {
                Height = 42,
                Padding = new Thickness(18, 0, 18, 0),
                CornerRadius = new CornerRadius(21),
                Background = BlossomKit.B("rgba(255,220,232,0.06)"),
                BorderBrush = BlossomKit.B("rgba(255,220,232,0.22)"),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = Cursors.Hand
            };
            var bStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            bStack.Children.Add(new TextBlock { Text = ic, FontFamily = BlossomKit.Icons, FontSize = 14, Foreground = BlossomKit.B("#e3cfd6"), Margin = new Thickness(0, 0, 8, 0) });
            bStack.Children.Add(new TextBlock { Text = txt, FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.B("#e3cfd6") });
            btnBorder.Child = bStack;
            toolRow.Children.Add(btnBorder);
        }
        Canvas.SetLeft(toolRow, 960);
        Canvas.SetTop(toolRow, 744);
        host.Children.Add(toolRow);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Apply"));
        hintsStack.Children.Add(CreateHint("X", "Duplicate"));
        hintsStack.Children.Add(CreateHint("Y", "Delete"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }
}

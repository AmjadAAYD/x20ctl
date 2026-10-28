using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Hanami night garden presentation for player docks in the Yozakura (Blossom) theme:
/// "One lantern per player hangs over the garden; it is lit when a controller sits in its light.
/// Empty bays are dark under dormant plum lanterns, waiting for a warrior's pad to be docked."
/// Strictly implements t_blossom.py:zone().
/// </summary>
public partial class DockView
{
    private bool blossom;
    private Canvas? blossomFace;

    private void InitBlossom()
    {
        if (ThemeCatalog.Current.Id != "blossom") return;
        blossom = true;
        Card.Visibility = Visibility.Collapsed;
        FocusButton.Cursor = Cursors.Hand;

        blossomFace = new Canvas { IsHitTestVisible = false };
        ContentCanvas.Children.Insert(0, blossomFace);
    }

    private void PresentBlossom(bool hero)
    {
        if (!blossom || blossomFace == null) return;
        blossomFace.Children.Clear();

        foreach (UIElement e in new UIElement[] { Card, PlayerPill, Details, Halo, ArtShadow, Art, AddControllerButton, RemoveControllerButton, ActionCanvas, ArrowBadge, PlusBadge, Silhouette })
        {
            e.Visibility = Visibility.Collapsed;
        }

        if (hero)
        {
            blossomFace.Visibility = Visibility.Collapsed;
            return;
        }

        blossomFace.Visibility = Visibility.Visible;

        bool isLit = FocusButton.IsKeyboardFocused || FocusButton.IsMouseOver || Halo.Visibility == Visibility.Visible;
        bool focused = (Index == 0) || isLit;
        bool assigned = Player.Model != null;

        double w = RenderedBounds.Width > 0 ? RenderedBounds.Width : (ActualWidth > 0 ? ActualWidth : 360);
        double h = RenderedBounds.Height > 0 ? RenderedBounds.Height : (ActualHeight > 0 ? ActualHeight : 540);

        var bay = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Cursor = Cursors.Hand
        };

        var bayContent = new Canvas
        {
            Width = w,
            Height = h
        };

        double cx = w / 2.0;

        // Overhead Hanging Paper Lantern (Number 1-4)
        double lanternW = focused ? 84 : 72;
        double lanternH = focused ? 106 : 92;
        double lanternTop = 16;
        var playerLantern = BlossomKit.PaperLantern(
            lanternW,
            lanternH,
            assigned,
            null,
            Player.Number.ToString(),
            focused ? 1.25 : 0.7,
            18);
        Canvas.SetLeft(playerLantern, cx - playerLantern.Width / 2.0);
        Canvas.SetTop(playerLantern, lanternTop);
        bayContent.Children.Add(playerLantern);

        // Light cone and ground glow when assigned
        double groundY = h - 140;
        if (assigned)
        {
            // Slanted light beam polygon
            double topW = lanternW * 0.7;
            double botW = Math.Min(w * 0.95, 320);
            string conePath = $"M {cx - topW / 2.0:F1} {lanternTop + lanternH:F1} " +
                              $"L {cx + topW / 2.0:F1} {lanternTop + lanternH:F1} " +
                              $"L {cx + botW / 2.0:F1} {groundY + 20:F1} " +
                              $"L {cx - botW / 2.0:F1} {groundY + 20:F1} Z";

            var cone = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(conePath),
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb((byte)(focused ? 75 : 45), 255, 190, 120), 0.0),
                        new GradientStop(Color.FromArgb((byte)(focused ? 35 : 18), 255, 154, 106), 0.65),
                        new GradientStop(Colors.Transparent, 1.0)
                    }
                },
                Opacity = focused ? 1.0 : 0.75
            };
            bayContent.Children.Add(cone);

            // Elliptical ground pool
            var groundPool = new Ellipse
            {
                Width = botW,
                Height = 44,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb((byte)(focused ? 65 : 35), 255, 170, 110), 0.0),
                        new GradientStop(Colors.Transparent, 1.0)
                    }
                },
                Effect = new BlurEffect { Radius = 10 }
            };
            Canvas.SetLeft(groundPool, cx - botW / 2.0);
            Canvas.SetTop(groundPool, groundY);
            bayContent.Children.Add(groundPool);

            // Controller Artwork resting in the light beam
            double padW = Math.Min(w * 0.88, 260);
            string modelKey = Player.ModelId ?? "x20";
            var padImg = BlossomKit.ControllerArt(modelKey, padW);
            if (padImg != null)
            {
                padImg.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 24,
                    ShadowDepth = 16,
                    Opacity = 0.65
                };
                Canvas.SetLeft(padImg, cx - padW / 2.0);
                Canvas.SetTop(padImg, groundY - padW * 0.44);
                bayContent.Children.Add(padImg);
            }

            // Controller Name & Finish
            var labelStack = new StackPanel
            {
                Width = w,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var nameTb = new TextBlock
            {
                Text = Player.Model ?? "X20 PRO",
                FontFamily = BlossomKit.Display,
                FontSize = 26,
                FontWeight = FontWeights.Normal,
                Foreground = focused ? BlossomKit.B("#ffe6cc") : BlossomKit.TextWhiteBrush,
                TextAlignment = TextAlignment.Center
            };
            var finishTb = new TextBlock
            {
                Text = Player.ModelId != null ? (Player.ModelId.Contains("black") ? "Obsidian" : "Cherry Blossom") : "Hanami Edition",
                FontFamily = BlossomKit.Heading,
                FontStyle = FontStyles.Italic,
                FontSize = 17,
                Foreground = BlossomKit.TextSakuraBrush,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            labelStack.Children.Add(nameTb);
            labelStack.Children.Add(finishTb);

            Canvas.SetLeft(labelStack, 0);
            Canvas.SetTop(labelStack, groundY + 36);
            bayContent.Children.Add(labelStack);
        }
        else
        {
            // Empty Bay State: dashed washi frame + "Empty" / "Add controller"
            double frameW = Math.Min(w * 0.8, 220);
            double frameH = 150;
            var dashedFrame = new Rectangle
            {
                Width = frameW,
                Height = frameH,
                RadiusX = 10,
                RadiusY = 10,
                Stroke = BlossomKit.B("rgba(255,220,232,0.22)"),
                StrokeThickness = 1.6,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            Canvas.SetLeft(dashedFrame, cx - frameW / 2.0);
            Canvas.SetTop(dashedFrame, groundY - frameH + 30);
            bayContent.Children.Add(dashedFrame);

            var emptyStack = new StackPanel
            {
                Width = w,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var emptyTb = new TextBlock
            {
                Text = "Empty",
                FontFamily = BlossomKit.Heading,
                FontStyle = FontStyles.Italic,
                FontSize = 22,
                Foreground = BlossomKit.B("#8e7480"),
                TextAlignment = TextAlignment.Center
            };
            var addTb = new TextBlock
            {
                Text = "Add controller",
                FontFamily = BlossomKit.Display,
                FontSize = 20,
                Foreground = BlossomKit.B("#e3cfd6"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            };
            emptyStack.Children.Add(emptyTb);
            emptyStack.Children.Add(addTb);

            Canvas.SetLeft(emptyStack, 0);
            Canvas.SetTop(emptyStack, groundY + 24);
            bayContent.Children.Add(emptyStack);
        }

        bay.Child = bayContent;
        blossomFace.Children.Add(bay);
    }
}

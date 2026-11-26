using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product.Community;

public enum RectBtnKind { Primary, Secondary, Ghost, Danger }

public static class CommunityUI
{
    public static readonly Color BgBase = Color.FromRgb(8, 13, 25);
    public static readonly Color BgCard = Color.FromRgb(11, 19, 37);
    public static readonly Color BgSidebar = Color.FromRgb(9, 15, 30);
    public static readonly Color BgInput = Color.FromRgb(6, 11, 22);
    public static readonly Color BorderSubtle = Color.FromRgb(29, 43, 70);
    public static readonly Color BorderFocus = Color.FromRgb(56, 189, 248);
    public static readonly Color PrimaryFill = Color.FromRgb(37, 99, 235);
    public static readonly Color PrimaryHover = Color.FromRgb(29, 78, 216);

    public static Button RectButton(string label, RectBtnKind kind = RectBtnKind.Secondary, double height = 32, double fontSize = 12)
    {
        var (bg, hoverBg, border, fg) = kind switch
        {
            RectBtnKind.Primary => (PrimaryFill, PrimaryHover, Color.FromRgb(96, 165, 250), Colors.White),
            RectBtnKind.Danger => (Color.FromArgb(70, 127, 29, 29), Color.FromArgb(120, 153, 27, 27), Color.FromRgb(185, 28, 28), Color.FromRgb(254, 202, 202)),
            RectBtnKind.Ghost => (Colors.Transparent, Color.FromArgb(190, 22, 34, 58), Colors.Transparent, Color.FromRgb(148, 163, 184)),
            _ => (Color.FromRgb(16, 26, 46), Color.FromRgb(24, 38, 66), Color.FromRgb(38, 55, 88), Color.FromRgb(226, 232, 240)),
        };

        var btn = new Button
        {
            Content = label,
            Height = height,
            MinHeight = height,
            Padding = new Thickness(12, 0, 12, 0),
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(fg),
            Background = new SolidColorBrush(bg),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Template = BuildRectButtonTemplate(6)
        };

        btn.MouseEnter += (_, _) =>
        {
            btn.Background = new SolidColorBrush(hoverBg);
            if (kind == RectBtnKind.Ghost) btn.Foreground = Brushes.White;
        };
        btn.MouseLeave += (_, _) =>
        {
            btn.Background = new SolidColorBrush(bg);
            if (kind == RectBtnKind.Ghost) btn.Foreground = new SolidColorBrush(fg);
        };
        return btn;
    }

    public static Button SidebarRowButton(string label, bool active, bool indented = false)
    {
        var row = new DockPanel();
        var indicator = new Border
        {
            Width = 3,
            Height = 16,
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = active ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : Brushes.Transparent
        };
        DockPanel.SetDock(indicator, Dock.Left);
        row.Children.Add(indicator);
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = indented ? 11.5 : 12.5,
            FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = new SolidColorBrush(active ? Colors.White : Color.FromRgb(148, 163, 184)),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var baseBg = active ? Color.FromRgb(20, 34, 62) : Colors.Transparent;
        var hoverBg = active ? Color.FromRgb(24, 42, 74) : Color.FromArgb(140, 20, 32, 56);
        var btn = new Button
        {
            Height = indented ? 28 : 31,
            MinHeight = indented ? 28 : 31,
            Margin = new Thickness(indented ? 10 : 0, 1, 0, 1),
            Padding = new Thickness(6, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(baseBg),
            BorderBrush = new SolidColorBrush(active ? Color.FromArgb(90, 56, 189, 248) : Colors.Transparent),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Template = BuildRectButtonTemplate(6),
            Content = row
        };
        btn.MouseEnter += (_, _) => btn.Background = new SolidColorBrush(hoverBg);
        btn.MouseLeave += (_, _) => btn.Background = new SolidColorBrush(baseBg);
        return btn;
    }

    public static Button UnderlineTabButton(string label, bool active)
    {
        var bar = new Border
        {
            Height = 2,
            Margin = new Thickness(0, 5, 0, 0),
            CornerRadius = new CornerRadius(1),
            Background = active ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : Brushes.Transparent
        };
        var text = new TextBlock
        {
            Text = label,
            FontSize = 13,
            FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold,
            Foreground = new SolidColorBrush(active ? Colors.White : Color.FromRgb(148, 163, 184)),
        };
        var btn = new Button
        {
            Height = 36,
            MinHeight = 36,
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(10, 4, 10, 0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Template = BuildRectButtonTemplate(4),
            Content = new StackPanel { Children = { text, bar } }
        };
        btn.MouseEnter += (_, _) => { if (!active) text.Foreground = Brushes.White; };
        btn.MouseLeave += (_, _) => { if (!active) text.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)); };
        return btn;
    }

    public static Border SquareTag(string text, Color? fg = null, Color? bg = null, Color? border = null) => new()
    {
        CornerRadius = new CornerRadius(4),
        Background = new SolidColorBrush(bg ?? Color.FromArgb(95, 22, 36, 64)),
        BorderBrush = new SolidColorBrush(border ?? Color.FromArgb(120, 59, 130, 246)),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(6, 2, 6, 2),
        Margin = new Thickness(0, 0, 5, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(fg ?? Color.FromRgb(147, 197, 253)),
        }
    };

    public static Border CardSurface(UIElement child, Thickness? padding = null, Color? border = null) => new()
    {
        CornerRadius = new CornerRadius(8),
        Background = new SolidColorBrush(BgCard),
        BorderBrush = new SolidColorBrush(border ?? BorderSubtle),
        BorderThickness = new Thickness(1),
        Padding = padding ?? new Thickness(14),
        Child = child
    };

    public static TextBox RectInput(string? text = null, double height = 34, bool multiline = false) => new()
    {
        Text = text ?? "",
        Height = multiline ? double.NaN : height,
        MinHeight = height,
        Padding = new Thickness(10, multiline ? 8 : 5, 10, multiline ? 8 : 5),
        FontSize = 12.5,
        Foreground = Brushes.White,
        CaretBrush = Brushes.White,
        Background = new SolidColorBrush(BgInput),
        BorderBrush = new SolidColorBrush(BorderSubtle),
        BorderThickness = new Thickness(1),
        VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
        AcceptsReturn = multiline,
        TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
        Template = BuildRectTextBoxTemplate(6)
    };

    public static Border BuildPreviewThumb(string category, string? shareCode = null)
    {
        var canvas = new Canvas { Width = 76, Height = 56, ClipToBounds = true };
        var box = new Border
        {
            Width = 78,
            Height = 58,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(BgInput),
            BorderBrush = new SolidColorBrush(BorderSubtle),
            BorderThickness = new Thickness(1),
            Child = canvas
        };

        // Subtle instrument grid lines
        for (int g = 1; g < 4; g++)
        {
            double x = 76.0 * g / 4.0;
            double y = 56.0 * g / 4.0;
            canvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = 56, Stroke = new SolidColorBrush(Color.FromArgb(30, 148, 163, 184)), StrokeThickness = 1 });
            canvas.Children.Add(new Line { X1 = 0, X2 = 76, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(Color.FromArgb(30, 148, 163, 184)), StrokeThickness = 1 });
        }

        if (category == "curve")
        {
            var pts = new PointCollection();
            if (!string.IsNullOrEmpty(shareCode) && StickLabShareCode.TryDecode(shareCode, out var decoded, out _) && decoded?.Curve is { } sc)
            {
                for (int i = 0; i <= 16; i++)
                {
                    double t = i / 16.0;
                    double yNorm = PadProcessor.Response(sc, decoded.Tuning, t);
                    pts.Add(new Point(4 + t * 68, 52 - yNorm * 46));
                }
            }
            else
            {
                pts = [new(4, 52), new(28, 36), new(52, 16), new(72, 6)];
            }
            canvas.Children.Add(new Line { X1 = 4, Y1 = 52, X2 = 72, Y2 = 6, Stroke = new SolidColorBrush(Color.FromArgb(55, 148, 163, 184)), StrokeDashArray = [2, 2], StrokeThickness = 1 });
            canvas.Children.Add(new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)), StrokeThickness = 2.2, Points = pts });
        }
        else if (category == "rgb")
        {
            canvas.Children.Add(new Rectangle
            {
                Width = 60, Height = 10, RadiusX = 3, RadiusY = 3,
                Fill = new LinearGradientBrush(Color.FromRgb(56, 189, 248), Color.FromRgb(255, 122, 24), 0)
            }.Also(r => { Canvas.SetLeft(r, 8); Canvas.SetTop(r, 23); }));
        }
        else if (category == "vibration")
        {
            var wave = new PointCollection { new(6, 28), new(16, 14), new(26, 42), new(36, 10), new(46, 46), new(56, 18), new(70, 28) };
            canvas.Children.Add(new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(74, 222, 128)), StrokeThickness = 2, Points = wave });
        }
        else if (category == "macro")
        {
            for (int k = 0; k < 3; k++)
            {
                var step = new Rectangle { Width = 14, Height = 10, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(Color.FromRgb(96, 165, 250)) };
                Canvas.SetLeft(step, 10 + k * 21);
                Canvas.SetTop(step, k % 2 == 0 ? 18 : 30);
                canvas.Children.Add(step);
            }
        }
        else
        {
            // Full setup controller outline badge
            var label = new TextBlock
            {
                Text = category == "pack" ? "PACK" : "SETUP",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250))
            };
            Canvas.SetLeft(label, 18);
            Canvas.SetTop(label, 20);
            canvas.Children.Add(label);
        }

        return box;
    }

    private static ControlTemplate BuildRectButtonTemplate(double radius)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetValue(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetValue(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetValue(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, new System.Windows.Data.Binding("HorizontalContentAlignment") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(cp);

        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }

    private static ControlTemplate BuildRectTextBoxTemplate(double radius)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetValue(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetValue(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

        var sv = new FrameworkElementFactory(typeof(ScrollViewer));
        sv.Name = "PART_ContentHost";
        sv.SetValue(FrameworkElement.MarginProperty, new Thickness(0));
        border.AppendChild(sv);

        return new ControlTemplate(typeof(TextBox)) { VisualTree = border };
    }
}

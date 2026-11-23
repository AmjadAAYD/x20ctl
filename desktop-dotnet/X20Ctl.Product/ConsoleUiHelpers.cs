using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Fixes WPF Slider track-clicking and single-gesture track-dragging across all tabs
/// so clicking anywhere on a horizontal or vertical slider immediately jumps and tracks smoothly.
/// </summary>
public static class SmoothSliderBehavior
{
    public static readonly DependencyProperty EnableTrackDragProperty =
        DependencyProperty.RegisterAttached(
            "EnableTrackDrag",
            typeof(bool),
            typeof(SmoothSliderBehavior),
            new PropertyMetadata(false, OnEnableTrackDragChanged));

    public static bool GetEnableTrackDrag(DependencyObject obj) => (bool)obj.GetValue(EnableTrackDragProperty);
    public static void SetEnableTrackDrag(DependencyObject obj, bool value) => obj.SetValue(EnableTrackDragProperty, value);

    static readonly DependencyProperty DraggingTrackProperty =
        DependencyProperty.RegisterAttached("DraggingTrack", typeof(bool), typeof(SmoothSliderBehavior), new PropertyMetadata(false));

    static void OnEnableTrackDragChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Slider slider) return;
        slider.PreviewMouseLeftButtonDown -= OnPreviewMouseDown;
        slider.PreviewMouseMove -= OnPreviewMouseMove;
        slider.PreviewMouseLeftButtonUp -= OnPreviewMouseUp;
        slider.LostMouseCapture -= OnLostCapture;

        if ((bool)e.NewValue)
        {
            slider.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
            slider.PreviewMouseMove += OnPreviewMouseMove;
            slider.PreviewMouseLeftButtonUp += OnPreviewMouseUp;
            slider.LostMouseCapture += OnLostCapture;
        }
    }

    static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider || !slider.IsEnabled) return;
        if (FindAncestor<Thumb>(e.OriginalSource as DependencyObject) != null) return;

        UpdateSliderFromPoint(slider, e.GetPosition(slider));
        slider.SetValue(DraggingTrackProperty, true);
        slider.CaptureMouse();
        slider.Focus();
        e.Handled = true;
    }

    static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not Slider slider || !(bool)slider.GetValue(DraggingTrackProperty)) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag(slider);
            return;
        }
        UpdateSliderFromPoint(slider, e.GetPosition(slider));
        e.Handled = true;
    }

    static void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider || !(bool)slider.GetValue(DraggingTrackProperty)) return;
        UpdateSliderFromPoint(slider, e.GetPosition(slider));
        EndDrag(slider);
        slider.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
        {
            RoutedEvent = UIElement.LostMouseCaptureEvent,
        });
        e.Handled = true;
    }

    static void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (sender is Slider slider)
            slider.SetValue(DraggingTrackProperty, false);
    }

    static void EndDrag(Slider slider)
    {
        slider.SetValue(DraggingTrackProperty, false);
        if (slider.IsMouseCaptured)
            slider.ReleaseMouseCapture();
    }

    static void UpdateSliderFromPoint(Slider slider, Point pt)
    {
        double min = slider.Minimum;
        double max = slider.Maximum;
        if (max <= min) return;

        var track = slider.Template?.FindName("PART_Track", slider) as Track;
        FrameworkElement refEl = (FrameworkElement?)track ?? slider;
        Point rel = ReferenceEquals(refEl, slider) ? pt : slider.TranslatePoint(pt, refEl);

        double ratio;
        if (slider.Orientation == Orientation.Vertical)
        {
            double h = Math.Max(1.0, refEl.ActualHeight);
            ratio = Math.Clamp(1.0 - rel.Y / h, 0.0, 1.0);
        }
        else
        {
            double w = Math.Max(1.0, refEl.ActualWidth);
            ratio = Math.Clamp(rel.X / w, 0.0, 1.0);
        }

        double raw = min + ratio * (max - min);
        if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
        {
            double step = slider.TickFrequency;
            raw = min + Math.Round((raw - min) / step) * step;
        }
        double clamped = Math.Clamp(raw, min, max);
        if (Math.Abs(slider.Value - clamped) > 1e-6)
            slider.Value = clamped;
    }

    static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T match) return match;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}

/// <summary>
/// Wraps unified scrollable Studio panels with an animated top scroll-down hint arrow that automatically
/// hides once the user starts scrolling down and reappears when returned to the top.
/// </summary>
internal static class ConsoleUiHelpers
{
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.RegisterAttached(
            "IsSelected",
            typeof(bool),
            typeof(ConsoleUiHelpers),
            new PropertyMetadata(false));

    public static bool GetIsSelected(DependencyObject obj) => (bool)obj.GetValue(IsSelectedProperty);
    public static void SetIsSelected(DependencyObject obj, bool value) => obj.SetValue(IsSelectedProperty, value);

    public static Grid WrapWithScrollHint(ScrollViewer scroll, string label = "Scroll down for more")
    {
        var host = new Grid();
        host.Children.Add(scroll);

        var arrowTranslate = new TranslateTransform();
        var chevron = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 1,1 L 6,5.5 L 11,1 M 1,5.5 L 6,10 L 11,5.5"),
            Stroke = new SolidColorBrush(Color.FromRgb(0x7D, 0xB6, 0xFF)),
            StrokeThickness = 1.7,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = 12,
            Height = 11,
            Margin = new(0, 1, 7, 0),
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = arrowTranslate,
        };

        var textBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xE7, 0xFF));
        textBrush.Freeze();
        var bgBrush = new SolidColorBrush(Color.FromArgb(232, 14, 24, 44));
        bgBrush.Freeze();
        var borderBrush = new SolidColorBrush(Color.FromArgb(180, 90, 162, 255));
        borderBrush.Freeze();

        var hintRow = new StackPanel { Orientation = Orientation.Horizontal };
        hintRow.Children.Add(chevron);
        hintRow.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = DS.Body,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var scrollHintBtn = new Button
        {
            Style = DS.Style("DS.ConsoleBtn"),
            Padding = new(10, 4, 10, 4),
            MinHeight = 26,
            Height = 26,
            Background = bgBrush,
            BorderBrush = borderBrush,
            Content = hintRow,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new(0, 0, 18, 0),
            Opacity = 0,
            IsHitTestVisible = false,
            ToolTip = "Scroll down to see all sections",
        };
        scrollHintBtn.Click += (_, _) =>
        {
            scroll.ScrollToVerticalOffset(Math.Min(scroll.ScrollableHeight, scroll.VerticalOffset + 280));
        };
        host.Children.Add(scrollHintBtn);

        bool animating = false;
        void SyncHint()
        {
            bool show = !ReviewSandbox.Active
                && scroll.IsLoaded
                && scroll.ScrollableHeight > 28
                && scroll.VerticalOffset < 22;

            scrollHintBtn.IsHitTestVisible = show;
            scrollHintBtn.Opacity = show ? 1.0 : 0.0;

            if (show && DS.Motion && !animating)
            {
                animating = true;
                var anim = new DoubleAnimation(0, 3.5, TimeSpan.FromMilliseconds(720))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                };
                arrowTranslate.BeginAnimation(TranslateTransform.YProperty, anim);
            }
            else if (!show && animating)
            {
                animating = false;
                arrowTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                arrowTranslate.Y = 0;
            }
        }

        scroll.ScrollChanged += (_, _) => SyncHint();
        scroll.SizeChanged += (_, _) => SyncHint();
        scroll.Loaded += (_, _) => SyncHint();
        scroll.Unloaded += (_, _) =>
        {
            animating = false;
            arrowTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        };

        return host;
    }
}

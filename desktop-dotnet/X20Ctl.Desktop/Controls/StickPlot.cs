using System.Globalization;
using System.Windows;
using System.Windows.Media;
using X20Ctl.Simulation;

namespace X20Ctl.Desktop.Controls;

public sealed class StickPlot : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(nameof(Frame), typeof(InputFrame), typeof(StickPlot), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public static readonly DependencyProperty RightProperty = DependencyProperty.Register(nameof(Right), typeof(bool), typeof(StickPlot), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public InputFrame? Frame { get => (InputFrame?)GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public bool Right { get => (bool)GetValue(RightProperty); set => SetValue(RightProperty, value); }
    public static readonly DependencyProperty InstrumentProperty = DependencyProperty.Register(nameof(Instrument), typeof(bool), typeof(StickPlot), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool Instrument { get => (bool)GetValue(InstrumentProperty); set => SetValue(InstrumentProperty, value); }
    private readonly Queue<Point> trace = new();
    private string? source;
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var plot = (StickPlot)d;
        if (e.NewValue is not InputFrame frame) return;
        if (!frame.Connected || plot.source != frame.SourceKey) plot.trace.Clear();
        plot.source = frame.SourceKey;
        if (frame.Connected) plot.trace.Enqueue(plot.Point(frame));
        if (plot.trace.Count > 40) plot.trace.Dequeue();
    }
    private Point Point(InputFrame frame) => Right ? new(InputFrame.NormalizeAxis(frame.RX), InputFrame.NormalizeAxis(frame.RY)) : new(frame.LeftX, frame.LeftY);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double radius = Math.Min(ActualWidth, ActualHeight) / 2 - 9;
        if (radius <= 0) return;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var grid = new Pen(new SolidColorBrush(Color.FromArgb(65, 130, 159, 201)), 1);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, 14, 23, 41)), grid, center, radius, radius);
        dc.DrawEllipse(null, grid, center, radius * .5, radius * .5);
        dc.DrawLine(grid, new(center.X - radius, center.Y), new(center.X + radius, center.Y));
        dc.DrawLine(grid, new(center.X, center.Y - radius), new(center.X, center.Y + radius));
        if (Instrument)
        {
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(115, 97, 159, 224)), 1.2), center, radius, radius);
            for (int i = 0; i < 24; i++)
            {
                double angle = i * Math.PI / 12, inner = radius - (i % 6 == 0 ? 6 : 3);
                dc.DrawLine(grid, new(center.X + Math.Cos(angle) * inner, center.Y + Math.Sin(angle) * inner), new(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius));
            }
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(104, 151, 201)), 1.5), new(center.X - 4, center.Y), new(center.X + 4, center.Y));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(104, 151, 201)), 1.5), new(center.X, center.Y - 4), new(center.X, center.Y + 4));
        }
        Point Transform(Point p) => new(center.X + p.X * radius, center.Y - p.Y * radius);
        Point? last = null;
        foreach (var point in trace)
        {
            var next = Transform(point);
            if (last != null) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(100, 86, 202, 245)), 1.5), last.Value, next);
            last = next;
        }
        if (Frame?.Connected == true)
        {
            var current = Transform(Point(Frame));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(45, 119, 218, 255)), null, current, 11, 11);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(126, 215, 255)), null, current, 4, 4);
            if (Instrument) dc.DrawEllipse(Brushes.White, null, current, 1.8, 1.8);
        }
    }
}

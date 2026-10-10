using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace X20Ctl.Product;

/// <summary>
/// The app is designed at one exact resolution, 1600 x 900 (owner direction 10 Oct 2026: "pick a perfect resolution
/// where everything fits"), and this stage scales it uniformly to whatever the window is. The scale follows the
/// narrower fit, so the design never shrinks below 1600 x 900: a 16:10 screen gives it a little more height, an
/// ultrawide a little more width, and nothing is ever cut off or letterboxed.
/// </summary>
public sealed class DesignStage : Decorator
{
    public const double DesignWidth = 1600, DesignHeight = 900;
    public double Scale { get; private set; } = 1;
    public event Action<double>? ScaleChanged;

    private (Size logical, double scale) Fit(Size available)
    {
        if (!double.IsFinite(available.Width) || !double.IsFinite(available.Height) || available.Width <= 0 || available.Height <= 0) return (new(DesignWidth, DesignHeight), 1);
        double scale = available.Width / available.Height >= DesignWidth / DesignHeight ? available.Height / DesignHeight : available.Width / DesignWidth;
        return (new(available.Width / scale, available.Height / scale), scale);
    }
    protected override Size MeasureOverride(Size constraint)
    {
        var (logical, _) = Fit(constraint);
        Child?.Measure(logical);
        return double.IsFinite(constraint.Width) && double.IsFinite(constraint.Height) ? constraint : logical;
    }
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var (logical, scale) = Fit(arrangeSize);
        if (Child != null)
        {
            Child.Arrange(new Rect(logical));
            Child.RenderTransform = new ScaleTransform(scale, scale);
        }
        if (Math.Abs(scale - Scale) > 1e-6) { Scale = scale; ScaleChanged?.Invoke(scale); }
        return arrangeSize;
    }
}

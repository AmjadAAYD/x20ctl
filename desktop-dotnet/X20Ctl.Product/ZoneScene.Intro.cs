using System.Globalization;
using System.Windows;
using System.Windows.Media;
namespace X20Ctl.Product;

/// <summary>What the opening hands over to: the real Controller Zone underneath it. The intro flies its X onto the
/// header's mark, so its last frame and the Zone's first are the same picture.</summary>
public interface IIntroHandoff
{
    /// <summary>The ink box of the header's X mark, in <paramref name="to"/>'s coordinates.</summary>
    Rect? MarkTarget(Visual to);
    /// <summary>The Zone shows, with everything the intro still has to reveal hidden.</summary>
    void HandoffBegin();
    /// <summary>reveal: the interface around the X (0..1); brand: the header mark and word; controller: Player 1's art;
    /// pulse: the header mark's one landing pulse (a scale, 1 at rest).</summary>
    void Handoff(double reveal, double brand, double controller, double pulse);
    /// <summary>Everything back to normal.</summary>
    void HandoffEnd();
}

/// <summary>The Zone's side of the opening's hand-over: where the header's logo and Player 1's controller really are, and
/// the interface growing in around them, header first, then the heading, the four players about 60 ms apart, the
/// footer last.</summary>
public partial class ZoneScene : IIntroHandoff
{
    // the header mark's ink inside its 26 px image (x20ctl-mark.png's alpha box, 10,85 - 502,427 of 512)
    private static readonly Rect MarkInk = new(10 / 512d * 26, 85 / 512d * 26, 492 / 512d * 26, 342 / 512d * 26);
    private readonly ScaleTransform brandPulse = new(1, 1);

    public Rect? MarkTarget(Visual to) => Within(BrandMark, MarkInk, to);
    public Rect? WordTarget(Visual to)
    {
        // "20CTL" inside "X20CTL": after the width of the X
        var face = new Typeface(BrandWord.FontFamily, BrandWord.FontStyle, BrandWord.FontWeight, BrandWord.FontStretch);
        double Width(string text) => new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, BrandWord.FontSize, Brushes.White, 1).WidthIncludingTrailingWhitespace;
        double x = Width("X"); return Within(BrandWord, new Rect(x, 0, Width("X20CTL") - x, BrandWord.ActualHeight), to);
    }
    public Rect? ControllerTarget(Visual to)
    {
        if (docks.Length == 0) return null; var dock = docks[0];
        if (dock.Illustration.Visibility == Visibility.Visible) return Within(dock.ImageClip, new Rect(22, 50, 1488, 940), to);
        var bounds = dock.SilhouettePath.RenderedGeometry.Bounds; return bounds.IsEmpty ? null : Within(dock.SilhouettePath, bounds, to);
    }
    private static Rect? Within(FrameworkElement from, Rect box, Visual to) =>
        from.IsLoaded && from.ActualWidth > 0 && PresentationSource.FromVisual(from) != null ? from.TransformToVisual(to).TransformBounds(box) : null;

    private IEnumerable<UIElement> Revealed => new UIElement[] { HeaderActions, BrandRule, BrandLabel, BrandTile, HeadingRow, FooterBar }.Concat(docks);
    public void HandoffBegin()
    {
        foreach (var e in Revealed) { e.BeginAnimation(OpacityProperty, null); e.Opacity = 0; }
        BrandMark.Opacity = BrandWord.Opacity = 0; BrandMark.RenderTransformOrigin = new(.5, .5); BrandMark.RenderTransform = brandPulse;
        if (docks.Length > 0) docks[0].Art.Opacity = 0;
        BeginAnimation(OpacityProperty, null); Opacity = 1; Workspace.BeginAnimation(OpacityProperty, null); Workspace.Opacity = 1;
    }
    public void Handoff(double reveal, double brand, double controller, double pulse)
    {
        static double In(double t, double a, double b) { double x = Math.Clamp((t - a) / (b - a), 0, 1); return x * x * (3 - 2 * x); }
        HeaderActions.Opacity = BrandRule.Opacity = BrandLabel.Opacity = In(reveal, .15, .55);
        BrandTile.Opacity = In(reveal, .35, .7); HeadingRow.Opacity = In(reveal, .25, .65); FooterBar.Opacity = In(reveal, .45, .85);
        for (int i = 0; i < docks.Length; i++) docks[i].Opacity = In(reveal, .3 + .06 * i, .6 + .06 * i);
        BrandMark.Opacity = BrandWord.Opacity = brand; brandPulse.ScaleX = brandPulse.ScaleY = pulse;
        if (docks.Length > 0) docks[0].Art.Opacity = controller;
    }
    public void HandoffEnd()
    {
        foreach (var e in Revealed) e.Opacity = 1;
        BrandMark.Opacity = BrandWord.Opacity = 1; BrandMark.RenderTransform = null;
        if (docks.Length > 0) docks[0].Art.Opacity = 1;
    }
}

using System.Windows;
using System.Windows.Media;
namespace X20Ctl.Product;

public partial class ZoneScene
{
    private Rect? RebelMarkTarget(Visual to)
    {
        if (ThemeCatalog.Current.Intro is not { } spec) return Within(BrandMark, MarkInk, to);
        double scale = Math.Min(BrandMark.ActualWidth / spec.Image.Width, BrandMark.ActualHeight / spec.Image.Height);
        var ink = new Rect((BrandMark.ActualWidth - spec.Image.Width * scale) / 2 + spec.Ink.X * scale,
            (BrandMark.ActualHeight - spec.Image.Height * scale) / 2 + spec.Ink.Y * scale,
            spec.Ink.Width * scale, spec.Ink.Height * scale);
        return Within(BrandMark, ink, to);
    }

    private void RebelHandoffBegin()
    {
        foreach (UIElement element in new UIElement?[] { zoneTitle, caption, hintsOverview, hintsHero, statusPill, addCard, HeaderActions, UpdateNotice }.OfType<UIElement>())
        { element.BeginAnimation(OpacityProperty, null); element.Opacity = 0; }
        foreach (var dock in docks) dock.Opacity = 0;
        BrandMark.Opacity = 0; BrandMark.RenderTransformOrigin = new(.5, .5); BrandMark.RenderTransform = brandPulse;
        BeginAnimation(OpacityProperty, null); Opacity = 1;
    }

    private void RebelHandoff(double reveal, double brand, double controller, double pulse)
    {
        static double In(double x, double start, double end) { double t = Math.Clamp((x - start) / (end - start), 0, 1); return t * t * (3 - 2 * t); }
        foreach (UIElement element in new UIElement?[] { zoneTitle, caption, hintsOverview, hintsHero, statusPill, addCard, HeaderActions, UpdateNotice }.OfType<UIElement>())
            element.Opacity = In(reveal, .15, .8);
        for (int i = 0; i < docks.Length; i++) docks[i].Opacity = In(reveal, .25 + i * .06, .55 + i * .06);
        BrandTile.Opacity = 1; BrandMark.Opacity = brand; brandPulse.ScaleX = brandPulse.ScaleY = pulse;
        if (docks.Length > 0) docks[0].Art.Opacity = controller;
    }
}

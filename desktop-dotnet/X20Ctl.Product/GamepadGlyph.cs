using System.Windows;
using System.Windows.Media;

namespace X20Ctl.Product;

/// <summary>The generic gamepad outline used where no model photo belongs: the empty player cards and the identity scan.
/// Traced from the owner's reference glyph (14 Oct 2026): the centre line of its tube, 485 x 306.</summary>
public static class GamepadGlyph
{
    private const string Data = "M193.9,1.0 L184.1,4.6 L172.8,9.5 L159.4,11.3 L146.3,11.2 L133.6,11.1 L121.4,11.3 L110.2,12.8 L99.9,16.4 L90.4,22.4 L81.6,30.3 L73.5,39.2 L66.5,48.8 L60.3,58.9 L54.7,69.3 L49.3,79.9 L44.2,90.4 L39.2,101.1 L34.3,111.8 L29.5,122.6 L24.9,133.5 L20.7,144.6 L16.8,156.1 L13.4,167.4 L10.4,178.9 L7.5,190.4 L5.0,201.9 L2.9,213.9 L1.0,226.2 L0.0,238.2 L0.3,249.9 L2.0,261.3 L5.2,272.0 L10.2,282.1 L17.1,291.0 L25.9,298.5 L35.8,303.7 L46.4,306.3 L57.2,306.0 L67.2,302.6 L76.2,296.6 L84.3,288.4 L92.2,279.0 L99.8,269.2 L107.3,259.3 L114.4,249.5 L121.5,239.6 L129.5,229.8 L139.4,221.4 L151.8,216.4 L165.3,214.7 L178.2,214.5 L190.9,214.5 L203.5,214.5 L216.2,214.5 L229.0,214.5 L242.4,214.5 L255.6,214.5 L268.4,214.5 L281.0,214.5 L293.7,214.5 L306.4,214.5 L319.4,214.6 L332.5,215.9 L344.8,220.4 L355.3,228.7 L363.6,238.6 L370.8,248.6 L377.9,258.4 L385.2,268.0 L392.7,277.8 L400.4,287.3 L408.6,295.7 L417.8,302.3 L427.9,305.8 L438.8,306.4 L449.6,304.0 L459.3,299.2 L467.9,292.2 L474.9,283.5 L480.0,273.6 L483.4,262.8 L485.2,251.6 L485.5,239.7 L484.9,227.4 L483.4,214.9 L481.2,203.0 L478.7,191.6 L475.8,180.2 L472.8,168.8 L469.4,157.3 L465.6,145.9 L461.6,135.0 L457.1,124.1 L452.4,113.4 L447.4,102.6 L442.3,91.7 L437.2,81.0 L431.8,70.5 L426.3,60.3 L420.2,50.2 L413.3,40.5 L405.3,31.3 L396.7,23.3 L387.3,17.1 L377.0,13.2 L365.8,11.4 L353.7,11.0 L341.1,11.2 L328.4,11.3 L315.0,9.8 L303.3,5.2 L293.6,1.3 L282.3,0.1 L269.9,0.0 L257.2,0.0 L244.5,0.0 L231.4,0.0 L218.0,0.0 L205.2,0.1 Z";

    /// <summary>The outline moved to the origin, frozen.</summary>
    public static readonly Geometry Outline;
    /// <summary>Distance round the outline, for dash animations.</summary>
    public static readonly double Length;
    public static readonly Size Size;
    /// <summary>Space to leave round the outline inside a Viewbox so its rim and glow are not clipped.</summary>
    public const double Pad = 26;

    static GamepadGlyph()
    {
        var raw = Geometry.Parse(Data);
        double length = 0;
        foreach (var figure in raw.GetFlattenedPathGeometry().Figures)
        {
            var from = figure.StartPoint;
            foreach (var segment in figure.Segments)
                if (segment is PolyLineSegment poly) foreach (var p in poly.Points) { length += (p - from).Length; from = p; }
            length += (figure.StartPoint - from).Length;
        }
        var bounds = raw.Bounds;
        var g = raw.Clone(); g.Transform = new TranslateTransform(-bounds.X, -bounds.Y); g.Freeze();
        Outline = g; Length = length; Size = bounds.Size;
    }
}

using System.Windows;
using System.Windows.Media;
namespace X20Ctl.Product;

/// <summary>How the white outline is drawn, shared with the sound (tools/build_intro_sound.py, OUTLINE): one smooth
/// stroke from 1.45 s to 2.45 s, slow to start, quick through the middle, easing as it closes. The plucks are placed where
/// the line passes each eighth of the controller, so the music follows the drawing rather than the drawing a beat (owner
/// 17 Oct 2026: the line drawn on a beat felt forced).</summary>
internal static class IntroBeats
{
    public const double OutlineFrom=1.45, OutlineFor=1.0;
    /// <summary>How much of the outline is drawn at time t, 0 to 1.</summary>
    public static double Drawn(double t){double x=Math.Clamp((t-OutlineFrom)/OutlineFor,0,1);return x*x*(3-2*x);}
}

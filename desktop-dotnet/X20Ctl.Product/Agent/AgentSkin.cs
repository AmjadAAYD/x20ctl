using System.Windows.Media;
namespace X20Ctl.Product.Agent;

/// <summary>
/// The agent dresses as the controller it lives in (owner request, 13 Oct 2026: "if I have chosen the X20, it chooses the
/// X20 white design"). Colours are read off each model's own photo: the shell from top to bottom, the grips, the stick
/// caps, the ink for words printed on the shell, and the lights the real pad has (side strips, rings round the sticks).
/// The screen stays dark on every model: it's a display.
/// </summary>
public sealed record AgentSkin(
    Color Top, Color Middle, Color Bottom, Color GripTop, Color GripBottom, Color Stroke,
    Color CapLight, Color CapDark, Color Edge, Color Ink,
    Color[]? Strips = null, Color[]? Rings = null)
{
    static Color C(byte r, byte g, byte b, byte a = 255) => Color.FromArgb(a, r, g, b);
    static readonly Color Blue = C(60, 140, 255), Magenta = C(230, 70, 200), Orange = C(255, 140, 50), Cyan = C(80, 200, 255), Pink = C(240, 90, 200), Violet = C(150, 100, 255);
    static readonly Color DarkCapLight = C(58, 60, 68), DarkCapDark = C(22, 23, 28);

    /// <summary>The navy shell, for a model without its own look.</summary>
    public static readonly AgentSkin Default = new(C(30, 39, 60), C(19, 25, 41), C(12, 16, 28), C(19, 25, 41), C(12, 16, 28), C(120, 150, 210, 170),
        C(46, 58, 84), C(20, 27, 44), C(90, 112, 156, 150), C(150, 165, 195));

    public static AgentSkin For(string modelId) => modelId switch
    {
        // white shell, grey fabric grips, RGB rings round the sticks, blue and pink side strips
        "x20" => new(C(246, 247, 250), C(228, 230, 236), C(204, 208, 216), C(118, 120, 128), C(76, 78, 86), C(255, 255, 255, 200),
            DarkCapLight, DarkCapDark, C(150, 155, 170, 160), C(60, 66, 80), [Blue, Magenta], [Magenta, Blue]),
        // smoky see-through grey, black grips, silver rims, cyan and pink strips
        "x20_pro" => new(C(98, 98, 108), C(70, 70, 78), C(44, 44, 50), C(30, 30, 34), C(18, 18, 22), C(180, 180, 195, 150),
            C(62, 62, 70), C(24, 24, 28), C(150, 150, 165, 150), C(205, 208, 218), [Cyan, Pink], [C(205, 205, 215), C(120, 120, 130)]),
        // all white with an orange badge, rings round the sticks, blue and orange glow
        "x05" => new(C(250, 250, 252), C(236, 237, 241), C(214, 216, 222), C(226, 228, 233), C(196, 199, 207), C(255, 255, 255, 210),
            C(200, 203, 211), C(150, 154, 165), C(160, 165, 178, 170), C(70, 74, 88), [Blue, Orange], [Magenta, Blue]),
        // black, blue and orange glow
        "x05_pro" => new(C(44, 45, 52), C(28, 29, 34), C(16, 16, 20), C(24, 24, 28), C(12, 12, 15), C(130, 130, 150, 140),
            C(56, 57, 66), C(20, 20, 24), C(120, 120, 140, 150), C(200, 203, 214), [Blue, Orange]),
        // silver-blue metal, navy grips
        "x10" => new(C(198, 207, 218), C(170, 180, 194), C(140, 150, 166), C(64, 64, 96), C(40, 40, 66), C(240, 245, 255, 190),
            C(78, 82, 100), C(34, 36, 48), C(110, 118, 140, 170), C(40, 46, 64)),
        // black, pink and violet strips
        "d10" => new(C(58, 58, 64), C(36, 36, 41), C(20, 20, 24), C(32, 32, 37), C(18, 18, 22), C(150, 150, 165, 130),
            C(60, 60, 68), C(22, 22, 26), C(130, 130, 145, 150), C(205, 205, 215), [Pink, Violet]),
        // dark grey, lilac grips
        "x15" => new(C(92, 92, 95), C(72, 72, 75), C(52, 52, 55), C(196, 176, 198), C(150, 130, 158), C(190, 190, 195, 140),
            C(64, 64, 68), C(26, 26, 29), C(140, 140, 150, 150), C(222, 222, 228)),
        // gold, dark grips, violet and blue under the grips
        "dune" => new(C(248, 212, 104), C(214, 166, 52), C(160, 112, 26), C(52, 50, 60), C(28, 27, 34), C(255, 236, 170, 210),
            C(60, 58, 66), C(24, 23, 28), C(170, 140, 70, 170), C(70, 50, 12), [Violet, Blue]),
        _ => Default,
    };

    public Brush Shell => new LinearGradientBrush(new GradientStopCollection { new(Top, 0), new(Middle, .5), new(Bottom, 1) }, 90);
    public Brush Grip => new LinearGradientBrush(GripTop, GripBottom, 90);
    public bool Light => Top.R + Top.G + Top.B > 450;
}

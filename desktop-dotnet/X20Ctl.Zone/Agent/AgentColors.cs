using System.Globalization;
using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;

/// <summary>A colour the owner described, as the RGB the lights get and the words it was read from.</summary>
public sealed record AgentColor(byte R, byte G, byte B, string Name)
{
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    public override string ToString() => $"{Name} ({Hex})";
}

/// <summary>
/// Reads colours the way people say them (owner, 8 Nov 2026: "blueish yellow and etc, it must understand the colour
/// variation"): names, two names blended ("blue green", "teal-purple"), a tint ("blueish yellow", "reddish orange",
/// "greeny blue"), shades ("light", "pale", "pastel", "dark", "deep", "bright", "neon", "soft", "muted", "warm", "cool",
/// "very …"), and #hex or rgb(…). Deterministic: the app decides the RGB, never the model.
/// </summary>
public static class AgentColors
{
    static readonly Dictionary<string, (byte R, byte G, byte B)> Names = new()
    {
        ["red"] = (255, 0, 0), ["crimson"] = (220, 20, 60), ["scarlet"] = (255, 36, 0), ["maroon"] = (128, 0, 0), ["burgundy"] = (128, 0, 32), ["ruby"] = (224, 17, 95), ["cherry"] = (222, 49, 99),
        ["orange"] = (255, 120, 0), ["amber"] = (255, 191, 0), ["tangerine"] = (255, 140, 30), ["coral"] = (255, 127, 80), ["salmon"] = (250, 128, 114), ["peach"] = (255, 190, 150), ["apricot"] = (251, 206, 177),
        ["yellow"] = (255, 230, 0), ["gold"] = (255, 200, 0), ["golden"] = (255, 200, 0), ["lemon"] = (255, 247, 0), ["mustard"] = (225, 173, 1), ["cream"] = (255, 253, 208), ["beige"] = (245, 225, 190),
        ["green"] = (0, 255, 0), ["lime"] = (170, 255, 0), ["mint"] = (152, 255, 152), ["emerald"] = (0, 200, 110), ["forest"] = (34, 139, 34), ["olive"] = (128, 128, 0), ["jade"] = (0, 168, 107), ["sage"] = (178, 198, 150),
        ["teal"] = (0, 160, 160), ["turquoise"] = (64, 224, 208), ["cyan"] = (0, 255, 255), ["aqua"] = (0, 255, 255), ["aquamarine"] = (127, 255, 212),
        ["blue"] = (0, 80, 255), ["navy"] = (0, 0, 128), ["sky"] = (110, 190, 255), ["azure"] = (0, 127, 255), ["cobalt"] = (0, 71, 171), ["royal"] = (65, 105, 225), ["sapphire"] = (15, 82, 186), ["ice"] = (190, 235, 255), ["indigo"] = (75, 0, 130),
        ["purple"] = (150, 0, 255), ["violet"] = (143, 0, 255), ["lavender"] = (200, 170, 255), ["lilac"] = (200, 162, 200), ["plum"] = (142, 69, 133), ["magenta"] = (255, 0, 255), ["fuchsia"] = (255, 0, 200),
        ["pink"] = (255, 105, 180), ["rose"] = (255, 0, 127), ["blush"] = (255, 170, 190), ["hot pink"] = (255, 20, 147),
        ["brown"] = (139, 69, 19), ["chocolate"] = (123, 63, 0), ["tan"] = (210, 180, 140), ["copper"] = (184, 115, 51), ["bronze"] = (205, 127, 50),
        ["white"] = (255, 255, 255), ["silver"] = (192, 192, 192), ["grey"] = (128, 128, 128), ["gray"] = (128, 128, 128), ["black"] = (0, 0, 0),
    };
    /// <summary>"-ish" and "-y" forms that tint the colour they stand before, rather than mix in half.</summary>
    static readonly Dictionary<string, string> Tints = new()
    {
        ["reddish"] = "red", ["redish"] = "red", ["orangish"] = "orange", ["orangey"] = "orange", ["orangy"] = "orange", ["yellowish"] = "yellow", ["yellowy"] = "yellow",
        ["greenish"] = "green", ["greeny"] = "green", ["blueish"] = "blue", ["bluish"] = "blue", ["bluey"] = "blue", ["purplish"] = "purple", ["purpleish"] = "purple", ["purply"] = "purple",
        ["pinkish"] = "pink", ["pinky"] = "pink", ["brownish"] = "brown", ["whitish"] = "white", ["greyish"] = "grey", ["grayish"] = "grey", ["tealish"] = "teal", ["cyanish"] = "cyan", ["violetish"] = "violet", ["goldish"] = "gold",
    };
    static readonly HashSet<string> Filler = ["the", "a", "an", "my", "colour", "color", "colours", "colors", "shade", "of", "kind", "sort", "like", "please", "some", "to", "into", "and", "with", "bit", "little", "slightly", "tone", "hue", "ish"];

    public static bool TryParse(string? text, out AgentColor color)
    {
        color = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string raw = text.Trim().ToLowerInvariant().Replace('’', '\'').Trim('"', '\'', '.', '!', '?');
        if (Regex.Match(raw, @"#?\b([0-9a-f]{6})\b") is { Success: true } hex && (raw.StartsWith('#') || !Regex.IsMatch(raw, "[g-z]")))
        {
            int v = int.Parse(hex.Groups[1].Value, NumberStyles.HexNumber);
            color = new((byte)(v >> 16), (byte)(v >> 8), (byte)v, "#" + hex.Groups[1].Value.ToUpperInvariant()); return true;
        }
        if (Regex.Match(raw, @"rgb\s*\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)") is { Success: true } rgb)
        {
            var n = Enumerable.Range(1, 3).Select(i => int.Parse(rgb.Groups[i].Value, CultureInfo.InvariantCulture)).ToArray();
            if (n.Any(x => x > 255)) return false;
            color = new((byte)n[0], (byte)n[1], (byte)n[2], $"rgb({n[0]}, {n[1]}, {n[2]})"); return true;
        }
        string s = Regex.Replace(raw, @"[^a-z\s-]", " ").Replace("hot pink", "hot_pink").Replace("light blue", "sky");
        var words = Regex.Split(s, @"[\s-]+").Where(w => w.Length > 0).Select(w => w == "hot_pink" ? "hot pink" : w).ToList();
        var main = new List<(double R, double G, double B)>(); var tints = new List<(double R, double G, double B)>(); var said = new List<string>();
        double light = 0, dark = 0, desat = 0, vivid = 0, warm = 0, cool = 0, very = 1;
        foreach (var w in words)
        {
            if (Filler.Contains(w)) continue;
            switch (w)
            {
                case "very" or "really" or "super" or "extra": very = 1.6; said.Add(w); continue;
                case "light" or "lighter" or "pale" or "pastel" or "baby" or "soft": light += (w is "pastel" or "baby" ? .5 : w == "soft" ? .25 : .4) * very; if (w == "soft") desat += .2; very = 1; said.Add(w); continue;
                case "dark" or "darker" or "deep" or "dim" or "night": dark += (w == "deep" ? .3 : .45) * very; very = 1; said.Add(w); continue;
                case "bright" or "vivid" or "neon" or "electric" or "intense" or "strong" or "rich": vivid += .5 * very; very = 1; said.Add(w); continue;
                case "muted" or "dull" or "dusty" or "washed": desat += .45 * very; very = 1; said.Add(w); continue;
                case "warm" or "warmer": warm += .18 * very; very = 1; said.Add(w); continue;
                case "cool" or "cold" or "cooler": cool += .18 * very; very = 1; said.Add(w); continue;
            }
            if (Tints.TryGetValue(w, out var tint)) { var c = Names[tint]; tints.Add((c.R, c.G, c.B)); said.Add(w); continue; }
            if (Names.TryGetValue(w, out var named)) { main.Add((named.R, named.G, named.B)); said.Add(w); continue; }
            if (w.EndsWith("ish") && Names.TryGetValue(w[..^3], out var stem)) { tints.Add((stem.R, stem.G, stem.B)); said.Add(w); continue; }
            return false; // a word that isn't a colour: not guessed
        }
        if (main.Count == 0 && tints.Count == 0) return false;
        if (main.Count == 0) { main.AddRange(tints); tints.Clear(); }
        var (r, g, b) = (main.Average(c => c.R), main.Average(c => c.G), main.Average(c => c.B));
        foreach (var t in tints) (r, g, b) = Mix((r, g, b), t, .35);
        if (warm > 0) (r, g, b) = Mix((r, g, b), (255, 140, 0), warm);
        if (cool > 0) (r, g, b) = Mix((r, g, b), (0, 120, 255), cool);
        if (desat > 0) { double grey = (r + g + b) / 3; (r, g, b) = Mix((r, g, b), (grey, grey, grey), Math.Min(desat, .8)); }
        if (vivid > 0)
        {
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            if (max > 0) { double k = 255 / max; (r, g, b) = (r * k, g * k, b * k); min *= k; }
            (r, g, b) = (Stretch(r, min, vivid), Stretch(g, min, vivid), Stretch(b, min, vivid));
        }
        if (light > 0) (r, g, b) = Mix((r, g, b), (255, 255, 255), Math.Min(light, .8));
        if (dark > 0) { double k = 1 - Math.Min(dark, .8); (r, g, b) = (r * k, g * k, b * k); }
        color = new(B(r), B(g), B(b), string.Join(" ", said)); return true;
    }

    static (double, double, double) Mix((double R, double G, double B) a, (double R, double G, double B) b, double t) => (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    static double Stretch(double v, double min, double amount) => Math.Max(0, v - min * Math.Min(amount, 1));
    static byte B(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);

    /// <summary>"Change my leds / rgb / lights to X", "make the lights X", "rgb X", "set led colour X": the colour words, or null.</summary>
    public static string? Asked(string sentence)
    {
        string s = sentence.Trim().ToLowerInvariant().Replace('’', '\'');
        if (!Regex.IsMatch(s, @"\b(leds?|lights?|lighting|rgb|glow|backlight|light ?bar|light ?ring)\b")) return Pad(s);
        var m = Regex.Match(s, @"\b(?:to|into|as|=|:|be|is|in)\s+(?<c>[^.!?]+?)\s*(?:please|pls|thanks)?[.!?]*$");
        if (!m.Success) m = Regex.Match(s, @"\b(?:leds?|lights?|lighting|rgb|glow|backlight|light ?bar|light ?ring)(?:'s)?\s+(?:colou?r\s+)?(?<c>[^.!?]+?)\s*[.!?]*$");
        if (!m.Success) m = Regex.Match(s, @"^(?:please\s+)?(?:make|turn|set|change|paint|switch)\s+(?:my\s+|the\s+)?(?:leds?|lights?|lighting|rgb)\s+(?<c>[^.!?]+?)\s*[.!?]*$");
        if (!m.Success) return null;
        string phrase = Regex.Replace(m.Groups["c"].Value, @"\b(colou?r|please|pls|now)\b", " ").Trim();
        return TryParse(phrase, out _) ? phrase : null;
    }

    /// <summary>"Change my controller to green", "make my pad red", "paint the controller purple": a colour asked of the
    /// controller as a whole. Its shell can't change colour, so it's the lights (unless the colour is one the shell is
    /// sold in: the app asks which). The colour words, or null.</summary>
    public static string? Pad(string sentence)
    {
        string s = sentence.Trim().ToLowerInvariant().Replace('’', '\'');
        var m = Regex.Match(s, @"^(?:please\s+|can you\s+|could you\s+|i want (?:you )?to\s+)*(?:make|turn|set|change|paint|switch|colou?r)\s+(?:my\s+|the\s+|this\s+)?(?:controller|pad|gamepad|x20(?: ?pro)?)(?:'s)?(?:\s+colou?r)?\s+(?:to\s+|into\s+|in\s+|be\s+)?(?<c>[^.!?]+?)\s*(?:please|pls|thanks)?[.!?]*$");
        if (!m.Success) return null;
        string phrase = Regex.Replace(m.Groups["c"].Value, @"\b(colou?r|please|pls|now)\b", " ").Trim();
        return phrase.Length > 0 && TryParse(phrase, out _) ? phrase : null;
    }
}

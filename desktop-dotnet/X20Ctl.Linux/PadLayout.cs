using System.Text.Json;
namespace X20Ctl.Linux;

/// <summary>An axis as the kernel reports it: its range, and the value it rested at when the pad was opened.</summary>
public sealed record AbsInfo(int Min, int Max, int Rest);

/// <summary>What a pad says it has: the evdev key codes and the axes with their ranges. Nothing here touches a device, so every decision below can be tested.</summary>
public sealed record PadCaps(IReadOnlySet<int> Keys, IReadOnlyDictionary<int, AbsInfo> Axes);

/// <summary>One physical axis feeding one logical input. <see cref="Invert"/> flips it: a stick that reports up as a low number, or a trigger that rests high.</summary>
public sealed class AxisUse
{
    public int Code { get; set; }
    public bool Invert { get; set; }
    public AxisUse() { }
    public AxisUse(int code, bool invert) { Code = code; Invert = invert; }
}

/// <summary>Where each Xbox input comes from on a particular pad: which key code is A, which axis is the left stick's X, and so on. PC mode reads every pad through one of these,
/// so it is the single place that has to be right for a pad to work.</summary>
public sealed class PadLayout
{
    /// <summary>"standard" (the layout the kernel's Xbox and gamepad drivers use), "guessed" (worked out from what the pad reports; not yet checked by its owner) or "learned" (taught by the button check).</summary>
    public string Source { get; set; } = "standard";
    /// <summary>evdev key code to the app's button name (A, B, X, Y, LB, RB, LT, RT, SELECT, START, HOME, L3, R3, DPAD_*). LT and RT here are digital triggers.</summary>
    public Dictionary<int, string> Buttons { get; set; } = [];
    public AxisUse? LX { get; set; }
    public AxisUse? LY { get; set; }
    public AxisUse? RX { get; set; }
    public AxisUse? RY { get; set; }
    public AxisUse? LT { get; set; }
    public AxisUse? RT { get; set; }
    /// <summary>D-pad on two axes (-1, 0, 1), when it isn't four buttons.</summary>
    public AxisUse? HatX { get; set; }
    public AxisUse? HatY { get; set; }

    public IEnumerable<int> AxisCodes => new[] { LX, LY, RX, RY, LT, RT, HatX, HatY }.Where(a => a != null).Select(a => a!.Code).Distinct();
    public bool HasSticks => LX != null && LY != null && RX != null && RY != null;
    public bool HasTriggers => (LT != null || Buttons.ContainsValue("LT")) && (RT != null || Buttons.ContainsValue("RT"));
    public bool HasDpad => HatX != null && HatY != null || new[] { "DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT" }.All(Buttons.ContainsValue);
}

public static class PadLayouts
{
    public const int AbsX = 0, AbsY = 1, AbsZ = 2, AbsRx = 3, AbsRy = 4, AbsRz = 5, AbsGas = 9, AbsBrake = 10, AbsHat0X = 0x10, AbsHat0Y = 0x11;

    /// <summary>The Linux gamepad layout (xpad and the other kernel gamepad drivers): position-based codes, X and Y as the kernel names them.</summary>
    public static readonly IReadOnlyDictionary<int, string> StandardKeys = new Dictionary<int, string>
    {
        [0x130] = "A", [0x131] = "B", [0x133] = "X", [0x134] = "Y", [0x136] = "LB", [0x137] = "RB", [0x13A] = "SELECT", [0x13B] = "START", [0x13C] = "HOME",
        [0x13D] = "L3", [0x13E] = "R3", [0x138] = "LT", [0x139] = "RT", [0x220] = "DPAD_UP", [0x221] = "DPAD_DOWN", [0x222] = "DPAD_LEFT", [0x223] = "DPAD_RIGHT"
    };
    static readonly int[] CoreKeys = [0x130, 0x131, 0x133, 0x134, 0x136, 0x137];
    static readonly string[] Order = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "SELECT", "START", "L3", "R3", "HOME"];

    /// <summary>Is this a game controller at all? Axes and several gamepad or joystick buttons, and none of the marks of a touchpad, tablet or mouse.</summary>
    public static bool LooksLikeGamepad(PadCaps caps)
    {
        if (caps.Axes.Count < 2) return false;
        if (caps.Keys.Contains(0x14A) || caps.Keys.Contains(0x140) || caps.Keys.Contains(0x145)) return false; // touch, pen, finger: a touchpad or tablet
        int pad = caps.Keys.Count(k => k is >= 0x120 and <= 0x13E or >= 0x2C0 and <= 0x2CF or >= 0x220 and <= 0x223);
        return pad >= 4;
    }

    static bool Centred(AbsInfo a) => a.Max > a.Min && Math.Abs(a.Rest - (a.Min + a.Max) / 2.0) <= (a.Max - a.Min) * .2;
    static bool AtLow(AbsInfo a) => a.Max > a.Min && a.Rest - a.Min <= (a.Max - a.Min) * .15;
    static bool AtHigh(AbsInfo a) => a.Max > a.Min && a.Max - a.Rest <= (a.Max - a.Min) * .15;

    /// <summary>Works out a layout from what the pad reports. A pad in the standard layout comes out "standard"; anything the rules had to guess at comes out "guessed".</summary>
    public static PadLayout Guess(PadCaps caps)
    {
        var l = new PadLayout();
        bool standardKeys = CoreKeys.All(caps.Keys.Contains);
        var axes = caps.Axes;
        bool Has(int c) => axes.ContainsKey(c);

        // sticks: X/Y are the left stick; the right stick is RX/RY, or Z/RZ on pads that use those for it
        if (Has(AbsX) && Has(AbsY)) { l.LX = new(AbsX, false); l.LY = new(AbsY, true); }
        if (Has(AbsRx) && Has(AbsRy) && Centred(axes[AbsRx]) && Centred(axes[AbsRy])) { l.RX = new(AbsRx, false); l.RY = new(AbsRy, true); }
        else if (Has(AbsZ) && Has(AbsRz) && Centred(axes[AbsZ]) && Centred(axes[AbsRz])) { l.RX = new(AbsZ, false); l.RY = new(AbsRz, true); }

        // triggers: an axis that rests at one end. Z/RZ, or Brake/Gas, whichever the right stick isn't using
        bool Trigger(int lt, int rt)
        {
            if (!Has(lt) || !Has(rt) || l.RX?.Code == lt || l.RX?.Code == rt) return false;
            if (AtLow(axes[lt]) && AtLow(axes[rt])) { l.LT = new(lt, false); l.RT = new(rt, false); return true; }
            if (AtHigh(axes[lt]) && AtHigh(axes[rt])) { l.LT = new(lt, true); l.RT = new(rt, true); return true; }
            return false;
        }
        if (!Trigger(AbsZ, AbsRz)) Trigger(AbsBrake, AbsGas);

        // the d-pad: two hat axes, else four buttons
        if (Has(AbsHat0X) && Has(AbsHat0Y)) { l.HatX = new(AbsHat0X, false); l.HatY = new(AbsHat0Y, false); }

        // buttons
        if (standardKeys)
        {
            foreach (var (code, name) in StandardKeys) if (caps.Keys.Contains(code)) l.Buttons[code] = name;
        }
        else
        {
            var names = Order.Where(n => !(n == "LT" && l.LT != null) && !(n == "RT" && l.RT != null)).ToList();
            var codes = caps.Keys.Where(k => k is >= 0x120 and <= 0x13E or >= 0x2C0 and <= 0x2CF).OrderBy(k => k).ToList();
            for (int i = 0; i < codes.Count && i < names.Count; i++) l.Buttons[codes[i]] = names[i];
        }
        // a hat-less pad with d-pad keys
        if (l.HatX == null) foreach (var (code, name) in StandardKeys) if (name.StartsWith("DPAD_") && caps.Keys.Contains(code)) l.Buttons[code] = name;
        // digital trigger keys the standard table carries only count when there's no analog trigger
        if (l.LT != null) foreach (var c in l.Buttons.Where(b => b.Value == "LT").Select(b => b.Key).ToList()) l.Buttons.Remove(c);
        if (l.RT != null) foreach (var c in l.Buttons.Where(b => b.Value == "RT").Select(b => b.Key).ToList()) l.Buttons.Remove(c);

        l.Source = standardKeys && l.HasSticks && l.HasTriggers && l.HasDpad && l.RX?.Code == AbsRx ? "standard" : "guessed";
        return l;
    }

    // ---- learning from the button check -----------------------------------------------------------------------

    /// <summary>The inputs the button check asks for, in order.</summary>
    public static readonly (string Id, string Prompt)[] Steps =
    [
        ("A", "Press A"), ("B", "Press B"), ("X", "Press X"), ("Y", "Press Y"), ("LB", "Press the left bumper (LB)"), ("RB", "Press the right bumper (RB)"),
        ("LT", "Squeeze the left trigger (LT) all the way"), ("RT", "Squeeze the right trigger (RT) all the way"),
        ("SELECT", "Press View (Select, Back)"), ("START", "Press Menu (Start)"), ("HOME", "Press the Home or Guide button"), ("L3", "Click the left stick in"), ("R3", "Click the right stick in"),
        ("DPAD_UP", "Press D-pad up"), ("DPAD_DOWN", "Press D-pad down"), ("DPAD_LEFT", "Press D-pad left"), ("DPAD_RIGHT", "Press D-pad right"),
        ("LS_UP", "Push the left stick up and hold it"), ("LS_RIGHT", "Push the left stick right and hold it"), ("RS_UP", "Push the right stick up and hold it"), ("RS_RIGHT", "Push the right stick right and hold it"),
    ];

    /// <summary>What the check saw for one step: a key that went down, or an axis that moved (and which way, as +1 or -1 away from rest).</summary>
    public sealed record Seen(int? Key, int? Axis, int Sign);

    /// <summary>Builds a layout from what the owner showed it. Anything skipped keeps the guess, so a pad without a Home button still works.</summary>
    public static PadLayout Learn(PadCaps caps, IReadOnlyDictionary<string, Seen> seen)
    {
        var guess = Guess(caps);
        var l = new PadLayout { Source = "learned", LX = guess.LX, LY = guess.LY, RX = guess.RX, RY = guess.RY, LT = guess.LT, RT = guess.RT, HatX = guess.HatX, HatY = guess.HatY };
        var shown = new HashSet<string>();
        foreach (var (id, s) in seen)
        {
            bool stick = id.StartsWith("LS_") || id.StartsWith("RS_");
            if (s.Key is int key && !stick) { l.Buttons[key] = id; shown.Add(id); if (id == "LT") l.LT = null; if (id == "RT") l.RT = null; continue; }
            if (s.Axis is not int axis) continue;
            shown.Add(id);
            switch (id)
            {
                case "LT": l.LT = new(axis, TriggerInverted(caps, axis, s.Sign)); break;
                case "RT": l.RT = new(axis, TriggerInverted(caps, axis, s.Sign)); break;
                case "DPAD_UP": l.HatY = new(axis, s.Sign > 0); break;    // up should read negative
                case "DPAD_LEFT": l.HatX = new(axis, s.Sign > 0); break;  // left should read negative
                case "LS_UP": l.LY = new(axis, s.Sign < 0); break;        // up should read positive
                case "LS_RIGHT": l.LX = new(axis, s.Sign < 0); break;
                case "RS_UP": l.RY = new(axis, s.Sign < 0); break;
                case "RS_RIGHT": l.RX = new(axis, s.Sign < 0); break;
            }
        }
        // what the owner didn't show keeps the guess, unless a key they did show has taken its place
        foreach (var (code, name) in guess.Buttons)
        {
            if (shown.Contains(name) || l.Buttons.ContainsKey(code)) continue;
            if (name == "LT" && l.LT != null || name == "RT" && l.RT != null) continue;
            if (name.StartsWith("DPAD_") && (l.HatX != null && l.HatY != null) && shown.Any(n => n.StartsWith("DPAD_") && seen[n].Axis != null)) continue;
            l.Buttons[code] = name;
        }
        return l;
    }

    static bool TriggerInverted(PadCaps caps, int axis, int sign) => sign < 0;

    /// <summary>What differs between two layouts, in words. Empty when they read the pad the same way.</summary>
    public static List<string> Differences(PadLayout a, PadLayout b)
    {
        var d = new List<string>();
        foreach (var name in StandardKeys.Values.Distinct())
        {
            int? ka = a.Buttons.Where(p => p.Value == name).Select(p => (int?)p.Key).FirstOrDefault(), kb = b.Buttons.Where(p => p.Value == name).Select(p => (int?)p.Key).FirstOrDefault();
            if (ka != kb && !(kb == null && AxisFor(b, name) != null) && !(ka == null && AxisFor(a, name) != null)) d.Add($"{name} is key {Show(kb)} on this pad, not {Show(ka)}");
        }
        void Axis(string what, AxisUse? x, AxisUse? y) { if (x == null && y == null) return; if (x?.Code != y?.Code || x?.Invert != y?.Invert) d.Add($"{what} reads axis {(y == null ? "none" : y.Code + (y.Invert ? " (flipped)" : ""))}, not {(x == null ? "none" : x.Code + (x.Invert ? " (flipped)" : ""))}"); }
        Axis("Left stick X", a.LX, b.LX); Axis("Left stick Y", a.LY, b.LY); Axis("Right stick X", a.RX, b.RX); Axis("Right stick Y", a.RY, b.RY); Axis("Left trigger", a.LT, b.LT); Axis("Right trigger", a.RT, b.RT);
        return d;
    }
    static AxisUse? AxisFor(PadLayout l, string name) => name == "LT" ? l.LT : name == "RT" ? l.RT : null;
    static string Show(int? k) => k == null ? "none" : "0x" + k.Value.ToString("X");

    // ---- saved layouts -------------------------------------------------------------------------------------------

    public sealed class Entry
    {
        public string Name { get; set; } = "";
        public bool Verified { get; set; }
        public PadLayout? Layout { get; set; }
    }

    public static string KeyOf(ushort vendor, ushort product) => $"{vendor:x4}:{product:x4}";
    public static string ToJson(Dictionary<string, Entry> all) => JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true });
    public static Dictionary<string, Entry> FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, Entry>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}

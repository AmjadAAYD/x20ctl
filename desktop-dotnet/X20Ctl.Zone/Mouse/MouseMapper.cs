using System.Text.Json;
using System.Text.Json.Serialization;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Mouse;

/// <summary>What a controller button does while the controller is the mouse.</summary>
public enum MouseAction
{
    None, LeftClick, RightClick, MiddleClick, Back, Forward, Precision,
    Enter, Escape, Tab, Space, Backspace, Windows, TaskView, OnScreenKeyboard,
    Up, Down, Left, Right, PageUp, PageDown, VolumeUp, VolumeDown, Mute, PlayPause,
    /// <summary>Mouse 6 and 7: the wheel tilted left and right. Windows sends only five real mouse buttons, and tilt is
    /// what numbers 6 and 7 are elsewhere (X11), so games that bind "Mouse 6/7" usually read these.</summary>
    TiltLeft, TiltRight,
    /// <summary>Moves the pointer while held, at the pointer speed (the D-pad's default).</summary>
    PointerUp, PointerDown, PointerLeft, PointerRight,
}

/// <summary>
/// Controller as a mouse (owner request, 11 Oct 2026; the default layout follows Gopher360's idea, written fresh): one
/// stick moves the pointer, the other scrolls, and every button picks an action. Stored as plain JSON on this PC.
/// </summary>
public sealed class MouseProfile
{
    public const int MinSpeed = 200, MaxSpeed = 5000, MaxDeadzone = 40, MinScroll = 1, MaxScroll = 60;
    public const double MinCurve = 1, MaxCurve = 3, MinPrecision = 10, MaxPrecision = 90;
    /// <summary>"Left" or "Right": the stick that moves the pointer. The other one scrolls.</summary>
    public string PointerStick { get; set; } = "Left";
    /// <summary>Pixels a second at full push.</summary>
    public int Speed { get; set; } = 1500;
    /// <summary>1 is straight; higher keeps small pushes slow and precise while full push stays fast.</summary>
    public double Curve { get; set; } = 2;
    /// <summary>Percent of the stick ignored at the centre, so a resting stick never drifts the pointer.</summary>
    public int Deadzone { get; set; } = 12;
    public bool Scroll { get; set; } = true;
    /// <summary>Lines a second at full push.</summary>
    public int ScrollSpeed { get; set; } = 18;
    /// <summary>Speed while a Precision button is held, in percent.</summary>
    public double PrecisionPercent { get; set; } = 30;
    /// <summary>Hold View + Y for a second to switch the mouse on and off from the controller.</summary>
    public bool Combo { get; set; } = true;
    public string[] ToggleButtons { get; set; } = ["SELECT", "Y"];
    public int HoldMilliseconds { get; set; } = 1000;
    public int SourceSlot { get; set; } = -1;
    /// <summary>The chosen controller when it is connected, otherwise the first that is; -1 when none is. A choice saved
    /// for a slot the controller no longer sits in must not leave the mouse waiting for it.</summary>
    public static int PickSlot(int chosen, IEnumerable<int> connected)
    {
        var slots = connected.ToArray();
        return slots.Contains(chosen) ? chosen : slots.Length > 0 ? slots[0] : -1;
    }
    public bool HorizontalScroll { get; set; } = true;
    public bool InvertVerticalScroll { get; set; }
    public bool InvertHorizontalScroll { get; set; }
    [JsonIgnore] public ushort ToggleMask => (ushort)ToggleButtons.Aggregate(0, (mask, key) => mask | PadButtons.Mask.GetValueOrDefault(key));
    /// <summary>Button name (PadButtons names plus LT, RT) → action.</summary>
    public Dictionary<string, MouseAction> Buttons { get; set; } = Defaults();
    /// <summary>Turbo: button → presses a second while held (clicks or keys repeat on their own).</summary>
    public Dictionary<string, int> Turbo { get; set; } = new();
    public const int MinTurbo = 2, MaxTurbo = 30;
    public static bool Moves(MouseAction a) => a is MouseAction.PointerUp or MouseAction.PointerDown or MouseAction.PointerLeft or MouseAction.PointerRight;

    public static Dictionary<string, MouseAction> Defaults() => new()
    {
        ["A"] = MouseAction.LeftClick, ["B"] = MouseAction.RightClick, ["X"] = MouseAction.MiddleClick, ["Y"] = MouseAction.OnScreenKeyboard,
        ["LB"] = MouseAction.Back, ["RB"] = MouseAction.Forward, ["LT"] = MouseAction.Precision, ["RT"] = MouseAction.LeftClick,
        ["DPAD_UP"] = MouseAction.PointerUp, ["DPAD_DOWN"] = MouseAction.PointerDown, ["DPAD_LEFT"] = MouseAction.PointerLeft, ["DPAD_RIGHT"] = MouseAction.PointerRight,
        ["START"] = MouseAction.Windows, ["SELECT"] = MouseAction.None, ["L3"] = MouseAction.Enter, ["R3"] = MouseAction.TaskView, ["HOME"] = MouseAction.None,
    };
    public static readonly string[] Inputs = [.. PadButtons.Mask.Keys, "LT", "RT"];

    public void Validate()
    {
        if (SourceSlot is < -1 or > 3 || HoldMilliseconds is < 500 or > 3000 || ToggleButtons == null || ToggleButtons.Length != 2
            || ToggleButtons.Distinct().Count() != 2 || ToggleButtons.Any(k => !PadButtons.Mask.ContainsKey(k)))
            throw new InvalidDataException("Choose two different reported buttons for the mouse shortcut.");
        if (PointerStick is not ("Left" or "Right") || Speed < MinSpeed || Speed > MaxSpeed || !double.IsFinite(Curve) || Curve < MinCurve || Curve > MaxCurve
            || Deadzone < 0 || Deadzone > MaxDeadzone || ScrollSpeed < MinScroll || ScrollSpeed > MaxScroll
            || !double.IsFinite(PrecisionPercent) || PrecisionPercent < MinPrecision || PrecisionPercent > MaxPrecision || Buttons == null)
            throw new InvalidDataException("Invalid mouse settings.");
        foreach (var (button, action) in Buttons)
            if (!Inputs.Contains(button) || !Enum.IsDefined(action)) throw new InvalidDataException("Invalid mouse button.");
        if (Turbo == null) throw new InvalidDataException("Invalid mouse settings.");
        foreach (var (button, rate) in Turbo)
            if (!Inputs.Contains(button) || rate < MinTurbo || rate > MaxTurbo) throw new InvalidDataException("Turbo needs a known button and 2 to 30 presses a second.");
    }

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "x20ctl", "mouse.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>The saved settings, or the defaults when there are none or they can't be used.</summary>
    public static MouseProfile Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new();
            var p = JsonSerializer.Deserialize<MouseProfile>(File.ReadAllBytes(path), Json) ?? new();
            foreach (var (k, v) in Defaults()) p.Buttons.TryAdd(k, v);
            p.Validate(); return p;
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException) { return new(); }
    }

    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(this, Json)); File.Move(temporary, path, true); }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } }
    }

    public MouseProfile Clone() => JsonSerializer.Deserialize<MouseProfile>(JsonSerializer.SerializeToUtf8Bytes(this, Json), Json)!;
}

/// <summary>What the mouse should do this tick: whole pixels to move, wheel units (120 = one notch), and button edges.</summary>
public readonly record struct MouseFrame(int Dx, int Dy, int Wheel, int HWheel, IReadOnlyList<(MouseAction Action, bool Down)> Edges);

/// <summary>
/// Turns controller states into pointer moves, scrolling and button actions. Pure logic, no Windows calls: the service
/// sends what it returns. Fractions of a pixel carry over between ticks, so slow pushes still move smoothly.
/// </summary>
public sealed class MouseMapper
{
    readonly MouseProfile profile;
    double carryX, carryY, carryWheel, carryH, clock;
    readonly HashSet<string> held = new();
    readonly Dictionary<string, double> turboSince = new();
    public MouseMapper(MouseProfile profile) { profile.Validate(); this.profile = profile; }

    /// <summary>How fast (0–1 of full speed) a push of this size moves, after the dead zone and the curve.</summary>
    public static double Push(double magnitude, int deadzone, double curve)
    {
        double dz = deadzone / 100.0;
        if (magnitude <= dz) return 0;
        return Math.Pow(Math.Clamp((magnitude - dz) / (1 - dz), 0, 1), curve);
    }

    public bool Precision => profile.Buttons.Any(b => b.Value == MouseAction.Precision && held.Contains(b.Key));

    public MouseFrame Step(PadState s, double seconds)
    {
        // Reserve both shortcut components before mapping. Even a partial chord cannot leak a click/key.
        if (profile.Combo) s = s with { Buttons = (ushort)(s.Buttons & ~profile.ToggleMask) };
        seconds = Math.Clamp(seconds, 0, .1); // a stall never turns into one huge jump
        var edges = new List<(MouseAction, bool)>();
        var now = new HashSet<string>();
        foreach (var (name, mask) in PadButtons.Mask) if ((s.Buttons & mask) != 0) now.Add(name);
        if (s.LeftTrigger > PadButtons.TriggerThreshold) now.Add("LT");
        if (s.RightTrigger > PadButtons.TriggerThreshold) now.Add("RT");
        clock += seconds;
        // the pointer-move buttons (the D-pad by default) push the pointer like a stick held fully over
        double mx = 0, my = 0;
        foreach (var name in now)
            switch (profile.Buttons.GetValueOrDefault(name))
            {
                case MouseAction.PointerUp: my -= 1; break; case MouseAction.PointerDown: my += 1; break;
                case MouseAction.PointerLeft: mx -= 1; break; case MouseAction.PointerRight: mx += 1; break;
            }
        // turbo: a held button is pressed and let go again and again, so a click repeats on its own
        foreach (var (name, rate) in profile.Turbo)
        {
            if (!now.Contains(name)) { turboSince.Remove(name); continue; }
            if (!turboSince.TryGetValue(name, out var since)) turboSince[name] = since = clock;
            if (((long)((clock - since) * rate * 2) & 1) == 1) now.Remove(name);
        }
        foreach (var action in profile.Buttons.Values.Distinct().Where(a=>a is not (MouseAction.None or MouseAction.Precision) && !MouseProfile.Moves(a)))
        {
            bool down = profile.Buttons.Any(b=>b.Value==action && now.Contains(b.Key));
            bool was = profile.Buttons.Any(b=>b.Value==action && held.Contains(b.Key));
            if (down == was) continue;
            edges.Add((action, down));
        }
        held.Clear(); held.UnionWith(now);

        bool left = profile.PointerStick == "Left";
        var (px, py) = left ? (s.LeftX, s.LeftY) : (s.RightX, s.RightY);
        var (sx, sy) = left ? (s.RightX, s.RightY) : (s.LeftX, s.LeftY);

        double nx = px / 32768.0, ny = py / 32768.0, m = Math.Min(1, Math.Sqrt(nx * nx + ny * ny));
        double speed = Push(m, profile.Deadzone, profile.Curve) * profile.Speed * (Precision ? profile.PrecisionPercent / 100 : 1);
        if (m > 0) { carryX += nx / m * speed * seconds; carryY -= ny / m * speed * seconds; } // screen y grows downwards
        else if (mx != 0 || my != 0)
        {
            double len = Math.Sqrt(mx * mx + my * my), pad = profile.Speed * .5 * (Precision ? profile.PrecisionPercent / 100 : 1);
            carryX += mx / len * pad * seconds; carryY += my / len * pad * seconds;
        }
        int dx = (int)Math.Truncate(carryX), dy = (int)Math.Truncate(carryY); carryX -= dx; carryY -= dy;

        int wheel = 0, hwheel = 0;
        if (profile.Scroll)
        {
            double wx = sx / 32768.0, wy = sy / 32768.0;
            // each axis on its own, so scrolling down a page doesn't also drift sideways
            carryWheel += Math.Sign(wy) * Push(Math.Abs(wy), profile.Deadzone, 1.5) * profile.ScrollSpeed * 120 * seconds;
            carryH += Math.Sign(wx) * Push(Math.Abs(wx), profile.Deadzone + 10, 1.5) * profile.ScrollSpeed * 120 * seconds;
            if (Math.Abs(carryWheel) >= 30) { wheel = (int)Math.Truncate(carryWheel); carryWheel -= wheel; }
            if (Math.Abs(carryH) >= 30) { hwheel = (int)Math.Truncate(carryH); carryH -= hwheel; }
            if (Math.Abs(wy) < profile.Deadzone / 100.0) carryWheel = 0;
            if (Math.Abs(wx) < profile.Deadzone / 100.0 + .1) carryH = 0;
        }
        return new(dx, dy, profile.InvertVerticalScroll ? -wheel : wheel,
            !profile.HorizontalScroll ? 0 : profile.InvertHorizontalScroll ? -hwheel : hwheel, edges);
    }

    /// <summary>Lets go of everything still held (the mouse is being switched off), so no click or key stays stuck.</summary>
    public IReadOnlyList<(MouseAction, bool)> Release()
    {
        var edges = held.Select(n => profile.Buttons.GetValueOrDefault(n)).Where(a => a is not (MouseAction.None or MouseAction.Precision) && !MouseProfile.Moves(a)).Distinct().Select(a => (a, false)).ToList();
        held.Clear(); turboSince.Clear(); carryX = carryY = carryWheel = carryH = 0; return edges;
    }
}

/// <summary>A button combination held for a while (like View + Menu for a second): fires once per hold.</summary>
public sealed class HoldCombo(ushort mask, long holdMs)
{
    long since = -1; bool fired;
    public bool Update(ushort buttons, long nowMs)
    {
        if ((buttons & mask) != mask) { since = -1; fired = false; return false; }
        if (since < 0) since = nowMs;
        if (fired || nowMs - since < holdMs) return false;
        fired = true; return true;
    }
    public bool Holding => since >= 0 && !fired;
}

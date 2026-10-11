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
    /// <summary>Button name (PadButtons names plus LT, RT) → action.</summary>
    public Dictionary<string, MouseAction> Buttons { get; set; } = Defaults();

    public static Dictionary<string, MouseAction> Defaults() => new()
    {
        ["A"] = MouseAction.LeftClick, ["B"] = MouseAction.RightClick, ["X"] = MouseAction.MiddleClick, ["Y"] = MouseAction.OnScreenKeyboard,
        ["LB"] = MouseAction.Back, ["RB"] = MouseAction.Forward, ["LT"] = MouseAction.Precision, ["RT"] = MouseAction.LeftClick,
        ["DPAD_UP"] = MouseAction.Up, ["DPAD_DOWN"] = MouseAction.Down, ["DPAD_LEFT"] = MouseAction.Left, ["DPAD_RIGHT"] = MouseAction.Right,
        ["START"] = MouseAction.Windows, ["SELECT"] = MouseAction.None, ["L3"] = MouseAction.Enter, ["R3"] = MouseAction.TaskView, ["HOME"] = MouseAction.None,
    };
    public static readonly string[] Inputs = [.. PadButtons.Mask.Keys, "LT", "RT"];

    public void Validate()
    {
        if (PointerStick is not ("Left" or "Right") || Speed < MinSpeed || Speed > MaxSpeed || !double.IsFinite(Curve) || Curve < MinCurve || Curve > MaxCurve
            || Deadzone < 0 || Deadzone > MaxDeadzone || ScrollSpeed < MinScroll || ScrollSpeed > MaxScroll
            || !double.IsFinite(PrecisionPercent) || PrecisionPercent < MinPrecision || PrecisionPercent > MaxPrecision || Buttons == null)
            throw new InvalidDataException("Invalid mouse settings.");
        foreach (var (button, action) in Buttons)
            if (!Inputs.Contains(button) || !Enum.IsDefined(action)) throw new InvalidDataException("Invalid mouse button.");
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
    double carryX, carryY, carryWheel, carryH;
    readonly HashSet<string> held = new();
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
        seconds = Math.Clamp(seconds, 0, .1); // a stall never turns into one huge jump
        var edges = new List<(MouseAction, bool)>();
        var now = new HashSet<string>();
        foreach (var (name, mask) in PadButtons.Mask) if ((s.Buttons & mask) != 0) now.Add(name);
        if (s.LeftTrigger > PadButtons.TriggerThreshold) now.Add("LT");
        if (s.RightTrigger > PadButtons.TriggerThreshold) now.Add("RT");
        foreach (var name in MouseProfile.Inputs)
        {
            bool down = now.Contains(name), was = held.Contains(name);
            if (down == was) continue;
            var action = profile.Buttons.GetValueOrDefault(name);
            if (action is not (MouseAction.None or MouseAction.Precision)) edges.Add((action, down));
        }
        held.Clear(); held.UnionWith(now);

        bool left = profile.PointerStick == "Left";
        var (px, py) = left ? (s.LeftX, s.LeftY) : (s.RightX, s.RightY);
        var (sx, sy) = left ? (s.RightX, s.RightY) : (s.LeftX, s.LeftY);

        double nx = px / 32768.0, ny = py / 32768.0, m = Math.Min(1, Math.Sqrt(nx * nx + ny * ny));
        double speed = Push(m, profile.Deadzone, profile.Curve) * profile.Speed * (Precision ? profile.PrecisionPercent / 100 : 1);
        if (m > 0) { carryX += nx / m * speed * seconds; carryY -= ny / m * speed * seconds; } // screen y grows downwards
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
        return new(dx, dy, wheel, hwheel, edges);
    }

    /// <summary>Lets go of everything still held (the mouse is being switched off), so no click or key stays stuck.</summary>
    public IReadOnlyList<(MouseAction, bool)> Release()
    {
        var edges = held.Select(n => profile.Buttons.GetValueOrDefault(n)).Where(a => a is not (MouseAction.None or MouseAction.Precision)).Select(a => (a, false)).ToList();
        held.Clear(); carryX = carryY = carryWheel = carryH = 0; return edges;
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

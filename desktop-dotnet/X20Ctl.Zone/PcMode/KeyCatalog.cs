namespace X20Ctl.Zone.PcMode;

/// <summary>One key on the drawn keyboard: its output id ("K:65", "K:13e" for an extended key), its label, and where it
/// sits (in key units: row, column, width).</summary>
public sealed record KeyInfo(string Id, string Label, double Row, double Col, double Width = 1, double Height = 1, string? Name = null)
{
    public ushort Vk => KeyCatalog.VkOf(Id);
    public bool Extended => Id.EndsWith('e');
}

/// <summary>
/// Everything the Keyboard tab can send: every key of a full-size keyboard, and the seven mouse buttons. Output ids are
/// "K:&lt;virtual key&gt;" (with a trailing "e" for an extended key such as the numpad Enter) or "M:&lt;MouseAction&gt;".
/// </summary>
public static class KeyCatalog
{
    public static readonly IReadOnlyList<KeyInfo> Keys = Build();
    static readonly Dictionary<string, KeyInfo> byId = Keys.ToDictionary(k => k.Id);

    /// <summary>Mouse outputs the Keyboard tab offers, with their M-number labels.</summary>
    public static readonly (string Id, string Label)[] Mouse =
    [
        ("M:LeftClick", "M1 Left"), ("M:RightClick", "M2 Right"), ("M:MiddleClick", "M3 Middle"), ("M:Back", "M4 Back"), ("M:Forward", "M5 Forward"),
        ("M:TiltLeft", "M6 Tilt left"), ("M:TiltRight", "M7 Tilt right"),
    ];

    public static bool IsValid(string? id) => id != null && (byId.ContainsKey(id) || Mouse.Any(m => m.Id == id));
    public static string Label(string id) => byId.TryGetValue(id, out var k) ? k.Name ?? k.Label : Mouse.FirstOrDefault(m => m.Id == id).Label ?? id;
    public static ushort VkOf(string id) => Convert.ToUInt16(id[2..].TrimEnd('e'));

    static List<KeyInfo> Build()
    {
        var keys = new List<KeyInfo>();
        void K(int vk, string label, double row, double col, double w = 1, double h = 1, bool ext = false, string? name = null) => keys.Add(new($"K:{vk}{(ext ? "e" : "")}", label, row, col, w, h, name));
        // row 0: Esc, F1–F12, print screen cluster
        K(0x1B, "Esc", 0, 0);
        for (int i = 0; i < 12; i++) K(0x70 + i, "F" + (i + 1), 0, 2 + i + i / 4 * .5);
        K(0x2C, "PrtSc", 0, 15.25, ext: true); K(0x91, "ScrLk", 0, 16.25); K(0x13, "Pause", 0, 17.25);
        // row 1: numbers
        double r = 1.25;
        K(0xC0, "`", r, 0); for (int i = 1; i <= 10; i++) K(0x30 + i % 10, (i % 10).ToString(), r, i);
        K(0xBD, "-", r, 11); K(0xBB, "=", r, 12); K(0x08, "Backspace", r, 13, 2);
        K(0x2D, "Ins", r, 15.25, ext: true); K(0x24, "Home", r, 16.25, ext: true); K(0x21, "PgUp", r, 17.25, ext: true);
        K(0x90, "Num", r, 18.5, ext: true); K(0x6F, "/", r, 19.5, ext: true); K(0x6A, "*", r, 20.5); K(0x6D, "−", r, 21.5);
        // row 2: QWERTY
        r = 2.25; K(0x09, "Tab", r, 0, 1.5); int c = 0;
        foreach (char ch in "QWERTYUIOP") K(ch, ch.ToString(), r, 1.5 + c++);
        K(0xDB, "[", r, 11.5); K(0xDD, "]", r, 12.5); K(0xDC, "\\", r, 13.5, 1.5);
        K(0x2E, "Del", r, 15.25, ext: true); K(0x23, "End", r, 16.25, ext: true); K(0x22, "PgDn", r, 17.25, ext: true);
        K(0x67, "7", r, 18.5); K(0x68, "8", r, 19.5); K(0x69, "9", r, 20.5); K(0x6B, "+", r, 21.5, 1, 2);
        // row 3: home row
        r = 3.25; K(0x14, "Caps", r, 0, 1.75); c = 0;
        foreach (char ch in "ASDFGHJKL") K(ch, ch.ToString(), r, 1.75 + c++);
        K(0xBA, ";", r, 10.75); K(0xDE, "'", r, 11.75); K(0x0D, "Enter", r, 12.75, 2.25);
        K(0x64, "4", r, 18.5); K(0x65, "5", r, 19.5); K(0x66, "6", r, 20.5);
        // row 4: shift row
        r = 4.25; K(0xA0, "Shift", r, 0, 2.25); c = 0;
        foreach (char ch in "ZXCVBNM") K(ch, ch.ToString(), r, 2.25 + c++);
        K(0xBC, ",", r, 9.25); K(0xBE, ".", r, 10.25); K(0xBF, "/", r, 11.25); K(0xA1, "Shift", r, 12.25, 2.75, name: "Right Shift");
        K(0x26, "↑", r, 16.25, ext: true);
        K(0x61, "1", r, 18.5); K(0x62, "2", r, 19.5); K(0x63, "3", r, 20.5); K(0x0D, "Enter", r, 21.5, 1, 2, ext: true, name: "Numpad Enter");
        // row 5: space row
        r = 5.25; K(0xA2, "Ctrl", r, 0, 1.25); K(0x5B, "Win", r, 1.25, 1.25, ext: true); K(0xA4, "Alt", r, 2.5, 1.25);
        K(0x20, "Space", r, 3.75, 6.25); K(0xA5, "Alt", r, 10, 1.25, ext: true, name: "Right Alt"); K(0x5C, "Win", r, 11.25, 1.25, ext: true, name: "Right Win");
        K(0x5D, "Menu", r, 12.5, 1.25, ext: true); K(0xA3, "Ctrl", r, 13.75, 1.25, ext: true, name: "Right Ctrl");
        K(0x25, "←", r, 15.25, ext: true); K(0x28, "↓", r, 16.25, ext: true); K(0x27, "→", r, 17.25, ext: true);
        K(0x60, "0", r, 18.5, 2); K(0x6E, ".", r, 20.5);
        // the number pad's keys read "Num 7" in lists, so they aren't mistaken for the number row
        for (int i = 0; i < keys.Count; i++)
            if (keys[i].Vk is >= 0x60 and <= 0x6F && keys[i].Name == null) keys[i] = keys[i] with { Name = "Num " + keys[i].Label };
        return keys;
    }
}

/// <summary>One step of a custom mapping: an output held for a while, then a pause before the next.</summary>
public sealed class KeyStep
{
    public string Output { get; set; } = "";
    public int HoldMs { get; set; } = 50;
    public int PauseMs { get; set; } = 50;
}

using System.Text;
using System.Text.Json;
namespace X20Ctl.Linux;

/// <summary>The layouts PC mode reads each pad through, and what the button check has taught or confirmed. Saved per vendor and product, in pad-layouts.json beside the PC mode settings.</summary>
internal sealed class PadLayoutStore
{
    public static PadLayoutStore Shared { get; } = new();
    readonly string path = Path.Combine(Path.GetDirectoryName(X20Ctl.Zone.PcMode.PcModeStore.DefaultPath)!, "pad-layouts.json");
    readonly object gate = new();
    Dictionary<string, PadLayouts.Entry> all;
    DateTime loaded;

    PadLayoutStore() { all = Read(); }

    Dictionary<string, PadLayouts.Entry> Read()
    {
        loaded = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        try { return File.Exists(path) ? PadLayouts.FromJson(File.ReadAllText(path)) : []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }

    /// <summary>The layout to read this pad through, and how far it can be trusted:
    /// "standard" (the kernel's gamepad layout), "learned" (taught by the owner), "checked" (guessed, and the owner confirmed it), "needs-check" (guessed, and nobody has confirmed it yet).</summary>
    public (PadLayout Layout, string State) Resolve(EvDevice device)
    {
        var caps = device.Caps ?? new PadCaps(new HashSet<int>(), new Dictionary<int, AbsInfo>());
        PadLayouts.Entry? entry;
        lock (gate)
        {
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) != loaded) all = Read();
            all.TryGetValue(PadLayouts.KeyOf(device.Vendor, device.Product), out entry);
        }
        if (entry?.Layout != null) return (entry.Layout, "learned");
        var guess = PadLayouts.Guess(caps);
        if (guess.Source == "standard") return (guess, entry?.Verified == true ? "checked" : "standard");
        return (guess, entry?.Verified == true ? "checked" : "needs-check");
    }

    /// <summary>Remembers what the check found: a taught layout, or just that the guess was right.</summary>
    public void Save(EvDevice device, PadLayout? learned, bool verified)
    {
        lock (gate)
        {
            all[PadLayouts.KeyOf(device.Vendor, device.Product)] = new PadLayouts.Entry { Name = device.Name, Verified = verified, Layout = learned };
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, PadLayouts.ToJson(all), Encoding.UTF8); loaded = File.GetLastWriteTimeUtc(path); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Forgets what was taught, so the pad is read by its guessed layout again (and asked to be checked if that was only a guess).</summary>
    public void Forget(EvDevice device)
    {
        lock (gate)
        {
            if (!all.Remove(PadLayouts.KeyOf(device.Vendor, device.Product))) return;
            try { File.WriteAllText(path, PadLayouts.ToJson(all), Encoding.UTF8); loaded = File.GetLastWriteTimeUtc(path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}

/// <summary>The button check. The owner is asked to press each input in turn; what the pad sends for each becomes its layout. Read-only: it listens, never writes, and the pad must not be held by PC mode or the mouse meanwhile.</summary>
internal sealed class LayoutCheck : IDisposable
{
    public sealed record Outcome(PadLayout Learned, PadLayout Guess, List<string> Differences, bool Matches, int Shown, string? ReportPath, string Summary);

    readonly EvDevice device; readonly EvReader reader; readonly PadCaps caps;
    readonly Dictionary<string, PadLayouts.Seen> seen = [];
    readonly HashSet<int> usedKeys = [], usedAxes = [];
    bool gate = true;
    public int Index { get; private set; }
    public int Total => PadLayouts.Steps.Length;
    public bool Finished => Index >= Total;
    public string Prompt => Finished ? "Done" : PadLayouts.Steps[Index].Prompt;
    public string Id => Finished ? "" : PadLayouts.Steps[Index].Id;
    public string Waiting => gate && !Finished ? "Let go of everything first…" : "";
    public int Shown => seen.Count;
    public EvDevice Device => device;

    public LayoutCheck(EvDevice device)
    {
        this.device = device; caps = device.Caps ?? throw new InvalidOperationException("This controller reported nothing to check.");
        reader = new EvReader(device);
    }

    bool AtRest()
    {
        if (reader.Down.Count > 0) return false;
        foreach (var (code, info) in caps.Axes)
        {
            if (!reader.Value.TryGetValue(code, out int v) || info.Max <= info.Min) continue;
            if (Math.Abs(v - info.Rest) > (info.Max - info.Min) * .25) return false;
        }
        return true;
    }

    public void Skip() { if (!Finished) { Index++; gate = true; } }

    /// <summary>Listens for what the owner did since the last call. Call it a few times a second or more.</summary>
    public void Tick()
    {
        if (Finished) return;
        var events = reader.Poll(0);
        if (gate) { if (AtRest()) gate = false; else return; }
        string id = Id; bool stick = id.StartsWith("LS_") || id.StartsWith("RS_"), either = id is "LT" or "RT" || id.StartsWith("DPAD_");
        foreach (var e in events)
        {
            if (e.Type == 1 && e.Value == 1 && !stick && !usedKeys.Contains(e.Code) && e.Code >= 0x100)
            { Accept(new(e.Code, null, 0)); usedKeys.Add(e.Code); return; }
            if (e.Type != 3 || !caps.Axes.TryGetValue(e.Code, out var info) || info.Max <= info.Min) continue;
            if (!stick && !either) continue;
            bool hat = e.Code is 0x10 or 0x11 or 0x12 or 0x13 or 0x14 or 0x15 or 0x16 or 0x17;
            if (stick && hat || usedAxes.Contains(e.Code) && !id.StartsWith("DPAD_")) continue;
            double dev = (e.Value - info.Rest) / (double)(info.Max - info.Min);
            if (Math.Abs(dev) < .4) continue;
            if (id.StartsWith("DPAD_") && !hat) continue;
            if (id is "LT" or "RT" && hat) continue;
            Accept(new(null, e.Code, Math.Sign(dev)));
            if (!id.StartsWith("DPAD_")) usedAxes.Add(e.Code);
            return;
        }
    }

    void Accept(PadLayouts.Seen s) { seen[Id] = s; Index++; gate = true; }

    /// <summary>Builds the result and saves it: a taught layout when it differs from the guess, a plain "this was right" when it doesn't. Writes a short report of what the pad sent either way when the layout wasn't the standard one.</summary>
    public Outcome Finish()
    {
        var guess = PadLayouts.Guess(caps); var learned = PadLayouts.Learn(caps, seen); var diffs = PadLayouts.Differences(guess, learned);
        bool enough = seen.Count >= Math.Max(8, Total / 2), matches = diffs.Count == 0;
        string? report = null;
        if (enough)
        {
            if (matches) PadLayoutStore.Shared.Save(device, null, true); else PadLayoutStore.Shared.Save(device, learned, true);
            if (!matches || guess.Source != "standard") report = WriteReport(guess, learned, diffs);
        }
        string summary = !enough ? $"Only {seen.Count} of {Total} inputs were shown, which isn't enough to teach a layout. Nothing was saved."
            : matches ? (guess.Source == "standard" ? "This controller reads exactly as an Xbox controller does. PC mode will map it correctly." : "The guess for this controller was right. PC mode is ready for it.")
            : $"This controller sends some inputs differently. PC mode now reads it the way you showed it ({diffs.Count} difference{(diffs.Count == 1 ? "" : "s")}).";
        return new(learned, guess, diffs, matches, seen.Count, report, summary);
    }

    string? WriteReport(PadLayout guess, PadLayout learned, List<string> diffs)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "X20CTLInputReports"); Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"layout-{device.Vendor:x4}-{device.Product:x4}.json");
            var doc = new
            {
                kind = "pad-layout", app = "X20CTL " + AppInfo.Version, controller = device.Name, vendor = device.Vendor.ToString("x4"), product = device.Product.ToString("x4"),
                keys = caps.Keys.OrderBy(k => k).Select(k => "0x" + k.ToString("X")).ToArray(), axes = caps.Axes.OrderBy(a => a.Key).ToDictionary(a => a.Key.ToString(), a => a.Value),
                guess, learned, differences = diffs, shown = seen.ToDictionary(p => p.Key, p => p.Value)
            };
            File.WriteAllText(file, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8); return file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public void Dispose() => reader.Dispose();
}

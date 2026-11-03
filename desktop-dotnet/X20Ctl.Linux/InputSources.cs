using X20Ctl.Zone.Input;
namespace X20Ctl.Linux;

/// <summary>Explicit session-only evdev ownership. Never infer a physical source from model art or list order.</summary>
internal static class InputSources
{
    static readonly PlayerInputAssignments assignments = new();
    static readonly Dictionary<string, EvDevice> known = new();
    static readonly object gate = new();
    public static event Action? Changed;
    public static bool UiActive { get; private set; }
    public static string Key(EvDevice d) => $"{d.Path}|{d.Vendor:X4}:{d.Product:X4}|{d.Name}|{d.Instance}";
    public static EvDevice? Source(int player)
    {
        lock (gate) return assignments.Source(player) is { } key ? known.GetValueOrDefault(key) : null;
    }
    public static (EvDevice? Device, long Revision) Snapshot(int player)
    {
        lock (gate) return (assignments.Source(player) is { } key ? known.GetValueOrDefault(key) : null, assignments.Revision(player));
    }
    public static bool UseLease(int player, long revision, Action action)
    {
        lock (gate) { if (assignments.Revision(player) != revision || assignments.Source(player) == null) return false; action(); return true; }
    }
    public static bool Select(int player, EvDevice? device)
    {
        lock (gate)
        {
            string? key = device == null ? null : Key(device);
            if (!assignments.Select(player, key)) return false;
            if (device != null) known[key!] = device;
        }
        Changed?.Invoke(); return true;
    }
    public static long Revision(int player) => assignments.Revision(player);
    public static void Lost(int player, EvDevice expected, long revision)
    {
        bool changed; lock (gate) changed = assignments.ClearIfCurrent(player, Key(expected), revision);
        if (changed) Changed?.Invoke();
    }
    public static void RemoveMissing(IEnumerable<EvDevice> devices)
    {
        var present = devices.Select(Key).ToHashSet(); bool changed;
        lock (gate)
        {
            var before = Enumerable.Range(0, 4).Select(assignments.Source).ToArray();
            assignments.RemoveMissing(present);
            changed = !before.SequenceEqual(Enumerable.Range(0, 4).Select(assignments.Source));
        }
        if (changed) Changed?.Invoke();
    }
    public static void SetUiActive(bool active)
    { if (active == UiActive) return; UiActive = active; Changed?.Invoke(); }
}

using System.Diagnostics;
using Avalonia.Threading;
namespace X20Ctl.Linux;

/// <summary>One reading of a controller: pressed buttons (by the app's names), both sticks (-1 to 1, up is negative) and both triggers (0 to 1).</summary>
internal sealed record PadFrame(IReadOnlySet<string> Down, double LX, double LY, double RX, double RY, double LT, double RT, long Stamp = 0)
{
    public static readonly PadFrame Empty = new(new HashSet<string>(), 0, 0, 0, 0, 0, 0);
}

/// <summary>Reads only the player's explicitly selected evdev source, never grabbing or writing to it.</summary>
internal sealed class GamepadReader : IDisposable
{
    readonly object gate = new();
    CancellationTokenSource? stop; Task? worker;
    PadFrame frame = PadFrame.Empty; int generation, pending, player; bool listening;
    long reports; long began; long lease; EvDevice? selectedSource;
    public string? Device { get; private set; }
    public bool Connected { get; private set; }
    public string Status { get; private set; } = "Select an input source in PC mode.";
    public event Action<PadFrame>? Frame;
    public double Rate => reports / Math.Max(.001, (Stopwatch.GetTimestamp() - began) / (double)Stopwatch.Frequency);
    public PadFrame Snapshot() { lock (gate) return frame; }
    public async Task<bool> WaitUntilConnected(CancellationToken cancel)
    {
        if (!OperatingSystem.IsLinux()) return false;
        for (int i = 0; i < 50 && !Connected; i++) { cancel.ThrowIfCancellationRequested(); if (InputSources.Source(player) == null) return false; await Task.Delay(10, cancel); }
        return Connected;
    }
    public void Start(int slot = 0)
    {
        Stop(); player = slot; listening = true; InputSources.Changed += SourceChanged; Restart();
    }
    void SourceChanged() => Dispatcher.UIThread.Post(() => { if (listening) Restart(); });
    void Restart()
    {
        EndWorker(); Frame?.Invoke(PadFrame.Empty);
        if (!InputSources.UiActive || !OperatingSystem.IsLinux()) return;
        var selection = InputSources.Snapshot(player); var source = selection.Device;
        if (source == null) return;
        if (PadLayoutStore.Shared.Resolve(source).State == "needs-check") { Status = "Check the selected controller's layout in PC mode."; return; }
        int epoch = generation; selectedSource = source; lease = selection.Revision; long selectedLease = lease; stop = new(); var token = stop.Token;
        began = Stopwatch.GetTimestamp(); reports = 0; pending = 0;
        worker = Task.Run(() => Read(source, selectedLease, epoch, token));
    }
    void Publish(PadFrame next, int epoch, bool connected, string status)
    {
        lock (gate) { if (epoch != generation || selectedSource != null && InputSources.Revision(player) != lease) return; frame = next; pending++; }
        if (pending > 256)
        {
            lock (gate) { if (epoch != generation) return; generation++; frame = PadFrame.Empty; Connected = false; }
            stop?.Cancel(); if (selectedSource != null) InputSources.Lost(player, selectedSource, lease);
            Dispatcher.UIThread.Post(() => { if (listening) Frame?.Invoke(PadFrame.Empty); }); return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            lock (gate) { if (epoch != generation || !listening || selectedSource != null && InputSources.Revision(player) != lease) return; pending--; }
            Connected = connected;
            Device = connected ? InputSources.Source(player)?.Path : null; Status = status; Frame?.Invoke(next);
        });
    }
    void Read(EvDevice source, long sourceLease, int epoch, CancellationToken token)
    {
        try
        {
            using var pad = new PcEngine.RealPad(source, grab: false);
            // A descriptor is owned by this worker and polling is bounded, so cancellation never waits for a blocking read.
            Publish(Convert(pad.State(), pad.Down), epoch, true, "Reading " + source.Path);
            while (!token.IsCancellationRequested)
            {
                pad.Wait(20);
                if (token.IsCancellationRequested) break;
                if (!pad.Read((state, names) => { reports++; Publish(Convert(state, names), epoch, true, "Reading " + source.Path); }))
                { InputSources.Lost(player, source, sourceLease); break; }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { Status = e.Message; InputSources.Lost(player, source, sourceLease); }
        finally { Publish(PadFrame.Empty, epoch, false, "Input stopped. Select the source again in PC mode."); }
    }
    static PadFrame Convert(X20Ctl.Zone.PcMode.PadState s, IReadOnlySet<string> names)
    {
        var down = names.ToHashSet();
        foreach (var (key, mask) in X20Ctl.Zone.PcMode.PadButtons.Mask) if ((s.Buttons & mask) != 0) down.Add(key);
        if (s.LeftTrigger > 140) down.Add("LT"); if (s.RightTrigger > 140) down.Add("RT");
        return new(down, Math.Clamp(s.LeftX / 32767d, -1, 1), Math.Clamp(-s.LeftY / 32767d, -1, 1),
            Math.Clamp(s.RightX / 32767d, -1, 1), Math.Clamp(-s.RightY / 32767d, -1, 1), s.LeftTrigger / 255d, s.RightTrigger / 255d, Stopwatch.GetTimestamp());
    }
    void EndWorker()
    {
        lock (gate) { generation++; pending = 0; frame = PadFrame.Empty; Connected = false; Device = null; }
        stop?.Cancel();
        // No worker waits for the UI thread; stale queued reports are rejected by generation.
        if (worker != null) worker.Wait(150);
        if (worker?.IsCompleted != false) stop?.Dispose();
        worker = null; stop = null; Status = "Select an input source in PC mode.";
    }
    public void Stop()
    {
        if (listening) InputSources.Changed -= SourceChanged;
        listening = false; EndWorker(); Frame?.Invoke(PadFrame.Empty);
    }
    public void Dispose() => Stop();
}

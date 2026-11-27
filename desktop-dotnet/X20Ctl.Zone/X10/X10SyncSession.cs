using System.IO;

namespace X20Ctl.Zone.X10;

/// <summary>Transport abstraction for talking KRLY over BLE or a deterministic test double.</summary>
public interface IX10WireTransport : IAsyncDisposable
{
    string DeviceName { get; }
    event Action<byte[]>? NotificationReceived;
    Task WriteChunkAsync(byte[] chunk, CancellationToken cancel);
}

/// <summary>
/// Serialized single-request KRLY protocol session:
/// chunks outbound frames at 20 bytes, matches replies by command ID,
/// aborts cleanly on timeout without retrying, and guarantees mode/stop cleanup on exit.
/// </summary>
public sealed class X10ProtocolSession : IDisposable
{
    private readonly IX10WireTransport transport;
    private readonly Krly.FrameReader reader = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeSpan requestTimeout;
    private readonly object waiterLock = new();

    private Action<byte[]>? waiting;
    private TaskCompletionSource? done;
    private Action<byte[]>? asyncListener;
    private Krly.Battery? lastBattery;
    private byte sequence;

    public X10ProtocolSession(IX10WireTransport transport, TimeSpan? requestTimeout = null)
    {
        this.transport = transport;
        this.requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(5);
        this.transport.NotificationReceived += OnNotification;
    }

    public Krly.Battery? LastBattery => lastBattery;

    private void OnNotification(byte[] raw)
    {
        foreach (var frame in reader.Feed(raw))
        {
            if (frame[1] == Krly.QueryBattery && Krly.ParseBattery(frame) is { } b)
                lastBattery = b;

            Action<byte[]>? curWaiting;
            TaskCompletionSource? curDone;
            Action<byte[]>? curAsync;
            lock (waiterLock)
            {
                curWaiting = waiting;
                curDone = done;
                curAsync = asyncListener;
            }

            try
            {
                curAsync?.Invoke(frame);
            }
            catch (Exception error)
            {
                curDone?.TrySetException(error);
            }

            if (curWaiting != null)
            {
                try { curWaiting(frame); }
                catch (Exception error) { curDone?.TrySetException(error); }
            }
        }
    }

    public async Task AskAsync(byte command, byte[] payload, Func<byte[], bool> accept, CancellationToken cancel = default)
    {
        await gate.WaitAsync(cancel);
        try
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (waiterLock)
            {
                done = tcs;
                waiting = frame =>
                {
                    if (accept(frame))
                        tcs.TrySetResult();
                };
            }
            try
            {
                await WriteFrameAsync(command, payload, cancel);
                await tcs.Task.WaitAsync(requestTimeout, cancel);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("The X10 didn't answer in time. Nothing was retried.");
            }
            finally
            {
                lock (waiterLock) { waiting = null; done = null; }
            }
        }
        finally { gate.Release(); }
    }

    public Task AskAckAsync(byte command, byte[] payload, CancellationToken cancel = default) =>
        AskAsync(command, payload, frame =>
        {
            if (frame[1] != command) return false;
            if (!Krly.IsAck(frame, command))
                throw new IOException($"X10 rejected command 0x{command:X2} (status 0x{(frame.Length >= 3 ? frame[2] : 0xFF):X2}).");
            return true;
        }, cancel);

    public async Task SendAsync(byte command, byte[] payload, CancellationToken cancel = default)
    {
        await gate.WaitAsync(cancel);
        try { await WriteFrameAsync(command, payload, cancel); }
        finally { gate.Release(); }
    }

    private async Task WriteFrameAsync(byte command, byte[] payload, CancellationToken cancel)
    {
        var frame = Krly.Frame(command, payload, sequence++);
        for (int at = 0; at < frame.Length; at += Krly.ChunkSize)
        {
            int len = Math.Min(Krly.ChunkSize, frame.Length - at);
            await transport.WriteChunkAsync(frame[at..(at + len)], cancel);
        }
    }

    public async Task<X10HardwareSnapshot> ReadSnapshotAsync(CancellationToken cancel = default)
    {
        Krly.Version? version = null;
        await AskAsync(Krly.QueryVersion, [0x01, 0x00], f => f[1] == Krly.QueryVersion && (version = Krly.ParseVersion(f)) != null, cancel);
        await AskAsync(Krly.QueryBattery, [], f => f[1] == Krly.QueryBattery && lastBattery != null, cancel);

        var settingsPages = new Krly.SettingsPages();
        Krly.Settings? settings = null;
        await AskAsync(Krly.QuerySettings, [0x02, 0x01], f => f[1] == Krly.QuerySettings && (settings = settingsPages.Accept(f)) != null, cancel);

        var mappingPages = new Krly.MappingPages();
        IReadOnlyDictionary<string, string>? rawMappings = null;
        await AskAsync(Krly.QueryMappings, [0x01], f => f[1] == Krly.QueryMappings && (rawMappings = mappingPages.Accept(f)) != null, cancel);

        var macroPages = new Krly.MacroPages();
        IReadOnlyDictionary<string, Krly.PaddleAssignment>? rawMacros = null;
        await AskAsync(Krly.QueryMacros, [0x01, 0x01], f => f[1] == Krly.QueryMacros && (rawMacros = macroPages.Accept(f)) != null, cancel);

        return X10HardwareSnapshot.FromRaw(transport.DeviceName, version!, lastBattery, settings!, rawMappings!, rawMacros!);
    }

    public async Task<X10SyncReport> UploadAndVerifyAsync(
        X10HardwareProfile profile,
        Action<string>? progress = null,
        CancellationToken cancel = default)
    {
        var plan = Krly.BuildUploadPlan(profile.Settings, profile.FaceMappings, profile.Paddles);
        bool inEditMode = false;
        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                var (cmd, payload) = plan[i];
                progress?.Invoke($"Writing X10 configuration ({i + 1}/{plan.Count})...");
                await AskAckAsync(cmd, payload, cancel);
                if (cmd == Krly.SetMode && payload is [0x10]) inEditMode = true;
                else if (cmd == Krly.SetMode && payload is [0x00]) inEditMode = false;
            }
        }
        finally
        {
            if (inEditMode)
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                    await AskAckAsync(Krly.SetMode, [0x00], cleanup.Token);
                }
                catch { }
            }
        }

        try
        {
            progress?.Invoke("Reading back X10 memory to verify...");
            var readBack = await ReadSnapshotAsync(cancel);
            var diffs = X10ProfileBridge.Compare(profile, readBack);
            return new X10SyncReport(
                diffs.Count == 0 ? Krly.VerificationStatus.Matched : Krly.VerificationStatus.Mismatch,
                diffs,
                readBack);
        }
        catch (Exception) when (!cancel.IsCancellationRequested)
        {
            return new X10SyncReport(Krly.VerificationStatus.Unconfirmed, [], null);
        }
    }

    public Task SendMotorPreviewAsync(int left, int right, byte channel, CancellationToken cancel = default) =>
        SendAsync(Krly.MotorTest, Krly.MotorTestPayload(left, right, channel), cancel);

    public async Task DiagnosticMotorTestAsync(bool left, int strength, int durationMs = 500, CancellationToken cancel = default)
    {
        var startPayload = left ? Krly.MotorTestLeftPayload(strength) : Krly.MotorTestRightPayload(strength);
        await AskAckAsync(Krly.MotorTest, startPayload, cancel);
        try
        {
            await Task.Delay(durationMs, cancel);
        }
        finally
        {
            try
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                await SendAsync(Krly.MotorTest, Krly.MotorStopPayload(), stopCts.Token);
            }
            catch { }
        }
    }

    public Task FactoryResetAsync(CancellationToken cancel = default) =>
        AskAckAsync(Krly.FactoryReset, Krly.FactoryResetPayload(), cancel);

    public async Task<Krly.CalibrationEvent> RunCalibrationAsync(
        Krly.CalibrationTarget target,
        Action<Krly.CalibrationEvent>? onEvent,
        TimeSpan? timeout = null,
        CancellationToken cancel = default)
    {
        var finished = new TaskCompletionSource<Krly.CalibrationEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (waiterLock)
        {
            asyncListener = frame =>
            {
                var ev = Krly.ParseCalibrationEvent(frame);
                if (ev == null) return;
                onEvent?.Invoke(ev);
                if (ev.Phase == Krly.CalibrationPhase.Succeeded)
                    finished.TrySetResult(ev);
            };
        }
        try
        {
            await SendAsync(Krly.Calibrate, Krly.CalibrationPayload(start: true, target), cancel);
            return await finished.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(90), cancel);
        }
        finally
        {
            lock (waiterLock) { asyncListener = null; }
            try
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                await SendAsync(Krly.Calibrate, Krly.CalibrationPayload(start: false, target), stopCts.Token);
            }
            catch { }
        }
    }

    public async Task<bool> RunSelfCalibrationAsync(TimeSpan? timeout = null, CancellationToken cancel = default)
    {
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool started = false;
        lock (waiterLock)
        {
            asyncListener = frame =>
            {
                if (Krly.ParseSelfCalibrationResult(frame) is { } ok)
                    finished.TrySetResult(ok);
            };
        }
        try
        {
            await AskAckAsync(Krly.SelfCalStart, [], cancel);
            started = true;
            return await finished.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(45), cancel);
        }
        finally
        {
            lock (waiterLock) { asyncListener = null; }
            if (started)
            {
                try
                {
                    using var stopCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                    await AskAckAsync(Krly.SelfCalStop, [], stopCts.Token);
                }
                catch { }
            }
        }
    }

    public void Dispose()
    {
        transport.NotificationReceived -= OnNotification;
        gate.Dispose();
    }
}

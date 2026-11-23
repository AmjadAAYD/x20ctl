using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace X20Ctl.Product.Diagnostics;

/// <summary>
/// Trigger rumble and 4-motor audio haptics via Chromium/Edge WebView2 Gamepad API
/// (<c>vibrationActuator.playEffect("trigger-rumble")</c>), plus low-overhead streaming over
/// <c>PostWebMessageAsJson</c> for simultaneous Left Grip, Right Grip, Left Trigger, and Right Trigger playback.
/// </summary>
internal sealed class BrowserTriggerRumble
{
    /// <summary>The app's one browser (null in reviews, which never touch the real controller).</summary>
    public static BrowserTriggerRumble? Shared { get; set; }

    const string Page = """
        <!doctype html><html><body style="margin:0;background:transparent">
        <script>
        let cached = null;
        let streamOk = false;
        let lastFocus = document.hasFocus();
        let lastStreamTag = "";
        const supportsTriggerEffect = g => {
          if (!g || !g.vibrationActuator) return false;
          const fx = g.vibrationActuator.effects;
          if (Array.isArray(fx)) return fx.includes("trigger-rumble");
          const id = (g.id || "").toLowerCase();
          if (id.includes("360") || id.includes("xinput standard gamepad")) return false;
          return g.vibrationActuator.type === "trigger-rumble";
        };
        const pad = () => {
          const pads = [...navigator.getGamepads()].filter(p => p && p.vibrationActuator);
          return pads.find(supportsTriggerEffect) || pads[0] || cached;
        };
        const tell = m => { try { window.chrome.webview.postMessage(m); } catch (e) {} };
        const tellStream = tag => {
          if (tag !== lastStreamTag) {
            lastStreamTag = tag;
            tell(tag);
          }
        };
        const syncFocus = force => {
          const f = document.hasFocus();
          if (force || f !== lastFocus) {
            lastFocus = f;
            if (!f) { streamOk = false; lastStreamTag = ""; }
            tell(f ? "focus-on" : "focus-off");
          }
          return f;
        };
        const reportArmed = g => {
          cached = g;
          const hasTrig = supportsTriggerEffect(g);
          const id = (g.id || "").trim();
          tell((hasTrig ? "armed:trigger:" : "armed:dual:") + id);
          syncFocus(true);
        };
        const arm = () => {
          const g = pad();
          if (g && (!cached || g.id !== cached.id || supportsTriggerEffect(g) !== supportsTriggerEffect(cached)))
            reportArmed(g);
        };
        addEventListener("focus", () => syncFocus(true));
        addEventListener("blur", () => { streamOk = false; lastStreamTag = ""; syncFocus(true); });
        addEventListener("gamepadconnected", e => { if (e.gamepad && e.gamepad.vibrationActuator) reportArmed(e.gamepad); });
        addEventListener("gamepaddisconnected", () => { cached = null; streamOk = false; lastStreamTag = ""; tell("disarmed"); });
        setInterval(arm, 450);

        window.buzz = (lt, rt, ms) => {
          const g = pad();
          if (!g) return "no gamepad";
          if (!supportsTriggerEffect(g)) return "unsupported";
          try { window.focus(); } catch (e) {}
          g.vibrationActuator.playEffect("trigger-rumble", {
            duration: ms, startDelay: 0, strongMagnitude: 0, weakMagnitude: 0, leftTrigger: lt, rightTrigger: rt
          }).catch(() => {});
          return "sent";
        };

        window.buzz4 = (lg, rg, lt, rt, ms) => {
          const g = pad();
          if (!g) return "no gamepad";
          if (!supportsTriggerEffect(g)) return "unsupported";
          try { window.focus(); } catch (e) {}
          g.vibrationActuator.playEffect("trigger-rumble", {
            duration: ms, startDelay: 0, strongMagnitude: lg, weakMagnitude: rg, leftTrigger: lt, rightTrigger: rt
          }).catch(() => {});
          return "sent";
        };

        window.chrome.webview.addEventListener("message", ev => {
          const d = ev.data;
          if (!d) return;
          const g = pad();
          if (!g) { streamOk = false; lastStreamTag = ""; tell("disarmed"); return; }
          if (d.reset) {
            streamOk = false;
            lastStreamTag = "";
            try { if (typeof g.vibrationActuator.reset === "function") g.vibrationActuator.reset(); } catch (e) {}
            if (document.hasFocus() && supportsTriggerEffect(g)) {
              g.vibrationActuator.playEffect("trigger-rumble", {
                duration: 40, startDelay: 0, strongMagnitude: 0, weakMagnitude: 0, leftTrigger: 0, rightTrigger: 0
              }).catch(() => {});
            }
            tell("stream-idle");
            return;
          }
          if (!supportsTriggerEffect(g)) {
            streamOk = false;
            tellStream("stream-err:NotSupportedError");
            return;
          }
          syncFocus(false);
          const effectType = d.dual ? "dual-rumble" : "trigger-rumble";
          g.vibrationActuator.playEffect(effectType, {
            duration: d.ms || 160,
            startDelay: 0,
            strongMagnitude: d.lg || 0,
            weakMagnitude: d.rg || 0,
            leftTrigger: d.lt || 0,
            rightTrigger: d.rt || 0
          }).then(res => {
            if ((res === "complete" || res === "preempted") && document.hasFocus()) {
              streamOk = true;
              tellStream("stream-ok:" + res);
            } else {
              streamOk = false;
              tellStream("stream-blocked:" + (res || "unknown"));
            }
          }).catch(err => {
            streamOk = false;
            tellStream("stream-err:" + (err && err.name ? err.name : "err"));
          });
        });
        </script></body></html>
        """;

    public WebView2 View { get; } = new()
    {
        Width = 2,
        Height = 2,
        DefaultBackgroundColor = System.Drawing.Color.Transparent,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Focusable = true,
    };

    Task? ready;
    Window? trackedWindow;
    int streamQueued;
    long waitingToArmUntilTick;
    long lastFocusAttemptTick;
    long lastUiFocusKickTick;

    /// <summary>True once the browser has seen the controller (a button was pressed on it); trigger rumble works from then on.</summary>
    public bool Armed { get; private set; }
    /// <summary>True while Chromium's document focus allows trigger-rumble playback.</summary>
    public bool StreamLive { get; private set; }
    /// <summary>True while WebView2's inner DOM reports <c>document.hasFocus()</c>.</summary>
    public bool HasDocumentFocus { get; private set; }
    /// <summary>True when the host WPF window is currently the active foreground window.</summary>
    public bool IsHostWindowActive { get; private set; }
    /// <summary>True when the detected Web Gamepad exposes <c>trigger-rumble</c>.</summary>
    public bool SupportsTriggerRumble { get; private set; }
    /// <summary>Browser Gamepad API <c>gamepad.id</c> string reported on arm.</summary>
    public string GamepadId { get; private set; } = "";
    /// <summary>Last status token returned by Chromium's <c>playEffect</c> promise.</summary>
    public string LastStreamState { get; private set; } = "idle";
    /// <summary>True during the 20s window after Listen() when controller presses should arm WebView2 rather than navigate UI.</summary>
    public bool WaitingToArm => !Armed && waitingToArmUntilTick != 0 && Environment.TickCount64 < waitingToArmUntilTick;

    public event Action? ArmedChanged;
    public event Action? StateChanged;

    /// <summary>Why the browser couldn't start (no WebView2 runtime, say), or null.</summary>
    public string? Failure { get; private set; }

    /// <summary>Starts the browser (its working files go in the temp folder, not the user's app data). Never throws: a
    /// failure is kept in <see cref="Failure"/> and trigger rumble stays off.</summary>
    public Task Ready() => ready ??= StartSafely();

    async Task StartSafely()
    {
        try { await Start(); }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Failure = error is WebView2RuntimeNotFoundException ? "Trigger rumble needs the Microsoft Edge WebView2 Runtime." : "Trigger rumble couldn't start: " + error.Message;
            StateChanged?.Invoke();
        }
    }

    async Task Start()
    {
        HookHostWindowOnUiThread();
        var folder = Path.Combine(Path.GetTempPath(), "x20ctl-trigger-webview");
        var environment = await CoreWebView2Environment.CreateAsync(null, folder);
        await View.EnsureCoreWebView2Async(environment);
        View.CoreWebView2.WebMessageReceived += (_, e) =>
        {
            string msg = e.TryGetWebMessageAsString();
            bool armedChanged = false;
            switch (msg)
            {
                case "armed":
                case "armed:trigger":
                    if (!SupportsTriggerRumble) { SupportsTriggerRumble = true; armedChanged = true; }
                    waitingToArmUntilTick = 0;
                    if (!Armed) { Armed = true; armedChanged = true; }
                    break;
                case "armed:dual":
                    if (SupportsTriggerRumble) { SupportsTriggerRumble = false; armedChanged = true; }
                    waitingToArmUntilTick = 0;
                    if (!Armed) { Armed = true; armedChanged = true; }
                    break;
                case "disarmed":
                    HasDocumentFocus = false;
                    StreamLive = false;
                    SupportsTriggerRumble = false;
                    GamepadId = "";
                    LastStreamState = "disarmed";
                    if (Armed) { Armed = false; armedChanged = true; }
                    break;
                case "focus-on":
                    HasDocumentFocus = true;
                    break;
                case "focus-off":
                    HasDocumentFocus = false;
                    StreamLive = false;
                    LastStreamState = "unfocused";
                    break;
                case "stream-idle":
                    StreamLive = false;
                    LastStreamState = "idle";
                    break;
                default:
                    if (msg.StartsWith("armed:trigger:", StringComparison.Ordinal))
                    {
                        GamepadId = msg["armed:trigger:".Length..];
                        if (!SupportsTriggerRumble) { SupportsTriggerRumble = true; armedChanged = true; }
                        waitingToArmUntilTick = 0;
                        if (!Armed) { Armed = true; armedChanged = true; }
                    }
                    else if (msg.StartsWith("armed:dual:", StringComparison.Ordinal))
                    {
                        GamepadId = msg["armed:dual:".Length..];
                        if (SupportsTriggerRumble) { SupportsTriggerRumble = false; armedChanged = true; }
                        waitingToArmUntilTick = 0;
                        if (!Armed) { Armed = true; armedChanged = true; }
                    }
                    else if (msg.StartsWith("stream-ok:", StringComparison.Ordinal))
                    {
                        LastStreamState = msg["stream-ok:".Length..];
                        StreamLive = HasDocumentFocus;
                    }
                    else if (msg.StartsWith("stream-blocked:", StringComparison.Ordinal))
                    {
                        LastStreamState = "blocked:" + msg["stream-blocked:".Length..];
                        StreamLive = false;
                    }
                    else if (msg.StartsWith("stream-err:", StringComparison.Ordinal))
                    {
                        LastStreamState = msg;
                        StreamLive = false;
                    }
                    break;
            }
            if (armedChanged) ArmedChanged?.Invoke();
            StateChanged?.Invoke();
        };
        var loaded = new TaskCompletionSource();
        View.CoreWebView2.NavigationCompleted += (_, _) => loaded.TrySetResult();
        View.CoreWebView2.NavigateToString(Page);
        await loaded.Task;
        HookHostWindowOnUiThread();
    }

    public void SyncHostWindowActiveOnUiThread() => HookHostWindowOnUiThread();

    void HookHostWindowOnUiThread()
    {
        if (Window.GetWindow(View) is not { } win) return;
        IsHostWindowActive = win.IsActive;
        if (ReferenceEquals(trackedWindow, win)) return;
        if (trackedWindow != null)
        {
            trackedWindow.Activated -= OnHostActivated;
            trackedWindow.Deactivated -= OnHostDeactivated;
        }
        trackedWindow = win;
        win.Activated += OnHostActivated;
        win.Deactivated += OnHostDeactivated;
    }

    void OnHostActivated(object? sender, EventArgs e)
    {
        IsHostWindowActive = true;
        StateChanged?.Invoke();
    }

    void OnHostDeactivated(object? sender, EventArgs e)
    {
        IsHostWindowActive = false;
        HasDocumentFocus = false;
        StreamLive = false;
        LastStreamState = "background";
        StateChanged?.Invoke();
    }

    /// <summary>Gives the browser focus so the next controller button press arms it (the hint's "turn on" step).</summary>
    public async Task Listen()
    {
        await Ready();
        if (Failure != null) return;
        HookHostWindowOnUiThread();
        if (!Armed) waitingToArmUntilTick = Environment.TickCount64 + 20_000;
        if (View.IsKeyboardFocusWithin && !HasDocumentFocus)
            Keyboard.ClearFocus();
        View.Focus();
        Keyboard.Focus(View);
        try { _ = View.CoreWebView2?.ExecuteScriptAsync("window.focus()"); } catch { }
        StateChanged?.Invoke();
    }

    public void CancelListen()
    {
        waitingToArmUntilTick = 0;
        StateChanged?.Invoke();
    }

    /// <summary>Trigger rumble for <paramref name="ms"/> (0..1 each side). The browser gets focus for the moment it's
    /// sent, then whatever text input had focus (the agent's text box, say) gets it back.</summary>
    public async Task<bool> Play(double left, double right, int ms)
    {
        if (!Armed || !SupportsTriggerRumble || Failure != null) return false;
        await Ready();
        if (Failure != null || !Armed || !SupportsTriggerRumble) return false;
        HookHostWindowOnUiThread();
        var had = Keyboard.FocusedElement as IInputElement;
        if (View.IsKeyboardFocusWithin && !HasDocumentFocus)
            Keyboard.ClearFocus();
        View.Focus();
        Keyboard.Focus(View);
        string sent;
        try
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            sent = JsonSerializer.Deserialize<string>(await View.CoreWebView2.ExecuteScriptAsync(
                $"buzz({Math.Clamp(left, 0, 1).ToString(inv)},{Math.Clamp(right, 0, 1).ToString(inv)},{Math.Clamp(ms, 40, 2000)})")) ?? "";
        }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException or JsonException) { sent = ""; }
        finally
        {
            if (had is TextBoxBase or ComboBox or PasswordBox && !ReferenceEquals(had, View))
                Keyboard.Focus(had);
        }
        return sent == "sent";
    }

    /// <summary>Simultaneous 4-motor one-shot pulse for Audio Studio testing.</summary>
    public async Task<bool> Play4(double leftGrip, double rightGrip, double leftTrigger, double rightTrigger, int ms)
    {
        if (!Armed || !SupportsTriggerRumble || Failure != null) return false;
        await Ready();
        if (Failure != null || !Armed || !SupportsTriggerRumble) return false;
        HookHostWindowOnUiThread();
        var had = Keyboard.FocusedElement as IInputElement;
        if (View.IsKeyboardFocusWithin && !HasDocumentFocus)
            Keyboard.ClearFocus();
        View.Focus();
        Keyboard.Focus(View);
        string sent;
        try
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            sent = JsonSerializer.Deserialize<string>(await View.CoreWebView2.ExecuteScriptAsync(
                $"buzz4({Math.Clamp(leftGrip, 0, 1).ToString(inv)},{Math.Clamp(rightGrip, 0, 1).ToString(inv)},{Math.Clamp(leftTrigger, 0, 1).ToString(inv)},{Math.Clamp(rightTrigger, 0, 1).ToString(inv)},{Math.Clamp(ms, 40, 2000)})")) ?? "";
        }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException or JsonException) { sent = ""; }
        finally
        {
            if (had is TextBoxBase or ComboBox or PasswordBox && !ReferenceEquals(had, View))
                Keyboard.Focus(had);
        }
        return sent == "sent";
    }

    bool TryFocusViewIfSafeOnUiThread(bool force = false)
    {
        HookHostWindowOnUiThread();
        if (Failure != null || !Armed || !SupportsTriggerRumble) return false;
        if (!IsHostWindowActive) return false;
        if (View.IsKeyboardFocusWithin && HasDocumentFocus) return true;
        if (Mouse.Captured != null || Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed)
            return false;
        if (Keyboard.FocusedElement is TextBoxBase or ComboBox or PasswordBox)
            return false;
        long now = Environment.TickCount64;
        if (!force && now - Volatile.Read(ref lastUiFocusKickTick) < 500)
            return View.IsKeyboardFocusWithin;
        Volatile.Write(ref lastUiFocusKickTick, now);
        if (View.IsKeyboardFocusWithin && !HasDocumentFocus)
            Keyboard.ClearFocus();
        bool focused = View.Focus();
        Keyboard.Focus(View);
        try { _ = View.CoreWebView2?.ExecuteScriptAsync("window.focus()"); } catch { }
        return focused || View.IsKeyboardFocusWithin || HasDocumentFocus;
    }

    /// <summary>Gently restores focus to WebView2 after clicking Audio Studio buttons so trigger rumble stays active.</summary>
    public void EnsureFocusForAudioTriggers()
    {
        if (!Armed || Failure != null || !SupportsTriggerRumble) return;
        long now = Environment.TickCount64;
        if (now - Volatile.Read(ref lastFocusAttemptTick) < 300) return;
        Volatile.Write(ref lastFocusAttemptTick, now);
        try
        {
            View.Dispatcher.BeginInvoke(() => TryFocusViewIfSafeOnUiThread(force: true), DispatcherPriority.Background);
        }
        catch { }
    }

    /// <summary>Non-blocking 4-motor stream packet (<c>PostWebMessageAsJson</c>) when host window is active.</summary>
    public void Stream4(double leftGrip, double rightGrip, double leftTrigger, double rightTrigger, int ms = 240)
    {
        if (!Armed || Failure != null || !SupportsTriggerRumble) return;
        if (Interlocked.CompareExchange(ref streamQueued, 1, 0) != 0) return;

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string json = string.Format(
            inv,
            "{{\"lg\":{0:0.###},\"rg\":{1:0.###},\"lt\":{2:0.###},\"rt\":{3:0.###},\"ms\":{4}}}",
            Math.Clamp(leftGrip, 0.0, 1.0),
            Math.Clamp(rightGrip, 0.0, 1.0),
            Math.Clamp(leftTrigger, 0.0, 1.0),
            Math.Clamp(rightTrigger, 0.0, 1.0),
            Math.Clamp(ms, 40, 1000));

        try
        {
            View.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    HookHostWindowOnUiThread();
                    if (!IsHostWindowActive)
                    {
                        HasDocumentFocus = false;
                        StreamLive = false;
                        LastStreamState = "background";
                        return;
                    }
                    if (!HasDocumentFocus && !TryFocusViewIfSafeOnUiThread())
                    {
                        StreamLive = false;
                        LastStreamState = "ui-busy";
                        return;
                    }
                    View.CoreWebView2?.PostWebMessageAsJson(json);
                }
                catch { StreamLive = false; }
                finally { Volatile.Write(ref streamQueued, 0); }
            });
        }
        catch
        {
            Volatile.Write(ref streamQueued, 0);
        }
    }

    /// <summary>Immediately stops any ongoing WebView2 trigger/grip stream.</summary>
    public void StopStream()
    {
        StreamLive = false;
        Volatile.Write(ref streamQueued, 0);
        if (!Armed || Failure != null) return;
        try
        {
            View.Dispatcher.BeginInvoke(() =>
            {
                try { View.CoreWebView2?.PostWebMessageAsJson("{\"reset\":true}"); }
                catch { }
            });
        }
        catch { }
    }
}

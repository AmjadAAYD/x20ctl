using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace X20Ctl.Product.Diagnostics;

/// <summary>
/// Trigger rumble and 4-motor audio haptics via Chromium/Edge WebView2 Gamepad API
/// (<c>vibrationActuator.playEffect("trigger-rumble")</c>), plus zero-focus-steal streaming over
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
        const pad = () => [...navigator.getGamepads()].find(p => p && p.vibrationActuator) || cached;
        const tell = m => { try { window.chrome.webview.postMessage(m); } catch (e) {} };
        const arm = () => {
          const g = pad();
          if (g) { cached = g; tell("armed"); }
        };
        addEventListener("gamepadconnected", e => { if (e.gamepad.vibrationActuator) { cached = e.gamepad; tell("armed"); } });
        addEventListener("gamepaddisconnected", () => { cached = null; streamOk = false; tell("disarmed"); });
        setInterval(() => { if (!cached) arm(); }, 450);

        window.buzz = (lt, rt, ms) => {
          const g = pad();
          if (!g) return "no gamepad";
          g.vibrationActuator.playEffect("trigger-rumble", {
            duration: ms, startDelay: 0, strongMagnitude: 0, weakMagnitude: 0, leftTrigger: lt, rightTrigger: rt
          }).catch(() => {});
          return "sent";
        };

        window.chrome.webview.addEventListener("message", ev => {
          const d = ev.data;
          if (!d) return;
          const g = pad();
          if (!g) return;
          if (d.reset) {
            streamOk = false;
            try { if (typeof g.vibrationActuator.reset === "function") g.vibrationActuator.reset(); } catch (e) {}
            g.vibrationActuator.playEffect("trigger-rumble", {
              duration: 40, startDelay: 0, strongMagnitude: 0, weakMagnitude: 0, leftTrigger: 0, rightTrigger: 0
            }).catch(() => {});
            tell("stream-idle");
            return;
          }
          g.vibrationActuator.playEffect("trigger-rumble", {
            duration: d.ms || 150,
            startDelay: 0,
            strongMagnitude: d.lg || 0,
            weakMagnitude: d.rg || 0,
            leftTrigger: d.lt || 0,
            rightTrigger: d.rt || 0
          }).then(() => {
            if (!streamOk) { streamOk = true; tell("stream-ok"); }
          }).catch(() => {
            if (streamOk) { streamOk = false; tell("stream-blocked"); }
          });
        });
        </script></body></html>
        """;

    public WebView2 View { get; } = new() { Width = 2, Height = 2, DefaultBackgroundColor = System.Drawing.Color.Transparent, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Focusable = true };
    Task? ready;
    int streamQueued;

    /// <summary>True once the browser has seen the controller (a button was pressed on it); trigger rumble works from then on.</summary>
    public bool Armed { get; private set; }
    /// <summary>True while Chromium's active focus policy accepts zero-focus-steal 4-motor stream packets.</summary>
    public bool StreamLive { get; private set; }
    public event Action? ArmedChanged;

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
        }
    }

    async Task Start()
    {
        var folder = Path.Combine(Path.GetTempPath(), "x20ctl-trigger-webview");
        var environment = await CoreWebView2Environment.CreateAsync(null, folder);
        await View.EnsureCoreWebView2Async(environment);
        View.CoreWebView2.WebMessageReceived += (_, e) =>
        {
            string msg = e.TryGetWebMessageAsString();
            switch (msg)
            {
                case "armed":
                    if (!Armed) { Armed = true; ArmedChanged?.Invoke(); }
                    break;
                case "disarmed":
                    StreamLive = false;
                    if (Armed) { Armed = false; ArmedChanged?.Invoke(); }
                    break;
                case "stream-ok":
                    StreamLive = true;
                    break;
                case "stream-blocked":
                case "stream-idle":
                    StreamLive = false;
                    break;
            }
        };
        var loaded = new TaskCompletionSource();
        View.CoreWebView2.NavigationCompleted += (_, _) => loaded.TrySetResult();
        View.CoreWebView2.NavigateToString(Page);
        await loaded.Task;
    }

    /// <summary>Gives the browser focus so the next controller button press arms it (the hint's "turn on" step).</summary>
    public async Task Listen() { await Ready(); if (Failure == null) View.Focus(); }

    /// <summary>Trigger rumble for <paramref name="ms"/> (0..1 each side). The browser gets focus for the moment it's
    /// sent, then whatever had focus (the agent's text box, say) gets it back.</summary>
    public async Task<bool> Play(double left, double right, int ms)
    {
        if (!Armed) return false;
        await Ready();
        if (Failure != null) return false;
        var had = Keyboard.FocusedElement as IInputElement;
        View.Focus();
        string sent;
        try
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            sent = JsonSerializer.Deserialize<string>(await View.CoreWebView2.ExecuteScriptAsync($"buzz({left.ToString(inv)},{right.ToString(inv)},{ms})")) ?? "";
        }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException or JsonException) { sent = ""; }
        finally { if (had != null && !ReferenceEquals(had, View)) Keyboard.Focus(had); }
        return sent == "sent";
    }

    /// <summary>Non-blocking 4-motor stream packet (<c>PostWebMessageAsJson</c>) without stealing keyboard focus.</summary>
    public void Stream4(double leftGrip, double rightGrip, double leftTrigger, double rightTrigger, int ms = 160)
    {
        if (!Armed || Failure != null) return;
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
                try { View.CoreWebView2?.PostWebMessageAsJson(json); }
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

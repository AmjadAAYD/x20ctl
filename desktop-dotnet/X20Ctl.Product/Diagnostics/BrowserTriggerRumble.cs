using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace X20Ctl.Product.Diagnostics;

/// <summary>
/// Trigger rumble the way the gamepad test site does it (owner, 9 Nov 2026: the site buzzes the triggers every time, while
/// X20CTL's own Windows.Gaming.Input and GameInput calls were accepted and dropped): a tiny, invisible Edge WebView2 inside
/// X20CTL runs the web Gamepad API, vibrationActuator.playEffect("trigger-rumble"). Like any browser, it only sees the
/// controller after a button on it has been pressed while the browser had focus; from then on it's "armed" until X20CTL
/// closes. One shared instance, hosted by the main window.
/// </summary>
internal sealed class BrowserTriggerRumble
{
    /// <summary>The app's one browser (null in reviews, which never touch the real controller).</summary>
    public static BrowserTriggerRumble? Shared { get; set; }

    const string Page = """
        <!doctype html><html><body style="margin:0;background:transparent">
        <script>
        let cached = null;
        const pad = () => [...navigator.getGamepads()].find(p => p && p.vibrationActuator) || cached;
        const tell = m => { try { window.chrome.webview.postMessage(m); } catch (e) {} };
        addEventListener("gamepadconnected", e => { if (e.gamepad.vibrationActuator) { cached = e.gamepad; tell("armed"); } });
        addEventListener("gamepaddisconnected", () => { cached = null; tell("disarmed"); });
        window.buzz = (lt, rt, ms) => {
          const g = pad();
          if (!g) return "no gamepad";
          g.vibrationActuator.playEffect("trigger-rumble", { duration: ms, startDelay: 0, strongMagnitude: 0, weakMagnitude: 0, leftTrigger: lt, rightTrigger: rt }).catch(() => {});
          return "sent";
        };
        </script></body></html>
        """;

    public WebView2 View { get; } = new() { Width = 2, Height = 2, DefaultBackgroundColor = System.Drawing.Color.Transparent, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Focusable = true };
    Task? ready;

    /// <summary>True once the browser has seen the controller (a button was pressed on it); trigger rumble works from then on.</summary>
    public bool Armed { get; private set; }
    public event Action? ArmedChanged;

    /// <summary>Starts the browser (its working files go in the temp folder, not the user's app data).</summary>
    public Task Ready() => ready ??= Start();

    async Task Start()
    {
        var folder = Path.Combine(Path.GetTempPath(), "x20ctl-trigger-webview");
        var environment = await CoreWebView2Environment.CreateAsync(null, folder);
        await View.EnsureCoreWebView2Async(environment);
        View.CoreWebView2.WebMessageReceived += (_, e) =>
        {
            bool armed = e.TryGetWebMessageAsString() == "armed";
            if (armed == Armed) return;
            Armed = armed; ArmedChanged?.Invoke();
        };
        var loaded = new TaskCompletionSource();
        View.CoreWebView2.NavigationCompleted += (_, _) => loaded.TrySetResult();
        View.CoreWebView2.NavigateToString(Page);
        await loaded.Task;
    }

    /// <summary>Gives the browser focus so the next controller button press arms it (the hint's "turn on" step).</summary>
    public async Task Listen() { await Ready(); View.Focus(); }

    /// <summary>Trigger rumble for <paramref name="ms"/> (0..1 each side). The browser gets focus for the moment it's
    /// sent, then whatever had focus (the agent's text box, say) gets it back.</summary>
    public async Task<bool> Play(double left, double right, int ms)
    {
        if (!Armed) return false;
        await Ready();
        var had = Keyboard.FocusedElement as IInputElement;
        View.Focus();
        string sent;
        try
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            sent = JsonSerializer.Deserialize<string>(await View.CoreWebView2.ExecuteScriptAsync($"buzz({left.ToString(inv)},{right.ToString(inv)},{ms})")) ?? "";
        }
        finally { if (had != null && !ReferenceEquals(had, View)) Keyboard.Focus(had); }
        return sent == "sent";
    }
}

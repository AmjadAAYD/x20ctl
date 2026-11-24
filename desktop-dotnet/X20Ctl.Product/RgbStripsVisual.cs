using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>
/// Live addressable LED strip mirror for the X20 Pro RGB page: renders in high-craft
/// HTML5 Canvas inside WebView2 in live desktop runs, with instant native WPF diffuser
/// rendering during startup and headless <see cref="ReviewSandbox"/> captures.
/// </summary>
internal sealed class RgbStripsVisual : Grid
{
    readonly bool animate;
    readonly NativeStripCanvas fallbackCanvas = new();
    WebView2? webView;
    bool webReady;
    bool webStarted;

    byte red = 0x3D, green = 0x8B, blue = 0xFF, brightness = 0x50, hardwareMode = LightingRecord.ModeSolid;
    bool effectActive;
    LightingEffectKind? effectKind;
    TimeSpan started, last;
    bool rendering;

    public RgbStripsVisual(bool animate = true)
    {
        this.animate = animate;
        IsHitTestVisible = false;
        MinHeight = 148;
        ClipToBounds = true;

        Children.Add(fallbackCanvas);

        Loaded += (_, _) =>
        {
            UpdateClock();
            if (!ReviewSandbox.Active && !webStarted && animate)
                _ = InitWebViewAsync();
        };
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) =>
        {
            UpdateClock();
            PushWebState();
        };
    }

    public void SetState(byte r, byte g, byte b, byte bright, byte modeByte, bool isEffect, LightingEffectKind? kind)
    {
        red = r;
        green = g;
        blue = b;
        brightness = bright;
        hardwareMode = modeByte;
        effectActive = isEffect;
        effectKind = kind;

        UpdateClock();
        fallbackCanvas.UpdateState(red, green, blue, brightness, hardwareMode, effectActive, effectKind, 0, animate);
        PushWebState();
    }

    bool NeedsClock => animate && IsVisible && brightness > 0 && (effectActive || hardwareMode is LightingRecord.ModeRainbow or LightingRecord.ModeBreathe or LightingRecord.ModeCycle);

    void UpdateClock()
    {
        if (!NeedsClock)
        {
            Stop();
            fallbackCanvas.UpdateState(red, green, blue, brightness, hardwareMode, effectActive, effectKind, 0, animate);
            return;
        }
        if (!rendering)
        {
            started = TimeSpan.Zero;
            rendering = true;
            CompositionTarget.Rendering += Tick;
        }
    }

    void Stop()
    {
        if (rendering)
        {
            CompositionTarget.Rendering -= Tick;
            rendering = false;
        }
    }

    void Tick(object? sender, EventArgs e)
    {
        if (webReady)
        {
            // WebView2 owns 60 FPS GPU animation once ready; stop redundant WPF redraws.
            Stop();
            return;
        }
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (last != TimeSpan.Zero && (now - last).TotalSeconds < 0.032) return;
        last = now;
        if (started == TimeSpan.Zero) started = now;
        fallbackCanvas.UpdateState(red, green, blue, brightness, hardwareMode, effectActive, effectKind, (now - started).TotalSeconds, true);
    }

    async Task InitWebViewAsync()
    {
        webStarted = true;
        try
        {
            var theme = ThemeCatalog.Current;
            var view = new WebView2
            {
                IsHitTestVisible = false,
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, theme.Void.R, theme.Void.G, theme.Void.B),
                Visibility = Visibility.Collapsed,
            };
            webView = view;
            Children.Add(view);

            string folder = Path.Combine(Path.GetTempPath(), "x20ctl-rgb-strips-webview");
            var env = await CoreWebView2Environment.CreateAsync(null, folder);
            await view.EnsureCoreWebView2Async(env);
            if (!IsLoaded) return;

            view.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess) return;
                webReady = true;
                view.Visibility = Visibility.Visible;
                Stop();
                PushWebState();
            };
            view.CoreWebView2.NavigateToString(BuildHtml(theme));
        }
        catch
        {
            // Keep crisp native WPF fallback seamlessly if WebView2 runtime is unavailable.
            if (webView != null)
            {
                Children.Remove(webView);
                webView.Dispose();
                webView = null;
            }
        }
    }

    void PushWebState()
    {
        if (!webReady || webView?.CoreWebView2 == null) return;
        try
        {
            string modeName = effectActive && effectKind.HasValue
                ? effectKind.Value.ToString()
                : LightingRecord.ModeName(hardwareMode);
            var payload = new
            {
                r = red,
                g = green,
                b = blue,
                brightness = brightness / 255.0,
                mode = modeName,
                off = (!effectActive && hardwareMode == LightingRecord.ModeOff) || brightness == 0,
                visible = IsVisible,
            };
            webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
        }
        catch { }
    }

    static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    static string BuildHtml(AppTheme t) => $$"""
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<style>
  html, body {
    margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden;
    background: {{Hex(t.Void)}};
    font-family: 'Segoe UI Variable Text', 'Segoe UI', sans-serif;
    user-select: none;
  }
  canvas { display: block; width: 100%; height: 100%; }
</style>
</head>
<body>
<canvas id="c"></canvas>
<script>
const canvas = document.getElementById('c');
const ctx = canvas.getContext('2d');
const themeBg = '{{Hex(t.Void)}}';
const themeRule = '{{Hex(t.Rule)}}';
const themeMuted = '{{Hex(t.Muted)}}';
let state = { r: 61, g: 139, b: 255, brightness: 0.35, mode: 'Solid', off: false, visible: true };
let curR = 61, curG = 139, curB = 255, curBr = 0.35;

if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener('message', ev => {
    if (ev.data && typeof ev.data === 'object') state = ev.data;
  });
}

function hsvToRgb(h, s, v) {
  h = ((h % 360) + 360) % 360;
  const c = v * s, x = c * (1 - Math.abs((h / 60) % 2 - 1)), m = v - c;
  let r = 0, g = 0, b = 0;
  if (h < 60) [r, g, b] = [c, x, 0];
  else if (h < 120) [r, g, b] = [x, c, 0];
  else if (h < 180) [r, g, b] = [0, c, x];
  else if (h < 240) [r, g, b] = [0, x, c];
  else if (h < 300) [r, g, b] = [x, 0, c];
  else [r, g, b] = [c, 0, x];
  return [Math.round((r + m) * 255), Math.round((g + m) * 255), Math.round((b + m) * 255)];
}

function ledSample(u, tSec) {
  if (state.off) return [32, 40, 56, 0.14];
  const m = (state.mode || '').toLowerCase();
  let r = curR, g = curG, b = curB, mul = 1.0;
  if (m.includes('rainbow') || m.includes('spectrum')) {
    [r, g, b] = hsvToRgb((u * 260 + tSec * 120) % 360, 0.92, 1.0);
  } else if (m.includes('cycle') || m.includes('fade')) {
    [r, g, b] = hsvToRgb((tSec * 70) % 360, 0.88, 1.0);
  } else if (m.includes('breathe') || m.includes('pulse') || m.includes('moonlight')) {
    mul = 0.25 + 0.75 * (0.5 + 0.5 * Math.sin(tSec * 2.4));
  } else if (m.includes('audio') || m.includes('trigger') || m.includes('stick')) {
    const wave = 0.55 + 0.45 * Math.sin(u * 12 - tSec * 7.2);
    [r, g, b] = hsvToRgb((u * 110 + tSec * 45) % 360, 0.85, 1.0);
    mul = wave;
  } else if (m.includes('strobe')) {
    mul = Math.sin(tSec * 16) > 0 ? 1.0 : 0.12;
  }
  const br = Math.max(0.08, Math.min(1.0, curBr)) * mul;
  return [Math.round(r), Math.round(g), Math.round(b), br];
}

function drawStrip(x, y, w, h, seed, tSec, label) {
  const leds = 18;
  const rMid = ledSample(0.5 + seed * 0.15, tSec);

  ctx.save();
  ctx.globalAlpha = rMid[3] * 0.42;
  const grad = ctx.createRadialGradient(x + w * 0.5, y + h * 0.5, 4, x + w * 0.5, y + h * 0.5, w * 0.62);
  grad.addColorStop(0, `rgb(${rMid[0]},${rMid[1]},${rMid[2]})`);
  grad.addColorStop(1, 'transparent');
  ctx.fillStyle = grad;
  ctx.fillRect(x - 18, y - 18, w + 36, h + 36);
  ctx.restore();

  // Housing channel
  ctx.fillStyle = 'rgba(255,255,255,0.04)';
  ctx.strokeStyle = themeRule;
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.roundRect(x, y, w, h, 4);
  ctx.fill();
  ctx.stroke();

  const pad = 4, innerW = w - pad * 2, gap = 2.5;
  const segW = (innerW - gap * (leds - 1)) / leds;
  for (let i = 0; i < leds; i++) {
    const u = i / (leds - 1);
    const [r, g, b, alpha] = ledSample((u + seed * 0.25) % 1.0, tSec);
    ctx.fillStyle = `rgba(${r},${g},${b},${Math.min(1, 0.20 + alpha * 0.80)})`;
    ctx.beginPath();
    ctx.roundRect(x + pad + i * (segW + gap), y + 4, Math.max(2, segW), h - 8, 3);
    ctx.fill();
  }

  // Soft frosted acrylic diffuser core across the strip
  const diff = ctx.createLinearGradient(x, y, x + w, y);
  const c0 = ledSample(seed * 0.25, tSec), c1 = ledSample((0.5 + seed * 0.25) % 1.0, tSec), c2 = ledSample((1.0 + seed * 0.25) % 1.0, tSec);
  diff.addColorStop(0, `rgba(${c0[0]},${c0[1]},${c0[2]},${c0[3] * 0.34})`);
  diff.addColorStop(0.5, `rgba(${c1[0]},${c1[1]},${c1[2]},${c1[3] * 0.44})`);
  diff.addColorStop(1, `rgba(${c2[0]},${c2[1]},${c2[2]},${c2[3] * 0.34})`);
  ctx.fillStyle = diff;
  ctx.beginPath();
  ctx.roundRect(x + 2, y + 2, w - 4, h - 4, 3);
  ctx.fill();

  ctx.fillStyle = themeMuted;
  ctx.font = '600 9.5px Consolas, monospace';
  ctx.fillText(label, x, y - 6);
}

function render(ms) {
  if (!state.visible) { requestAnimationFrame(render); return; }
  const dpr = window.devicePixelRatio || 1;
  const rect = canvas.getBoundingClientRect();
  if (canvas.width !== Math.round(rect.width * dpr) || canvas.height !== Math.round(rect.height * dpr)) {
    canvas.width = Math.max(1, Math.round(rect.width * dpr));
    canvas.height = Math.max(1, Math.round(rect.height * dpr));
  }
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const W = rect.width, H = rect.height;

  curR += (state.r - curR) * 0.2;
  curG += (state.g - curG) * 0.2;
  curB += (state.b - curB) * 0.2;
  curBr += (state.brightness - curBr) * 0.2;

  ctx.fillStyle = themeBg;
  ctx.fillRect(0, 0, W, H);

  const t = ms * 0.001;
  const m = 18;
  const usableH = H - 30;
  const stripH = Math.max(22, Math.min(34, usableH * 0.26));
  const y1 = Math.max(24, H * 0.28 - stripH * 0.5);
  const y2 = Math.max(y1 + stripH + 26, H * 0.68 - stripH * 0.5);
  drawStrip(m, y1, W - m * 2, stripH, 0.0, t, 'LEFT LED STRIP');
  drawStrip(m, y2, W - m * 2, stripH, 0.5, t, 'RIGHT LED STRIP');

  requestAnimationFrame(render);
}
requestAnimationFrame(render);
</script>
</body>
</html>
""";

    sealed class NativeStripCanvas : FrameworkElement
    {
        static readonly Typeface Mono = new(new FontFamily("Consolas, Cascadia Mono, Courier New"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        byte red = 0x3D, green = 0x8B, blue = 0xFF, brightness = 0x50, hardwareMode = LightingRecord.ModeSolid;
        bool effectActive;
        LightingEffectKind? effectKind;
        double timeSec;
        bool animated;

        public void UpdateState(byte r, byte g, byte b, byte bright, byte modeByte, bool isEffect, LightingEffectKind? kind, double t, bool anim)
        {
            red = r;
            green = g;
            blue = b;
            brightness = bright;
            hardwareMode = modeByte;
            effectActive = isEffect;
            effectKind = kind;
            timeSec = t;
            animated = anim;
            InvalidateVisual();
        }

        static Color Hsv(double h, double s, double v)
        {
            int rgb = ColourPalette.FromHsv((h % 360 + 360) % 360, s, v);
            return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }

        Color SampleLed(double u, double seed, out double alpha)
        {
            bool off = (!effectActive && hardwareMode == LightingRecord.ModeOff) || brightness == 0;
            if (off)
            {
                alpha = 0.18;
                return Color.FromRgb(42, 52, 72);
            }

            double br = Math.Clamp(brightness / 255.0, 0.12, 1.0);
            Color baseCol = Color.FromRgb(red, green, blue);
            if (effectActive && effectKind is LightingEffectKind.SmoothHueWave or LightingEffectKind.NativeRainbow or LightingEffectKind.AudioReactive || !effectActive && hardwareMode == LightingRecord.ModeRainbow)
            {
                baseCol = Hsv((u + seed) * 260 + (animated ? timeSec * 120 : 25), 0.90, 1.0);
            }
            else if (!effectActive && hardwareMode == LightingRecord.ModeCycle || effectActive && effectKind is LightingEffectKind.PaletteFade or LightingEffectKind.NativeCycle)
            {
                baseCol = Hsv(animated ? timeSec * 70 : seed * 90, 0.88, 1.0);
            }
            else if (!effectActive && hardwareMode == LightingRecord.ModeBreathe || effectActive && effectKind is LightingEffectKind.MoonlightPulse)
            {
                br *= animated ? (0.30 + 0.70 * (0.5 + 0.5 * Math.Sin(timeSec * 2.4))) : 0.78;
            }

            alpha = Math.Clamp(0.20 + br * 0.80, 0.18, 1.0);
            return baseCol;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 40 || h < 40) return;

            var t = ThemeCatalog.Current;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            var midCol = SampleLed(0.5, 0.0, out double midA);
            var aura = new RadialGradientBrush(
                Color.FromArgb((byte)Math.Clamp(midA * 72, 0, 110), midCol.R, midCol.G, midCol.B),
                Colors.Transparent);
            aura.Freeze();
            dc.DrawEllipse(aura, null, new Point(w * 0.5, h * 0.52), w * 0.52, h * 0.48);

            string[] labels = ["LEFT LED STRIP", "RIGHT LED STRIP"];
            double marginX = 14;
            double innerW = Math.Max(40, w - marginX * 2);
            double slotH = (h - 14) / 2.0;
            double barH = Math.Clamp(slotH * 0.44, 22, 32);
            const int leds = 16;

            var shellBrush = new SolidColorBrush(Color.FromArgb(145, t.Deep.R, t.Deep.G, t.Deep.B));
            shellBrush.Freeze();
            var rulePen = new Pen(new SolidColorBrush(Color.FromArgb(140, t.Rule.R, t.Rule.G, t.Rule.B)), 1);
            rulePen.Freeze();
            var labelBrush = new SolidColorBrush(Color.FromArgb(205, t.Muted.R, t.Muted.G, t.Muted.B));
            labelBrush.Freeze();

            for (int row = 0; row < 2; row++)
            {
                double topY = 12 + row * slotH;
                var ft = new FormattedText(labels[row], CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Mono, 9.5, labelBrush, dpi);
                dc.DrawText(ft, new Point(marginX, topY));

                double barY = topY + ft.Height + 5;
                dc.DrawRoundedRectangle(shellBrush, rulePen, new Rect(marginX, barY, innerW, barH), 5, 5);

                double pad = 4;
                double gap = 3;
                double ledW = Math.Max(2, (innerW - pad * 2 - gap * (leds - 1)) / leds);
                for (int i = 0; i < leds; i++)
                {
                    var ledCol = SampleLed(i / (double)(leds - 1), row * 0.5, out double la);
                    var b = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(la * 255, 25, 255), ledCol.R, ledCol.G, ledCol.B));
                    b.Freeze();
                    dc.DrawRoundedRectangle(b, null, new Rect(marginX + pad + i * (ledW + gap), barY + 4, ledW, Math.Max(8, barH - 8)), 3, 3);
                }
            }
        }
    }
}

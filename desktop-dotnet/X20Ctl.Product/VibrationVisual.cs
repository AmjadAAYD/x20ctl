using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Real-time 3D-perspective vibration &amp; acoustic telemetry stage: renders in hardware-accelerated
/// HTML5 Canvas inside WebView2 on live runs and falls back instantaneously to a matching native WPF
/// 3D-projected vector stage at startup, in reduced-motion mode, and in <see cref="ReviewSandbox"/>.
/// Completely stationary at 0% idle standby until live haptics, pattern playback, or a test runs.
/// </summary>
public sealed class VibrationVisual : Grid
{
    readonly bool animations;
    readonly MotionStageCanvas stage;
    WebView2? webView;
    bool webReady;
    bool webStarted;

    int strength;
    double audioRms;
    bool vocalDetected;
    bool ecoMode;
    bool hasLiveMotors;
    double liveLeftGrip, liveRightGrip, liveLeftTrigger, liveRightTrigger;
    string region = "whole";
    TimeSpan started, last, lastWebPush;
    bool rendering;

    public int Strength => strength;
    public bool IsRunning { get; private set; }
    public bool ReducedMotion => !animations;
    public VibrationVisualProfile Profile => VibrationVisualProfile.For(strength);

    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(VibrationVisual), new FrameworkPropertyMetadata(0d));

    public double Phase
    {
        get => (double)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    public event Action<double, double>? Levels;

    public VibrationVisual(Geometry outline, bool animate, string model = "x20")
    {
        _ = outline;
        _ = model;
        animations = animate;
        MinWidth = 300;
        MinHeight = 250;
        IsHitTestVisible = false;
        ClipToBounds = true;

        stage = new MotionStageCanvas();
        Children.Add(stage);

        Loaded += (_, _) =>
        {
            UpdateMotionState();
            if (!ReviewSandbox.Active && !webStarted && animations)
                _ = InitWebViewAsync();
        };
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) =>
        {
            UpdateMotionState();
            PushWebState();
        };
    }

    public void SetRegion(string targetRegion)
    {
        region = targetRegion ?? "whole";
        stage.Update(strength, Phase, audioRms, vocalDetected, ecoMode, hasLiveMotors,
            liveLeftGrip, liveRightGrip, liveLeftTrigger, liveRightTrigger, animations);
        PushWebState();
    }

    public void SetStrength(int percent)
    {
        strength = Math.Clamp(percent, 0, 100);
        UpdateMotionState();
        PushWebState();
    }

    public void SetTelemetry(double rms, bool vocal, bool eco)
    {
        audioRms = Math.Clamp(rms, 0, 1);
        vocalDetected = vocal;
        ecoMode = eco;
        stage.Update(strength, Phase, audioRms, vocalDetected, ecoMode, hasLiveMotors,
            liveLeftGrip, liveRightGrip, liveLeftTrigger, liveRightTrigger, animations);
        PushWebStateThrottled();
    }

    public void SetLiveMotors(double leftGrip, double rightGrip, double leftTrigger, double rightTrigger)
    {
        hasLiveMotors = true;
        liveLeftGrip = Math.Clamp(leftGrip, 0, 1);
        liveRightGrip = Math.Clamp(rightGrip, 0, 1);
        liveLeftTrigger = Math.Clamp(leftTrigger, 0, 1);
        liveRightTrigger = Math.Clamp(rightTrigger, 0, 1);
        UpdateMotionState();
        Levels?.Invoke(liveLeftGrip, liveRightGrip);
        PushWebStateThrottled();
    }

    public void ClearLiveMotors()
    {
        if (!hasLiveMotors && strength == 0) return;
        hasLiveMotors = false;
        liveLeftGrip = liveRightGrip = liveLeftTrigger = liveRightTrigger = 0;
        UpdateMotionState();
        if (strength == 0) Levels?.Invoke(0, 0);
        PushWebState();
    }

    bool NeedsAnimation => animations && IsLoaded && IsVisible && (hasLiveMotors || strength > 0);

    void UpdateMotionState()
    {
        if (!hasLiveMotors && strength == 0)
        {
            Stop();
            Phase = 0;
            Levels?.Invoke(0, 0);
            stage.Update(0, 0, audioRms, false, ecoMode, false, 0, 0, 0, 0, animations);
            return;
        }

        if (!animations)
        {
            Stop();
            Phase = 0;
            double level = hasLiveMotors
                ? Math.Clamp(liveLeftGrip, 0, 1)
                : strength / 100.0;
            Levels?.Invoke(level, hasLiveMotors ? Math.Clamp(liveRightGrip, 0, 1) : level);
            stage.Update(strength, 0, audioRms, vocalDetected, ecoMode, hasLiveMotors,
                liveLeftGrip, liveRightGrip, liveLeftTrigger, liveRightTrigger, false);
            return;
        }

        if (NeedsAnimation && !rendering)
        {
            started = TimeSpan.Zero;
            last = TimeSpan.Zero;
            IsRunning = true;
            rendering = true;
            CompositionTarget.Rendering += Tick;
        }
        else if (!NeedsAnimation)
        {
            Stop();
        }

        stage.Update(strength, Phase, audioRms, vocalDetected, ecoMode, hasLiveMotors,
            liveLeftGrip, liveRightGrip, liveLeftTrigger, liveRightTrigger, animations);
    }

    void Stop()
    {
        IsRunning = false;
        if (rendering)
        {
            CompositionTarget.Rendering -= Tick;
            rendering = false;
        }
    }

    void Tick(object? sender, EventArgs e)
    {
        if (!NeedsAnimation)
        {
            Stop();
            return;
        }
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (last != TimeSpan.Zero && (now - last).TotalSeconds < 0.032) return;
        last = now;
        if (started == TimeSpan.Zero) started = now;

        double cycle = Math.Max(0.32, hasLiveMotors ? 0.85 : Profile.CycleSeconds);
        double elapsed = (now - started).TotalSeconds;
        Phase = (elapsed / cycle) % 1.0;

        if (hasLiveMotors)
        {
            Levels?.Invoke(liveLeftGrip, liveRightGrip);
        }
        else
        {
            double s = strength / 100.0;
            double waveL = Math.Clamp(s * (0.68 + 0.32 * Math.Sin(Phase * Math.PI * 2)), 0, 1);
            double waveR = Math.Clamp(s * (0.68 + 0.32 * Math.Cos(Phase * Math.PI * 2 + 0.6)), 0, 1);
            Levels?.Invoke(waveL, waveR);
        }

        // WebView2 owns GPU 3D rendering when active; only update WPF canvas until WebView2 is ready.
        if (!webReady)
        {
            stage.Update(strength, Phase, audioRms, vocalDetected, ecoMode, hasLiveMotors,
                liveLeftGrip, liveRightGrip, liveLeftTrigger, liveRightTrigger, animations);
        }
        PushWebStateThrottled(now);
    }

    async Task InitWebViewAsync()
    {
        webStarted = true;
        try
        {
            var t = ThemeCatalog.Current;
            var view = new WebView2
            {
                IsHitTestVisible = false,
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, t.Void.R, t.Void.G, t.Void.B),
                Visibility = Visibility.Collapsed,
            };
            webView = view;
            Children.Add(view);

            string folder = Path.Combine(Path.GetTempPath(), "x20ctl-vibration-3d-webview");
            var env = await CoreWebView2Environment.CreateAsync(null, folder);
            await view.EnsureCoreWebView2Async(env);
            if (!IsLoaded) return;

            view.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess) return;
                webReady = true;
                view.Visibility = Visibility.Visible;
                PushWebState();
            };
            view.CoreWebView2.NavigateToString(BuildHtml(t));
        }
        catch
        {
            if (webView != null)
            {
                Children.Remove(webView);
                webView.Dispose();
                webView = null;
            }
        }
    }

    void PushWebStateThrottled(TimeSpan? now = null)
    {
        if (!webReady) return;
        var current = now ?? TimeSpan.FromTicks(Environment.TickCount64 * TimeSpan.TicksPerMillisecond);
        if (lastWebPush != TimeSpan.Zero && Math.Abs((current - lastWebPush).TotalSeconds) < 0.032) return;
        lastWebPush = current;
        PushWebState();
    }

    void PushWebState()
    {
        if (!webReady || webView?.CoreWebView2 == null) return;
        try
        {
            var t = ThemeCatalog.Current;
            double lg = hasLiveMotors ? liveLeftGrip : (strength / 100.0) * 0.85;
            double rg = hasLiveMotors ? liveRightGrip : (strength / 100.0) * 0.82;
            double lt = hasLiveMotors ? liveLeftTrigger : (strength / 100.0) * 0.42;
            double rt = hasLiveMotors ? liveRightTrigger : (strength / 100.0) * 0.42;
            int readoutPct = hasLiveMotors
                ? (int)Math.Round(Math.Max(Math.Max(lg, rg), Math.Max(lt, rt)) * 100)
                : strength;
            var payload = new
            {
                active = hasLiveMotors || strength > 0,
                live = hasLiveMotors,
                strength = readoutPct,
                lg,
                rg,
                lt,
                rt,
                rms = audioRms,
                vocal = vocalDetected,
                eco = ecoMode,
                region,
                visible = IsVisible,
                theme = new
                {
                    bg = Hex(t.Void),
                    surface = Hex(t.Night),
                    accent = Hex(t.Accent),
                    accentSoft = Hex(t.AccentHi),
                    good = "#34D399",
                    hot = "#FF3B7F",
                    text = Hex(t.Text),
                    soft = Hex(t.Soft),
                    muted = Hex(t.Muted),
                    rule = Hex(t.Rule),
                }
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
    font-family: 'Segoe UI Variable Display', 'Segoe UI', sans-serif;
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
let st = {
  active: false, live: false, strength: 0,
  lg: 0, rg: 0, lt: 0, rt: 0,
  rms: 0, vocal: false, eco: false, region: 'whole', visible: true,
  theme: {
    bg: '{{Hex(t.Void)}}', surface: '{{Hex(t.Night)}}', accent: '{{Hex(t.Accent)}}',
    accentSoft: '{{Hex(t.AccentHi)}}', good: '#34D399', hot: '#FF3B7F',
    text: '{{Hex(t.Text)}}', soft: '{{Hex(t.Soft)}}', muted: '{{Hex(t.Muted)}}', rule: '{{Hex(t.Rule)}}'
  }
};
let cur = { lg: 0, rg: 0, lt: 0, rt: 0, rms: 0, peak: [0,0,0,0] };
let animId = 0;
let lastMs = performance.now();
let phaseT = 0;

function ensureLoop() {
  if (!animId && st.visible) {
    lastMs = performance.now();
    animId = requestAnimationFrame(render);
  }
}

if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener('message', ev => {
    if (ev.data && typeof ev.data === 'object') {
      st = ev.data;
      ensureLoop();
    }
  });
}

function hexRgb(hex, fallback) {
  if (!hex || hex.length < 7) return fallback;
  return [parseInt(hex.slice(1,3),16), parseInt(hex.slice(3,5),16), parseInt(hex.slice(5,7),16)];
}
function rgba(c, a) { return `rgba(${c[0]},${c[1]},${c[2]},${a.toFixed(3)})`; }

function resize() {
  const dpr = window.devicePixelRatio || 1;
  const w = Math.max(1, Math.floor(canvas.clientWidth * dpr));
  const h = Math.max(1, Math.floor(canvas.clientHeight * dpr));
  if (canvas.width !== w || canvas.height !== h) {
    canvas.width = w;
    canvas.height = h;
  }
}

// Perspective 3D projection: x,z form acoustic ground plane; y is upward displacement.
function project3D(x, y, z, cx, cy, scale) {
  const fov = 3.8;
  const camZ = 4.6 + z;
  const p = fov / Math.max(0.4, camZ);
  return [cx + x * p * scale, cy - (y - 0.14 - z * 0.34) * p * scale, p];
}

function drawBar3D(bx, bw, val, peak, label, colRgb, accentSoftRgb, w, h, groundY) {
  const maxH = h * 0.30;
  const barH = Math.max(3, val * maxH);
  const depthX = bw * 0.22;
  const depthY = bw * 0.12;
  const topY = groundY - barH;

  // Ambient aura when motor active
  if (val > 0.02) {
    const glow = ctx.createRadialGradient(bx + bw * 0.5, topY + barH * 0.4, 2, bx + bw * 0.5, groundY - barH * 0.3, bw * 1.35);
    glow.addColorStop(0, rgba(colRgb, 0.32 * val));
    glow.addColorStop(1, 'transparent');
    ctx.fillStyle = glow;
    ctx.fillRect(bx - bw * 0.6, topY - 26, bw * 2.2, barH + 54);
  }

  // Housing back track
  ctx.fillStyle = 'rgba(255,255,255,0.035)';
  ctx.strokeStyle = 'rgba(255,255,255,0.09)';
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.roundRect(bx, groundY - maxH, bw, maxH, 5);
  ctx.fill();
  ctx.stroke();

  if (val > 0.01) {
    // 3D right side extruded face
    ctx.fillStyle = rgba(colRgb, 0.36);
    ctx.beginPath();
    ctx.moveTo(bx + bw, topY);
    ctx.lineTo(bx + bw + depthX, topY - depthY);
    ctx.lineTo(bx + bw + depthX, groundY - depthY);
    ctx.lineTo(bx + bw, groundY);
    ctx.closePath();
    ctx.fill();

    // 3D top cap face
    ctx.fillStyle = rgba(accentSoftRgb, 0.85);
    ctx.beginPath();
    ctx.moveTo(bx, topY);
    ctx.lineTo(bx + depthX, topY - depthY);
    ctx.lineTo(bx + bw + depthX, topY - depthY);
    ctx.lineTo(bx + bw, topY);
    ctx.closePath();
    ctx.fill();

    // Front bevel gradient
    const fg = ctx.createLinearGradient(0, topY, 0, groundY);
    fg.addColorStop(0, rgba(colRgb, 0.96));
    fg.addColorStop(1, rgba(colRgb, 0.42));
    ctx.fillStyle = fg;
    ctx.fillRect(bx, topY, bw, barH);
  }

  // Floating peak hold hairline
  if (peak > 0.025) {
    const py = groundY - peak * maxH;
    ctx.fillStyle = rgba(colRgb, 0.95);
    ctx.fillRect(bx - 1, py - 2, bw + 2, 2);
  }

  // Labels
  ctx.textAlign = 'center';
  ctx.fillStyle = 'rgba(226,232,240,0.68)';
  ctx.font = '600 10.5px "Segoe UI Variable Text", "Segoe UI", sans-serif';
  ctx.fillText(label, bx + bw * 0.5, groundY + 18);

  const pct = Math.round(val * 100);
  ctx.fillStyle = pct > 0 ? '#FFFFFF' : 'rgba(148,163,184,0.65)';
  ctx.font = '700 12.5px "Segoe UI Variable Display", "Segoe UI", sans-serif';
  ctx.fillText(pct + '%', bx + bw * 0.5, groundY + 34);
}

function render(now) {
  resize();
  const dt = Math.min(0.05, Math.max(0.001, (now - lastMs) / 1000));
  lastMs = now;

  const t = st.theme || {};
  const bgRgb = hexRgb(t.bg, [7, 13, 24]);
  const surfRgb = hexRgb(t.surface, [13, 25, 45]);
  const accRgb = hexRgb(t.accent, [56, 189, 248]);
  const accSoftRgb = hexRgb(t.accentSoft, [147, 197, 253]);
  const hotRgb = hexRgb(t.hot, [244, 63, 94]);
  const goodRgb = hexRgb(t.good, [52, 211, 153]);

  const smooth = 1 - Math.exp(-dt * 16);
  const tgtLg = st.active ? st.lg : 0;
  const tgtRg = st.active ? st.rg : 0;
  const tgtLt = st.active ? st.lt : 0;
  const tgtRt = st.active ? st.rt : 0;
  const tgtRms = st.active ? Math.max(st.rms, (tgtLg + tgtRg + tgtLt + tgtRt) * 0.25) : 0;

  cur.lg += (tgtLg - cur.lg) * smooth;
  cur.rg += (tgtRg - cur.rg) * smooth;
  cur.lt += (tgtLt - cur.lt) * smooth;
  cur.rt += (tgtRt - cur.rt) * smooth;
  cur.rms += (tgtRms - cur.rms) * smooth;

  const vals = [cur.lg, cur.rg, cur.lt, cur.rt];
  for (let i = 0; i < 4; i++) {
    if (vals[i] >= cur.peak[i]) cur.peak[i] = vals[i];
    else cur.peak[i] = Math.max(vals[i], cur.peak[i] - dt * 0.42);
  }

  const totalEnergy = cur.lg + cur.rg + cur.lt + cur.rt + cur.rms + cur.peak[0] + cur.peak[1] + cur.peak[2] + cur.peak[3];
  if (st.active || totalEnergy > 0.004) phaseT += dt * (2.2 + totalEnergy * 1.4);

  const dpr = window.devicePixelRatio || 1;
  const w = canvas.width / dpr;
  const h = canvas.height / dpr;

  ctx.save();
  ctx.scale(dpr, dpr);

  // Clean architectural radial studio stage
  ctx.fillStyle = t.bg || '#070D18';
  ctx.fillRect(0, 0, w, h);
  const halo = ctx.createRadialGradient(w * 0.5, h * 0.34, 6, w * 0.5, h * 0.36, w * 0.56);
  const pulseAlpha = st.active ? 0.14 + Math.min(0.28, cur.rms * 0.35) : 0.04;
  halo.addColorStop(0, rgba(st.vocal ? hotRgb : accRgb, pulseAlpha));
  halo.addColorStop(0.55, rgba(surfRgb, 0.18));
  halo.addColorStop(1, 'transparent');
  ctx.fillStyle = halo;
  ctx.fillRect(0, 0, w, h);

  // Top telemetry header (clean typography, zero badge boxes)
  ctx.textAlign = 'left';
  const dotCol = st.active ? (st.eco ? goodRgb : (st.vocal ? hotRgb : accRgb)) : [100, 116, 139];
  ctx.fillStyle = rgba(dotCol, 0.95);
  ctx.beginPath();
  ctx.arc(16, 18, 3.5, 0, Math.PI * 2);
  ctx.fill();

  const statusText = !st.active
    ? 'STANDBY  0%'
    : (st.vocal ? `VOCAL LOCK  ${st.strength}%` : (st.eco ? `ECO SAVER  ${st.strength}%` : `LIVE FIELD  ${st.strength}%`));
  ctx.fillStyle = st.active ? (t.text || '#F8FAFC') : (t.muted || '#64748B');
  ctx.font = '700 11px "Segoe UI Variable Display", "Segoe UI", sans-serif';
  ctx.fillText(statusText, 26, 22);

  // 3D Perspective Acoustic Mesh in upper 54%
  const meshCx = w * 0.5;
  const meshCy = h * 0.34;
  const scale3d = Math.min(w, h) * 0.72;
  const cols = 18;
  const rows = 9;
  const amp = st.active ? (0.04 + cur.rms * 0.36 + (cur.lg + cur.rg) * 0.16) : 0;
  const trigAmp = st.active ? (cur.lt + cur.rt) * 0.22 : 0;

  for (let r = rows; r >= 0; r--) {
    const zNorm = r / rows;
    const zWorld = (zNorm - 0.35) * 2.5;
    ctx.beginPath();
    for (let c = 0; c <= cols; c++) {
      const u = c / cols;
      const xWorld = (u - 0.5) * 3.8;
      const dist = Math.hypot(xWorld * 0.75, zWorld);
      const env = Math.exp(-dist * dist * 0.55);
      const wave = amp * env * Math.sin(dist * 4.4 - phaseT * 3.1)
                 + trigAmp * env * Math.cos(xWorld * 4.0 + phaseT * 5.2);
      const [px, py] = project3D(xWorld, wave, zWorld, meshCx, meshCy, scale3d);
      if (c === 0) ctx.moveTo(px, py);
      else ctx.lineTo(px, py);
    }
    const lineAlpha = st.active ? (0.18 + (1 - zNorm) * 0.48 + cur.rms * 0.24) : (0.14 + (1 - zNorm) * 0.16);
    ctx.strokeStyle = rgba(r % 2 === 0 && st.vocal ? hotRgb : accRgb, Math.min(0.92, lineAlpha));
    ctx.lineWidth = st.active && r <= 3 ? 1.5 : 1.0;
    ctx.stroke();
  }

  // Longitudinal perspective ribs for crisp 3D depth cues
  for (let c = 0; c <= cols; c += 3) {
    const u = c / cols;
    const xWorld = (u - 0.5) * 3.8;
    ctx.beginPath();
    for (let r = 0; r <= rows; r++) {
      const zWorld = (r / rows - 0.35) * 2.5;
      const dist = Math.hypot(xWorld * 0.75, zWorld);
      const env = Math.exp(-dist * dist * 0.55);
      const wave = amp * env * Math.sin(dist * 4.4 - phaseT * 3.1);
      const [px, py] = project3D(xWorld, wave, zWorld, meshCx, meshCy, scale3d);
      if (r === 0) ctx.moveTo(px, py);
      else ctx.lineTo(px, py);
    }
    ctx.strokeStyle = rgba(accSoftRgb, st.active ? 0.18 : 0.12);
    ctx.lineWidth = 0.85;
    ctx.stroke();
  }

  // 4-Channel 3D Extruded Motor Energy Columns
  const barAreaW = Math.min(w - 36, 360);
  const startX = (w - barAreaW) * 0.5;
  const gap = 14;
  const bw = (barAreaW - gap * 3) * 0.25;
  const groundY = h - 42;

  const specs = [
    { label: 'L GRIP', v: cur.lg, p: cur.peak[0], c: accRgb },
    { label: 'R GRIP', v: cur.rg, p: cur.peak[1], c: accSoftRgb },
    { label: 'L TRIG', v: cur.lt, p: cur.peak[2], c: hotRgb },
    { label: 'R TRIG', v: cur.rt, p: cur.peak[3], c: goodRgb }
  ];
  for (let i = 0; i < 4; i++) {
    drawBar3D(startX + i * (bw + gap), bw, specs[i].v, specs[i].p, specs[i].label, specs[i].c, accSoftRgb, w, h, groundY);
  }

  ctx.restore();

  if (st.visible && (st.active || totalEnergy > 0.002)) {
    animId = requestAnimationFrame(render);
  } else {
    animId = 0;
  }
}

window.addEventListener('resize', () => { resize(); ensureLoop(); });
ensureLoop();
</script>
</body>
</html>
""";

    sealed class MotionStageCanvas : FrameworkElement
    {
        int str;
        double ph, rms, lg, rg, lt, rt;
        bool voc, eco, live, anim;

        public void Update(int strength, double phase, double audioRms, bool vocal, bool ecoMode, bool hasLive,
            double leftGrip, double rightGrip, double leftTrig, double rightTrig, bool animated)
        {
            str = strength;
            ph = phase;
            rms = audioRms;
            voc = vocal;
            eco = ecoMode;
            live = hasLive;
            lg = leftGrip;
            rg = rightGrip;
            lt = leftTrig;
            rt = rightTrig;
            anim = animated;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 10 || h < 10) return;

            var t = ThemeCatalog.Current;
            var rulePen = new Pen(new SolidColorBrush(t.Rule), 1);
            var textBrush = new SolidColorBrush(t.Text);
            var softBrush = new SolidColorBrush(t.Soft);
            var mutedBrush = new SolidColorBrush(t.Muted);
            bool isActive = live || str > 0;
            dc.DrawRoundedRectangle(new SolidColorBrush(t.Void), rulePen, new Rect(0, 0, w, h), 12, 12);

            double cLg = live ? lg : (isActive ? (str / 100.0) * (anim ? 0.72 + 0.25 * Math.Sin(ph * Math.PI * 2) : 0.85) : 0);
            double cRg = live ? rg : (isActive ? (str / 100.0) * (anim ? 0.72 + 0.25 * Math.Cos(ph * Math.PI * 2 + 0.5) : 0.85) : 0);
            double cLt = live ? lt : (isActive ? (str / 100.0) * 0.40 : 0);
            double cRt = live ? rt : (isActive ? (str / 100.0) * 0.40 : 0);

            int readoutPct = live
                ? (int)Math.Round(Math.Max(Math.Max(cLg, cRg), Math.Max(cLt, cRt)) * 100)
                : str;

            Color hotColor = Color.FromRgb(0xFF, 0x3B, 0x7F);
            Color goodColor = Color.FromRgb(0x34, 0xD3, 0x99);
            Color acc = voc ? hotColor : t.Accent;
            if (isActive)
            {
                var halo = new RadialGradientBrush(Color.FromArgb((byte)(34 + str * 0.5), acc.R, acc.G, acc.B), Colors.Transparent);
                dc.DrawEllipse(halo, null, new Point(w * 0.5, h * 0.34), w * 0.42, h * 0.26);
            }

            // Header telemetry
            dc.DrawEllipse(new SolidColorBrush(isActive ? (eco ? goodColor : acc) : t.Muted), null, new Point(16, 18), 3.5, 3.5);
            string hud = !isActive ? "STANDBY  0%" : (voc ? $"VOCAL LOCK  {readoutPct}%" : (eco ? $"ECO SAVER  {readoutPct}%" : $"ACTIVE  {readoutPct}%"));
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var hudFt = new FormattedText(hud, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(DS.Display, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11, isActive ? textBrush : mutedBrush, dpi);
            dc.DrawText(hudFt, new Point(26, 11));

            // 3D-projected vector horizon waves
            double cx = w * 0.5, cy = h * 0.30, scale3d = Math.Min(w, h) * 0.68;
            double amp = isActive ? Math.Max(0.06, Math.Max(rms * 0.35, str / 260.0)) : 0;
            const int rows = 7, cols = 16;
            for (int r = rows; r >= 0; r--)
            {
                double zNorm = r / (double)rows;
                double zWorld = (zNorm - 0.35) * 2.4;
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    for (int c = 0; c <= cols; c++)
                    {
                        double xWorld = (c / (double)cols - 0.5) * 3.6;
                        double dist = Math.Sqrt(xWorld * xWorld * 0.56 + zWorld * zWorld);
                        double env = Math.Exp(-dist * dist * 0.55);
                        double y = amp * env * Math.Sin(dist * 4.2 - ph * Math.PI * 2);
                        double p = 3.8 / Math.Max(0.4, 4.6 + zWorld);
                        var pt = new Point(cx + xWorld * p * scale3d, cy - (y - 0.14 - zWorld * 0.34) * p * scale3d);
                        if (c == 0) ctx.BeginFigure(pt, false, false);
                        else ctx.LineTo(pt, true, false);
                    }
                }
                byte alpha = isActive ? (byte)(48 + (1 - zNorm) * 135) : (byte)(36 + (1 - zNorm) * 44);
                dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(alpha, acc.R, acc.G, acc.B)), isActive && r <= 2 ? 1.5 : 1.0), geo);
            }
            for (int c = 0; c <= cols; c += 3)
            {
                double xWorld = (c / (double)cols - 0.5) * 3.6;
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    for (int r = 0; r <= rows; r++)
                    {
                        double zWorld = (r / (double)rows - 0.35) * 2.4;
                        double dist = Math.Sqrt(xWorld * xWorld * 0.56 + zWorld * zWorld);
                        double env = Math.Exp(-dist * dist * 0.55);
                        double y = amp * env * Math.Sin(dist * 4.2 - ph * Math.PI * 2);
                        double p = 3.8 / Math.Max(0.4, 4.6 + zWorld);
                        var pt = new Point(cx + xWorld * p * scale3d, cy - (y - 0.14 - zWorld * 0.34) * p * scale3d);
                        if (r == 0) ctx.BeginFigure(pt, false, false);
                        else ctx.LineTo(pt, true, false);
                    }
                }
                dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(isActive ? (byte)46 : (byte)28, t.AccentHi.R, t.AccentHi.G, t.AccentHi.B)), 0.85), geo);
            }

            // 4-Channel 3D columns
            double barAreaW = Math.Min(w - 32, 340);
            double startX = (w - barAreaW) * 0.5;
            double gap = 12;
            double bw = (barAreaW - gap * 3) * 0.25;
            double groundY = h - 42;
            double maxH = h * 0.28;

            (string Label, double Val, Color Col)[] bars =
            [
                ("L GRIP", cLg, t.Accent),
                ("R GRIP", cRg, t.AccentHi),
                ("L TRIG", cLt, hotColor),
                ("R TRIG", cRt, goodColor),
            ];

            for (int i = 0; i < bars.Length; i++)
            {
                double x = startX + i * (bw + gap);
                double v = Math.Clamp(bars[i].Val, 0, 1);
                double bh = Math.Max(3, v * maxH);
                double topY = groundY - bh;

                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)),
                    rulePen, new Rect(x, groundY - maxH, bw, maxH), 5, 5);

                if (v > 0.01)
                {
                    var fg = new LinearGradientBrush(bars[i].Col, Color.FromArgb(110, bars[i].Col.R, bars[i].Col.G, bars[i].Col.B), 90);
                    dc.DrawRoundedRectangle(fg, null, new Rect(x, topY, bw, bh), 4, 4);
                }

                var lblFt = new FormattedText(bars[i].Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(DS.Display, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                    10, softBrush, dpi);
                dc.DrawText(lblFt, new Point(x + (bw - lblFt.Width) * 0.5, groundY + 6));

                string pctStr = $"{Math.Round(v * 100):0}%";
                var pctFt = new FormattedText(pctStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(DS.Display, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    12, v > 0.01 ? textBrush : mutedBrush, dpi);
                dc.DrawText(pctFt, new Point(x + (bw - pctFt.Width) * 0.5, groundY + 20));
            }
        }
    }
}

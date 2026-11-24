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
/// Real-time 3D Kinetic Acoustic Orb &amp; segmented 4-motor VU telemetry stage:
/// Renders in hardware-accelerated HTML5 Canvas (WebView2) during live desktop sessions and falls back
/// seamlessly to a matching 3D-projected WPF vector stage during startup, reduced-motion, and ReviewSandbox.
/// Never fakes active motion or 70% readouts at idle standby.
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
        MinHeight = 260;
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
        UpdateCanvas();
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
        UpdateCanvas();
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

    bool MotorSignalActive =>
        hasLiveMotors && Math.Max(Math.Max(liveLeftGrip, liveRightGrip), Math.Max(liveLeftTrigger, liveRightTrigger)) > 0.005;

    bool IsVisualActive => MotorSignalActive || strength > 0;

    bool NeedsAnimation => animations && IsLoaded && IsVisible && IsVisualActive;

    void UpdateCanvas()
    {
        stage.Update(strength, Phase, audioRms, vocalDetected, ecoMode, hasLiveMotors,
            liveLeftGrip, liveRightGrip, liveLeftTrigger, liveRightTrigger, animations);
    }

    void UpdateMotionState()
    {
        if (!IsVisualActive)
        {
            Stop();
            Phase = 0;
            Levels?.Invoke(0, 0);
            UpdateCanvas();
            return;
        }

        if (!animations)
        {
            Stop();
            Phase = 0;
            double level = MotorSignalActive ? Math.Clamp(liveLeftGrip, 0, 1) : strength / 100.0;
            Levels?.Invoke(level, MotorSignalActive ? Math.Clamp(liveRightGrip, 0, 1) : level);
            UpdateCanvas();
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

        UpdateCanvas();
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

        double cycle = Math.Max(0.32, MotorSignalActive ? 0.85 : Profile.CycleSeconds);
        double elapsed = (now - started).TotalSeconds;
        Phase = (elapsed / cycle) % 1.0;

        if (MotorSignalActive)
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

        if (!webReady)
            UpdateCanvas();
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
            double lg = MotorSignalActive ? liveLeftGrip : (strength / 100.0) * 0.85;
            double rg = MotorSignalActive ? liveRightGrip : (strength / 100.0) * 0.82;
            double lt = MotorSignalActive ? liveLeftTrigger : (strength / 100.0) * 0.42;
            double rt = MotorSignalActive ? liveRightTrigger : (strength / 100.0) * 0.42;
            int readoutPct = MotorSignalActive
                ? (int)Math.Round(Math.Max(Math.Max(lg, rg), Math.Max(lt, rt)) * 100)
                : strength;
            var payload = new
            {
                active = IsVisualActive,
                armed = hasLiveMotors,
                strength = readoutPct,
                lg,
                rg,
                lt,
                rt,
                rms = audioRms,
                vocal = vocalDetected && MotorSignalActive,
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
  active: false, armed: false, strength: 0,
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
let spinY = 0.45;

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
function rgba(c, a) { return `rgba(${c[0]},${c[1]},${c[2]},${Math.max(0, Math.min(1, a)).toFixed(3)})`; }

function resize() {
  const dpr = window.devicePixelRatio || 1;
  const w = Math.max(1, Math.floor(canvas.clientWidth * dpr));
  const h = Math.max(1, Math.floor(canvas.clientHeight * dpr));
  if (canvas.width !== w || canvas.height !== h) {
    canvas.width = w;
    canvas.height = h;
  }
}

function rotY(x, y, z, a) {
  const c = Math.cos(a), s = Math.sin(a);
  return [x * c + z * s, y, -x * s + z * c];
}
function rotX(x, y, z, a) {
  const c = Math.cos(a), s = Math.sin(a);
  return [x, y * c - z * s, y * s + z * c];
}
function proj(x, y, z, cx, cy, scale) {
  const fov = 3.2;
  const p = fov / Math.max(0.6, fov + z + 1.8);
  return [cx + x * p * scale, cy + y * p * scale, p, z];
}

function drawOrbStage(cx, cy, radius, accRgb, accSoftRgb, hotRgb, goodRgb) {
  const gripEnergy = (cur.lg + cur.rg) * 0.5;
  const trigEnergy = (cur.lt + cur.rt) * 0.5;
  const energy = st.active ? Math.min(1, gripEnergy * 0.65 + trigEnergy * 0.45 + cur.rms * 0.35) : 0;

  // Inner core radial halo
  const coreR = radius * (0.85 + energy * 0.65);
  const coreGlow = ctx.createRadialGradient(cx, cy, 4, cx, cy, coreR);
  coreGlow.addColorStop(0, rgba(st.vocal ? hotRgb : accSoftRgb, st.active ? 0.36 + energy * 0.42 : 0.14));
  coreGlow.addColorStop(0.5, rgba(accRgb, st.active ? 0.16 + energy * 0.20 : 0.05));
  coreGlow.addColorStop(1, 'transparent');
  ctx.fillStyle = coreGlow;
  ctx.beginPath();
  ctx.arc(cx, cy, coreR, 0, Math.PI * 2);
  ctx.fill();

  const pts = [];
  const lats = 7;
  const lons = 14;
  const scale = radius * 1.48;

  // Latitude rings of 3D Kinetic Orb
  for (let i = 1; i < lats; i++) {
    const phi = (i / lats) * Math.PI;
    const sp = Math.sin(phi), cp = Math.cos(phi);
    ctx.beginPath();
    let started = false;
    for (let j = 0; j <= lons; j++) {
      const th = (j / lons) * Math.PI * 2;
      const disp = 1 + (st.active ? 0.16 * energy * Math.sin(th * 3 + spinY * 3.5 + phi * 2) : 0);
      let [x, y, z] = [sp * Math.cos(th) * disp, cp * disp, sp * Math.sin(th) * disp];
      [x, y, z] = rotY(x, y, z, spinY);
      [x, y, z] = rotX(x, y, z, 0.36);
      const [sx, sy, pr, pz] = proj(x, y, z, cx, cy, scale);
      if (!started) { ctx.moveTo(sx, sy); started = true; } else ctx.lineTo(sx, sy);
      if (j < lons && (i % 2 === 1)) pts.push({ sx, sy, pz, pr });
    }
    ctx.strokeStyle = rgba(st.vocal ? hotRgb : accRgb, st.active ? 0.24 + energy * 0.38 : 0.16);
    ctx.lineWidth = st.active ? 1.2 : 0.9;
    ctx.stroke();
  }

  // Longitude meridians
  for (let j = 0; j < lons; j += 2) {
    const th = (j / lons) * Math.PI * 2;
    ctx.beginPath();
    for (let i = 0; i <= 16; i++) {
      const phi = (i / 16) * Math.PI;
      const disp = 1 + (st.active ? 0.14 * energy * Math.cos(phi * 4 - spinY * 3.2) : 0);
      let [x, y, z] = [Math.sin(phi) * Math.cos(th) * disp, Math.cos(phi) * disp, Math.sin(phi) * Math.sin(th) * disp];
      [x, y, z] = rotY(x, y, z, spinY);
      [x, y, z] = rotX(x, y, z, 0.36);
      const [sx, sy] = proj(x, y, z, cx, cy, scale);
      if (i === 0) ctx.moveTo(sx, sy); else ctx.lineTo(sx, sy);
    }
    ctx.strokeStyle = rgba(accSoftRgb, st.active ? 0.18 + energy * 0.26 : 0.11);
    ctx.lineWidth = 0.9;
    ctx.stroke();
  }

  // Tilted Outer Gyroscopic Ring 1 (Grips) & Ring 2 (Triggers)
  const rings = [
    { tiltX: 1.05, tiltZ: 0.38, spin: spinY * 1.4, rScale: 1.26 + gripEnergy * 0.24, col: accRgb, act: st.active ? 0.45 + gripEnergy * 0.50 : 0.22 },
    { tiltX: -0.95, tiltZ: -0.45, spin: -spinY * 1.8, rScale: 1.42 + trigEnergy * 0.28, col: st.vocal || trigEnergy > 0.05 ? hotRgb : goodRgb, act: st.active ? 0.28 + trigEnergy * 0.65 : 0.14 }
  ];
  for (const rg of rings) {
    ctx.beginPath();
    for (let k = 0; k <= 48; k++) {
      const a = (k / 48) * Math.PI * 2;
      let [x, y, z] = [Math.cos(a) * rg.rScale, 0, Math.sin(a) * rg.rScale];
      [x, y, z] = rotY(x, y, z, rg.spin);
      [x, y, z] = rotX(x, y, z, rg.tiltX);
      const [sx, sy] = proj(x, y, z, cx, cy, scale);
      if (k === 0) ctx.moveTo(sx, sy); else ctx.lineTo(sx, sy);
    }
    ctx.strokeStyle = rgba(rg.col, rg.act);
    ctx.lineWidth = st.active ? 1.7 : 1.05;
    ctx.stroke();
  }

  // Depth-sorted glowing vertex nodes
  pts.sort((a, b) => b.pz - a.pz);
  for (const pt of pts) {
    const front = Math.max(0.15, 0.72 - pt.pz * 0.45);
    ctx.fillStyle = rgba(st.vocal ? hotRgb : accSoftRgb, front * (st.active ? 0.95 : 0.45));
    ctx.beginPath();
    ctx.arc(pt.sx, pt.sy, st.active ? 1.8 * pt.pr + energy * 1.2 : 1.3 * pt.pr, 0, Math.PI * 2);
    ctx.fill();
  }
}

function drawSegmentedBar(bx, bw, val, peak, label, colRgb, groundY, maxH) {
  const segs = 10;
  const gapY = 3;
  const segH = Math.max(4, (maxH - gapY * (segs - 1)) / segs);
  const lit = Math.round(val * segs);
  const peakSeg = Math.round(peak * segs);

  // Subtle recessed column bezel
  ctx.fillStyle = 'rgba(255,255,255,0.025)';
  ctx.strokeStyle = 'rgba(255,255,255,0.08)';
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.roundRect(bx - 4, groundY - maxH - 4, bw + 8, maxH + 8, 6);
  ctx.fill();
  ctx.stroke();

  if (val > 0.02) {
    const glow = ctx.createRadialGradient(bx + bw * 0.5, groundY - val * maxH * 0.5, 2, bx + bw * 0.5, groundY - val * maxH * 0.4, bw * 1.2);
    glow.addColorStop(0, rgba(colRgb, 0.24 * val));
    glow.addColorStop(1, 'transparent');
    ctx.fillStyle = glow;
    ctx.fillRect(bx - bw * 0.5, groundY - maxH - 12, bw * 2, maxH + 24);
  }

  for (let s = 0; s < segs; s++) {
    const sy = groundY - (s + 1) * segH - s * gapY;
    const isLit = s < lit;
    const isPeak = peakSeg > 0 && s === Math.min(segs - 1, peakSeg - 1);
    if (isLit) {
      ctx.fillStyle = rgba(colRgb, 0.62 + (s / segs) * 0.38);
    } else if (isPeak) {
      ctx.fillStyle = rgba(colRgb, 0.85);
    } else {
      ctx.fillStyle = 'rgba(255,255,255,0.055)';
    }
    ctx.beginPath();
    ctx.roundRect(bx, sy, bw, segH, 2.5);
    ctx.fill();
  }

  ctx.textAlign = 'center';
  ctx.fillStyle = 'rgba(203,213,225,0.72)';
  ctx.font = '600 10px "Segoe UI Variable Text", "Segoe UI", sans-serif';
  ctx.fillText(label, bx + bw * 0.5, groundY + 15);

  const pct = Math.round(val * 100);
  ctx.fillStyle = pct > 0 ? '#FFFFFF' : 'rgba(148,163,184,0.68)';
  ctx.font = '700 12px "Segoe UI Variable Display", "Segoe UI", sans-serif';
  ctx.fillText(pct + '%', bx + bw * 0.5, groundY + 30);
}

function render(now) {
  resize();
  const dt = Math.min(0.05, Math.max(0.001, (now - lastMs) / 1000));
  lastMs = now;

  const t = st.theme || {};
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
  if (st.active || totalEnergy > 0.004) spinY += dt * (0.75 + totalEnergy * 1.15);

  const dpr = window.devicePixelRatio || 1;
  const w = canvas.width / dpr;
  const h = canvas.height / dpr;

  ctx.save();
  ctx.scale(dpr, dpr);

  ctx.fillStyle = t.bg || '#070D18';
  ctx.fillRect(0, 0, w, h);

  // Clean HUD status dot + readout
  ctx.textAlign = 'left';
  const dotCol = st.eco ? goodRgb : (st.active ? (st.vocal ? hotRgb : accRgb) : (st.armed ? accRgb : [100, 116, 139]));
  ctx.fillStyle = rgba(dotCol, 0.95);
  ctx.beginPath();
  ctx.arc(16, 18, 3.5, 0, Math.PI * 2);
  ctx.fill();

  const statusText = st.eco
    ? 'ECO MODE  OFF'
    : (st.active
        ? (st.vocal ? `VOCAL LOCK  ${st.strength}%` : `ACTIVE  ${st.strength}%`)
        : (st.armed ? 'ARMED  0%  SILENCE' : 'STANDBY  0%'));
  ctx.fillStyle = st.active ? (t.text || '#F8FAFC') : (t.muted || '#64748B');
  ctx.font = '700 11px "Segoe UI Variable Display", "Segoe UI", sans-serif';
  ctx.fillText(statusText, 26, 22);

  // Upper 58%: Kinetic 3D Orb & Orbital Rings (strictly separated from meters)
  const orbCy = h * 0.32;
  const orbRadius = Math.min(w * 0.24, h * 0.22);
  drawOrbStage(w * 0.5, orbCy, orbRadius, accRgb, accSoftRgb, hotRgb, goodRgb);

  // Crisp divider line separating 3D Kinetic Orb from bottom segmented VU deck
  const dividerY = Math.round(h * 0.59);
  const divGrad = ctx.createLinearGradient(24, dividerY, w - 24, dividerY);
  divGrad.addColorStop(0, 'transparent');
  divGrad.addColorStop(0.5, rgba(accSoftRgb, st.active ? 0.30 : 0.14));
  divGrad.addColorStop(1, 'transparent');
  ctx.fillStyle = divGrad;
  ctx.fillRect(24, dividerY, w - 48, 1);

  // Lower 35%: 4-Channel Segmented VU Meters
  const barAreaW = Math.min(w - 40, 320);
  const startX = (w - barAreaW) * 0.5;
  const gapX = 16;
  const bw = (barAreaW - gapX * 3) * 0.25;
  const groundY = h - 38;
  const maxH = Math.max(48, groundY - dividerY - 16);

  const specs = [
    { label: 'L GRIP', v: cur.lg, p: cur.peak[0], c: accRgb },
    { label: 'R GRIP', v: cur.rg, p: cur.peak[1], c: accSoftRgb },
    { label: 'L TRIG', v: cur.lt, p: cur.peak[2], c: hotRgb },
    { label: 'R TRIG', v: cur.rt, p: cur.peak[3], c: goodRgb }
  ];
  for (let i = 0; i < 4; i++) {
    drawSegmentedBar(startX + i * (bw + gapX), bw, specs[i].v, specs[i].p, specs[i].label, specs[i].c, groundY, maxH);
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

        static (double X, double Y, double Z) RotY(double x, double y, double z, double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            return (x * c + z * s, y, -x * s + z * c);
        }

        static (double X, double Y, double Z) RotX(double x, double y, double z, double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            return (x, y * c - z * s, y * s + z * c);
        }

        static Point Proj3D(double x, double y, double z, double cx, double cy, double scale)
        {
            double p = 3.2 / Math.Max(0.6, 5.0 + z);
            return new Point(cx + x * p * scale, cy + y * p * scale);
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

            bool liveSignal = live && Math.Max(Math.Max(lg, rg), Math.Max(lt, rt)) > 0.005;
            bool isActive = liveSignal || str > 0;

            dc.DrawRoundedRectangle(new SolidColorBrush(t.Void), rulePen, new Rect(0, 0, w, h), 12, 12);

            double cLg = liveSignal ? lg : (isActive ? (str / 100.0) * (anim ? 0.72 + 0.25 * Math.Sin(ph * Math.PI * 2) : 0.85) : 0);
            double cRg = liveSignal ? rg : (isActive ? (str / 100.0) * (anim ? 0.72 + 0.25 * Math.Cos(ph * Math.PI * 2 + 0.5) : 0.85) : 0);
            double cLt = liveSignal ? lt : (isActive ? (str / 100.0) * 0.40 : 0);
            double cRt = liveSignal ? rt : (isActive ? (str / 100.0) * 0.40 : 0);

            int readoutPct = liveSignal
                ? (int)Math.Round(Math.Max(Math.Max(cLg, cRg), Math.Max(cLt, cRt)) * 100)
                : str;

            Color hotColor = Color.FromRgb(0xFF, 0x3B, 0x7F);
            Color goodColor = Color.FromRgb(0x34, 0xD3, 0x99);
            Color acc = voc && liveSignal ? hotColor : t.Accent;
            double energy = isActive ? Math.Clamp((cLg + cRg) * 0.4 + (cLt + cRt) * 0.3 + rms * 0.3, 0.12, 1.0) : 0;

            // Header telemetry
            dc.DrawEllipse(new SolidColorBrush(eco ? goodColor : (isActive ? acc : (live ? t.Accent : t.Muted))), null, new Point(16, 18), 3.5, 3.5);
            string hud = eco
                ? "ECO MODE  OFF"
                : (isActive ? (voc && liveSignal ? $"VOCAL LOCK  {readoutPct}%" : $"ACTIVE  {readoutPct}%") : (live ? "ARMED  0%  SILENCE" : "STANDBY  0%"));
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var hudFt = new FormattedText(hud, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(DS.Display, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11, isActive ? textBrush : mutedBrush, dpi);
            dc.DrawText(hudFt, new Point(26, 11));

            // Upper 58%: 3D Kinetic Orb + Dual Orbital Rings
            double cx = w * 0.5, cy = h * 0.32;
            double radius = Math.Min(w * 0.24, h * 0.22);
            double scale3d = radius * 1.48;
            double spinY = 0.45 + ph * Math.PI * 2;

            var halo = new RadialGradientBrush(
                Color.FromArgb(isActive ? (byte)(60 + energy * 95) : (byte)28, acc.R, acc.G, acc.B),
                Colors.Transparent);
            dc.DrawEllipse(halo, null, new Point(cx, cy), radius * (0.9 + energy * 0.45), radius * (0.9 + energy * 0.45));

            var orbPen = new Pen(new SolidColorBrush(Color.FromArgb(isActive ? (byte)(90 + energy * 110) : (byte)48, acc.R, acc.G, acc.B)), isActive ? 1.2 : 0.9);
            for (int i = 1; i < 6; i++)
            {
                double phi = (i / 6.0) * Math.PI;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    for (int j = 0; j <= 16; j++)
                    {
                        double th = (j / 16.0) * Math.PI * 2;
                        double disp = 1 + (isActive ? 0.14 * energy * Math.Sin(th * 3 + spinY * 2) : 0);
                        var p1 = RotY(Math.Sin(phi) * Math.Cos(th) * disp, Math.Cos(phi) * disp, Math.Sin(phi) * Math.Sin(th) * disp, spinY);
                        var p2 = RotX(p1.X, p1.Y, p1.Z, 0.36);
                        var pt = Proj3D(p2.X, p2.Y, p2.Z, cx, cy, scale3d);
                        if (j == 0) ctx.BeginFigure(pt, false, false);
                        else ctx.LineTo(pt, true, false);
                    }
                }
                dc.DrawGeometry(null, orbPen, g);
            }

            var ringPen1 = new Pen(new SolidColorBrush(Color.FromArgb(isActive ? (byte)185 : (byte)65, t.AccentHi.R, t.AccentHi.G, t.AccentHi.B)), isActive ? 1.5 : 1.0);
            var ringPen2 = new Pen(new SolidColorBrush(Color.FromArgb(isActive ? (byte)165 : (byte)45, hotColor.R, hotColor.G, hotColor.B)), isActive ? 1.4 : 0.95);
            foreach (var (tilt, pen, rs) in new[] { (1.05, ringPen1, 1.26 + (cLg + cRg) * 0.12), (-0.95, ringPen2, 1.40 + (cLt + cRt) * 0.14) })
            {
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    for (int k = 0; k <= 36; k++)
                    {
                        double a = (k / 36.0) * Math.PI * 2;
                        var p1 = RotY(Math.Cos(a) * rs, 0, Math.Sin(a) * rs, tilt > 0 ? spinY : -spinY);
                        var p2 = RotX(p1.X, p1.Y, p1.Z, tilt);
                        var pt = Proj3D(p2.X, p2.Y, p2.Z, cx, cy, scale3d);
                        if (k == 0) ctx.BeginFigure(pt, false, false);
                        else ctx.LineTo(pt, true, false);
                    }
                }
                dc.DrawGeometry(null, pen, g);
            }

            // Horizon divider line
            double dividerY = Math.Round(h * 0.59);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(isActive ? (byte)85 : (byte)42, t.AccentHi.R, t.AccentHi.G, t.AccentHi.B)), 1),
                new Point(22, dividerY), new Point(w - 22, dividerY));

            // Lower 35%: 4 Segmented Hardware VU Columns
            double barAreaW = Math.Min(w - 36, 310);
            double startX = (w - barAreaW) * 0.5;
            double gapX = 14;
            double bw = (barAreaW - gapX * 3) * 0.25;
            double groundY = h - 38;
            double maxH = Math.Max(46, groundY - dividerY - 16);

            (string Label, double Val, Color Col)[] bars =
            [
                ("L GRIP", cLg, t.Accent),
                ("R GRIP", cRg, t.AccentHi),
                ("L TRIG", cLt, hotColor),
                ("R TRIG", cRt, goodColor),
            ];

            const int segs = 10;
            const double gapY = 3;
            double segH = Math.Max(3.5, (maxH - gapY * (segs - 1)) / segs);

            for (int i = 0; i < bars.Length; i++)
            {
                double x = startX + i * (bw + gapX);
                double v = Math.Clamp(bars[i].Val, 0, 1);
                int lit = (int)Math.Round(v * segs);

                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)),
                    rulePen, new Rect(x - 3, groundY - maxH - 3, bw + 6, maxH + 6), 5, 5);

                for (int s = 0; s < segs; s++)
                {
                    double sy = groundY - (s + 1) * segH - s * gapY;
                    bool isLit = s < lit;
                    var fill = isLit
                        ? new SolidColorBrush(Color.FromArgb((byte)(155 + s * 10), bars[i].Col.R, bars[i].Col.G, bars[i].Col.B))
                        : new SolidColorBrush(Color.FromArgb(16, 255, 255, 255));
                    dc.DrawRoundedRectangle(fill, null, new Rect(x, sy, bw, segH), 2, 2);
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

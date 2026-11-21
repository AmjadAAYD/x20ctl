"""Generate Carbon Hardware Precision Grid SVG banners for README.md.

Usage:
    python tools/build_readme_art.py --shots <captured-shots-dir> [--tag v0.025] [--export-webp]
"""
from __future__ import annotations

import argparse
import base64
import io
import json
from pathlib import Path
from PIL import Image

from version_name import name as release_name

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "assets" / "readme"
DOCS_SHOTS = ROOT / "docs" / "screenshots"

# Curated V0.025 screenshots relative to --shots -> docs/screenshots/<name>.webp
SCREENSHOT_MAP = {
    "zone": "themes/default/1-zone.png",
    "choose-controller": "inputs/selector.png",
    "dashboard": "themes/default/3-dashboard.png",
    "buttons": "themes/default/4-buttons.png",
    "curves": "themes/default/5-curves.png",
    "macros": "themes/default/6-macros.png",
    "rgb-studio": "tabs/RGB.png",
    "rgb-effects": "lighting/lighting-effects.png",
    "screen": "tabs/Screen.png",
    "vibration": "inputs/x20pro-vibration-off-0.png",
    "pc-mode": "themes/default/8b-pcmode.png",
    "keyboard": "inputs/keyboard-0.png",
    "mouse": "themes/default/8c-mouse.png",
    "agent-xctl": "agent/9e-red-lights.png",
    "agent-checkup": "agent/10-page-checkup.png",
    "tester": "themes/default/8e-tester.png",
    "diagnostics": "themes/default/8f-diagnostics.png",
    "controller-check": "themes/default/11d-check.png",
    "tools": "themes/default/9-tools.png",
    "linux-studio": "linux/dashboard.png",
}

THEMES = [
    ("default", "Default", "Space glass", "#3B82F6"),
    ("channel", "Channel", "Gold viewfinder", "#EAB308"),
    ("undertow", "Undertow", "Deep water", "#06B6D4"),
    ("alert", "Alert", "Violet armour", "#84CC16"),
    ("slate", "Slate", "Relief map", "#F97316"),
    ("multiverse", "Multiverse", "Red signal", "#EF4444"),
]


def _encode_jpeg_uri(src: Path, width: int, quality: int = 84) -> str:
    with Image.open(src) as im:
        rgb = im.convert("RGB")
        h = max(1, round(rgb.height * (width / rgb.width)))
        resized = rgb.resize((width, h), Image.Resampling.LANCZOS)
        buf = io.BytesIO()
        resized.save(buf, format="JPEG", quality=quality, optimize=True, progressive=True)
    b64 = base64.b64encode(buf.getvalue()).decode("ascii")
    return f"data:image/jpeg;base64,{b64}"


def export_webp_screenshots(shots_dir: Path) -> list[Path]:
    DOCS_SHOTS.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []
    for slug, rel in SCREENSHOT_MAP.items():
        src = shots_dir / rel
        if not src.is_file():
            raise FileNotFoundError(f"Missing screenshot source: {src}")
        dst = DOCS_SHOTS / f"{slug}.webp"
        with Image.open(src) as im:
            rgb = im.convert("RGB")
            if abs((rgb.width / rgb.height) - (16 / 9)) > 0.03:
                w16 = max(rgb.width, round(rgb.height * 16 / 9))
                h16 = round(w16 * 9 / 16)
                pad_color = rgb.getpixel((min(4, rgb.width - 1), min(4, rgb.height - 1)))
                canvas = Image.new("RGB", (w16, h16), pad_color)
                canvas.paste(rgb, ((w16 - rgb.width) // 2, (h16 - rgb.height) // 2))
                rgb = canvas
            rgb = rgb.resize((1280, 720), Image.Resampling.LANCZOS)
            rgb.save(dst, format="WEBP", quality=88, method=6)
        written.append(dst)
    return written


def build_hero(shots_dir: Path, ver_label: str) -> str:
    rgb_uri = _encode_jpeg_uri(shots_dir / "tabs" / "RGB.png", width=620, quality=84)
    dash_uri = _encode_jpeg_uri(shots_dir / "themes" / "default" / "3-dashboard.png", width=560, quality=80)

    return f'''<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 1200 440" width="1200" height="440" role="img" aria-label="X20CTL {ver_label} — Open-source EasySMX Controller Studio">
  <defs>
    <linearGradient id="cardBg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#090D14"/>
      <stop offset="55%" stop-color="#0D131F"/>
      <stop offset="100%" stop-color="#090D16"/>
    </linearGradient>
    <linearGradient id="rgbSweep" x1="-100%" y1="0%" x2="200%" y2="0%">
      <stop offset="0%" stop-color="#0EA5E9" stop-opacity="0.15"/>
      <stop offset="28%" stop-color="#38BDF8"/>
      <stop offset="48%" stop-color="#818CF8"/>
      <stop offset="68%" stop-color="#10B981"/>
      <stop offset="100%" stop-color="#0EA5E9" stop-opacity="0.15"/>
      <animate attributeName="x1" values="-100%;0%;-100%" dur="9s" repeatCount="indefinite"/>
      <animate attributeName="x2" values="100%;200%;100%" dur="9s" repeatCount="indefinite"/>
    </linearGradient>
    <radialGradient id="rgbAura" cx="72%" cy="56%" r="36%">
      <stop offset="0%" stop-color="#38BDF8" stop-opacity="0.22"/>
      <stop offset="55%" stop-color="#6366F1" stop-opacity="0.09"/>
      <stop offset="100%" stop-color="#090D14" stop-opacity="0"/>
    </radialGradient>
    <pattern id="dotGrid" width="24" height="24" patternUnits="userSpaceOnUse">
      <circle cx="1.5" cy="1.5" r="0.9" fill="#334155" opacity="0.55"/>
    </pattern>
    <clipPath id="clipDash"><rect x="654" y="34" width="504" height="284" rx="10"/></clipPath>
    <clipPath id="clipRgb"><rect x="596" y="114" width="516" height="290" rx="11"/></clipPath>
    <style>
      .sans {{ font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }}
      .mono {{ font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }}
      @keyframes pulseDot {{
        0%, 100% {{ opacity: 1; transform: scale(1); }}
        50% {{ opacity: 0.45; transform: scale(0.85); }}
      }}
      .live-dot {{ transform-origin: 554px 362px; animation: pulseDot 2.8s ease-in-out infinite; }}
    </style>
  </defs>

  <!-- Outer frame seamlessly matched to GitHub Dark -->
  <rect width="1200" height="440" fill="#0D1117"/>
  <rect x="1" y="1" width="1198" height="438" rx="14" fill="url(#cardBg)" stroke="#263348" stroke-width="1"/>
  <rect x="1" y="1" width="1198" height="438" rx="14" fill="url(#dotGrid)" opacity="0.7"/>
  <rect x="1" y="1" width="1198" height="438" rx="14" fill="url(#rgbAura)"/>

  <!-- Animated RGB precision lightbar along top rim -->
  <rect x="16" y="1.5" width="1168" height="2.5" rx="1.25" fill="url(#rgbSweep)"/>

  <!-- Blueprint corner ticks -->
  <path d="M16 30 V16 H30 M1170 16 H1184 V30 M16 410 V424 H30 M1170 424 H1184 V410" fill="none" stroke="#38BDF8" stroke-width="1.2" opacity="0.55"/>

  <!-- Left column typography -->
  <text x="44" y="54" class="mono" font-size="11" font-weight="600" fill="#38BDF8" letter-spacing="2">OPEN-SOURCE CONTROLLER STUDIO</text>

  <text x="42" y="112" class="sans" font-size="56" font-weight="800" fill="#F8FAFC" letter-spacing="-1.5">X20CTL</text>

  <!-- Release Pill (strictly maintained by tools/sync_readme_release.py) -->
  <rect x="262" y="72" width="168" height="32" rx="16" fill="#0F172A" stroke="#38BDF8" stroke-opacity="0.65" stroke-width="1"/>
  <circle cx="280" cy="88" r="4" fill="#10B981"/>
  <text id="release-badge" x="292" y="92" class="mono" font-size="12.5" font-weight="700" fill="#E2E8F0">RELEASE · {ver_label}</text>

  <text x="44" y="148" class="sans" font-size="16" fill="#94A3B8">Remap buttons, sculpt stick &amp; trigger curves, drive X20 Pro RGB &amp;</text>
  <text x="44" y="171" class="sans" font-size="16" fill="#94A3B8">built-in screen, or run low-latency PC Mode on any EasySMX pad.</text>

  <!-- 2x2 Precision spec cards -->
  <rect x="44" y="198" width="256" height="72" rx="10" fill="#0B111B" fill-opacity="0.88" stroke="#1E293B"/>
  <text x="58" y="219" class="mono" font-size="10" fill="#38BDF8" letter-spacing="1.2">01 / HARDWARE</text>
  <text x="58" y="241" class="sans" font-size="14" font-weight="700" fill="#F1F5F9">7 EasySMX Models</text>
  <text x="58" y="258" class="sans" font-size="11.5" fill="#64748B">X20 · X20 Pro · Dune · X15 · X10 · X05 Pro · D10</text>

  <rect x="312" y="198" width="256" height="72" rx="10" fill="#0B111B" fill-opacity="0.88" stroke="#1E293B"/>
  <text x="326" y="219" class="mono" font-size="10" fill="#38BDF8" letter-spacing="1.2">02 / LINK &amp; LIGHTING</text>
  <text x="326" y="241" class="sans" font-size="14" font-weight="700" fill="#F1F5F9">Dongle + Unpaired BLE</text>
  <text x="326" y="258" class="sans" font-size="11.5" fill="#64748B">X20 Pro RGB Studio, effects &amp; 160x80 screen</text>

  <rect x="44" y="282" width="256" height="72" rx="10" fill="#0B111B" fill-opacity="0.88" stroke="#1E293B"/>
  <text x="58" y="303" class="mono" font-size="10" fill="#818CF8" letter-spacing="1.2">03 / PC MODE VIRTUAL PAD</text>
  <text x="58" y="325" class="sans" font-size="14" font-weight="700" fill="#F1F5F9">Xbox 360 &amp; PS4 Output</text>
  <text x="58" y="342" class="sans" font-size="11" fill="#64748B">64-key macros · Mouse · 200% rumble</text>

  <rect x="312" y="282" width="256" height="72" rx="10" fill="#0B111B" fill-opacity="0.88" stroke="#1E293B"/>
  <text x="326" y="303" class="mono" font-size="10" fill="#10B981" letter-spacing="1.2">04 / ASSISTANT &amp; CORE</text>
  <text x="326" y="325" class="sans" font-size="14" font-weight="700" fill="#F1F5F9">100% Offline Agent XCTL</text>
  <text x="326" y="342" class="sans" font-size="11.5" fill="#64748B">6 themes · 10 languages · Zero cloud telemetry</text>

  <!-- Bottom telemetry bar -->
  <line x1="44" y1="382" x2="568" y2="382" stroke="#1E293B" stroke-width="1"/>
  <text x="44" y="405" class="mono" font-size="11" fill="#64748B">WINDOWS 10/11 (WPF)  ·  LINUX x64 (AVALONIA)  ·  PYTHON CLI  ·  GPL-3.0-OR-LATER</text>

  <!-- Right studio frames -->
  <rect x="652" y="32" width="508" height="288" rx="12" fill="#070A10" stroke="#1E293B" stroke-width="1.2"/>
  <image href="{dash_uri}" x="654" y="34" width="504" height="284" preserveAspectRatio="xMidYMid slice" clip-path="url(#clipDash)"/>
  <rect x="652" y="32" width="508" height="288" rx="12" fill="#070A10" fill-opacity="0.22" stroke="#334155" stroke-width="1"/>

  <rect x="593" y="111" width="522" height="296" rx="13" fill="#070A10" stroke="#38BDF8" stroke-opacity="0.45" stroke-width="1.4"/>
  <image href="{rgb_uri}" x="596" y="114" width="516" height="290" preserveAspectRatio="xMidYMid slice" clip-path="url(#clipRgb)"/>

  <!-- Corner caption badge over front preview -->
  <rect x="608" y="370" width="152" height="24" rx="6" fill="#090D14" fill-opacity="0.92" stroke="#334155" stroke-width="0.8"/>
  <circle cx="621" cy="382" r="3.5" fill="#38BDF8"/>
  <text x="631" y="386" class="mono" font-size="10.5" font-weight="600" fill="#E2E8F0">X20 PRO RGB STUDIO</text>
</svg>
'''


def build_themes_strip(shots_dir: Path) -> str:
    cards = []
    defs_clips = []
    w, gap, start_x = 182, 12, 24
    for idx, (tid, title, sub, accent) in enumerate(THEMES):
        src = shots_dir / "themes" / tid / "3-dashboard.png"
        if not src.is_file():
            raise FileNotFoundError(f"Missing theme screenshot: {src}")
        uri = _encode_jpeg_uri(src, width=320, quality=80)
        x = start_x + idx * (w + gap)
        img_x, img_y, img_w, img_h = x + 8, 56, w - 16, 94
        defs_clips.append(f'<clipPath id="tclip_{idx}"><rect x="{img_x}" y="{img_y}" width="{img_w}" height="{img_h}" rx="6"/></clipPath>')
        cards.append(f'''
    <rect x="{x}" y="46" width="{w}" height="162" rx="10" fill="#0B111B" stroke="#1E293B" stroke-width="1"/>
    <rect x="{x + 12}" y="46" width="{w - 24}" height="2.5" rx="1.25" fill="{accent}"/>
    <image href="{uri}" x="{img_x}" y="{img_y}" width="{img_w}" height="{img_h}" preserveAspectRatio="xMidYMid slice" clip-path="url(#tclip_{idx})"/>
    <text x="{x + 12}" y="173" class="sans" font-size="13.5" font-weight="700" fill="#F1F5F9">{title}</text>
    <text x="{x + 12}" y="191" class="mono" font-size="10.5" fill="#64748B">{sub}</text>''')

    return f'''<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 1200 250" width="1200" height="250" role="img" aria-label="6 Native X20CTL Themes and 10 Languages">
  <defs>
    <linearGradient id="tBg" x1="0%" y1="0%" x2="100%" y2="0%">
      <stop offset="0%" stop-color="#090D14"/>
      <stop offset="100%" stop-color="#0D131F"/>
    </linearGradient>
    <pattern id="tGrid" width="24" height="24" patternUnits="userSpaceOnUse">
      <circle cx="1.5" cy="1.5" r="0.85" fill="#334155" opacity="0.45"/>
    </pattern>
    {"".join(defs_clips)}
    <style>
      .sans {{ font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }}
      .mono {{ font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }}
    </style>
  </defs>
  <rect width="1200" height="250" fill="#0D1117"/>
  <rect x="1" y="1" width="1198" height="248" rx="14" fill="url(#tBg)" stroke="#263348" stroke-width="1"/>
  <rect x="1" y="1" width="1198" height="248" rx="14" fill="url(#tGrid)" opacity="0.65"/>
  <text x="24" y="30" class="mono" font-size="11" font-weight="600" fill="#38BDF8" letter-spacing="1.6">VISUAL SYSTEM  ·  6 NATIVE THEMES</text>
  <text x="1176" y="30" text-anchor="end" class="mono" font-size="11" fill="#94A3B8">SINGLE REACTIVE LAYOUT SHELL</text>
  {"".join(cards)}
  <text x="600" y="230" text-anchor="middle" class="mono" font-size="11" fill="#64748B">10 UI LANGUAGES:  English · 中文 · Français · Deutsch · Polski · Español · 日本語 · Русский · Italiano · Tiếng Việt</text>
</svg>
'''


def build_pipeline() -> str:
    return '''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1200 228" width="1200" height="228" role="img" aria-label="X20CTL Hardware and PC Mode Architecture">
  <defs>
    <linearGradient id="pBg" x1="0%" y1="0%" x2="100%" y2="0%">
      <stop offset="0%" stop-color="#090D14"/>
      <stop offset="100%" stop-color="#0D131F"/>
    </linearGradient>
    <pattern id="pGrid" width="24" height="24" patternUnits="userSpaceOnUse">
      <circle cx="1.5" cy="1.5" r="0.85" fill="#334155" opacity="0.45"/>
    </pattern>
    <style>
      .sans { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }
      .mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }
    </style>
  </defs>
  <rect width="1200" height="228" fill="#0D1117"/>
  <rect x="1" y="1" width="1198" height="226" rx="14" fill="url(#pBg)" stroke="#263348" stroke-width="1"/>
  <rect x="1" y="1" width="1198" height="226" rx="14" fill="url(#pGrid)" opacity="0.65"/>
  <text x="24" y="28" class="mono" font-size="11" font-weight="600" fill="#38BDF8" letter-spacing="1.6">TWO CLEAR CONTROL PATHS  ·  EXPLICIT TARGETS · ZERO GUESSING</text>

  <!-- Connectors + subtle animated signal pulses -->
  <path id="topWire" d="M262 96 C 310 96, 310 74, 360 74 H 815" fill="none" stroke="#38BDF8" stroke-opacity="0.45" stroke-width="1.8"/>
  <path id="botWire" d="M262 128 C 310 128, 310 156, 360 156 H 815" fill="none" stroke="#818CF8" stroke-opacity="0.45" stroke-width="1.8"/>
  <line x1="770" y1="74" x2="824" y2="74" stroke="#38BDF8" stroke-opacity="0.6" stroke-width="1.8"/>
  <line x1="770" y1="156" x2="824" y2="156" stroke="#818CF8" stroke-opacity="0.6" stroke-width="1.8"/>

  <circle r="3.5" fill="#38BDF8">
    <animateMotion dur="3.4s" begin="-0.4s" repeatCount="indefinite" path="M262 96 C 310 96, 310 74, 360 74 H 824"/>
  </circle>
  <circle r="3.5" fill="#818CF8">
    <animateMotion dur="3.4s" begin="-1.7s" repeatCount="indefinite" path="M262 128 C 310 128, 310 156, 360 156 H 824"/>
  </circle>

  <!-- Node 1: Physical Controller -->
  <rect x="24" y="54" width="238" height="118" rx="11" fill="#0B111B" stroke="#334155"/>
  <text x="40" y="78" class="mono" font-size="10" fill="#94A3B8" letter-spacing="1.2">INPUT HARDWARE</text>
  <text x="40" y="102" class="sans" font-size="16" font-weight="700" fill="#F8FAFC">EasySMX Controller</text>
  <text x="40" y="124" class="sans" font-size="12" fill="#94A3B8">USB · 2.4 GHz (413D:2131)</text>
  <text x="40" y="143" class="sans" font-size="12" fill="#94A3B8">Bluetooth LE · XInput</text>
  <text x="40" y="162" class="mono" font-size="10" fill="#10B981">VERIFIED BEFORE WRITE</text>

  <!-- Track 1: Native On-Device -->
  <rect x="360" y="42" width="420" height="64" rx="10" fill="#0B111B" stroke="#38BDF8" stroke-opacity="0.55"/>
  <text x="376" y="64" class="mono" font-size="10" fill="#38BDF8" letter-spacing="1.2">PATH A · NATIVE MEMORY + RGB / SCREEN</text>
  <text x="376" y="85" class="sans" font-size="14" font-weight="700" fill="#F1F5F9">X20 &amp; X20 Pro On-Device Profile</text>
  <text x="376" y="99" class="mono" font-size="10" fill="#64748B">M1–M6 · Curves · Macros · RGB · 160x80 Display · Dongle + BLE</text>

  <rect x="824" y="42" width="352" height="64" rx="10" fill="#0B111B" stroke="#1E293B"/>
  <text x="840" y="64" class="mono" font-size="10" fill="#10B981" letter-spacing="1.2">OUTPUT · CONTROLLER FLASH</text>
  <text x="840" y="85" class="sans" font-size="14" font-weight="700" fill="#F1F5F9">Verified Read-Back Persistence</text>
  <text x="840" y="99" class="mono" font-size="10" fill="#64748B">PC Mode Off · Never flashes firmware</text>

  <!-- Track 2: PC Mode Virtual Pad -->
  <rect x="360" y="124" width="420" height="64" rx="10" fill="#0B111B" stroke="#818CF8" stroke-opacity="0.55"/>
  <text x="376" y="146" class="mono" font-size="10" fill="#818CF8" letter-spacing="1.2">PATH B · REAL-TIME PC MODE ENGINE</text>
  <text x="376" y="167" class="sans" font-size="14" font-weight="700" fill="#F1F5F9">All 7 Supported Controllers (1–4 Players)</text>
  <text x="376" y="181" class="mono" font-size="10" fill="#64748B">64-key Sequences · Mouse M1–M7 · Turbo 30 Hz · 200% Rumble</text>

  <rect x="824" y="124" width="352" height="64" rx="10" fill="#0B111B" stroke="#1E293B"/>
  <text x="840" y="146" class="mono" font-size="10" fill="#818CF8" letter-spacing="1.2">OUTPUT · VIRTUAL PAD &amp; INPUT</text>
  <text x="840" y="167" class="sans" font-size="14" font-weight="700" fill="#F1F5F9">ViGEmBus (Win) / uinput (Linux)</text>
  <text x="840" y="181" class="mono" font-size="10" fill="#64748B">Xbox 360 or PS4 DS4 · Per-game auto profiles</text>

  <!-- Bottom note -->
  <text x="600" y="212" text-anchor="middle" class="mono" font-size="10.5" fill="#64748B">LOCAL DRAFTS STAY ON DISK UNTIL APPLIED  ·  X10 NATIVE READ-ONLY VIA KRLY BLE</text>
</svg>
'''


def main() -> int:
    parser = argparse.ArgumentParser(description="Build README Carbon Hardware Precision Grid SVGs.")
    parser.add_argument("--shots", required=True, type=Path, help="Directory with rendered review screenshots")
    parser.add_argument("--tag", default="v0.025", help="Release tag to stamp (e.g. v0.025)")
    parser.add_argument("--export-webp", action="store_true", help="Also encode docs/screenshots/*.webp")
    args = parser.parse_args()

    ver_label = release_name(args.tag)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    if args.export_webp:
        written = export_webp_screenshots(args.shots)
        print(f"Exported {len(written)} WebP screenshots into {DOCS_SHOTS}")

    (OUT_DIR / "hero.svg").write_text(build_hero(args.shots, ver_label), encoding="utf-8")
    (OUT_DIR / "themes-strip.svg").write_text(build_themes_strip(args.shots), encoding="utf-8")
    (OUT_DIR / "protocol-pipeline.svg").write_text(build_pipeline(), encoding="utf-8")
    print(f"Generated SVG banners for {ver_label} in {OUT_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

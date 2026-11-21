"""Generate Obsidian Hardware Showroom README artwork from real controller renders and app captures.

Usage:
    python tools/build_readme_art.py [--tag v0.025] [--social-preview]
"""
from __future__ import annotations

import argparse
import base64
import html
import io
import os
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image

from version_name import name as release_name

ROOT = Path(__file__).resolve().parents[1]
README_ASSETS = ROOT / "assets" / "readme"
SRC_DIR = README_ASSETS / "src"
CTRL_DIR = ROOT / "src" / "assets" / "controllers"
SOCIAL_PREVIEW_PATH = ROOT / "assets" / "social-preview.png"

# Obsidian Hardware Showroom palette (PlayStation / Steam Deck industrial aesthetic)
BG_TOP = "#080A0F"
BG_MID = "#0D111A"
BG_BOT = "#121824"
CARD_TOP = "#101521"
CARD_BOT = "#0B0F17"
SURFACE_SUB = "#141A28"
BORDER_SILVER = "#E2ECFF"
TEXT_PRIMARY = "#F4F7FC"
TEXT_SECONDARY = "#9FAAC0"
TEXT_MUTED = "#6D798E"
HEART_RED = "#E5484D"

FONT_SANS = "'Segoe UI', -apple-system, BlinkMacSystemFont, 'Inter', 'Helvetica Neue', Arial, sans-serif"
FONT_MONO = "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace"

CONTROLLERS_LINEUP = [
    ("X20", CTRL_DIR / "x20/native/front-full.png", "Verified", "#22C55E", True),
    ("X20 Pro", CTRL_DIR / "x20_pro/native/front-full.png", "Full + RGB", "#22C55E", True),
    ("X15", CTRL_DIR / "x15/native/front-full.png", "PC Mode", "#60A5FA", False),
    ("X10", CTRL_DIR / "x10/native/front-full.png", "BLE Read", "#F59E0B", False),
    ("X05 Pro", CTRL_DIR / "x05_pro/native/front-full.png", "PC Mode", "#60A5FA", False),
    ("D10", CTRL_DIR / "d10/native/front-full.png", "PC Mode", "#60A5FA", False),
    ("Dune 8K", CTRL_DIR / "dune/native/front-full.png", "PC Mode", "#60A5FA", False),
]

THEMES_GRID = [
    ("Default", "Space glass", "#3B82F6", SRC_DIR / "theme-default.webp"),
    ("Channel", "Gold viewfinder", "#D4A853", SRC_DIR / "theme-channel.webp"),
    ("Undertow", "Deep water", "#0EA5E9", SRC_DIR / "theme-undertow.webp"),
    ("Alert", "Violet lime", "#84CC16", SRC_DIR / "theme-alert.webp"),
    ("Slate", "Relief map", "#F97316", SRC_DIR / "theme-slate.webp"),
    ("Multiverse", "Red signal", "#EF4444", SRC_DIR / "theme-multiverse.webp"),
]


def _esc(text: str) -> str:
    return html.escape(text, quote=True)


def _load_cropped_webp_uri(path: Path, target_width: int, crop_alpha: bool = True, quality: int = 92) -> tuple[str, int, int]:
    """Load an image, optionally crop strictly to non-transparent pixels, resize with Lanczos, and encode as WebP data URI."""
    with Image.open(path) as img:
        if crop_alpha:
            rgba = img.convert("RGBA")
            bbox = rgba.getbbox()
            if bbox:
                rgba = rgba.crop(bbox)
        else:
            rgba = img.convert("RGBA")
        w, h = rgba.size
        target_height = max(1, round(h * (target_width / w)))
        resized = rgba.resize((target_width, target_height), Image.Resampling.LANCZOS)
        buf = io.BytesIO()
        resized.save(buf, format="WEBP", quality=quality, method=6)
        b64 = base64.b64encode(buf.getvalue()).decode("ascii")
        return f"data:image/webp;base64,{b64}", target_width, target_height


def _fit_box(uri_size: tuple[str, int, int], max_w: float, max_h: float) -> tuple[str, float, float]:
    uri, w, h = uri_size
    scale = min(max_w / w, max_h / h)
    return uri, round(w * scale, 2), round(h * scale, 2)


def _obsidian_frame_defs(prefix: str, softbox_x: str = "74%", softbox_y: str = "46%") -> str:
    return f"""  <defs>
    <linearGradient id="{prefix}-bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="{BG_TOP}"/>
      <stop offset="55%" stop-color="{BG_MID}"/>
      <stop offset="100%" stop-color="{BG_BOT}"/>
    </linearGradient>
    <radialGradient id="{prefix}-softbox" cx="{softbox_x}" cy="{softbox_y}" r="48%">
      <stop offset="0%" stop-color="#B8CEF0" stop-opacity="0.16"/>
      <stop offset="45%" stop-color="#7EA0D6" stop-opacity="0.05"/>
      <stop offset="100%" stop-color="#0D111A" stop-opacity="0"/>
    </radialGradient>
    <radialGradient id="{prefix}-key" cx="18%" cy="14%" r="55%">
      <stop offset="0%" stop-color="#D7E4F8" stop-opacity="0.06"/>
      <stop offset="100%" stop-color="#0D111A" stop-opacity="0"/>
    </radialGradient>
    <linearGradient id="{prefix}-card" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" stop-color="{CARD_TOP}"/>
      <stop offset="100%" stop-color="{CARD_BOT}"/>
    </linearGradient>"""


def build_hero_svg(tag: str) -> str:
    ver = release_name(tag)
    w, h = 1200, 430
    ctrl = _load_cropped_webp_uri(CTRL_DIR / "x20_pro/native/front-full.png", 680)
    ctrl_uri, cw, ch = _fit_box(ctrl, 510, 345)
    cx, cy = 898.0, 206.0
    ix, iy = round(cx - cw / 2, 1), round(cy - ch / 2, 1)

    return f"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 {w} {h}" width="{w}" height="{h}" role="img" aria-label="X20CTL {_esc(ver)} — Controller studio for EasySMX pads">
  <style>
    .s {{ font-family: {FONT_SANS}; }}
    .m {{ font-family: {FONT_MONO}; }}
    @keyframes pill-sheen {{
      0%, 72% {{ transform: translateX(-280px); opacity: 0; }}
      80% {{ opacity: 0.45; }}
      96% {{ transform: translateX(300px); opacity: 0; }}
      100% {{ transform: translateX(300px); opacity: 0; }}
    }}
    .sheen {{ animation: pill-sheen 8s ease-in-out infinite; }}
  </style>
{_obsidian_frame_defs("hero", "75%", "48%")}
    <linearGradient id="hero-pill" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" stop-color="#272F3E"/>
      <stop offset="50%" stop-color="#1A202C"/>
      <stop offset="100%" stop-color="#212836"/>
    </linearGradient>
    <linearGradient id="hero-sheen-grad" x1="0%" y1="0%" x2="100%" y2="0%">
      <stop offset="0%" stop-color="#FFFFFF" stop-opacity="0"/>
      <stop offset="50%" stop-color="#E6EEFF" stop-opacity="0.32"/>
      <stop offset="100%" stop-color="#FFFFFF" stop-opacity="0"/>
    </linearGradient>
    <radialGradient id="hero-floor-shadow" cx="50%" cy="50%" r="50%">
      <stop offset="0%" stop-color="#020306" stop-opacity="0.88"/>
      <stop offset="70%" stop-color="#030509" stop-opacity="0.45"/>
      <stop offset="100%" stop-color="#030509" stop-opacity="0"/>
    </radialGradient>
    <clipPath id="hero-clip">
      <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="18"/>
    </clipPath>
    <clipPath id="hero-pill-clip">
      <rect x="72" y="282" width="234" height="52" rx="26"/>
    </clipPath>
  </defs>

  <g clip-path="url(#hero-clip)">
    <rect width="{w}" height="{h}" fill="url(#hero-bg)"/>
    <rect width="{w}" height="{h}" fill="url(#hero-softbox)"/>
    <rect width="{w}" height="{h}" fill="url(#hero-key)"/>

    <!-- Architectural studio plinth shadow beneath real X20 Pro -->
    <ellipse cx="{cx}" cy="382" rx="220" ry="22" fill="url(#hero-floor-shadow)"/>

    <!-- Real EasySMX X20 Pro Matte Black cutout -->
    <g transform="rotate(-3.5 {cx} {cy})">
      <image x="{ix}" y="{iy}" width="{cw}" height="{ch}" preserveAspectRatio="xMidYMid meet" href="{ctrl_uri}" xlink:href="{ctrl_uri}"/>
    </g>

    <!-- Studio eyebrow -->
    <text x="72" y="76" class="m" font-size="11.5" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="2.2">OPEN-SOURCE CONTROLLER STUDIO</text>

    <!-- Wordmark + Version badge -->
    <text x="72" y="152" class="s" font-size="70" font-weight="800" fill="{TEXT_PRIMARY}" letter-spacing="-1.6">X20CTL</text>
    <rect x="342" y="109" width="90" height="30" rx="15" fill="#151B27" stroke="{BORDER_SILVER}" stroke-opacity="0.22" stroke-width="1"/>
    <text x="387" y="128.5" text-anchor="middle" class="m" font-size="12.5" font-weight="600" fill="#DCE5F5" letter-spacing="0.4"><tspan id="hero-version">{_esc(ver)}</tspan></text>

    <!-- Subtitle & platform facts -->
    <text x="72" y="198" class="s" font-size="22" font-weight="400" fill="#B6C0D2">Controller studio for EasySMX pads</text>
    <text x="72" y="236" class="s" font-size="14" font-weight="400" fill="{TEXT_MUTED}">Windows 10/11 · Linux x64 · 100% Offline Agent · GPL-3.0</text>

    <!-- Brushed anodized metal Download pill button -->
    <rect x="72" y="282" width="234" height="52" rx="26" fill="url(#hero-pill)" stroke="{BORDER_SILVER}" stroke-opacity="0.30" stroke-width="1"/>
    <line x1="94" y1="283" x2="284" y2="283" stroke="#FFFFFF" stroke-opacity="0.18" stroke-width="1"/>
    <g clip-path="url(#hero-pill-clip)">
      <polygon class="sheen" points="72,282 142,282 114,334 44,334" fill="url(#hero-sheen-grad)"/>
    </g>
    <text x="189" y="313.5" text-anchor="middle" class="s" font-size="15.5" font-weight="600" fill="{TEXT_PRIMARY}" letter-spacing="0.2">Download {_esc(ver)}</text>

    <!-- Quiet bottom studio ledger -->
    <line x1="72" y1="364" x2="544" y2="364" stroke="{BORDER_SILVER}" stroke-opacity="0.08" stroke-width="1"/>
    <text x="72" y="388" class="m" font-size="11.5" fill="#7A869B" letter-spacing="0.4">X20 Pro · X20 · X15 · X10 · X05 Pro · D10 · Dune 8K</text>
  </g>

  <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="18" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.14" stroke-width="1"/>
  <line x1="22" y1="1.5" x2="{w - 22}" y2="1.5" stroke="#FFFFFF" stroke-opacity="0.09" stroke-width="1"/>
</svg>
"""


def build_thanks_svg() -> str:
    w, h = 1200, 128
    white_raw = _load_cropped_webp_uri(CTRL_DIR / "x20_pro/finishes/white/front-full.png", 260)
    black_raw = _load_cropped_webp_uri(CTRL_DIR / "x20_pro/native/front-full.png", 260)
    w_uri, ww, wh = _fit_box(white_raw, 154, 98)
    b_uri, bw, bh = _fit_box(black_raw, 154, 98)

    return f"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 {w} {h}" width="{w}" height="{h}" role="img" aria-label="Thank you, EasySMX Kiin and Team EasySMX for supplying an EasySMX X20 Pro to build and test X20CTL">
  <style>
    .s {{ font-family: {FONT_SANS}; }}
  </style>
{_obsidian_frame_defs("thx", "50%", "50%")}
    <radialGradient id="thx-spot-l" cx="10%" cy="50%" r="22%">
      <stop offset="0%" stop-color="#D7E3F7" stop-opacity="0.14"/>
      <stop offset="100%" stop-color="#0D111A" stop-opacity="0"/>
    </radialGradient>
    <radialGradient id="thx-spot-r" cx="90%" cy="50%" r="22%">
      <stop offset="0%" stop-color="#B9CEEF" stop-opacity="0.13"/>
      <stop offset="100%" stop-color="#0D111A" stop-opacity="0"/>
    </radialGradient>
    <clipPath id="thx-clip">
      <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="18"/>
    </clipPath>
  </defs>

  <g clip-path="url(#thx-clip)">
    <rect width="{w}" height="{h}" fill="url(#thx-card)"/>
    <rect width="{w}" height="{h}" fill="url(#thx-spot-l)"/>
    <rect width="{w}" height="{h}" fill="url(#thx-spot-r)"/>

    <!-- Real Pearl White X20 Pro render -->
    <ellipse cx="118" cy="111" rx="68" ry="8" fill="#020306" fill-opacity="0.65"/>
    <g transform="rotate(-4 118 62)">
      <image x="{round(118 - ww / 2, 1)}" y="{round(62 - wh / 2, 1)}" width="{ww}" height="{wh}" preserveAspectRatio="xMidYMid meet" href="{w_uri}" xlink:href="{w_uri}"/>
    </g>

    <!-- Real Matte Black X20 Pro render -->
    <ellipse cx="1082" cy="111" rx="68" ry="8" fill="#020306" fill-opacity="0.65"/>
    <g transform="rotate(4 1082 62)">
      <image x="{round(1082 - bw / 2, 1)}" y="{round(62 - bh / 2, 1)}" width="{bw}" height="{bh}" preserveAspectRatio="xMidYMid meet" href="{b_uri}" xlink:href="{b_uri}"/>
    </g>

    <!-- Crisp heart mark -->
    <g transform="translate(591, 21) scale(0.75)">
      <path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z" fill="{HEART_RED}"/>
    </g>

    <text x="600" y="69" text-anchor="middle" class="s" font-size="22" font-weight="600" fill="{TEXT_PRIMARY}" letter-spacing="-0.2">Thank you, EasySMX Kiin &amp; Team EasySMX</text>
    <text x="600" y="96" text-anchor="middle" class="s" font-size="14" font-weight="400" fill="{TEXT_SECONDARY}">for supplying an EasySMX X20 Pro to build and test X20CTL</text>
  </g>

  <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="18" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.15" stroke-width="1"/>
  <line x1="22" y1="1.5" x2="{w - 22}" y2="1.5" stroke="#FFFFFF" stroke-opacity="0.08" stroke-width="1"/>
</svg>
"""


def build_controllers_lineup_svg() -> str:
    w, h = 1200, 260
    card_w, card_h, gap = 152, 174, 12
    start_x = (w - (7 * card_w + 6 * gap)) / 2  # 32.0

    slots: list[str] = []
    for i, (label, path, badge, dot_color, verified) in enumerate(CONTROLLERS_LINEUP):
        x = round(start_x + i * (card_w + gap), 1)
        y = 64.0
        cx = round(x + card_w / 2, 1)
        raw = _load_cropped_webp_uri(path, 230)
        uri, cw, ch = _fit_box(raw, 124, 84)
        ix = round(cx - cw / 2, 1)
        iy = round(122 - ch / 2, 1)
        border_op = "0.22" if verified else "0.10"
        bg = "#131926" if verified else "#0F141F"

        text_half = len(badge) * 3.1
        dot_x = round(cx - text_half - 5, 1)
        slots.append(
            f'  <rect x="{x}" y="{y}" width="{card_w}" height="{card_h}" rx="12" fill="{bg}" stroke="{BORDER_SILVER}" stroke-opacity="{border_op}" stroke-width="1"/>\n'
            f'  <ellipse cx="{cx}" cy="166" rx="46" ry="6.5" fill="#030508" fill-opacity="0.72"/>\n'
            f'  <image x="{ix}" y="{iy}" width="{cw}" height="{ch}" preserveAspectRatio="xMidYMid meet" href="{uri}" xlink:href="{uri}"/>\n'
            f'  <text x="{cx}" y="193" text-anchor="middle" class="s" font-size="14" font-weight="600" fill="{TEXT_PRIMARY}">{_esc(label)}</text>\n'
            f'  <circle cx="{dot_x}" cy="214" r="3" fill="{dot_color}"/>\n'
            f'  <text x="{round(dot_x + 8, 1)}" y="218" class="m" font-size="10.5" fill="{TEXT_SECONDARY}">{_esc(badge)}</text>'
        )

    return f"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 {w} {h}" width="{w}" height="{h}" role="img" aria-label="Supported EasySMX controller hardware lineup: X20, X20 Pro, X15, X10, X05 Pro, D10, and Dune 8K">
  <style>
    .s {{ font-family: {FONT_SANS}; }}
    .m {{ font-family: {FONT_MONO}; }}
  </style>
{_obsidian_frame_defs("lineup", "50%", "30%")}
    <clipPath id="lineup-clip">
      <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="18"/>
    </clipPath>
  </defs>

  <g clip-path="url(#lineup-clip)">
    <rect width="{w}" height="{h}" fill="url(#lineup-bg)"/>
    <rect width="{w}" height="{h}" fill="url(#lineup-softbox)"/>
    <text x="32" y="34" class="m" font-size="11.5" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="2">SUPPORTED HARDWARE LINEUP</text>
    <text x="{w - 32}" y="34" text-anchor="end" class="m" font-size="11.5" fill="{TEXT_MUTED}" letter-spacing="1.6">7 REAL PAD MODELS</text>
    <line x1="32" y1="48" x2="{w - 32}" y2="48" stroke="{BORDER_SILVER}" stroke-opacity="0.09" stroke-width="1"/>
{chr(10).join(slots)}
  </g>

  <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="18" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.14" stroke-width="1"/>
</svg>
"""


def build_themes_showcase_svg() -> str:
    w, h = 1200, 624
    card_w, img_h, footer_h = 362, 204, 42
    card_h = img_h + footer_h
    gap_x, gap_y = 23, 20
    start_x = (w - (3 * card_w + 2 * gap_x)) / 2  # 34.0
    start_y = 66.0

    cards: list[str] = []
    for idx, (title, sub, accent, img_path) in enumerate(THEMES_GRID):
        col = idx % 3
        row = idx // 3
        cx = round(start_x + col * (card_w + gap_x), 1)
        cy = round(start_y + row * (card_h + gap_y), 1)
        shot_uri, sw, sh = _load_cropped_webp_uri(img_path, 420, crop_alpha=False, quality=92)
        clip_id = f"theme-card-clip-{idx}"

        cards.append(
            f'    <clipPath id="{clip_id}"><rect x="{cx}" y="{cy}" width="{card_w}" height="{card_h}" rx="12"/></clipPath>\n'
            f'    <g clip-path="url(#{clip_id})">\n'
            f'      <rect x="{cx}" y="{cy}" width="{card_w}" height="{card_h}" fill="#111622"/>\n'
            f'      <image x="{cx}" y="{cy}" width="{card_w}" height="{img_h}" preserveAspectRatio="xMidYMid slice" href="{shot_uri}" xlink:href="{shot_uri}"/>\n'
            f'      <rect x="{cx}" y="{cy}" width="{card_w}" height="3" fill="{accent}"/>\n'
            f'      <line x1="{cx}" y1="{cy + img_h}" x2="{cx + card_w}" y2="{cy + img_h}" stroke="{BORDER_SILVER}" stroke-opacity="0.10" stroke-width="1"/>\n'
            f'      <text x="{cx + 14}" y="{cy + img_h + 26}" class="s" font-size="14" font-weight="600" fill="{TEXT_PRIMARY}">{_esc(title)}</text>\n'
            f'      <text x="{cx + card_w - 14}" y="{cy + img_h + 26}" text-anchor="end" class="s" font-size="12.5" fill="{TEXT_MUTED}">{_esc(sub)}</text>\n'
            f'    </g>\n'
            f'    <rect x="{cx}" y="{cy}" width="{card_w}" height="{card_h}" rx="12" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.15" stroke-width="1"/>'
        )

    langs = "English  ·  中文  ·  Français  ·  Deutsch  ·  Polski  ·  Español  ·  日本語  ·  Русский  ·  Italiano  ·  Tiếng Việt"
    return f"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 {w} {h}" width="{w}" height="{h}" role="img" aria-label="6 native themes (Default, Channel, Undertow, Alert, Slate, Multiverse) and 10 UI languages">
  <style>
    .s {{ font-family: {FONT_SANS}; }}
    .m {{ font-family: {FONT_MONO}; }}
  </style>
{_obsidian_frame_defs("themes", "50%", "20%")}
    <clipPath id="themes-outer-clip">
      <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="18"/>
    </clipPath>
  </defs>

  <g clip-path="url(#themes-outer-clip)">
    <rect width="{w}" height="{h}" fill="url(#themes-bg)"/>
    <rect width="{w}" height="{h}" fill="url(#themes-key)"/>
    <text x="34" y="34" class="m" font-size="11.5" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="2">VISUAL SYSTEM · 6 NATIVE THEMES</text>
    <text x="{w - 34}" y="34" text-anchor="end" class="m" font-size="11.5" fill="{TEXT_MUTED}" letter-spacing="1.6">SINGLE REACTIVE SHELL</text>
    <line x1="34" y1="48" x2="{w - 34}" y2="48" stroke="{BORDER_SILVER}" stroke-opacity="0.09" stroke-width="1"/>

{chr(10).join(cards)}

    <line x1="34" y1="574" x2="{w - 34}" y2="574" stroke="{BORDER_SILVER}" stroke-opacity="0.09" stroke-width="1"/>
    <text x="600" y="601" text-anchor="middle" class="s" font-size="12.5" fill="{TEXT_SECONDARY}">10 UI Languages: {_esc(langs)}</text>
  </g>

  <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="18" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.14" stroke-width="1"/>
</svg>
"""


def build_protocol_svg() -> str:
    w, h = 1200, 196
    pkt = [
        ("OPCODE", "1B command", 148),
        ("LENGTH", "payload size", 150),
        ("SERIAL", "session ctr", 150),
        ("NONCE", "keystream idx", 162),
        ("PAYLOAD", "up to 15 bytes", 316),
        ("CRC-8", "poly 0xEB", 148),
    ]
    pkt_parts: list[str] = []
    px = 34
    for name, desc, pw in pkt:
        pkt_parts.append(
            f'  <rect x="{px}" y="58" width="{pw}" height="52" rx="10" fill="#121723" stroke="{BORDER_SILVER}" stroke-opacity="0.14" stroke-width="1"/>\n'
            f'  <text x="{px + 14}" y="79" class="m" font-size="11.5" font-weight="700" fill="{TEXT_PRIMARY}" letter-spacing="0.8">{_esc(name)}</text>\n'
            f'  <text x="{px + 14}" y="97" class="s" font-size="12" fill="{TEXT_MUTED}">{_esc(desc)}</text>'
        )
        px += pw + 12

    steps = ["1. Decompile", "2. Capture BLE", "3. Diff 1 setting", "4. Verify read-back", "5. Ship in Studio"]
    step_parts: list[str] = []
    sx, sw, gap = 34, 210, 20
    for i, label in enumerate(steps):
        hi = i >= 3
        bg = "#192132" if hi else "#111621"
        stroke_op = "0.28" if hi else "0.12"
        txt_col = TEXT_PRIMARY if hi else TEXT_SECONDARY
        step_parts.append(
            f'  <rect x="{sx}" y="130" width="{sw}" height="38" rx="19" fill="{bg}" stroke="{BORDER_SILVER}" stroke-opacity="{stroke_op}" stroke-width="1"/>\n'
            f'  <text x="{sx + sw / 2}" y="153.5" text-anchor="middle" class="s" font-size="12.5" font-weight="600" fill="{txt_col}">{_esc(label)}</text>'
        )
        if i < len(steps) - 1:
            ax = sx + sw + 4
            step_parts.append(
                f'  <text x="{ax + 6}" y="153.5" text-anchor="middle" class="s" font-size="13" fill="{TEXT_MUTED}">→</text>'
            )
        sx += sw + gap

    return f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}" role="img" aria-label="KeyLinker 20-byte scrambled BLE GATT packet format and hardware verification pipeline">
  <style>
    .s {{ font-family: {FONT_SANS}; }}
    .m {{ font-family: {FONT_MONO}; }}
  </style>
{_obsidian_frame_defs("proto", "80%", "30%")}
    <clipPath id="proto-clip">
      <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="18"/>
    </clipPath>
  </defs>

  <g clip-path="url(#proto-clip)">
    <rect width="{w}" height="{h}" fill="url(#proto-bg)"/>
    <rect width="{w}" height="{h}" fill="url(#proto-softbox)"/>
    <text x="34" y="34" class="m" font-size="11.5" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="2">KEYLINKER WIRE PROTOCOL · 20-BYTE SCRAMBLED GATT FRAME</text>
    <text x="{w - 34}" y="34" text-anchor="end" class="m" font-size="11.5" fill="{TEXT_MUTED}" letter-spacing="1.6">NO OTA FIRMWARE TOUCHED</text>
    <line x1="34" y1="45" x2="{w - 34}" y2="45" stroke="{BORDER_SILVER}" stroke-opacity="0.09" stroke-width="1"/>
{chr(10).join(pkt_parts)}
{chr(10).join(step_parts)}
  </g>

  <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="18" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.14" stroke-width="1"/>
</svg>
"""


def build_guide_header_svg(
    prefix: str, eyebrow: str, title: str, subtitle: str, ctrl_rel: str = "x20_pro/native/front-full.png"
) -> str:
    w, h = 1200, 108
    raw = _load_cropped_webp_uri(CTRL_DIR / ctrl_rel, 260)
    uri, cw, ch = _fit_box(raw, 134, 86)

    return f"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 {w} {h}" width="{w}" height="{h}" role="img" aria-label="{_esc(title)} — {_esc(subtitle)}">
  <style>
    .s {{ font-family: {FONT_SANS}; }}
    .m {{ font-family: {FONT_MONO}; }}
  </style>
{_obsidian_frame_defs(prefix, "92%", "50%")}
    <clipPath id="{prefix}-clip">
      <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="16"/>
    </clipPath>
  </defs>

  <g clip-path="url(#{prefix}-clip)">
    <rect width="{w}" height="{h}" fill="url(#{prefix}-bg)"/>
    <rect width="{w}" height="{h}" fill="url(#{prefix}-softbox)"/>
    <text x="34" y="30" class="m" font-size="11" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="2">X20CTL · {_esc(eyebrow)}</text>
    <text x="34" y="64" class="s" font-size="26" font-weight="700" fill="{TEXT_PRIMARY}" letter-spacing="-0.4">{_esc(title)}</text>
    <text x="34" y="88" class="s" font-size="13.5" fill="{TEXT_SECONDARY}">{_esc(subtitle)}</text>
    <g transform="rotate(-4 1102 54)" opacity="0.92">
      <image x="{round(1102 - cw / 2, 1)}" y="{round(54 - ch / 2, 1)}" width="{cw}" height="{ch}" preserveAspectRatio="xMidYMid meet" href="{uri}" xlink:href="{uri}"/>
    </g>
  </g>

  <rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="16" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.14" stroke-width="1"/>
  <line x1="20" y1="1.5" x2="{w - 20}" y2="1.5" stroke="#FFFFFF" stroke-opacity="0.08" stroke-width="1"/>
</svg>
"""


def build_social_preview(tag: str) -> None:
    from playwright.sync_api import sync_playwright

    ver = release_name(tag)
    w, h = 1280, 640
    blk_raw = _load_cropped_webp_uri(CTRL_DIR / "x20_pro/native/front-full.png", 680)
    wht_raw = _load_cropped_webp_uri(CTRL_DIR / "x20_pro/finishes/white/front-full.png", 560)
    blk_uri, bw, bh = _fit_box(blk_raw, 480, 326)
    wht_uri, ww, wh = _fit_box(wht_raw, 390, 260)

    svg = f"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 {w} {h}" width="{w}" height="{h}">
  <style>
    .s {{ font-family: {FONT_SANS}; }}
    .m {{ font-family: {FONT_MONO}; }}
  </style>
{_obsidian_frame_defs("soc", "74%", "50%")}
    <radialGradient id="soc-shadow-a" cx="50%" cy="50%" r="50%">
      <stop offset="0%" stop-color="#020306" stop-opacity="0.88"/>
      <stop offset="100%" stop-color="#020306" stop-opacity="0"/>
    </radialGradient>
  </defs>
  <rect width="{w}" height="{h}" fill="url(#soc-bg)"/>
  <rect width="{w}" height="{h}" fill="url(#soc-softbox)"/>
  <rect width="{w}" height="{h}" fill="url(#soc-key)"/>

  <!-- Subtle architectural frame inside 1280x640 social preview -->
  <rect x="28" y="28" width="{w - 56}" height="{h - 56}" rx="24" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.14" stroke-width="1"/>

  <!-- Right-side studio duo using real X20 Pro White + Black cutouts -->
  <ellipse cx="835" cy="396" rx="185" ry="22" fill="url(#soc-shadow-a)"/>
  <g transform="rotate(-7 825 232)" opacity="0.95">
    <image x="{round(825 - ww / 2, 1)}" y="{round(232 - wh / 2, 1)}" width="{ww}" height="{wh}" preserveAspectRatio="xMidYMid meet" href="{wht_uri}" xlink:href="{wht_uri}"/>
  </g>
  <ellipse cx="978" cy="516" rx="225" ry="24" fill="url(#soc-shadow-a)"/>
  <g transform="rotate(4 978 360)">
    <image x="{round(978 - bw / 2, 1)}" y="{round(360 - bh / 2, 1)}" width="{bw}" height="{bh}" preserveAspectRatio="xMidYMid meet" href="{blk_uri}" xlink:href="{blk_uri}"/>
  </g>

  <!-- Left copy -->
  <text x="86" y="138" class="m" font-size="14" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="2.8">OPEN-SOURCE CONTROLLER STUDIO</text>
  <text x="86" y="252" class="s" font-size="96" font-weight="800" fill="{TEXT_PRIMARY}" letter-spacing="-2.5">X20CTL</text>
  <rect x="86" y="286" width="108" height="36" rx="18" fill="#161D2B" stroke="{BORDER_SILVER}" stroke-opacity="0.24" stroke-width="1"/>
  <text x="140" y="309" text-anchor="middle" class="m" font-size="15" font-weight="600" fill="#DCE5F5">{_esc(ver)}</text>

  <text x="86" y="378" class="s" font-size="28" font-weight="400" fill="#B7C2D5">Configure, tune curves &amp; RGB</text>
  <text x="86" y="416" class="s" font-size="28" font-weight="400" fill="#B7C2D5">for EasySMX controllers</text>

  <line x1="86" y1="472" x2="540" y2="472" stroke="{BORDER_SILVER}" stroke-opacity="0.12" stroke-width="1"/>
  <text x="86" y="510" class="m" font-size="14.5" fill="{TEXT_MUTED}">Windows 10/11 · Linux x64 · 100% Offline Agent · GPL-3.0</text>
</svg>
"""
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", suffix=".html", delete=False) as tf:
        tf.write(
            "<!doctype html><html><body style='margin:0;background:#080A0F;overflow:hidden;'>"
            + svg
            + "</body></html>"
        )
        tmp_path = tf.name
    try:
        with sync_playwright() as p:
            browser = p.chromium.launch()
            page = browser.new_page(viewport={"width": w, "height": h}, device_scale_factor=1)
            page.goto(Path(tmp_path).as_uri(), wait_until="load")
            page.screenshot(path=str(SOCIAL_PREVIEW_PATH))
            browser.close()
    finally:
        os.unlink(tmp_path)


def generate_all(tag: str, social_preview: bool = False) -> list[Path]:
    README_ASSETS.mkdir(parents=True, exist_ok=True)
    outputs: list[tuple[Path, str]] = [
        (README_ASSETS / "hero.svg", build_hero_svg(tag)),
        (README_ASSETS / "thanks.svg", build_thanks_svg()),
        (README_ASSETS / "controllers-lineup.svg", build_controllers_lineup_svg()),
        (README_ASSETS / "themes-showcase.svg", build_themes_showcase_svg()),
        (README_ASSETS / "protocol.svg", build_protocol_svg()),
        (
            README_ASSETS / "header-changelog.svg",
            build_guide_header_svg(
                "hchg",
                "RELEASE HISTORY",
                "Changelog",
                "Release history, KeyLinker protocol fixes & milestone notes",
                "x20_pro/native/front-full.png",
            ),
        ),
        (
            README_ASSETS / "header-troubleshoot.svg",
            build_guide_header_svg(
                "htrb",
                "SUPPORT GUIDE",
                "If Your Controller Isn't Working",
                "Quick fixes for XInput / DInput, dongle + BLE sync, HidHide & Linux permissions",
                "x20_pro/finishes/white/front-full.png",
            ),
        ),
        (
            README_ASSETS / "header-linux.svg",
            build_guide_header_svg(
                "hlnx",
                "LINUX USER GUIDE",
                "X20CTL Studio on Linux",
                "AppImage, Flatpak, tarballs, exclusive evdev capture & /dev/uinput rules",
                "x20/native/front-full.png",
            ),
        ),
    ]
    written: list[Path] = []
    for path, content in outputs:
        ET.fromstring(content)
        path.write_text(content, encoding="utf-8")
        written.append(path)
    if social_preview:
        build_social_preview(tag)
        written.append(SOCIAL_PREVIEW_PATH)
    return written


def main() -> None:
    parser = argparse.ArgumentParser(description="Build Obsidian Hardware Showroom README artwork.")
    parser.add_argument("--tag", default="v0.025", help="Release tag (e.g. v0.025)")
    parser.add_argument("--social-preview", action="store_true", help="Also render assets/social-preview.png (1280x640)")
    args = parser.parse_args()
    written = generate_all(args.tag, social_preview=args.social_preview)
    print(f"Generated {len(written)} Obsidian assets for {args.tag}.")


if __name__ == "__main__":
    main()

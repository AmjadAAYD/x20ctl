"""Generate the NieR:Automata YoRHa beige illustrated README slices in assets/readme/bg/.

Usage:
    python tools/build_readme_art.py [--tag v0.025] [--shots <captured-shots-dir>] [--export-webp]
"""
from __future__ import annotations

import argparse
import base64
import html
import io
from pathlib import Path
from PIL import Image

from version_name import name as release_name

ROOT = Path(__file__).resolve().parents[1]
BG_DIR = ROOT / "assets" / "readme" / "bg"
SRC_DIR = ROOT / "assets" / "readme" / "src"
DOCS_SHOTS = ROOT / "docs" / "screenshots"

# Unified NieR:Automata YoRHa Parchment Palette (strictly zero dark/black page canvas)
PAPER_L = "#DDD7C3"
PAPER_MID = "#DAD4BE"
PAPER_R = "#D5CEB6"
PAPER_EDGE = "#CFC7AE"
CARD_BG = "#E7E2CE"
CARD_ALT = "#DFD9C4"
SHADOW = "#BDB69E"
INK = "#454138"
INK_DARK = "#35322B"
INK_MUTED = "#6F6858"
RULE = "#9E9781"
RAIL = "#BCB49A"
CREAM = "#EFEAD8"
ACCENT = "#A84A3A"

FONT_SANS = '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif'
FONT_MONO = 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace'

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
    ("channel", "Channel", "Gold finder", "#C99A00"),
    ("undertow", "Undertow", "Deep water", "#0284C7"),
    ("alert", "Alert", "Violet lime", "#65A30D"),
    ("slate", "Slate", "Relief map", "#D97706"),
    ("multiverse", "Multiverse", "Red signal", "#DC2626"),
]


def _esc(s: str) -> str:
    return html.escape(s, quote=True)


def _webp_uri(path: Path, width: int | None = None, quality: int = 84) -> str:
    if width is None and path.suffix.lower() == ".webp":
        b64 = base64.b64encode(path.read_bytes()).decode("ascii")
        return f"data:image/webp;base64,{b64}"
    with Image.open(path) as im:
        rgb = im.convert("RGBA") if im.mode in ("RGBA", "LA") else im.convert("RGB")
        if width and rgb.width != width:
            h = max(1, round(rgb.height * (width / rgb.width)))
            rgb = rgb.resize((width, h), Image.Resampling.LANCZOS)
        buf = io.BytesIO()
        rgb.save(buf, format="WEBP", quality=quality, method=6)
    b64 = base64.b64encode(buf.getvalue()).decode("ascii")
    return f"data:image/webp;base64,{b64}"


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


def _base_defs(uid: str, extra_defs: str = "", extra_css: str = "") -> str:
    # Horizontal-only gradient so stacked slices join seamlessly top-to-bottom.
    return f"""  <defs>
    <linearGradient id="bg_{uid}" x1="0%" y1="0%" x2="100%" y2="0%">
      <stop offset="0%" stop-color="{PAPER_EDGE}"/>
      <stop offset="5%" stop-color="{PAPER_L}"/>
      <stop offset="50%" stop-color="{PAPER_MID}"/>
      <stop offset="95%" stop-color="{PAPER_R}"/>
      <stop offset="100%" stop-color="{PAPER_EDGE}"/>
    </linearGradient>
    <pattern id="scan_{uid}" width="8" height="4" patternUnits="userSpaceOnUse">
      <line x1="0" y1="0" x2="8" y2="0" stroke="{RAIL}" stroke-width="0.6" opacity="0.22"/>
    </pattern>
    {extra_defs}
    <style>
      .s {{ font-family: {FONT_SANS}; }}
      .m {{ font-family: {FONT_MONO}; }}
      {extra_css}
    </style>
  </defs>"""


def _slice_bg(uid: str, w: int, h: int, left_rail: bool = True, right_rail: bool = True, x_offset: int = 0) -> str:
    # Continuous x-mapped gradient in global [0..1000] space so cropped button slices match edge-to-edge.
    lines = [
        f'  <rect x="{-x_offset}" y="0" width="1000" height="{h}" fill="url(#bg_{uid})"/>',
        f'  <rect x="{-x_offset}" y="0" width="1000" height="{h}" fill="url(#scan_{uid})"/>',
    ]
    if left_rail:
        rx = 26 - x_offset
        if 0 <= rx <= w:
            lines.append(f'  <line x1="{rx}" y1="0" x2="{rx}" y2="{h}" stroke="{RAIL}" stroke-width="1"/>')
            lines.append(f'  <line x1="{rx + 4}" y1="0" x2="{rx + 4}" y2="{h}" stroke="{RAIL}" stroke-width="0.6" opacity="0.65"/>')
    if right_rail:
        rx = 974 - x_offset
        if 0 <= rx <= w:
            lines.append(f'  <line x1="{rx}" y1="0" x2="{rx}" y2="{h}" stroke="{RAIL}" stroke-width="1"/>')
            lines.append(f'  <line x1="{rx - 4}" y1="0" x2="{rx - 4}" y2="{h}" stroke="{RAIL}" stroke-width="0.6" opacity="0.65"/>')
    return "\n".join(lines)


def _wrap_svg(
    uid: str,
    w: int,
    h: int,
    aria: str,
    body: str,
    extra_defs: str = "",
    extra_css: str = "",
    left_rail: bool = True,
    right_rail: bool = True,
    x_offset: int = 0,
) -> str:
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" '
        f'viewBox="0 0 {w} {h}" width="{w}" height="{h}" role="img" aria-label="{_esc(aria)}">\n'
        + _base_defs(uid, extra_defs, extra_css)
        + "\n"
        + _slice_bg(uid, w, h, left_rail=left_rail, right_rail=right_rail, x_offset=x_offset)
        + "\n"
        + body
        + "\n</svg>\n"
    )


def _filigree(y: float, x1: int = 26, x2: int = 974) -> str:
    # Classic YoRHa alternating squares and tiny diamonds
    elems = [
        f'<line x1="{x1}" y1="{y}" x2="{x2}" y2="{y}" stroke="{INK}" stroke-width="1.2"/>',
        f'<line x1="{x1}" y1="{y + 6}" x2="{x2}" y2="{y + 6}" stroke="{RULE}" stroke-width="0.8"/>',
    ]
    x = x1 + 14
    step = 26
    while x < x2 - 12:
        elems.append(f'<polygon points="{x},{y - 3} {x + 3.5},{y} {x},{y + 3} {x - 3.5},{y}" fill="{INK}"/>')
        if x + 13 < x2 - 10:
            elems.append(f'<rect x="{x + 11.5}" y="{y - 1.5}" width="3" height="3" fill="{RULE}"/>')
        x += step
    return "\n  ".join(elems)


def _heading(x: int, y: int, num: str, title: str, right_tag: str = "", end_x: int = 948) -> str:
    line_y = y + 24
    right = (
        f'<text x="{end_x}" y="{y + 15}" text-anchor="end" class="m" font-size="11" fill="{INK_MUTED}" letter-spacing="1.4">{_esc(right_tag)}</text>'
        if right_tag
        else ""
    )
    return f"""  <rect x="{x}" y="{y - 2}" width="5" height="22" fill="{INK}"/>
  <rect x="{x + 8}" y="{y - 2}" width="2" height="22" fill="{INK}"/>
  <rect x="{x + 17}" y="{y + 5}" width="8" height="8" fill="{INK}"/>
  <text x="{x + 33}" y="{y + 16}" class="s" font-size="20" font-weight="700" fill="{INK_DARK}" letter-spacing="2.4">{_esc(title)}</text>
  <text x="{x + 33}" y="{y - 8}" class="m" font-size="10.5" fill="{INK_MUTED}" letter-spacing="1.6">ARCHIVE // {num}</text>
  {right}
  <line x1="{x}" y1="{line_y}" x2="{end_x - 8}" y2="{line_y}" stroke="{INK}" stroke-width="1"/>
  <polygon points="{end_x - 4},{line_y - 3} {end_x},{line_y} {end_x - 4},{line_y + 3} {end_x - 8},{line_y}" fill="{INK}"/>"""


def _shot_frame(uid: str, x: int, y: int, w: int, h: int, uri: str, title: str, tag: str = "") -> tuple[str, str]:
    bar_h = 22
    img_x, img_y = x + 4, y + bar_h + 4
    img_w, img_h = w - 8, h - bar_h - 8
    clip = f'<clipPath id="clip_{uid}"><rect x="{img_x}" y="{img_y}" width="{img_w}" height="{img_h}"/></clipPath>'
    body = f"""  <rect x="{x + 4}" y="{y + 4}" width="{w}" height="{h}" fill="{SHADOW}"/>
  <rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{CARD_BG}" stroke="{INK}" stroke-width="1"/>
  <rect x="{x}" y="{y}" width="{w}" height="{bar_h}" fill="{INK}"/>
  <rect x="{x + 7}" y="{y + 7}" width="7" height="7" fill="{CREAM}"/>
  <text x="{x + 20}" y="{y + 15}" class="m" font-size="10.5" font-weight="700" fill="{CREAM}" letter-spacing="1">{_esc(title)}</text>
  <text x="{x + w - 8}" y="{y + 14.5}" text-anchor="end" class="m" font-size="9.5" fill="#C9C2AC">{_esc(tag)}</text>
  <image href="{uri}" x="{img_x}" y="{img_y}" width="{img_w}" height="{img_h}" preserveAspectRatio="xMidYMid slice" clip-path="url(#clip_{uid})"/>"""
    return clip, body


def _card(x: int, y: int, w: int, h: int, fill: str = CARD_BG) -> str:
    return (
        f'  <rect x="{x + 4}" y="{y + 4}" width="{w}" height="{h}" fill="{SHADOW}"/>\n'
        f'  <rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" stroke="{INK}" stroke-width="1"/>\n'
        f'  <line x1="{x}" y1="{y + 3}" x2="{x + w}" y2="{y + 3}" stroke="{INK}" stroke-width="2.2"/>'
    )


def _btn_row(row_name: str, height: int, segments: list[dict]) -> list[str]:
    """Generate horizontally aligned SVG slices summing cleanly to 1000px."""
    total_w = sum(s["w"] for s in segments)
    if total_w != 1000:
        raise ValueError(f"{row_name}: widths sum to {total_w}, expected 1000")
    x_cur = 0
    written: list[str] = []
    for idx, seg in enumerate(segments):
        sw = seg["w"]
        uid = f"{row_name}_{idx}"
        body_parts: list[str] = []
        if seg.get("kind") == "btn":
            bx = seg.get("bx", 4)
            bw = sw - bx - seg.get("br", 4)
            by, bh = 8, height - 18
            primary = seg.get("primary", False)
            if primary:
                body_parts.append(f'  <rect x="{bx + 3}" y="{by + 3}" width="{bw}" height="{bh}" fill="{SHADOW}"/>')
                body_parts.append(f'  <rect x="{bx}" y="{by}" width="{bw}" height="{bh}" fill="{INK}" stroke="{INK}" stroke-width="1"/>')
                body_parts.append(f'  <rect x="{bx + 12}" y="{by + bh/2 - 4:.1f}" width="8" height="8" fill="{CREAM}"/>')
                body_parts.append(
                    f'  <text x="{bx + 28}" y="{by + bh/2 + 4.5:.1f}" class="s" font-size="13.5" font-weight="700" fill="{CREAM}" letter-spacing="0.4">{_esc(seg["label"])}</text>'
                )
            else:
                body_parts.append(f'  <rect x="{bx + 3}" y="{by + 3}" width="{bw}" height="{bh}" fill="{SHADOW}"/>')
                body_parts.append(f'  <rect x="{bx}" y="{by}" width="{bw}" height="{bh}" fill="{CARD_BG}" stroke="{INK}" stroke-width="1"/>')
                body_parts.append(f'  <rect x="{bx + 11}" y="{by + bh/2 - 3.5:.1f}" width="7" height="7" fill="{INK}"/>')
                body_parts.append(
                    f'  <text x="{bx + 25}" y="{by + bh/2 + 4.5:.1f}" class="s" font-size="12.5" font-weight="600" fill="{INK_DARK}">{_esc(seg["label"])}</text>'
                )
                body_parts.append(
                    f'  <text x="{bx + bw - 11}" y="{by + bh/2 + 4:.1f}" text-anchor="end" class="m" font-size="12" fill="{INK_MUTED}">→</text>'
                )
        svg = _wrap_svg(
            uid,
            sw,
            height,
            seg.get("aria", seg.get("label", "")),
            "\n".join(body_parts),
            x_offset=x_cur,
        )
        out = BG_DIR / f"{seg['file']}.svg"
        out.write_text(svg, encoding="utf-8")
        written.append(out.name)
        x_cur += sw
    return written


def build_all(ver_label: str, tag: str) -> None:
    BG_DIR.mkdir(parents=True, exist_ok=True)
    # 1. hero.svg (1000x366)
    rgb_uri = _webp_uri(DOCS_SHOTS / "rgb-studio.webp", width=560)
    h_clip, h_frame = _shot_frame("hero_shot", 514, 42, 434, 270, rgb_uri, "RGB STUDIO · X20 PRO", ver_label)
    hero_css = """
      @keyframes cmdCycle {
        0%, 16% { opacity: 1; }
        19%, 100% { opacity: 0; }
      }
      @keyframes cursorBlink {
        0%, 49% { opacity: 1; }
        50%, 100% { opacity: 0.15; }
      }
      .cl { opacity: 0; animation: cmdCycle 17.5s linear infinite; }
      .cur { animation: cursorBlink 1.1s steps(1) infinite; }
    """
    cmds = [
        "remap every button &amp; rear paddle",
        "drive X20 Pro RGB &amp; 160x80 screen",
        "ask offline Agent XCTL (3,048 phrases)",
        "Xbox 360 / PS4 PC mode + 64-key macros",
        "6 native themes · 10 languages · Win &amp; Linux",
    ]
    cmd_svg = "\n".join(
        f'  <text class="cl m" style="animation-delay:{idx * 3.5:.1f}s" x="54" y="246" font-size="14.5" fill="{INK_DARK}">'
        f'<tspan fill="{ACCENT}" font-weight="700">■ </tspan>{label}</text>'
        for idx, label in enumerate(cmds)
    )
    hero_body = f"""  {_filigree(16)}
  <text x="54" y="54" class="m" font-size="11.5" font-weight="700" fill="{INK_MUTED}" letter-spacing="2">REVERSE-ENGINEERED · OPEN SOURCE · <tspan id="hero-version">{ver_label}</tspan></text>
  <text x="54" y="138" class="s" font-size="82" font-weight="800" fill="#C4BDA5" letter-spacing="-2.5">X20CTL</text>
  <text x="50" y="134" class="s" font-size="82" font-weight="800" fill="{INK_DARK}" letter-spacing="-2.5">X20CTL</text>
  <line x1="54" y1="154" x2="468" y2="154" stroke="{INK}" stroke-width="1.6"/>
  <rect x="468" y="150.5" width="7" height="7" fill="{INK}"/>
  <text x="54" y="192" class="s" font-size="22" font-weight="700" fill="{INK_DARK}">A controller studio for EasySMX pads.</text>
  <rect x="52" y="218" width="424" height="44" fill="{CARD_BG}" stroke="{RULE}" stroke-width="1"/>
  <rect x="52" y="218" width="4" height="44" fill="{INK}"/>
{cmd_svg}
  <rect class="cur" x="458" y="233" width="7" height="14" fill="{INK}"/>
  <text x="54" y="294" class="m" font-size="12" font-weight="700" fill="{INK_DARK}" letter-spacing="1">{ver_label} — FINAL RELEASE FOR NOW</text>
  <text x="54" y="315" class="m" font-size="11.5" fill="{INK_MUTED}">Windows 10/11 (WPF) · Linux x64 (Avalonia) · GPL-3.0-or-later</text>
{h_frame}"""
    (BG_DIR / "hero.svg").write_text(
        _wrap_svg("hero", 1000, 344, f"X20CTL {ver_label}: a controller studio for EasySMX pads", hero_body, extra_defs=h_clip, extra_css=hero_css),
        encoding="utf-8",
    )

    # 2. Primary nav button row (nav1..nav4 = 1000x68)
    _btn_row("nav", 68, [
        {"file": "nav1", "w": 270, "kind": "btn", "primary": True, "bx": 52, "br": 8, "label": f"Download {ver_label}", "aria": f"Download {ver_label}"},
        {"file": "nav2", "w": 230, "kind": "btn", "bx": 6, "br": 6, "label": "Reverse engineering", "aria": "Reverse engineering"},
        {"file": "nav3", "w": 220, "kind": "btn", "bx": 6, "br": 6, "label": "Changelog", "aria": "Changelog"},
        {"file": "nav4", "w": 280, "kind": "btn", "bx": 6, "br": 52, "label": "Controller not working?", "aria": "Controller not working?"},
    ])

    # 3. thanks.svg (1000x268) — White & Black X20 Pro flanking Kiin & Team EasySMX
    w_pad = _webp_uri(SRC_DIR / "x20pro-white.webp")
    b_pad = _webp_uri(SRC_DIR / "x20pro-black.webp")
    thanks_body = f"""{_card(52, 16, 896, 232)}
  <image href="{w_pad}" x="72" y="34" width="218" height="166" preserveAspectRatio="xMidYMid meet"/>
  <text x="181" y="220" text-anchor="middle" class="m" font-size="10.5" fill="{INK_MUTED}" letter-spacing="1.6">X20 PRO · WHITE</text>
  <image href="{b_pad}" x="710" y="36" width="218" height="163" preserveAspectRatio="xMidYMid meet"/>
  <text x="819" y="220" text-anchor="middle" class="m" font-size="10.5" fill="{INK_MUTED}" letter-spacing="1.6">X20 PRO · BLACK</text>
  <path d="M500 59 C493 48, 478 51, 478 63 C478 73, 500 84, 500 84 C500 84, 522 73, 522 63 C522 51, 507 48, 500 59 Z" fill="{ACCENT}"/>
  <text x="500" y="106" text-anchor="middle" class="m" font-size="11.5" font-weight="700" fill="{INK_MUTED}" letter-spacing="2.8">WITH THANKS</text>
  <text x="500" y="138" text-anchor="middle" class="s" font-size="25" font-weight="800" fill="{INK_DARK}">Thank you, EasySMX Kiin</text>
  <text x="500" y="168" text-anchor="middle" class="s" font-size="25" font-weight="800" fill="{INK_DARK}">&amp; Team EasySMX</text>
  <line x1="395" y1="184" x2="605" y2="184" stroke="{INK}" stroke-width="1.6"/>
  <text x="500" y="210" text-anchor="middle" class="s" font-size="14" fill="{INK}">for supplying an EasySMX X20 Pro to build and test X20CTL.</text>"""
    (BG_DIR / "thanks.svg").write_text(
        _wrap_svg("thanks", 1000, 268, "Thank you EasySMX Kiin and Team EasySMX for supplying an EasySMX X20 Pro to build and test X20CTL.", thanks_body),
        encoding="utf-8",
    )

    # 4. demo.svg (1000x590) — Animated V0.025 Studio Carousel in YoRHa Museum Frame
    slides = [
        ("RGB Studio (X20 Pro)", "4-zone live LED control, swatches, battery gauge, and simultaneous 2.4 GHz dongle + BLE", "rgb-studio"),
        ("Live Lighting Engine", "Rainbow Wave, Trigger Glow, Stick Heat, Button Flash & Ambient Screen Sync", "rgb-effects"),
        ("Built-in 160x80 Screen", "Upload static images or animated GIFs over USB serial or export .bin projects", "screen"),
        ("100% Offline Agent XCTL", "3,048 handwritten phrases, Tab autocomplete, Check-up, RGB cards & Undo", "agent-xctl"),
        ("Buttons & Rear Paddles", "Listen-to-bind front & rear controls, local drafts, and setup presets", "buttons"),
        ("Stick & Trigger Curves", "Sculpt custom curves, presets, inner/outer dead zones, and keyboard testing", "curves"),
        ("Keyboard Sequences & Mouse", "Map 64-key custom chains, M1–M7 mouse clicks, and View + Y mouse toggle", "keyboard"),
        ("4-Zone Vibration & 200% Boost", "Isolated grip and trigger sliders plus up to 200% PC Mode rumble amplification", "vibration"),
    ]
    n_slides = len(slides)
    step_s = 3.5
    total_s = n_slides * step_s
    p_hold = round(100 / n_slides, 2)
    p_end = round(100 / n_slides + 2.0, 2)
    demo_css = f"""
      @keyframes slideFade {{
        0% {{ opacity: 0; }}
        2%, {p_hold}% {{ opacity: 1; }}
        {p_end}%, 100% {{ opacity: 0; }}
      }}
      @keyframes sweepBar {{
        0% {{ transform: scaleX(0); }}
        100% {{ transform: scaleX(1); }}
      }}
      .sl {{ opacity: 0; animation: slideFade {total_s:.1f}s linear infinite; }}
      .pbar {{ transform-origin: 68px 546px; animation: sweepBar {step_s:.1f}s linear infinite; }}
    """
    clip_demo = '<clipPath id="clip_demo"><rect x="68" y="54" width="864" height="486"/></clipPath>'
    slide_nodes = []
    for idx, (stitle, scap, slug) in enumerate(slides):
        uri = _webp_uri(DOCS_SHOTS / f"{slug}.webp", width=900)
        delay = idx * step_s - 0.7
        slide_nodes.append(
            f'  <g class="sl" style="animation-delay:{delay:.1f}s">\n'
            f'    <image href="{uri}" x="68" y="54" width="864" height="486" preserveAspectRatio="xMidYMid slice" clip-path="url(#clip_demo)"/>\n'
            f'    <text x="68" y="566" class="s" font-size="17" font-weight="700" fill="{INK_DARK}">{_esc(stitle)}</text>\n'
            f'    <text x="68" y="586" class="s" font-size="13" fill="{INK_MUTED}">{_esc(scap)}</text>\n'
            f'    <text x="932" y="566" text-anchor="end" class="m" font-size="11.5" font-weight="700" fill="{INK_MUTED}">FRAME {idx + 1:02d} / {n_slides:02d}</text>\n'
            f'  </g>'
        )
    demo_body = f"""  <rect x="56" y="18" width="896" height="584" fill="{SHADOW}"/>
  <rect x="52" y="14" width="896" height="584" fill="{CARD_BG}" stroke="{INK}" stroke-width="1"/>
  <rect x="52" y="14" width="896" height="28" fill="{INK}"/>
  <rect x="64" y="23" width="9" height="9" fill="{CREAM}"/>
  <text x="82" y="33" class="m" font-size="11.5" font-weight="700" fill="{CREAM}" letter-spacing="1.6">UNIT MONITOR // A TOUR OF X20CTL {ver_label}</text>
  <text x="934" y="32.5" text-anchor="end" class="m" font-size="10.5" fill="#C9C2AC">OFFLINE STUDIO CAPTURE</text>
{chr(10).join(slide_nodes)}
  <rect x="68" y="546" width="864" height="3" fill="{CARD_ALT}"/>
  <rect class="pbar" x="68" y="546" width="864" height="3" fill="{INK}"/>"""
    (BG_DIR / "demo.svg").write_text(
        _wrap_svg("demo", 1000, 616, "Animated tour of X20CTL V0.025", demo_body, extra_defs=clip_demo, extra_css=demo_css),
        encoding="utf-8",
    )

    # 5. intro.svg (1000x268) — Independent project notice + What it is
    intro_body = f"""{_card(52, 12, 896, 82, fill=CARD_ALT)}
  <rect x="52" y="12" width="5" height="82" fill="{INK}"/>
  <text x="72" y="38" class="s" font-size="13.5" fill="{INK_DARK}"><tspan font-weight="700">Independent project.</tspan> X20CTL is open-source software. EasySMX supplied an X20 Pro to help develop it, but</text>
  <text x="72" y="59" class="s" font-size="13.5" fill="{INK_DARK}">the app is not made, endorsed, sponsored or supported by EasySMX. Product names belong to their owners</text>
  <text x="72" y="80" class="s" font-size="13.5" fill="{INK_DARK}">and are used only to say which controllers X20CTL works with.</text>

{_heading(52, 134, "01", "WHAT IT IS")}
  <text x="52" y="186" class="s" font-size="14.5" fill="{INK_DARK}">X20CTL is a desktop studio for EasySMX controllers on Windows (WPF + C++) and Linux (Avalonia). Remap</text>
  <text x="52" y="208" class="s" font-size="14.5" fill="{INK_DARK}">buttons, sculpt stick &amp; trigger curves, edit X20 Pro RGB &amp; built-in screen art, preview 4-zone vibration, talk to</text>
  <text x="52" y="230" class="s" font-size="14.5" fill="{INK_DARK}">100% offline Agent XCTL, or run low-latency PC Mode across up to four players — with no account or web shell.</text>"""
    (BG_DIR / "intro.svg").write_text(
        _wrap_svg("intro", 1000, 254, "Independent project notice and what X20CTL is", intro_body),
        encoding="utf-8",
    )

    # 6. reverse_eng.svg (1000x390) + link row
    pkts = [("OPCODE", "1B command"), ("LENGTH", "payload size"), ("SERIAL", "session ctr"), ("NONCE", "key index"), ("PAYLOAD", "up to 15B"), ("CRC-8", "poly 0xEB")]
    pkt_svg = []
    px = 52
    pw = 142
    for title, sub in pkts:
        pkt_svg.append(
            f'  <rect x="{px + 2}" y="140" width="{pw}" height="52" fill="{SHADOW}"/>\n'
            f'  <rect x="{px}" y="138" width="{pw}" height="52" fill="{CARD_BG}" stroke="{INK}" stroke-width="1"/>\n'
            f'  <rect x="{px}" y="138" width="{pw}" height="18" fill="{INK}"/>\n'
            f'  <text x="{px + pw/2:.1f}" y="151" text-anchor="middle" class="m" font-size="10.5" font-weight="700" fill="{CREAM}">{title}</text>\n'
            f'  <text x="{px + pw/2:.1f}" y="176" text-anchor="middle" class="s" font-size="12" fill="{INK_DARK}">{sub}</text>'
        )
        px += pw + 8

    steps = ["1. Decompile", "2. Capture BLE", "3. Diff 1 setting", "4. Verify read-back", "5. Ship in Studio"]
    pipe_svg = []
    sx = 52
    sw = 166
    for i, st in enumerate(steps):
        pipe_svg.append(
            f'  <rect x="{sx}" y="206" width="{sw}" height="32" fill="{CARD_ALT}" stroke="{RULE}" stroke-width="1"/>\n'
            f'  <text x="{sx + sw/2:.1f}" y="226" text-anchor="middle" class="s" font-size="12" font-weight="700" fill="{INK_DARK}">{st}</text>'
        )
        if i < len(steps) - 1:
            ax = sx + sw + 3
            pipe_svg.append(f'  <text x="{ax + 6}" y="226" text-anchor="middle" class="m" font-size="12" fill="{INK_MUTED}">→</text>')
        sx += sw + 16

    re_body = f"""{_heading(52, 30, "02", "REVERSE ENGINEERING", "KEYLINKER WIRE PROTOCOL")}
  <text x="52" y="82" class="s" font-size="14" fill="{INK_DARK}">EasySMX configures its pads from KeyLinker on mobile. X20CTL recovers those packets on real hardware, verifies</text>
  <text x="52" y="103" class="s" font-size="14" fill="{INK_DARK}">every setting by reading it back, and scrambles each 20-byte GATT message with the protocol keystreams:</text>
{chr(10).join(pkt_svg)}
{chr(10).join(pipe_svg)}
  <text x="52" y="266" class="s" font-size="13.5" fill="{INK_DARK}"><tspan font-weight="700">■ Transport &amp; Safety:</tspan> Bluetooth LE GATT (+ USB serial for X20 Pro screen). OTA firmware service is untouched: X20CTL never flashes firmware.</text>
  <text x="52" y="289" class="s" font-size="13.5" fill="{INK_DARK}"><tspan font-weight="700">■ Accurate ID:</tspan> Matches hardware BLE / USB descriptors (avoiding generic 045E:028E &amp; STK-7024X / 0079:181C DInput traps).</text>
  <text x="52" y="312" class="s" font-size="13.5" fill="{INK_DARK}"><tspan font-weight="700">■ Read-back rule:</tspan> Nothing writes to onboard memory unless read-back is verified; everything else uses local drafts + PC Mode.</text>
  <text x="52" y="335" class="s" font-size="13.5" fill="{INK_DARK}"><tspan font-weight="700">■ Research boundary:</tspan> Raw captures stay private; only sanitized protocol docs and templates ship publicly.</text>"""
    (BG_DIR / "reverse_eng.svg").write_text(
        _wrap_svg("re", 1000, 354, "Reverse engineering KeyLinker packet structure and hardware safety rules", re_body),
        encoding="utf-8",
    )
    _btn_row("re_links", 54, [
        {"file": "re_links_0", "w": 52, "kind": "pad"},
        {"file": "re_links_1", "w": 224, "kind": "btn", "bx": 0, "br": 8, "label": "Protocol reference", "aria": "docs/01-protocol.md"},
        {"file": "re_links_2", "w": 236, "kind": "btn", "bx": 0, "br": 8, "label": "Reverse-engineering log", "aria": "docs/00-findings.md"},
        {"file": "re_links_3", "w": 218, "kind": "btn", "bx": 0, "br": 8, "label": "Controller scan kit", "aria": "docs/controller-scan-kit/"},
        {"file": "re_links_4", "w": 218, "kind": "btn", "bx": 0, "br": 0, "label": "Research policy", "aria": "docs/research-policy.md"},
        {"file": "re_links_f", "w": 52, "kind": "pad"},
    ])

    # 7. Tour slices (clean twin parchment plates, concise captions)
    def make_pair_slice(
        fname: str,
        height: int,
        heading_num: str | None,
        heading_title: str | None,
        sub1: tuple[str, str, str, str],
        sub2: tuple[str, str, str, str],
        foot_lines: list[str],
    ) -> None:
        parts = []
        clips = []
        top_y = 14
        if heading_num and heading_title:
            parts.append(_heading(52, 28, heading_num, heading_title, "V0.025 FEATURE TOUR"))
            top_y = 74
        for idx, (slug, title, tag_str, desc) in enumerate((sub1, sub2)):
            cx = 52 if idx == 0 else 508
            cw = 440
            uri = _webp_uri(DOCS_SHOTS / f"{slug}.webp", width=520)
            c, f = _shot_frame(f"{fname}_{idx}", cx, top_y, cw, 270, uri, title, tag_str)
            clips.append(c)
            parts.append(f)
            parts.append(f'  <text x="{cx}" y="{top_y + 294}" class="s" font-size="13" font-weight="600" fill="{INK_DARK}">{_esc(desc)}</text>')
        fy = top_y + 322
        for line in foot_lines:
            parts.append(f'  <text x="52" y="{fy}" class="s" font-size="13" fill="{INK_MUTED}">{_esc(line)}</text>')
            fy += 20
        (BG_DIR / f"{fname}.svg").write_text(
            _wrap_svg(fname, 1000, height, heading_title or sub1[1], "\n".join(parts), extra_defs="\n".join(clips)),
            encoding="utf-8",
        )

    make_pair_slice(
        "tour_1",
        438,
        "03",
        "STUDIO TOUR — V0.025",
        ("rgb-studio", "RGB STUDIO · X20 PRO", "4-ZONE", "Live 4-zone colour picker, hex codes & brightness presets"),
        ("rgb-effects", "LIVE LIGHTING ENGINE", "EFFECTS", "Wave, Trigger Glow, Stick Heat, Ambient Sync & Battery gauge"),
        ["Simultaneous 2.4 GHz dongle (413D:2131) + unpaired BLE lighting works automatically whenever PC Bluetooth is enabled."],
    )
    make_pair_slice(
        "tour_2",
        378,
        None,
        None,
        ("screen", "BUILT-IN 160x80 SCREEN · X20 PRO", "USB SERIAL", "Upload images or animated GIFs and export .bin screen projects"),
        ("agent-xctl", "OFFLINE HANDHELD AGENT XCTL", "3,048 PHRASES", "Zero cloud AI: Tab autocomplete, Check-up, RGB cards & Undo"),
        ["Ask Agent XCTL in plain language to check stick drift, explain settings, change X20 Pro LEDs, or restore previous states."],
    )
    make_pair_slice(
        "tour_3",
        378,
        None,
        None,
        ("buttons", "BUTTONS & REAR PADDLES", "REMAP", "Listen-to-bind front buttons and hardware slots (M1–M4 / M1–M6)"),
        ("curves", "STICK & TRIGGER CURVES", "DEAD ZONES", "Built-in response presets, outer caps, and keyboard curve preview"),
        ["Sequence macros record timings on rear paddles with preview timelines; PC Mode uses the same layout for any controller."],
    )
    make_pair_slice(
        "tour_4",
        378,
        None,
        None,
        ("keyboard", "PC MODE · KEYBOARD SEQUENCES", "UP TO 64 KEYS", "Bind real keyboard keys, M1–M7 mouse clicks, or 64-key chains"),
        ("mouse", "CONTROLLER AS MOUSE", "VIEW + Y", "Cursor & scroll control with M1–M7 bindings and 2–30 Hz turbo"),
        ["PC Mode creates an Xbox 360 or PlayStation 4 DualShock 4 pad via ViGEmBus + HidHide (Win) or /dev/uinput (Linux) for up to 4 players."],
    )

    # tour_5: 4-zone vibration + Controller Zone / Diagnostics
    make_pair_slice(
        "tour_5",
        378,
        None,
        None,
        ("vibration", "4-ZONE VIBRATION & 200% BOOST", "4 MOTORS", "Independent grip & trigger tests plus up to 200% game rumble boost"),
        ("diagnostics", "DIAGNOSTICS & TESTER", "READ-ONLY", "Measure raw stick resolution, update rates & center drift safely"),
        ["Switch controller colors per player across finish packs; X20 Pro and Dune render dedicated left and right trigger zones."],
    )

    # tour_6: 6 Native Themes & 10 Languages + Linux Studio (custom layout!)
    t_clips = []
    t_cards = []
    tw, tgap, tx0 = 140, 11, 52
    for idx, (tid, tname, tsub, tacc) in enumerate(THEMES):
        uri = _webp_uri(SRC_DIR / f"theme-{tid}.webp")
        x = tx0 + idx * (tw + tgap)
        iy = 82
        t_clips.append(f'<clipPath id="tc_{idx}"><rect x="{x + 5}" y="{iy}" width="{tw - 10}" height="74"/></clipPath>')
        t_cards.append(
            f'  <rect x="{x + 3}" y="68" width="{tw}" height="128" fill="{SHADOW}"/>\n'
            f'  <rect x="{x}" y="65" width="{tw}" height="128" fill="{CARD_BG}" stroke="{INK}" stroke-width="1"/>\n'
            f'  <rect x="{x}" y="65" width="{tw}" height="4" fill="{tacc}"/>\n'
            f'  <image href="{uri}" x="{x + 5}" y="{iy}" width="{tw - 10}" height="74" preserveAspectRatio="xMidYMid slice" clip-path="url(#tc_{idx})"/>\n'
            f'  <text x="{x + 8}" y="173" class="s" font-size="13" font-weight="700" fill="{INK_DARK}">{tname}</text>\n'
            f'  <text x="{x + 8}" y="187" class="m" font-size="10" fill="{INK_MUTED}">{tsub}</text>'
        )
    lc, lf = _shot_frame("tour_linux", 52, 238, 440, 266, _webp_uri(DOCS_SHOTS / "linux-studio.webp", width=500), "LINUX STUDIO · AVALONIA", "X86_64")
    tc, tf = _shot_frame("tour_check", 508, 238, 440, 266, _webp_uri(DOCS_SHOTS / "controller-check.webp", width=500), "CONTROLLER CHECK", "READ & WRITE SCAN")
    t_clips.extend([lc, tc])
    tour6_body = f"""  <rect x="52" y="18" width="6" height="18" fill="{INK}"/>
  <text x="66" y="33" class="s" font-size="17" font-weight="700" fill="{INK_DARK}" letter-spacing="1.4">6 NATIVE THEMES &amp; 10 LANGUAGES</text>
  <text x="948" y="32" text-anchor="end" class="m" font-size="10.5" fill="{INK_MUTED}" letter-spacing="1">SINGLE REACTIVE SHELL</text>
  <line x1="52" y1="45" x2="948" y2="45" stroke="{RULE}" stroke-width="1"/>
{chr(10).join(t_cards)}
  <text x="500" y="217" text-anchor="middle" class="m" font-size="11" fill="{INK_MUTED}">LANGUAGES: English · 中文 · Français · Deutsch · Polski · Español · 日本語 · Русский · Italiano · Tiếng Việt</text>
{lf}
{tf}
  <text x="52" y="527" class="s" font-size="12.5" font-weight="600" fill="{INK_DARK}">Avalonia Studio mirrors Windows UI (AppImage, Flatpak &amp;</text>
  <text x="52" y="545" class="s" font-size="12.5" fill="{INK_MUTED}">tarballs build on CI; real Linux hardware testing pending).</text>
  <text x="508" y="527" class="s" font-size="12.5" font-weight="600" fill="{INK_DARK}">Read scan records inputs safely; mismatch prompt offers</text>
  <text x="508" y="545" class="s" font-size="12.5" fill="{INK_MUTED}">Enter Studio as detected, Continue, or Cancel safely.</text>"""
    (BG_DIR / "tour_6.svg").write_text(
        _wrap_svg("tour_6", 1000, 566, "6 Native Themes, 10 Languages, Linux Studio and Controller Check", tour6_body, extra_defs="\n".join(t_clips)),
        encoding="utf-8",
    )

    # 8. models.svg (1000x436) — Supported controllers table + choose-controller inset
    mc, mf = _shot_frame("models_pic", 602, 82, 346, 222, _webp_uri(DOCS_SHOTS / "choose-controller.webp", width=440), "7 SUPPORTED CONTROLLERS", "CHOOSE SLOT")
    rows = [
        ("EasySMX X20", "Full on-device (M1–M4) + PC Mode", "Verified"),
        ("EasySMX X20 Pro", "Full + RGB + Screen + Dongle+BLE", "Verified"),
        ("EasySMX X15", "Read-only preview + PC Mode", "PC Mode"),
        ("EasySMX X10", "Experimental KRLY BLE read + PC Mode", "Unverified BLE"),
        ("EasySMX X05 Pro", "Read-only preview + PC Mode", "PC Mode"),
        ("EasySMX D10", "Read-only preview + PC Mode", "PC Mode"),
        ("EasySMX Dune 8K", "4-motor visual + M1–M4 PC Mode", "PC Mode"),
    ]
    tbl = [
        f'  <rect x="52" y="82" width="532" height="270" fill="{SHADOW}"/>',
        f'  <rect x="48" y="78" width="532" height="270" fill="{CARD_BG}" stroke="{INK}" stroke-width="1"/>',
        f'  <rect x="48" y="78" width="532" height="28" fill="{INK}"/>',
        f'  <text x="62" y="96" class="m" font-size="11" font-weight="700" fill="{CREAM}">MODEL</text>',
        f'  <text x="210" y="96" class="m" font-size="11" font-weight="700" fill="{CREAM}">CONFIGURATION &amp; FEATURES</text>',
        f'  <text x="566" y="96" text-anchor="end" class="m" font-size="11" font-weight="700" fill="{CREAM}">STATUS</text>',
    ]
    ry = 128
    for i, (mname, feat, st) in enumerate(rows):
        if i % 2 == 1:
            tbl.append(f'  <rect x="49" y="{ry - 17}" width="530" height="32" fill="{CARD_ALT}"/>')
        tbl.append(f'  <text x="62" y="{ry + 3}" class="s" font-size="13" font-weight="700" fill="{INK_DARK}">{mname}</text>')
        tbl.append(f'  <text x="210" y="{ry + 3}" class="s" font-size="12.5" fill="{INK}">{feat}</text>')
        tbl.append(f'  <text x="566" y="{ry + 3}" text-anchor="end" class="m" font-size="11" font-weight="700" fill="{INK_MUTED}">{st}</text>')
        ry += 32
    models_body = f"""{_heading(52, 28, "04", "SUPPORTED CONTROLLERS", "ALL 7 MODELS")}
{chr(10).join(tbl)}
{mf}
  <text x="602" y="330" class="s" font-size="12.5" fill="{INK_DARK}"><tspan font-weight="700">DInput tip (STK-7024X):</tspan> Switch pads using</text>
  <text x="602" y="349" class="s" font-size="12.5" fill="{INK_DARK}">0079:181C back to XInput for full PC Mode.</text>
  <text x="52" y="378" class="s" font-size="13" fill="{INK_MUTED}">Controller Choice works offline: PC Mode configures every listed pad on this PC without writing to unverified flash.</text>
  <text x="52" y="399" class="s" font-size="13" fill="{INK_MUTED}">X10 Bluetooth read-back was adapted from EasyGamepad and still awaits a physical X10 hardware check.</text>"""
    (BG_DIR / "models.svg").write_text(
        _wrap_svg("models", 1000, 422, "Supported EasySMX controller models and PC Mode capabilities", models_body, extra_defs=mc),
        encoding="utf-8",
    )

    # 9. Download section (download_head + synced clickable rows + download_foot + meta row)
    dl_head_body = f"""{_heading(52, 28, "05", f"DOWNLOAD — {ver_label}", f"TAG {tag}")}
  <text x="52" y="82" class="s" font-size="14" fill="{INK_DARK}"><tspan font-weight="700">Windows 10/11 (x64):</tspan> Grab the installer (recommended, Start menu + uninstaller) or the portable single-file exe.</text>
  <text x="52" y="104" class="s" font-size="14" fill="{INK_DARK}">Everything is bundled (.NET 10, native engine, assets). Check downloads against <tspan class="m" font-size="13">SHA256SUMS.txt</tspan>.</text>"""
    (BG_DIR / "download_head.svg").write_text(
        _wrap_svg("dlh", 1000, 124, f"Download X20CTL {ver_label}", dl_head_body),
        encoding="utf-8",
    )

    _btn_row("dl_win", 54, [
        {"file": "dl_win_0", "w": 52, "kind": "pad"},
        {"file": "dl_win_1", "w": 448, "kind": "btn", "primary": True, "bx": 0, "br": 8, "label": f"X20CTL-{ver_label}-windows-x64-setup.exe", "aria": f"Download X20CTL-{ver_label}-windows-x64-setup.exe"},
        {"file": "dl_win_2", "w": 448, "kind": "btn", "bx": 4, "br": 0, "label": f"X20CTL-{ver_label}-windows-x64.exe (Portable)", "aria": f"Download X20CTL-{ver_label}-windows-x64.exe"},
        {"file": "dl_win_f", "w": 52, "kind": "pad"},
    ])
    _btn_row("dl_lin1", 52, [
        {"file": "dl_lin1_0", "w": 52, "kind": "pad"},
        {"file": "dl_lin1_1", "w": 324, "kind": "btn", "bx": 0, "br": 6, "label": f"X20CTL-{ver_label}-linux-x86_64.AppImage", "aria": f"Download X20CTL-{ver_label}-linux-x86_64.AppImage"},
        {"file": "dl_lin1_2", "w": 304, "kind": "btn", "bx": 2, "br": 6, "label": f"X20CTL-{ver_label}-linux-x86_64.flatpak", "aria": f"Download X20CTL-{ver_label}-linux-x86_64.flatpak"},
        {"file": "dl_lin1_3", "w": 268, "kind": "btn", "bx": 2, "br": 0, "label": f"X20CTL-{ver_label}-linux-check.zip", "aria": f"Download X20CTL-{ver_label}-linux-check.zip"},
        {"file": "dl_lin1_f", "w": 52, "kind": "pad"},
    ])
    _btn_row("dl_lin2", 52, [
        {"file": "dl_lin2_0", "w": 52, "kind": "pad"},
        {"file": "dl_lin2_1", "w": 310, "kind": "btn", "bx": 0, "br": 6, "label": f"X20CTL-{ver_label}-linux-x64.tar.gz", "aria": f"Download X20CTL-{ver_label}-linux-x64.tar.gz"},
        {"file": "dl_lin2_2", "w": 344, "kind": "btn", "bx": 2, "br": 6, "label": f"X20CTL-{ver_label}-linux-musl-x64.tar.gz", "aria": f"Download X20CTL-{ver_label}-linux-musl-x64.tar.gz"},
        {"file": "dl_lin2_3", "w": 242, "kind": "btn", "bx": 2, "br": 0, "label": "SHA256SUMS.txt", "aria": "Download SHA256SUMS.txt"},
        {"file": "dl_lin2_f", "w": 52, "kind": "pad"},
    ])

    dl_foot_body = f"""  <text x="52" y="28" class="s" font-size="13.5" fill="{INK_DARK}"><tspan font-weight="700">Code signing:</tspan> Built on GitHub Actions; applied for free open-source signing via SignPath Foundation. Until active,</text>
  <text x="52" y="49" class="s" font-size="13.5" fill="{INK_DARK}">executables are unsigned so SmartScreen may warn — verify hashes and never turn off antivirus protection.</text>
  <text x="52" y="74" class="s" font-size="13.5" fill="{INK_MUTED}"><tspan font-weight="700" fill="{INK_DARK}">Linux note:</tspan> Avalonia Studio bundles build in CI and pass headless checks, awaiting real Linux hardware tests. CLI: <tspan class="m">uv run x20 scan</tspan>.</text>"""
    (BG_DIR / "download_foot.svg").write_text(
        _wrap_svg("dlf", 1000, 94, "Code signing and Linux runtime status", dl_foot_body),
        encoding="utf-8",
    )
    _btn_row("dl_meta", 52, [
        {"file": "dl_meta_0", "w": 52, "kind": "pad"},
        {"file": "dl_meta_1", "w": 224, "kind": "btn", "bx": 0, "br": 8, "label": "SignPath Foundation", "aria": "SignPath Foundation"},
        {"file": "dl_meta_2", "w": 224, "kind": "btn", "bx": 0, "br": 8, "label": "Linux user guide", "aria": "docs/linux-user-guide.md"},
        {"file": "dl_meta_3", "w": 224, "kind": "btn", "bx": 0, "br": 8, "label": "Version numbers", "aria": "docs/versions.md"},
        {"file": "dl_meta_4", "w": 224, "kind": "btn", "bx": 0, "br": 0, "label": "All releases", "aria": "All GitHub releases"},
        {"file": "dl_meta_f", "w": 52, "kind": "pad"},
    ])

    # 10. Local first + privacy/troubleshooting row
    local_body = f"""{_heading(52, 28, "06", "LOCAL FIRST", "NO ACCOUNT · ZERO TELEMETRY")}
  <text x="52" y="84" class="s" font-size="13.2" fill="{INK_DARK}">■ <tspan font-weight="700">100% offline assistant &amp; storage:</tspan> Agent XCTL, setups, drafts and PC Mode profiles stay in your local user folder.</text>
  <text x="52" y="109" class="s" font-size="13.2" fill="{INK_DARK}">■ <tspan font-weight="700">Update check &amp; drivers:</tspan> Checks GitHub stable releases only (toggle off in Tools); asks before installing ViGEmBus / HidHide.</text>
  <text x="52" y="134" class="s" font-size="13.2" fill="{INK_DARK}">■ <tspan font-weight="700">Local game library reads:</tspan> Reads local Steam, Epic, GOG, Riot, Heroic and Lutris files only for automatic per-game profiles.</text>
  <text x="52" y="159" class="s" font-size="13.2" fill="{INK_DARK}">■ <tspan font-weight="700">Opt-in uploads only:</tspan> Nothing is sent unless you confirm Scan and send or Feedback; X20CTL never flashes firmware.</text>"""
    (BG_DIR / "local_first.svg").write_text(
        _wrap_svg("loc", 1000, 178, "Local-first privacy guarantees", local_body),
        encoding="utf-8",
    )
    _btn_row("local_links", 52, [
        {"file": "local_links_0", "w": 52, "kind": "pad"},
        {"file": "local_links_1", "w": 280, "kind": "btn", "bx": 0, "br": 8, "label": "Privacy policy", "aria": "PRIVACY.md"},
        {"file": "local_links_2", "w": 320, "kind": "btn", "bx": 0, "br": 8, "label": "Controller troubleshooting", "aria": "IF-YOUR-CONTROLLER-ISNT-WORKING.md"},
        {"file": "local_links_f", "w": 348, "kind": "pad"},
    ])

    # 11. Credits & License
    credits_body = f"""{_heading(52, 28, "07", "CREDITS & LICENSE", "GPL-3.0-OR-LATER")}
  <text x="52" y="84" class="s" font-size="13.2" fill="{INK_DARK}">■ <tspan font-weight="700">Kiin &amp; Team EasySMX:</tspan> Special thanks for supplying a free X20 Pro to turn protocol research into verified hardware support.</text>
  <text x="52" y="109" class="s" font-size="13.2" fill="{INK_DARK}">■ <tspan font-weight="700">Edgard (edgardmessias):</tspan> Thanks for decoding the X10 Bluetooth protocol in EasyGamepad; X10 read-back builds on his work.</text>
  <text x="52" y="134" class="s" font-size="13.2" fill="{INK_DARK}">■ <tspan font-weight="700">Monratis:</tspan> Thanks for resolving simultaneous mouse/gamepad focus and testing X20 Pro support on real hardware.</text>
  <text x="52" y="159" class="s" font-size="13.2" fill="{INK_DARK}">■ <tspan font-weight="700">Design &amp; input inspiration:</tspan> Console layout inspired by ApexSenseBridge (Kayn arts); mouse defaults inspired by Gopher360.</text>"""
    (BG_DIR / "credits.svg").write_text(
        _wrap_svg("cred", 1000, 178, "Credits and open-source contributors", credits_body),
        encoding="utf-8",
    )
    _btn_row("cred_links", 52, [
        {"file": "cred_links_0", "w": 52, "kind": "pad"},
        {"file": "cred_links_1", "w": 210, "kind": "btn", "bx": 0, "br": 8, "label": "EasyGamepad", "aria": "EasyGamepad"},
        {"file": "cred_links_2", "w": 195, "kind": "btn", "bx": 0, "br": 8, "label": "Monratis", "aria": "Monratis"},
        {"file": "cred_links_3", "w": 245, "kind": "btn", "bx": 0, "br": 8, "label": "ApexSenseBridge", "aria": "ApexSenseBridge"},
        {"file": "cred_links_4", "w": 246, "kind": "btn", "bx": 0, "br": 0, "label": "Support on Ko-fi", "aria": "Support X20CTL on Ko-fi"},
        {"file": "cred_links_f", "w": 52, "kind": "pad"},
    ])

    lic_body = f"""  <text x="52" y="32" class="s" font-size="13.5" fill="{INK_DARK}">From V0.012 (4.1.0 Preview 3) onward, X20CTL is free software under the <tspan font-weight="700">GNU General Public License v3.0 or later</tspan>.</text>
  <text x="52" y="53" class="s" font-size="13.5" fill="{INK_MUTED}">Earlier MIT-licensed code (up to V0.011) keeps its notice in licenses/ and NOTICE.</text>"""
    (BG_DIR / "license_note.svg").write_text(
        _wrap_svg("lic", 1000, 70, "GPL-3.0-or-later license note", lic_body),
        encoding="utf-8",
    )
    _btn_row("lic_links", 52, [
        {"file": "lic_links_0", "w": 52, "kind": "pad"},
        {"file": "lic_links_1", "w": 240, "kind": "btn", "bx": 0, "br": 8, "label": "GPL-3.0-or-later", "aria": "LICENSE"},
        {"file": "lic_links_2", "w": 180, "kind": "btn", "bx": 0, "br": 8, "label": "NOTICE", "aria": "NOTICE"},
        {"file": "lic_links_3", "w": 210, "kind": "btn", "bx": 0, "br": 8, "label": "Third-party notices", "aria": "THIRD_PARTY.md"},
        {"file": "lic_links_f", "w": 318, "kind": "pad"},
    ])

    end_body = f"""  {_filigree(16)}
  <text x="52" y="42" class="m" font-size="11" fill="{INK_MUTED}" letter-spacing="1.8">X20CTL // {ver_label} // GPL-3.0-OR-LATER</text>
  <text x="948" y="42" text-anchor="end" class="m" font-size="11" fill="{INK_MUTED}" letter-spacing="1.4">FOR THE PLAYERS · BUILT FOR REAL HARDWARE</text>"""
    (BG_DIR / "end.svg").write_text(
        _wrap_svg("end", 1000, 62, "X20CTL, GPL-3.0-or-later", end_body),
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Build NieR:Automata beige SVG slices for README.md.")
    parser.add_argument("--tag", default="v0.025", help="Release tag (e.g. v0.025)")
    parser.add_argument("--shots", type=Path, default=None, help="Optional raw captures dir to re-encode docs/screenshots/*.webp")
    parser.add_argument("--export-webp", action="store_true", help="Re-encode docs/screenshots/*.webp from --shots")
    args = parser.parse_args()

    ver_label = release_name(args.tag)
    if args.export_webp:
        if not args.shots:
            raise SystemExit("--export-webp requires --shots")
        written = export_webp_screenshots(args.shots)
        print(f"Exported {len(written)} WebP screenshots into {DOCS_SHOTS}")

    build_all(ver_label, args.tag)
    count = len(list(BG_DIR.glob("*.svg")))
    print(f"Generated {count} NieR YoRHa SVG slices for {ver_label} in {BG_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

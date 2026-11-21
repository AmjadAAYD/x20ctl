"""Generate the full-page Obsidian Hardware Showroom README slices in assets/readme/bg/
plus guide header banners in assets/readme/.

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
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from version_name import name as release_name

ROOT = Path(__file__).resolve().parents[1]
BG_DIR = ROOT / "assets" / "readme" / "bg"
README_ASSETS = ROOT / "assets" / "readme"
SRC_DIR = README_ASSETS / "src"
CTRL_DIR = ROOT / "src" / "assets" / "controllers"
SHOTS_DIR = ROOT / "docs" / "screenshots"
SOCIAL_PREVIEW_PATH = ROOT / "assets" / "social-preview.png"

W = 1000
MARGIN = 48
CONTENT_W = W - 2 * MARGIN  # 904

# Obsidian Hardware Showroom Palette
GITHUB_DARK = "#0D1117"
OBS_EDGE = "#06080E"
OBS_MID = "#0B101C"
OBS_CORE = "#0E1526"
CARD_FILL = "#0D1322"
CARD_ALT = "#10182B"
PILL_FILL = "#121A2D"
PILL_PRI_TOP = "#27334D"
PILL_PRI_MID = "#192236"
PILL_PRI_BOT = "#1D2840"
BORDER_SILVER = "#DCE8FF"
TEXT_PRIMARY = "#F4F7FC"
TEXT_SECONDARY = "#BAC5D8"
TEXT_MUTED = "#7D8B9F"
HEART_RED = "#F43F75"

FONT_SANS = '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif'
FONT_MONO = 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace'

CONTROLLERS_LINEUP = [
    ("X20", CTRL_DIR / "x20/native/front-full.png", "Verified", "#22C55E", True),
    ("X20 Pro", CTRL_DIR / "x20_pro/native/front-full.png", "Full + RGB", "#22C55E", True),
    ("X15", CTRL_DIR / "x15/native/front-full.png", "PC Mode", "#60A5FA", False),
    ("X10", CTRL_DIR / "x10/native/front-full.png", "BLE Read", "#F59E0B", False),
    ("X05 Pro", CTRL_DIR / "x05_pro/native/front-full.png", "PC Mode", "#60A5FA", False),
    ("D10", CTRL_DIR / "d10/native/front-full.png", "PC Mode", "#60A5E5", False),
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


def _load_cropped_webp_uri(
    path: Path, target_width: int, crop_alpha: bool = True, quality: int = 90
) -> tuple[str, int, int]:
    """Load an image, crop strictly to non-transparent alpha bounds if requested, resize with Lanczos, encode WebP URI."""
    with Image.open(path) as img:
        rgba = img.convert("RGBA") if crop_alpha else img.convert("RGB")
        if crop_alpha:
            bbox = rgba.getbbox()
            if bbox:
                rgba = rgba.crop(bbox)
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


# Global studio softboxes & diagonal architectural satin planes (in 0..1000 x 0..TOTAL_H coordinates)
@dataclass(frozen=True)
class _Softbox:
    cx: float
    cy: float
    rx: float
    ry: float
    color: str
    opacity: float


def _build_studio_planes(y_offsets: dict[str, float], total_h: int) -> tuple[list[_Softbox], list[str]]:
    hero_y = y_offsets.get("hero", 0.0)
    thx_y = y_offsets.get("thanks", 446.0)
    demo_y = y_offsets.get("demo", 734.0)
    s1_y = y_offsets.get("spot_1", 1556.0)
    s2_y = y_offsets.get("spot_2", 2196.0)
    s3_y = y_offsets.get("spot_3", 2836.0)
    s4_y = y_offsets.get("spot_4", 3476.0)
    th_y = y_offsets.get("themes", 4116.0)
    mod_y = y_offsets.get("models", 4812.0)
    dl_y = y_offsets.get("download_head", 5406.0)
    re_y = y_offsets.get("reverse_eng", 5844.0)
    end_y = y_offsets.get("end", total_h - 72.0)

    softboxes = [
        # Bright studio rim behind Hero X20 Pro & soft key light top-left
        _Softbox(755, hero_y + 188, 360, 220, "#B8CEF0", 0.16),
        _Softbox(220, hero_y + 70, 380, 220, "#D4E3FA", 0.06),
        # Rich colorful wash spilling softly around the Thanks showcase card
        _Softbox(190, thx_y + 144, 340, 175, "#22D3EE", 0.10),
        _Softbox(500, thx_y + 130, 420, 180, "#7C5CFF", 0.11),
        _Softbox(820, thx_y + 148, 340, 175, "#E879F9", 0.09),
        # Soft studio keylights behind animated demo + 4 full-width screenshots
        _Softbox(500, demo_y + 300, 480, 280, "#3B6FD8", 0.08),
        _Softbox(780, s1_y + 280, 460, 300, "#22A6E0", 0.07),
        _Softbox(240, s2_y + 300, 480, 300, "#6366F1", 0.075),
        _Softbox(740, s3_y + 300, 470, 300, "#3B82F6", 0.07),
        _Softbox(280, s4_y + 300, 480, 300, "#10B981", 0.055),
        _Softbox(500, th_y + 250, 500, 280, "#7AA2F7", 0.065),
        _Softbox(500, mod_y + 210, 490, 250, "#9BB8ED", 0.085),
        _Softbox(280, dl_y + 150, 440, 220, "#3B82F6", 0.07),
        _Softbox(720, re_y + 160, 440, 210, "#60A5FA", 0.065),
        _Softbox(500, end_y + 10, 420, 120, "#9DBBF0", 0.06),
    ]

    # Seamless multi-section diagonal architectural satin light planes spanning global canvas height
    beams = []
    step = 960
    for by in range(-200, total_h + 200, step):
        beams.append(
            f'<polygon points="380,{by} 740,{by} 340,{by + 860} -20,{by + 860}" '
            f'fill="#B9D0F8" fill-opacity="0.022"/>'
        )
        beams.append(
            f'<polygon points="790,{by + 280} 1020,{by + 280} 620,{by + 1120} 390,{by + 1120}" '
            f'fill="#4F7BE8" fill-opacity="0.026"/>'
        )
    return softboxes, beams


def _obsidian_bg(
    x0: float,
    y0: float,
    sw: float,
    sh: float,
    total_h: int,
    softboxes: list[_Softbox],
    beams: list[str],
    clip_id: str,
    round_top: bool = False,
    round_bot: bool = False,
) -> tuple[str, str]:
    """Return (<defs> content, <g> background layers) mapped seamlessly into slice (x0, y0, sw, sh)."""
    # Determine unique radial colors used in visible softboxes
    vis_boxes = [sb for sb in softboxes if sb.cy + sb.ry >= y0 - 40 and sb.cy - sb.ry <= y0 + sh + 40]
    used_cols = sorted({(sb.color.lstrip("#").lower(), round(sb.opacity, 3)) for sb in vis_boxes})

    rad_defs = []
    for col_hex, op in used_cols:
        gid = f"sb_{col_hex}_{int(round(op * 1000))}"
        rad_defs.append(
            f'<radialGradient id="{gid}" cx="50%" cy="50%" r="50%">'
            f'<stop offset="0%" stop-color="#{col_hex}" stop-opacity="{op}"/>'
            f'<stop offset="48%" stop-color="#{col_hex}" stop-opacity="{round(op * 0.36, 4)}"/>'
            f'<stop offset="100%" stop-color="#{col_hex}" stop-opacity="0"/>'
            f"</radialGradient>"
        )

    outer_clip_shape: str
    inner_border_markup: str
    card_l, card_r = 18, W - 18
    if round_top:
        r = 18
        outer_clip_shape = (
            f'<path d="M {card_l},{r} Q {card_l},0 {card_l + r},0 H {card_r - r} Q {card_r},0 {card_r},{r} '
            f'V {sh} H {card_l} Z"/>'
        )
        inner_border_markup = (
            f'<path d="M {card_l + 0.5},{sh} V {r} Q {card_l + 0.5},1 {card_l + 0.5 + r},1 '
            f'H {card_r - 0.5 - r} Q {card_r - 0.5},1 {card_r - 0.5},{r} V {sh}" '
            f'fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.16" stroke-width="1"/>'
            f'<line x1="{card_l + 20}" y1="2" x2="{card_r - 20}" y2="2" stroke="#FFFFFF" stroke-opacity="0.11" stroke-width="1"/>'
        )
    elif round_bot:
        r = 18
        outer_clip_shape = (
            f'<path d="M {card_l},0 H {card_r} V {sh - r} Q {card_r},{sh} {card_r - r},{sh} '
            f'H {card_l + r} Q {card_l},{sh} {card_l},{sh - r} Z"/>'
        )
        inner_border_markup = (
            f'<path d="M {card_l + 0.5},0 V {sh - 1 - r} Q {card_l + 0.5},{sh - 1} {card_l + 0.5 + r},{sh - 1} '
            f'H {card_r - 0.5 - r} Q {card_r - 0.5},{sh - 1} {card_r - 0.5},{sh - 1 - r} V 0" '
            f'fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.16" stroke-width="1"/>'
        )
    else:
        outer_clip_shape = (
            f'<rect x="{max(0.0, card_l - x0)}" y="0" '
            f'width="{max(0.0, min(x0 + sw, float(card_r)) - max(x0, float(card_l)))}" height="{sh}"/>'
        )
        lines = []
        if x0 <= card_l < x0 + sw:
            bx = round(card_l + 0.5 - x0, 2)
            lines.append(
                f'<line x1="{bx}" y1="0" x2="{bx}" y2="{sh}" stroke="{BORDER_SILVER}" stroke-opacity="0.15" stroke-width="1"/>'
            )
        if x0 < card_r <= x0 + sw:
            bx = round(card_r - 0.5 - x0, 2)
            lines.append(
                f'<line x1="{bx}" y1="0" x2="{bx}" y2="{sh}" stroke="{BORDER_SILVER}" stroke-opacity="0.15" stroke-width="1"/>'
            )
        inner_border_markup = "".join(lines)

    defs_block = (
        f'<linearGradient id="obs_h" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="1000" y2="0">'
        f'<stop offset="0%" stop-color="{OBS_EDGE}"/>'
        f'<stop offset="16%" stop-color="{OBS_MID}"/>'
        f'<stop offset="50%" stop-color="{OBS_CORE}"/>'
        f'<stop offset="84%" stop-color="{OBS_MID}"/>'
        f'<stop offset="100%" stop-color="{OBS_EDGE}"/>'
        f"</linearGradient>"
        f'<linearGradient id="obs_vl" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="{card_l}" y2="0">'
        f'<stop offset="0%" stop-color="#020306" stop-opacity="0"/>'
        f'<stop offset="100%" stop-color="#020306" stop-opacity="0.48"/>'
        f"</linearGradient>"
        f'<linearGradient id="obs_vr" gradientUnits="userSpaceOnUse" x1="1000" y1="0" x2="{card_r}" y2="0">'
        f'<stop offset="0%" stop-color="#020306" stop-opacity="0"/>'
        f'<stop offset="100%" stop-color="#020306" stop-opacity="0.48"/>'
        f"</linearGradient>"
        + "".join(rad_defs)
        + f'<clipPath id="{clip_id}">{outer_clip_shape}</clipPath>'
    )

    ellipses = [
        f'<ellipse cx="{sb.cx}" cy="{sb.cy}" rx="{sb.rx}" ry="{sb.ry}" '
        f'fill="url(#sb_{sb.color.lstrip("#").lower()}_{int(round(sb.opacity * 1000))})"/>'
        for sb in vis_boxes
    ]
    y_sh_top = 18 if round_top else 0
    y_sh_h = max(0, sh - (18 if round_top or round_bot else 0))
    shadow_margin_l = (
        f'<g transform="translate({round(-x0, 2)}, 0)">'
        f'<rect x="0" y="{y_sh_top}" width="{card_l}" height="{y_sh_h}" fill="url(#obs_vl)"/>'
        f"</g>"
        if x0 < card_l
        else ""
    )
    shadow_margin_r = (
        f'<g transform="translate({round(-x0, 2)}, 0)">'
        f'<rect x="{card_r}" y="{y_sh_top}" width="{W - card_r}" height="{y_sh_h}" fill="url(#obs_vr)"/>'
        f"</g>"
        if x0 + sw > card_r
        else ""
    )

    bg_group = (
        f'{shadow_margin_l}{shadow_margin_r}'
        f'<g clip-path="url(#{clip_id})">'
        f'<g transform="translate({round(-x0, 2)}, {round(-y0, 2)})">'
        f'<rect x="0" y="0" width="1000" height="{total_h}" fill="url(#obs_h)"/>'
        f'{"".join(beams)}{"".join(ellipses)}'
        f"</g></g>{inner_border_markup}"
    )
    return defs_block, bg_group


def _wrap_svg(
    w: float,
    h: float,
    aria: str,
    defs_extra: str,
    body: str,
    style: str = "",
) -> str:
    w_attr = int(w) if float(w).is_integer() else w
    h_attr = int(h) if float(h).is_integer() else h
    style_tag = f"<style>{style}</style>" if style else ""
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" '
        f'viewBox="0 0 {w_attr} {h_attr}" width="{w_attr}" height="{h_attr}" '
        f'role="img" aria-label="{_esc(aria)}">'
        f"{style_tag}<defs>{defs_extra}</defs>{body}</svg>\n"
    )


def _section_heading(eyebrow: str, title: str, right_meta: str, y: float = 34.0) -> str:
    return (
        f'<text x="{MARGIN}" y="{y}" class="m" font-size="11" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="2">{_esc(eyebrow)}</text>'
        f'<text x="{W - MARGIN}" y="{y}" text-anchor="end" class="m" font-size="11" fill="{TEXT_MUTED}" letter-spacing="1.6">{_esc(right_meta)}</text>'
        f'<text x="{MARGIN}" y="{y + 29}" class="s" font-size="22.5" font-weight="700" fill="{TEXT_PRIMARY}" letter-spacing="-0.3">{_esc(title)}</text>'
        f'<line x1="{MARGIN}" y1="{y + 42}" x2="{W - MARGIN}" y2="{y + 42}" stroke="{BORDER_SILVER}" stroke-opacity="0.12" stroke-width="1"/>'
    )


def _render_hero(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str], tag: str) -> tuple[str, int]:
    h = 384
    ver = release_name(tag)
    ctrl_raw = _load_cropped_webp_uri(CTRL_DIR / "x20_pro/native/front-full.png", 560)
    ctrl_uri, cw, ch = _fit_box(ctrl_raw, 434, 294)
    cx, cy = 746.0, 194.0
    ix, iy = round(cx - cw / 2, 1), round(cy - ch / 2, 1)

    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_hero", round_top=True)
    defs_extra = (
        defs_bg
        + '<linearGradient id="h_bar" x1="0" y1="0" x2="1" y2="0">'
        + '<stop offset="0%" stop-color="#DCE8FF" stop-opacity="0.85"/>'
        + '<stop offset="65%" stop-color="#60A5FA" stop-opacity="0.65"/>'
        + '<stop offset="100%" stop-color="#60A5FA" stop-opacity="0"/>'
        + "</linearGradient>"
        + '<radialGradient id="h_floor" cx="50%" cy="50%" r="50%">'
        + '<stop offset="0%" stop-color="#010205" stop-opacity="0.88"/>'
        + '<stop offset="70%" stop-color="#020308" stop-opacity="0.45"/>'
        + '<stop offset="100%" stop-color="#020308" stop-opacity="0"/>'
        + "</radialGradient>"
    )
    style = (
        f".s{{font-family:{FONT_SANS}}}"
        f".m{{font-family:{FONT_MONO}}}"
        "@keyframes fl{to{transform:translateY(-6px)}}"
        ".fctrl{animation:fl 5.5s ease-in-out infinite alternate}"
        "@keyframes pr{0%,21%{opacity:1}25%,96%{opacity:0}100%{opacity:1}}"
        ".p0{animation:pr 12s ease-in-out -0.4s infinite}"
        ".p1{animation:pr 12s ease-in-out 2.6s infinite;opacity:0}"
        ".p2{animation:pr 12s ease-in-out 5.6s infinite;opacity:0}"
        ".p3{animation:pr 12s ease-in-out 8.6s infinite;opacity:0}"
        "@keyframes cur{0%,49%{opacity:1}50%,100%{opacity:0}}"
        ".cur{animation:cur 1.05s steps(1) infinite}"
        "@media (prefers-reduced-motion:reduce){*{animation:none!important}}"
    )
    body = (
        f"{bg_g}"
        f'<text x="{MARGIN}" y="58" class="m" font-size="11.5" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="2.2">OPEN-SOURCE CONTROLLER STUDIO · <tspan id="hero-version">{_esc(ver)}</tspan></text>'
        f'<text x="{MARGIN}" y="128" class="s" font-size="68" font-weight="800" fill="{TEXT_PRIMARY}" letter-spacing="-1.6">X20CTL</text>'
        f'<rect x="{MARGIN}" y="146" width="184" height="3" rx="1.5" fill="url(#h_bar)"/>'
        f'<text x="{MARGIN}" y="182" class="s" font-size="20" font-weight="500" fill="#DCE5F5">A controller studio for EasySMX pads.</text>'
        f'<rect x="{MARGIN}" y="206" width="430" height="38" rx="8" fill="{CARD_FILL}" fill-opacity="0.88" stroke="{BORDER_SILVER}" stroke-opacity="0.16" stroke-width="1"/>'
        f'<text x="{MARGIN + 14}" y="229.5" class="m" font-size="12.5" font-weight="600" fill="#7DD3FC">&gt;</text>'
        f'<text x="{MARGIN + 30}" y="229.5" class="m" font-size="12.5" fill="{TEXT_PRIMARY}"><tspan class="p0">live 4-zone X20 Pro RGB &amp; 160x80 screen</tspan></text>'
        f'<text x="{MARGIN + 30}" y="229.5" class="m" font-size="12.5" fill="{TEXT_PRIMARY}"><tspan class="p1">100% offline handheld Agent XCTL (3,048)</tspan></text>'
        f'<text x="{MARGIN + 30}" y="229.5" class="m" font-size="12.5" fill="{TEXT_PRIMARY}"><tspan class="p2">sculpt stick curves &amp; 64-key macro chains</tspan></text>'
        f'<text x="{MARGIN + 30}" y="229.5" class="m" font-size="12.5" fill="{TEXT_PRIMARY}"><tspan class="p3">Xbox 360 / PS4 PC mode across 4 players</tspan></text>'
        f'<rect x="{MARGIN + 408}" y="217" width="8" height="16" rx="1" fill="#7DD3FC" class="cur"/>'
        f'<text x="{MARGIN}" y="286" class="m" font-size="12" font-weight="600" fill="#E2ECFF" letter-spacing="0.8">{_esc(ver)} — FINAL RELEASE FOR NOW</text>'
        f'<text x="{MARGIN}" y="310" class="s" font-size="13" fill="{TEXT_MUTED}">Windows 10/11 (WPF + C++) · Linux x64 (Avalonia) · GPL-3.0-or-later</text>'
        f'<text x="{MARGIN}" y="346" class="m" font-size="11" fill="#6F7D93">EasySMX X20 Pro · X20 · X15 · X10 · X05 Pro · D10 · Dune 8K</text>'
        f'<ellipse cx="{cx}" cy="346" rx="186" ry="18" fill="url(#h_floor)"/>'
        f'<g class="fctrl"><g transform="rotate(-3.5 {cx} {cy})">'
        f'<image x="{ix}" y="{iy}" width="{cw}" height="{ch}" preserveAspectRatio="xMidYMid meet" href="{ctrl_uri}" xlink:href="{ctrl_uri}"/>'
        f"</g></g>"
    )
    return _wrap_svg(W, h, f"X20CTL {ver}: a controller studio for EasySMX pads", defs_extra, body, style), h


def _emit_button_row(
    prefix: str,
    y0: float,
    sh: int,
    total_h: int,
    softboxes: list[_Softbox],
    beams: list[str],
    buttons: list[tuple[str, str, bool]],  # (label, aria, primary)
    pad_l: int = MARGIN,
    gap: int = 10,
    btn_widths: list[int] | None = None,
) -> tuple[dict[str, str], list[tuple[str, float]], int]:
    """Build compact button slices (each carrying half-gap side gutters, no tiny gap imgs) summing to 99.97%."""
    out: dict[str, str] = {}
    row_meta: list[tuple[str, float]] = []
    half_gap = gap // 2

    if btn_widths is None:
        inner_w = W - pad_l - pad_l - gap * (len(buttons) - 1)
        w_each = inner_w // len(buttons)
        pill_widths = [w_each] * (len(buttons) - 1) + [inner_w - w_each * (len(buttons) - 1)]
    else:
        pill_widths = list(btn_widths)

    def make_slice(fname: str, x0: float, sw: int, pad_x: int = 0, label: str | None = None, aria: str = "", primary: bool = False, pct_override: float | None = None) -> None:
        cid = f"c_{fname[:-4]}"
        defs_bg, bg_g = _obsidian_bg(x0, y0, sw, sh, total_h, softboxes, beams, cid)
        defs_extra = defs_bg
        style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
        inner = bg_g
        if label is not None:
            pw = sw - 2 * pad_x
            bh = sh - 10
            by = 5
            if primary:
                defs_extra += (
                    f'<linearGradient id="p_{cid}" x1="0" y1="0" x2="0" y2="1">'
                    f'<stop offset="0%" stop-color="{PILL_PRI_TOP}"/>'
                    f'<stop offset="50%" stop-color="{PILL_PRI_MID}"/>'
                    f'<stop offset="100%" stop-color="{PILL_PRI_BOT}"/>'
                    f"</linearGradient>"
                )
                fill = f"url(#p_{cid})"
                stroke_op = "0.38"
                text_col = TEXT_PRIMARY
                weight = "700"
                dot_col = "#60A5FA"
            else:
                fill = PILL_FILL
                stroke_op = "0.18"
                text_col = "#DCE5F4"
                weight = "600"
                dot_col = "#7D8B9F"

            fs = "12.5" if len(label) < 26 else "11.5"
            inner += (
                f'<rect x="{pad_x}" y="{by}" width="{pw}" height="{bh}" rx="10" fill="{fill}" fill-opacity="0.92" '
                f'stroke="{BORDER_SILVER}" stroke-opacity="{stroke_op}" stroke-width="1"/>'
                f'<line x1="{pad_x + 12}" y1="{by + 1}" x2="{pad_x + pw - 12}" y2="{by + 1}" stroke="#FFFFFF" stroke-opacity="{"0.18" if primary else "0.07"}" stroke-width="1"/>'
                f'<circle cx="{pad_x + 15}" cy="{sh / 2}" r="2.8" fill="{dot_col}"/>'
                f'<text x="{pad_x + 26}" y="{sh / 2 + 4.3}" class="s" font-size="{fs}" font-weight="{weight}" fill="{text_col}">{_esc(label)}</text>'
                f'<text x="{pad_x + pw - 13}" y="{sh / 2 + 4}" text-anchor="end" class="s" font-size="12" fill="{TEXT_MUTED}">→</text>'
            )
        out[fname] = _wrap_svg(sw, sh, aria, defs_extra, inner, style)
        pct = pct_override if pct_override is not None else round(sw / 10.0, 4)
        row_meta.append((fname, pct))

    cx = 0
    lead = max(0, pad_l - half_gap)
    if lead > 0:
        make_slice(f"{prefix}_0.svg", 0, lead, aria="")
        cx = lead

    for idx, (label, aria, primary) in enumerate(buttons, start=1):
        sw = pill_widths[idx - 1] + 2 * half_gap
        make_slice(f"{prefix}_{idx}.svg", cx, sw, pad_x=half_gap, label=label, aria=aria, primary=primary)
        cx += sw

    rem = W - cx
    if rem > 0:
        make_slice(f"{prefix}_f.svg", cx, rem, aria="", pct_override=round(rem / 10.0 - 0.03, 4))
    else:
        last_name, last_pct = row_meta[-1]
        row_meta[-1] = (last_name, round(last_pct - 0.03, 4))

    return out, row_meta, sh


def _render_thanks(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str]) -> tuple[str, int]:
    h = 288
    wht_raw = _load_cropped_webp_uri(CTRL_DIR / "x20_pro/finishes/white/front-full.png", 250)
    blk_raw = _load_cropped_webp_uri(CTRL_DIR / "x20_pro/native/front-full.png", 250)
    w_uri, ww, wh = _fit_box(wht_raw, 178, 114)
    b_uri, bw, bh = _fit_box(blk_raw, 178, 118)

    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_thanks")
    defs_extra = (
        defs_bg
        + '<clipPath id="tk_card"><rect x="44" y="10" width="912" height="268" rx="16"/></clipPath>'
        + '<radialGradient id="tk_g1"><stop offset="0" stop-color="#22D3EE" stop-opacity="0.34"/><stop offset="1" stop-color="#22D3EE" stop-opacity="0"/></radialGradient>'
        + '<radialGradient id="tk_g2"><stop offset="0" stop-color="#7C5CFF" stop-opacity="0.34"/><stop offset="1" stop-color="#7C5CFF" stop-opacity="0"/></radialGradient>'
        + '<radialGradient id="tk_g3"><stop offset="0" stop-color="#E879F9" stop-opacity="0.28"/><stop offset="1" stop-color="#E879F9" stop-opacity="0"/></radialGradient>'
        + '<linearGradient id="tk_led" x1="0" y1="0" x2="1" y2="0">'
        + '<stop offset="0%" stop-color="#22D3EE"/>'
        + '<stop offset="33%" stop-color="#7C5CFF"/>'
        + '<stop offset="66%" stop-color="#E879F9"/>'
        + '<stop offset="100%" stop-color="#22D3EE"/>'
        + "</linearGradient>"
        + '<clipPath id="tk_lct"><rect x="396" y="190" width="208" height="4.5" rx="2.25"/></clipPath>'
    )
    style = (
        f".s{{font-family:{FONT_SANS}}}"
        f".m{{font-family:{FONT_MONO}}}"
        "@keyframes tk_led{to{transform:translateX(-208px)}}"
        ".tkled{animation:tk_led 4.8s linear infinite}"
        "@keyframes tk_d1{to{transform:translate(36px,14px)}}"
        "@keyframes tk_d2{to{transform:translate(-40px,-12px)}}"
        "@keyframes tk_d3{to{transform:translate(20px,-22px)}}"
        ".d1{animation:tk_d1 15s ease-in-out infinite alternate}"
        ".d2{animation:tk_d2 19s ease-in-out infinite alternate}"
        ".d3{animation:tk_d3 23s ease-in-out infinite alternate}"
        "@keyframes tk_f{to{transform:translateY(-6px)}}"
        ".f1{animation:tk_f 5s ease-in-out infinite alternate}"
        ".f2{animation:tk_f 5s ease-in-out -2.5s infinite alternate}"
        "@keyframes tk_hb{0%,100%{transform:scale(1)}18%{transform:scale(1.18)}36%{transform:scale(1)}54%{transform:scale(1.1)}}"
        ".hb{transform-box:fill-box;transform-origin:center;animation:tk_hb 1.8s ease-in-out infinite}"
        "@media (prefers-reduced-motion:reduce){*{animation:none!important}}"
    )
    body = (
        f"{bg_g}"
        f'<rect x="44" y="10" width="912" height="268" rx="16" fill="#090D18" fill-opacity="0.62"/>'
        f'<g clip-path="url(#tk_card)">'
        f'  <g class="d1"><ellipse cx="145" cy="145" rx="230" ry="145" fill="url(#tk_g1)"/></g>'
        f'  <g class="d3"><ellipse cx="500" cy="108" rx="300" ry="135" fill="url(#tk_g2)"/></g>'
        f'  <g class="d2"><ellipse cx="855" cy="150" rx="230" ry="145" fill="url(#tk_g3)"/></g>'
        f"</g>"
        f'<ellipse cx="146" cy="212" rx="68" ry="8" fill="#020307" fill-opacity="0.72"/>'
        f'<g class="f1"><g transform="rotate(-4 146 146)">'
        f'<image x="{round(146 - ww / 2, 1)}" y="{round(146 - wh / 2, 1)}" width="{ww}" height="{wh}" preserveAspectRatio="xMidYMid meet" href="{w_uri}" xlink:href="{w_uri}"/>'
        f"</g></g>"
        f'<ellipse cx="854" cy="212" rx="68" ry="8" fill="#020307" fill-opacity="0.72"/>'
        f'<g class="f2"><g transform="rotate(4 854 146)">'
        f'<image x="{round(854 - bw / 2, 1)}" y="{round(146 - bh / 2, 1)}" width="{bw}" height="{bh}" preserveAspectRatio="xMidYMid meet" href="{b_uri}" xlink:href="{b_uri}"/>'
        f"</g></g>"
        f'<g transform="translate(500 44) scale(1.2)"><path class="hb" d="M0 9 C-17 -4 -15 -19 -7 -19 C-3 -19 0 -15 0 -12 C0 -15 3 -19 7 -19 C15 -19 17 -4 0 9Z" fill="{HEART_RED}"/></g>'
        f'<text x="500" y="75" text-anchor="middle" class="m" font-size="11.5" font-weight="600" letter-spacing="3.2" fill="#9FB0CC">WITH THANKS</text>'
        f'<text x="500" y="121" text-anchor="middle" class="s" font-size="40" font-weight="800" letter-spacing="-1.0" fill="{TEXT_PRIMARY}">Thank you, EasySMX Kiin</text>'
        f'<text x="500" y="169" text-anchor="middle" class="s" font-size="40" font-weight="800" letter-spacing="-1.0" fill="{TEXT_PRIMARY}">&amp; Team EasySMX</text>'
        f'<g clip-path="url(#tk_lct)"><rect class="tkled" x="396" y="190" width="416" height="4.5" fill="url(#tk_led)"/></g>'
        f'<text x="500" y="234" text-anchor="middle" class="s" font-size="16.5" font-weight="500" fill="#D4DFEF">for supplying an EasySMX X20 Pro to build and test X20CTL.</text>'
        f'<rect x="44" y="10" width="912" height="268" rx="16" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.22" stroke-width="1"/>'
        f'<line x1="66" y1="11" x2="934" y2="11" stroke="#FFFFFF" stroke-opacity="0.14" stroke-width="1"/>'
    )
    return (
        _wrap_svg(
            W,
            h,
            "Thank you, EasySMX Kiin and Team EasySMX for supplying an EasySMX X20 Pro to build and test X20CTL.",
            defs_extra,
            body,
            style,
        ),
        h,
    )


def _render_demo(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str], tag: str) -> tuple[str, int]:
    h = 606
    ver = release_name(tag)
    slides = [
        ("rgb-studio.webp", "RGB Studio · X20 Pro", "4-zone live LEDs + simultaneous dongle & unpaired BLE sync"),
        ("rgb-effects.webp", "Live Lighting Engine", "Rainbow Wave, Trigger Glow, Stick Heat & Ambient Screen Sync"),
        ("agent-xctl.webp", "100% Offline Agent XCTL", "3,048 phrases inside your controller frame — zero cloud calls"),
        ("curves.webp", "Stick & Trigger Curves", "Response presets, dead zones & keyboard curve testing"),
        ("mouse.webp", "Controller as Mouse", "M1–M7 bindings, 2–30 Hz turbo & View + Y quick toggle"),
        ("vibration.webp", "4-Zone Vibration & 200% Boost", "Grip & trigger sliders plus PC Mode game rumble boost"),
    ]
    n = len(slides)
    step_s = 3.2
    dur = round(n * step_s, 2)
    p_hold = 100.0 / n
    p_fade = 2.4

    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_demo")
    defs_extra = defs_bg + '<clipPath id="dm_clip"><rect x="48" y="42" width="904" height="508" rx="8"/></clipPath>'
    style = (
        f".s{{font-family:{FONT_SANS}}}"
        f".m{{font-family:{FONT_MONO}}}"
        f"@keyframes dm_vis{{"
        f"0%{{opacity:0}}"
        f"{p_fade:.2f}%{{opacity:1}}"
        f"{p_hold:.2f}%{{opacity:1}}"
        f"{p_hold + p_fade:.2f}%{{opacity:0}}"
        f"100%{{opacity:0}}}}"
        "@keyframes dm_bar{from{width:0}to{width:904px}}"
        ".dmbar{animation:dm_bar " + f"{dur}s linear infinite}}"
        "@media (prefers-reduced-motion:reduce){*{animation:none!important}}"
    )
    frame_nodes = []
    cap_nodes = []
    for idx, (fname, title, sub) in enumerate(slides):
        shot_uri, _, _ = _load_cropped_webp_uri(SHOTS_DIR / fname, 860, crop_alpha=False, quality=76)
        delay = round(idx * step_s - 0.7, 2)
        anim = f"animation:dm_vis {dur}s linear {delay}s infinite;"
        init_op = "1" if idx == 0 else "0"
        frame_nodes.append(
            f'<g style="opacity:{init_op};{anim}">'
            f'<image x="48" y="42" width="904" height="508" preserveAspectRatio="xMidYMid slice" href="{shot_uri}" xlink:href="{shot_uri}"/>'
            f"</g>"
        )
        cap_nodes.append(
            f'<g style="opacity:{init_op};{anim}">'
            f'<text x="58" y="576" class="s" font-size="14" font-weight="600" fill="{TEXT_PRIMARY}">{_esc(title)}</text>'
            f'<text x="292" y="576" class="s" font-size="12.5" fill="{TEXT_MUTED}">{_esc(sub)}</text>'
            f'<text x="944" y="576" text-anchor="end" class="m" font-size="11" fill="#7DD3FC">FRAME {idx + 1:02d} / {n:02d}</text>'
            f"</g>"
        )

    body = (
        f"{bg_g}"
        f'<rect x="44" y="10" width="912" height="584" rx="14" fill="#0A0E18" fill-opacity="0.88" stroke="{BORDER_SILVER}" stroke-opacity="0.16" stroke-width="1"/>'
        f'<text x="58" y="31" class="m" font-size="11" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="1.6">SHOWROOM MONITOR // X20CTL {_esc(ver)}</text>'
        f'<text x="944" y="31" text-anchor="end" class="m" font-size="11" fill="#7D8B9F">LIVE STUDIO CAPTURES</text>'
        f'<g clip-path="url(#dm_clip)">{"".join(frame_nodes)}</g>'
        f'<rect x="48" y="42" width="904" height="508" rx="8" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.18" stroke-width="1"/>'
        f'{"".join(cap_nodes)}'
        f'<rect x="48" y="586" width="904" height="2.5" rx="1.25" fill="#1B253A"/>'
        f'<rect x="48" y="586" width="0" height="2.5" rx="1.25" fill="#60A5FA" class="dmbar"/>'
    )
    return _wrap_svg(W, h, f"Animated studio tour of X20CTL {ver}", defs_extra, body, style), h


def _render_intro(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str]) -> tuple[str, int]:
    h = 220
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_intro")
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    body = (
        f"{bg_g}"
        f'<rect x="{MARGIN}" y="12" width="{CONTENT_W}" height="64" rx="12" fill="{CARD_FILL}" fill-opacity="0.90" stroke="{BORDER_SILVER}" stroke-opacity="0.14" stroke-width="1"/>'
        f'<rect x="{MARGIN}" y="22" width="3" height="44" rx="1.5" fill="#60A5FA"/>'
        f'<text x="{MARGIN + 18}" y="38" class="s" font-size="13" fill="{TEXT_SECONDARY}"><tspan font-weight="700" fill="{TEXT_PRIMARY}">Independent project.</tspan> X20CTL is open-source software. EasySMX supplied an X20 Pro to help develop it,</text>'
        f'<text x="{MARGIN + 18}" y="58" class="s" font-size="13" fill="{TEXT_SECONDARY}">but the app is not made, endorsed, sponsored or supported by EasySMX.</text>'
        f"{_section_heading('ARCHIVE // 01', 'WHAT IT IS', 'WINDOWS · LINUX · OFFLINE', y=104)}"
        f'<text x="{MARGIN}" y="168" class="s" font-size="14" fill="{TEXT_SECONDARY}">A desktop studio for EasySMX pads on Windows (.NET 10 WPF + C++) and Linux (Avalonia). Remap front &amp; rear paddles,</text>'
        f'<text x="{MARGIN}" y="190" class="s" font-size="14" fill="{TEXT_SECONDARY}">sculpt stick/trigger curves, tune X20 Pro RGB lighting &amp; its 160x80 screen, or run low-latency PC Mode for up to 4 players.</text>'
    )
    return _wrap_svg(W, h, "Independent project notice and overview of X20CTL", defs_bg, body, style), h


def _render_spotlight(
    y0: float,
    total_h: int,
    softboxes: list[_Softbox],
    beams: list[str],
    clip_id: str,
    eyebrow: str,
    title: str,
    badge: str,
    line1: str,
    line2: str,
    shot_file: str,
    aria: str,
) -> tuple[str, int]:
    h = 640
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, clip_id)
    shot_uri, _, _ = _load_cropped_webp_uri(SHOTS_DIR / shot_file, 904, crop_alpha=False, quality=85)
    sc_id = f"{clip_id}_s"
    defs_extra = defs_bg + f'<clipPath id="{sc_id}"><rect x="{MARGIN}" y="114" width="904" height="508" rx="10"/></clipPath>'
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    body = (
        f"{bg_g}"
        f'<text x="{MARGIN}" y="26" class="m" font-size="11" font-weight="600" fill="#7DD3FC" letter-spacing="1.8">{_esc(eyebrow)}</text>'
        f'<text x="{W - MARGIN}" y="26" text-anchor="end" class="m" font-size="11" fill="{TEXT_MUTED}">{_esc(badge)} · CLICK IMAGE FOR FULL 1280×720</text>'
        f'<text x="{MARGIN}" y="52" class="s" font-size="21" font-weight="700" fill="{TEXT_PRIMARY}">{_esc(title)}</text>'
        f'<text x="{MARGIN}" y="76" class="s" font-size="13.5" fill="{TEXT_SECONDARY}">{_esc(line1)}</text>'
        f'<text x="{MARGIN}" y="96" class="s" font-size="13.5" fill="{TEXT_SECONDARY}">{_esc(line2)}</text>'
        f'<g clip-path="url(#{sc_id})">'
        f'<image x="{MARGIN}" y="114" width="904" height="508" preserveAspectRatio="xMidYMid slice" href="{shot_uri}" xlink:href="{shot_uri}"/>'
        f"</g>"
        f'<rect x="{MARGIN}" y="114" width="904" height="508" rx="10" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.22" stroke-width="1"/>'
    )
    return _wrap_svg(W, h, aria, defs_extra, body, style), h


def _render_themes(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str]) -> tuple[str, int]:
    h = 540
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_themes")
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    card_w, img_h, foot_h = 290, 163, 34
    card_h = img_h + foot_h
    gap_x, gap_y = 17, 16
    start_x, start_y = MARGIN, 90

    clips = []
    cards = []
    for idx, (title, sub, accent, img_path) in enumerate(THEMES_GRID):
        col = idx % 3
        row = idx // 3
        cx = start_x + col * (card_w + gap_x)
        cy = start_y + row * (card_h + gap_y)
        uri, _, _ = _load_cropped_webp_uri(img_path, 380, crop_alpha=False, quality=90)
        cid = f"th_c_{idx}"
        clips.append(f'<clipPath id="{cid}"><rect x="{cx}" y="{cy}" width="{card_w}" height="{card_h}" rx="10"/></clipPath>')
        cards.append(
            f'<g clip-path="url(#{cid})">'
            f'<rect x="{cx}" y="{cy}" width="{card_w}" height="{card_h}" fill="#101625"/>'
            f'<image x="{cx}" y="{cy}" width="{card_w}" height="{img_h}" preserveAspectRatio="xMidYMid slice" href="{uri}" xlink:href="{uri}"/>'
            f'<rect x="{cx}" y="{cy}" width="{card_w}" height="2.5" fill="{accent}"/>'
            f'<text x="{cx + 12}" y="{cy + img_h + 22}" class="s" font-size="13" font-weight="600" fill="{TEXT_PRIMARY}">{_esc(title)}</text>'
            f'<text x="{cx + card_w - 12}" y="{cy + img_h + 22}" text-anchor="end" class="s" font-size="12" fill="{TEXT_MUTED}">{_esc(sub)}</text>'
            f"</g>"
            f'<rect x="{cx}" y="{cy}" width="{card_w}" height="{card_h}" rx="10" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.16" stroke-width="1"/>'
        )

    langs = "English · 中文 · Français · Deutsch · Polski · Español · 日本語 · Русский · Italiano · Tiếng Việt"
    body = (
        f"{bg_g}"
        f"{_section_heading('VISUAL SYSTEM', '6 NATIVE THEMES & 10 LANGUAGES', 'SINGLE REACTIVE SHELL', y=34)}"
        f'{"".join(cards)}'
        f'<text x="500" y="520" text-anchor="middle" class="s" font-size="12.5" fill="{TEXT_SECONDARY}">10 UI Languages: {_esc(langs)}</text>'
    )
    return _wrap_svg(W, h, "6 native themes and 10 UI languages", defs_bg + "".join(clips), body, style), h


def _render_shots_head(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str]) -> tuple[str, int]:
    h = 42
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_shead")
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    body = (
        f"{bg_g}"
        f'<text x="{MARGIN}" y="27" class="m" font-size="11" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="1.8">OPEN FULL-RESOLUTION 1280×720 STUDIO SCREENS</text>'
        f'<text x="{W - MARGIN}" y="27" text-anchor="end" class="m" font-size="11" fill="#7D8B9F">DIRECT WEBP LINKS</text>'
    )
    return _wrap_svg(W, h, "More studio screenshots", defs_bg, body, style), h


def _render_models(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str]) -> tuple[str, int]:
    h = 594
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_models")
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"

    # Top row: 7 real controller cards from src/assets/controllers/
    cw_box, ch_box, gap = 119, 148, 12
    # 7 * 119 + 6 * 12 = 833 + 72 = 905 -> exact fit in 904!
    sx0 = 47.5
    pedestals = []
    for i, (label, path, badge, dot_col, ver_hw) in enumerate(CONTROLLERS_LINEUP):
        px = round(sx0 + i * (cw_box + gap), 1)
        py = 92.0
        cx = round(px + cw_box / 2, 1)
        raw = _load_cropped_webp_uri(path, 210)
        uri, iw, ih = _fit_box(raw, 102, 70)
        bg = "#131C30" if ver_hw else CARD_FILL
        op = "0.26" if ver_hw else "0.12"
        tw = len(badge) * 3.0
        dx = round(cx - tw - 4, 1)
        pedestals.append(
            f'<rect x="{px}" y="{py}" width="{cw_box}" height="{ch_box}" rx="10" fill="{bg}" fill-opacity="0.90" stroke="{BORDER_SILVER}" stroke-opacity="{op}" stroke-width="1"/>'
            f'<ellipse cx="{cx}" cy="176" rx="38" ry="5.5" fill="#020307" fill-opacity="0.72"/>'
            f'<image x="{round(cx - iw / 2, 1)}" y="{round(140 - ih / 2, 1)}" width="{iw}" height="{ih}" preserveAspectRatio="xMidYMid meet" href="{uri}" xlink:href="{uri}"/>'
            f'<text x="{cx}" y="203" text-anchor="middle" class="s" font-size="13" font-weight="600" fill="{TEXT_PRIMARY}">{_esc(label)}</text>'
            f'<circle cx="{dx}" cy="222" r="2.8" fill="{dot_col}"/>'
            f'<text x="{round(dx + 7, 1)}" y="225.5" class="m" font-size="10" fill="{TEXT_SECONDARY}">{_esc(badge)}</text>'
        )

    # Bottom table covering honest capability matrix
    rows = [
        ("EasySMX X20", "Buttons, curves, macros (M1–M4), vibration + PC Mode", "Verified BLE GATT", "#22C55E"),
        ("EasySMX X20 Pro", "Full on-device + 4-motor RGB + 160x80 screen + Dongle/BLE + PC Mode", "Verified BLE + USB", "#22C55E"),
        ("EasySMX X15", "Read-only preview + Full Windows/Linux PC Mode", "PC Mode", "#60A5FA"),
        ("EasySMX X10", "Experimental KRLY BLE read (from EasyGamepad) + PC Mode", "Unverified BLE", "#F59E0B"),
        ("EasySMX X05 Pro", "Read-only preview + DInput recognition helper + PC Mode", "PC Mode", "#60A5FA"),
        ("EasySMX D10", "Read-only preview + Full PC Mode curves, keyboard & mouse", "PC Mode", "#60A5FA"),
        ("EasySMX Dune 8K", "4-motor haptic visual + M1–M4 layout + PC Mode (native unverified)", "PC Mode", "#60A5FA"),
    ]
    tbl_y = 258
    row_h = 35
    tbl = [
        f'<rect x="{MARGIN}" y="{tbl_y}" width="{CONTENT_W}" height="{32 + len(rows) * row_h}" rx="12" fill="{CARD_FILL}" fill-opacity="0.90" stroke="{BORDER_SILVER}" stroke-opacity="0.14" stroke-width="1"/>',
        f'<rect x="{MARGIN}" y="{tbl_y}" width="{CONTENT_W}" height="32" rx="12" fill="#131C30"/>',
        f'<text x="{MARGIN + 18}" y="{tbl_y + 20}" class="m" font-size="10.5" font-weight="700" fill="{TEXT_MUTED}" letter-spacing="1.4">CONTROLLER</text>',
        f'<text x="{MARGIN + 188}" y="{tbl_y + 20}" class="m" font-size="10.5" font-weight="700" fill="{TEXT_MUTED}" letter-spacing="1.4">CONFIGURATION &amp; FEATURES</text>',
        f'<text x="{W - MARGIN - 18}" y="{tbl_y + 20}" text-anchor="end" class="m" font-size="10.5" font-weight="700" fill="{TEXT_MUTED}" letter-spacing="1.4">STATUS</text>',
    ]
    for r_i, (model, feat, status, scol) in enumerate(rows):
        ry = tbl_y + 32 + r_i * row_h
        if r_i % 2 == 1:
            tbl.append(f'<rect x="{MARGIN + 1}" y="{ry}" width="{CONTENT_W - 2}" height="{row_h}" fill="#10182B" fill-opacity="0.55"/>')
        tbl.append(
            f'<line x1="{MARGIN}" y1="{ry}" x2="{W - MARGIN}" y2="{ry}" stroke="{BORDER_SILVER}" stroke-opacity="0.06" stroke-width="1"/>'
            f'<text x="{MARGIN + 18}" y="{ry + 22}" class="s" font-size="13" font-weight="600" fill="{TEXT_PRIMARY}">{_esc(model)}</text>'
            f'<text x="{MARGIN + 188}" y="{ry + 22}" class="s" font-size="12.5" fill="{TEXT_SECONDARY}">{_esc(feat)}</text>'
            f'<text x="{W - MARGIN - 18}" y="{ry + 22}" text-anchor="end" class="m" font-size="11.5" font-weight="600" fill="{scol}">{_esc(status)}</text>'
        )

    tip_y = tbl_y + 32 + len(rows) * row_h + 14
    body = (
        f"{bg_g}"
        f"{_section_heading('ARCHIVE // 02', 'SUPPORTED CONTROLLERS', 'ALL 7 REAL MODELS', y=34)}"
        f'{"".join(pedestals)}'
        f'{"".join(tbl)}'
        f'<rect x="{MARGIN}" y="{tip_y}" width="{CONTENT_W}" height="38" rx="8" fill="#10182B" fill-opacity="0.88" stroke="{BORDER_SILVER}" stroke-opacity="0.12" stroke-width="1"/>'
        f'<text x="{MARGIN + 16}" y="{tip_y + 23}" class="s" font-size="12.5" fill="{TEXT_SECONDARY}">'
        f'<tspan font-weight="700" fill="{TEXT_PRIMARY}">DInput tip (STK-7024X / 0079:181C):</tspan> Switch pads using DInput back to XInput mode for full PC Mode and reliable X20 Pro detection.'
        f"</text>"
    )
    return _wrap_svg(W, h, "Supported EasySMX controllers: X20, X20 Pro, X15, X10, X05 Pro, D10, Dune 8K", defs_bg, body, style), h


def _render_download_head(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str], tag: str) -> tuple[str, int]:
    h = 128
    ver = release_name(tag)
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_dlhead")
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    body = (
        f"{bg_g}"
        f"{_section_heading('ARCHIVE // 03', f'DOWNLOAD — {ver}', f'TAG {tag}', y=34)}"
        f'<text x="{MARGIN}" y="102" class="s" font-size="13.5" fill="{TEXT_SECONDARY}"><tspan font-weight="700" fill="{TEXT_PRIMARY}">Windows 10/11 (x64):</tspan> Grab the installer (recommended, Start menu + uninstaller) or portable single-file exe.</text>'
        f'<text x="{MARGIN}" y="121" class="s" font-size="13" fill="{TEXT_MUTED}">Everything is bundled (.NET 10, native engine, assets). Verify checksums against SHA256SUMS.txt.</text>'
    )
    return _wrap_svg(W, h, f"Download X20CTL {ver}", defs_bg, body, style), h


def _render_download_foot(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str]) -> tuple[str, int]:
    h = 94
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_dlfoot")
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    body = (
        f"{bg_g}"
        f'<text x="{MARGIN}" y="28" class="s" font-size="13" fill="{TEXT_SECONDARY}"><tspan font-weight="700" fill="{TEXT_PRIMARY}">Code signing:</tspan> Built straight from this repo on GitHub Actions; free open-source signing applied via SignPath Foundation.</text>'
        f'<text x="{MARGIN}" y="47" class="s" font-size="13" fill="{TEXT_MUTED}">Until active, executables are unsigned so Windows SmartScreen may warn — check SHA256SUMS.txt and never disable antivirus.</text>'
        f'<text x="{MARGIN}" y="70" class="s" font-size="13" fill="{TEXT_SECONDARY}"><tspan font-weight="700" fill="{TEXT_PRIMARY}">Linux Studio status:</tspan> Avalonia bundles compile on CI and pass headless checks; end-to-end physical Linux verification is pending.</text>'
    )
    return _wrap_svg(W, h, "Code signing and Linux Studio verification status", defs_bg, body, style), h


def _render_reverse_eng(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str]) -> tuple[str, int]:
    h = 312
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_re")
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"

    pkt = [
        ("OPCODE", "1B command", 122),
        ("LENGTH", "payload size", 122),
        ("SERIAL", "session ctr", 122),
        ("NONCE", "keystream idx", 132),
        ("PAYLOAD", "up to 15 bytes", 232),
        ("CRC-8", "poly 0xEB", 124),
    ]
    px = MARGIN
    pkt_svg = []
    for name, sub, pw in pkt:
        pkt_svg.append(
            f'<rect x="{px}" y="94" width="{pw}" height="48" rx="8" fill="{CARD_FILL}" stroke="{BORDER_SILVER}" stroke-opacity="0.16" stroke-width="1"/>'
            f'<text x="{px + 12}" y="114" class="m" font-size="11" font-weight="700" fill="{TEXT_PRIMARY}">{_esc(name)}</text>'
            f'<text x="{px + 12}" y="131" class="s" font-size="11.5" fill="{TEXT_MUTED}">{_esc(sub)}</text>'
        )
        px += pw + 10

    steps = ["1. Decompile", "2. Capture BLE", "3. Diff 1 setting", "4. Verify read-back", "5. Ship in Studio"]
    sx, sw, gap = MARGIN, 166, 18
    step_svg = []
    for i, lbl in enumerate(steps):
        hi = i >= 3
        step_svg.append(
            f'<rect x="{sx}" y="156" width="{sw}" height="34" rx="17" fill="{"#17233B" if hi else "#0E1422"}" '
            f'stroke="{BORDER_SILVER}" stroke-opacity="{"0.30" if hi else "0.14"}" stroke-width="1"/>'
            f'<text x="{sx + sw / 2}" y="177" text-anchor="middle" class="s" font-size="12" font-weight="600" fill="{TEXT_PRIMARY if hi else TEXT_SECONDARY}">{_esc(lbl)}</text>'
        )
        if i < len(steps) - 1:
            step_svg.append(f'<text x="{sx + sw + gap / 2}" y="177" text-anchor="middle" class="s" font-size="12" fill="{TEXT_MUTED}">→</text>')
        sx += sw + gap

    bullets = [
        ("Transport & Safety:", "BLE GATT (+ USB serial for X20 Pro screen uploads). OTA characteristics are left untouched — never flashes firmware."),
        ("Honest Identity:", "Matches real hardware BLE and USB descriptors while avoiding ambiguous 045E:028E and STK-7024X DInput traps."),
        ("Verified Writes Only:", "Nothing writes to controller memory unless read-back has been verified; drafts stay local in PC Mode."),
        ("Research Boundary:", "Raw captures and personal IDs stay private; only sanitized protocol docs ship publicly."),
    ]
    bsvg = []
    for bi, (head, desc) in enumerate(bullets):
        by = 218 + bi * 22
        bsvg.append(
            f'<rect x="{MARGIN}" y="{by - 8}" width="5" height="5" fill="#60A5FA"/>'
            f'<text x="{MARGIN + 15}" y="{by}" class="s" font-size="12.5" fill="{TEXT_SECONDARY}"><tspan font-weight="700" fill="{TEXT_PRIMARY}">{_esc(head)}</tspan> {_esc(desc)}</text>'
        )

    body = (
        f"{bg_g}"
        f"{_section_heading('ARCHIVE // 04', 'REVERSE ENGINEERING', 'KEYLINKER WIRE PROTOCOL', y=34)}"
        f'{"".join(pkt_svg)}{"".join(step_svg)}{"".join(bsvg)}'
    )
    return _wrap_svg(W, h, "Reverse engineering KeyLinker GATT packet structure and safety guarantees", defs_bg, body, style), h


def _render_local_first(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str]) -> tuple[str, int]:
    h = 186
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_local")
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    pts = [
        ("Zero accounts & zero telemetry:", "Handheld Agent XCTL, setups, drafts, and PC Mode profiles remain locally in your user folder."),
        ("User-controlled updates & drivers:", "Checks stable GitHub releases only (toggleable in Tools); asks before installing pinned ViGEmBus/HidHide."),
        ("Offline launcher library reads:", "Game shelf inspects only local Steam, Epic, GOG, Riot, Heroic, and Lutris manifests on disk."),
        ("Explicit opt-in uploads:", "Controller Check and Feedback upload only when confirmed by you. X20CTL never flashes firmware."),
    ]
    bsvg = []
    for i, (head, desc) in enumerate(pts):
        by = 104 + i * 21
        bsvg.append(
            f'<rect x="{MARGIN}" y="{by - 8}" width="5" height="5" fill="#22C55E"/>'
            f'<text x="{MARGIN + 15}" y="{by}" class="s" font-size="12.5" fill="{TEXT_SECONDARY}"><tspan font-weight="700" fill="{TEXT_PRIMARY}">{_esc(head)}</tspan> {_esc(desc)}</text>'
        )
    body = f"{bg_g}{_section_heading('ARCHIVE // 05', 'LOCAL FIRST', 'NO ACCOUNT · ZERO TELEMETRY', y=34)}{''.join(bsvg)}"
    return _wrap_svg(W, h, "Local first guarantees", defs_bg, body, style), h


def _render_credits(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str]) -> tuple[str, int]:
    h = 186
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_credits")
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    lines = [
        ("Kiin & Team EasySMX:", "Special thanks for supplying an EasySMX X20 Pro to turn protocol guesses into verified hardware support."),
        ("Edgard (edgardmessias):", "Thanks for decoding the X10 Bluetooth protocol in EasyGamepad; X10 read-back builds on his work."),
        ("Monratis:", "Thanks for resolving simultaneous mouse/gamepad focus and testing X20 Pro support on physical hardware."),
        ("Design & input inspiration:", "Console layout inspired by ApexSenseBridge (Kayn arts); mouse defaults inspired by Gopher360."),
    ]
    bsvg = []
    for i, (head, desc) in enumerate(lines):
        by = 104 + i * 21
        bsvg.append(
            f'<rect x="{MARGIN}" y="{by - 8}" width="5" height="5" fill="#E5484D"/>'
            f'<text x="{MARGIN + 15}" y="{by}" class="s" font-size="12.5" fill="{TEXT_SECONDARY}"><tspan font-weight="700" fill="{TEXT_PRIMARY}">{_esc(head)}</tspan> {_esc(desc)}</text>'
        )
    body = f"{bg_g}{_section_heading('ARCHIVE // 06', 'CREDITS & LICENSE', 'GPL-3.0-OR-LATER', y=34)}{''.join(bsvg)}"
    return _wrap_svg(W, h, "Credits and contributors", defs_bg, body, style), h


def _render_license_note(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str]) -> tuple[str, int]:
    h = 64
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_lic")
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    body = (
        f"{bg_g}"
        f'<text x="{MARGIN}" y="26" class="s" font-size="13" fill="{TEXT_SECONDARY}">From V0.012 (4.1.0 Preview 3) onward, X20CTL is free software under the <tspan font-weight="700" fill="{TEXT_PRIMARY}">GNU General Public License v3.0 or later</tspan>.</text>'
        f'<text x="{MARGIN}" y="46" class="s" font-size="12.5" fill="{TEXT_MUTED}">Earlier MIT-licensed code (up to V0.011) keeps its notice in licenses/ and NOTICE.</text>'
    )
    return _wrap_svg(W, h, "GPL-3.0-or-later license note", defs_bg, body, style), h


def _render_end(y0: float, total_h: int, softboxes: list[_Softbox], beams: list[str], tag: str) -> tuple[str, int]:
    h = 68
    ver = release_name(tag)
    defs_bg, bg_g = _obsidian_bg(0, y0, W, h, total_h, softboxes, beams, "c_end", round_bot=True)
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    body = (
        f"{bg_g}"
        f'<line x1="{MARGIN}" y1="18" x2="{W - MARGIN}" y2="18" stroke="{BORDER_SILVER}" stroke-opacity="0.12" stroke-width="1"/>'
        f'<text x="{MARGIN}" y="44" class="m" font-size="11" fill="{TEXT_MUTED}" letter-spacing="1.6">X20CTL // {_esc(ver)} // GPL-3.0-OR-LATER</text>'
        f'<text x="{W - MARGIN}" y="44" text-anchor="end" class="m" font-size="11" fill="{TEXT_MUTED}" letter-spacing="1.6">BUILT ON REAL HARDWARE</text>'
    )
    return _wrap_svg(W, h, f"X20CTL {ver} footer", defs_bg, body, style), h


# Companion header helpers for CHANGELOG.md, IF-YOUR-CONTROLLER-ISNT-WORKING.md, docs/linux-user-guide.md
def build_guide_header_svg(
    prefix: str, eyebrow: str, title: str, subtitle: str, ctrl_rel: str
) -> str:
    w, h = 1200, 108
    raw = _load_cropped_webp_uri(CTRL_DIR / ctrl_rel, 260)
    uri, cw, ch = _fit_box(raw, 134, 86)
    defs = (
        f'<linearGradient id="{prefix}_bg" x1="0%" y1="0%" x2="100%" y2="100%">'
        f'<stop offset="0%" stop-color="#080A0F"/><stop offset="55%" stop-color="#0D111A"/><stop offset="100%" stop-color="#121824"/>'
        f"</linearGradient>"
        f'<radialGradient id="{prefix}_sb" cx="92%" cy="50%" r="45%">'
        f'<stop offset="0%" stop-color="#B8CEF0" stop-opacity="0.16"/><stop offset="100%" stop-color="#0D111A" stop-opacity="0"/>'
        f"</radialGradient>"
        f'<clipPath id="{prefix}_clip"><rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="16"/></clipPath>'
    )
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    body = (
        f'<g clip-path="url(#{prefix}_clip)">'
        f'<rect width="{w}" height="{h}" fill="url(#{prefix}_bg)"/>'
        f'<rect width="{w}" height="{h}" fill="url(#{prefix}_sb)"/>'
        f'<text x="34" y="30" class="m" font-size="11" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="2">X20CTL · {_esc(eyebrow)}</text>'
        f'<text x="34" y="64" class="s" font-size="26" font-weight="700" fill="{TEXT_PRIMARY}" letter-spacing="-0.4">{_esc(title)}</text>'
        f'<text x="34" y="88" class="s" font-size="13.5" fill="{TEXT_SECONDARY}">{_esc(subtitle)}</text>'
        f'<g transform="rotate(-4 1102 54)" opacity="0.92">'
        f'<image x="{round(1102 - cw / 2, 1)}" y="{round(54 - ch / 2, 1)}" width="{cw}" height="{ch}" preserveAspectRatio="xMidYMid meet" href="{uri}" xlink:href="{uri}"/>'
        f"</g></g>"
        f'<rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="16" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.14" stroke-width="1"/>'
        f'<line x1="20" y1="1.5" x2="{w - 20}" y2="1.5" stroke="#FFFFFF" stroke-opacity="0.08" stroke-width="1"/>'
    )
    return _wrap_svg(w, h, f"{title} — {subtitle}", defs, body, style)


def build_social_preview(tag: str) -> None:
    from playwright.sync_api import sync_playwright

    ver = release_name(tag)
    w, h = 1280, 640
    blk_raw = _load_cropped_webp_uri(CTRL_DIR / "x20_pro/native/front-full.png", 680)
    wht_raw = _load_cropped_webp_uri(CTRL_DIR / "x20_pro/finishes/white/front-full.png", 560)
    blk_uri, bw, bh = _fit_box(blk_raw, 480, 326)
    wht_uri, ww, wh = _fit_box(wht_raw, 390, 260)

    defs = (
        f'<linearGradient id="soc_bg" x1="0%" y1="0%" x2="100%" y2="100%">'
        f'<stop offset="0%" stop-color="#080A0F"/><stop offset="55%" stop-color="#0D111A"/><stop offset="100%" stop-color="#121824"/>'
        f"</linearGradient>"
        f'<radialGradient id="soc_sb" cx="74%" cy="50%" r="48%">'
        f'<stop offset="0%" stop-color="#B8CEF0" stop-opacity="0.16"/><stop offset="100%" stop-color="#0D111A" stop-opacity="0"/>'
        f"</radialGradient>"
        f'<radialGradient id="soc_sh" cx="50%" cy="50%" r="50%">'
        f'<stop offset="0%" stop-color="#020306" stop-opacity="0.88"/><stop offset="100%" stop-color="#020306" stop-opacity="0"/>'
        f"</radialGradient>"
    )
    style = f".s{{font-family:{FONT_SANS}}}.m{{font-family:{FONT_MONO}}}"
    body = (
        f'<rect width="{w}" height="{h}" fill="url(#soc_bg)"/>'
        f'<rect width="{w}" height="{h}" fill="url(#soc_sb)"/>'
        f'<rect x="28" y="28" width="{w - 56}" height="{h - 56}" rx="24" fill="none" stroke="{BORDER_SILVER}" stroke-opacity="0.14" stroke-width="1"/>'
        f'<ellipse cx="835" cy="396" rx="185" ry="22" fill="url(#soc_sh)"/>'
        f'<g transform="rotate(-7 825 232)" opacity="0.95"><image x="{round(825 - ww / 2, 1)}" y="{round(232 - wh / 2, 1)}" width="{ww}" height="{wh}" preserveAspectRatio="xMidYMid meet" href="{wht_uri}" xlink:href="{wht_uri}"/></g>'
        f'<ellipse cx="978" cy="516" rx="225" ry="24" fill="url(#soc_sh)"/>'
        f'<g transform="rotate(4 978 360)"><image x="{round(978 - bw / 2, 1)}" y="{round(360 - bh / 2, 1)}" width="{bw}" height="{bh}" preserveAspectRatio="xMidYMid meet" href="{blk_uri}" xlink:href="{blk_uri}"/></g>'
        f'<text x="86" y="138" class="m" font-size="14" font-weight="600" fill="{TEXT_MUTED}" letter-spacing="2.8">OPEN-SOURCE CONTROLLER STUDIO</text>'
        f'<text x="86" y="252" class="s" font-size="96" font-weight="800" fill="{TEXT_PRIMARY}" letter-spacing="-2.5">X20CTL</text>'
        f'<rect x="86" y="286" width="108" height="36" rx="18" fill="#161D2B" stroke="{BORDER_SILVER}" stroke-opacity="0.24" stroke-width="1"/>'
        f'<text x="140" y="309" text-anchor="middle" class="m" font-size="15" font-weight="600" fill="#DCE5F5">{_esc(ver)}</text>'
        f'<text x="86" y="378" class="s" font-size="28" font-weight="400" fill="#B7C2D5">Configure, tune curves &amp; RGB</text>'
        f'<text x="86" y="416" class="s" font-size="28" font-weight="400" fill="#B7C2D5">for EasySMX controllers</text>'
        f'<line x1="86" y1="472" x2="540" y2="472" stroke="{BORDER_SILVER}" stroke-opacity="0.12" stroke-width="1"/>'
        f'<text x="86" y="510" class="m" font-size="14.5" fill="{TEXT_MUTED}">Windows 10/11 · Linux x64 · 100% Offline Agent · GPL-3.0</text>'
    )
    svg = _wrap_svg(w, h, f"X20CTL {ver} social preview", defs, body, style)
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", suffix=".html", delete=False) as tf:
        tf.write(f"<!doctype html><html><body style='margin:0;background:#080A0F;overflow:hidden;'>{svg}</body></html>")
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
    ver = release_name(tag)
    BG_DIR.mkdir(parents=True, exist_ok=True)
    for old_svg in BG_DIR.glob("*.svg"):
        old_svg.unlink()

    # Fixed exact heights per band to guarantee exact global Y coordinate stitching (0px seams)
    band_heights = [
        ("hero", 384),
        ("nav", 62),
        ("thanks", 288),
        ("demo", 606),
        ("intro", 220),
        ("spot_1", 640),
        ("spot_2", 640),
        ("spot_3", 640),
        ("spot_4", 640),
        ("themes", 540),
        ("shots_head", 42),
        ("shots_r1", 50),
        ("shots_r2", 56),
        ("models", 594),
        ("download_head", 128),
        ("dl_win", 54),
        ("dl_lin1", 52),
        ("dl_lin2", 52),
        ("download_foot", 94),
        ("dl_meta", 52),
        ("reverse_eng", 312),
        ("re_links", 52),
        ("local_first", 186),
        ("local_links", 52),
        ("credits", 186),
        ("cred_links", 52),
        ("license_note", 64),
        ("lic_links", 52),
        ("end", 68),
    ]
    y_offsets: dict[str, float] = {}
    cy = 0
    for key, bh in band_heights:
        y_offsets[key] = float(cy)
        cy += bh
    total_h = cy

    softboxes, beams = _build_studio_planes(y_offsets, total_h)
    slices: dict[str, str] = {}

    slices["hero.svg"], _ = _render_hero(y_offsets["hero"], total_h, softboxes, beams, tag)

    s_nav, _, _ = _emit_button_row(
        "nav",
        y_offsets["nav"],
        62,
        total_h,
        softboxes,
        beams,
        [
            (f"Download {ver}", f"Download {ver}", True),
            ("Reverse engineering", "Reverse engineering", False),
            ("Changelog", "Changelog", False),
            ("Controller not working?", "Controller not working?", False),
        ],
        btn_widths=[210, 224, 180, 260],
        gap=10,
    )
    slices.update(s_nav)

    slices["thanks.svg"], _ = _render_thanks(y_offsets["thanks"], total_h, softboxes, beams)
    slices["demo.svg"], _ = _render_demo(y_offsets["demo"], total_h, softboxes, beams, tag)
    slices["intro.svg"], _ = _render_intro(y_offsets["intro"], total_h, softboxes, beams)

    slices["spot_1.svg"], _ = _render_spotlight(
        y_offsets["spot_1"],
        total_h,
        softboxes,
        beams,
        "c_s1",
        "HIGHLIGHT 01 // X20 PRO LIGHTING & DISPLAY",
        "RGB Studio, Live Effects & 160x80 Screen",
        "4-ZONE LED + DONGLE & BLE",
        "Configure all four zones live, run Rainbow Wave / Trigger Glow / Stick Heat / Ambient Screen Sync / Battery",
        "gauges, or upload images & GIFs to the 160x80 screen over USB serial (simultaneous 413D:2131 dongle + unpaired BLE).",
        "rgb-studio.webp",
        "X20CTL RGB Studio and 160x80 screen features on EasySMX X20 Pro",
    )
    slices["spot_2.svg"], _ = _render_spotlight(
        y_offsets["spot_2"],
        total_h,
        softboxes,
        beams,
        "c_s2",
        "HIGHLIGHT 02 // LOCAL NATURAL-LANGUAGE ASSISTANT",
        "100% Offline Handheld Agent XCTL",
        "3,048 PHRASES · ZERO CLOUD",
        "Ask in plain language inside your controller frame: 3,048 handwritten phrases, Tab autocomplete, + New chat,",
        "live Check-up drift audits, interactive RGB cards, doc search, and multi-step Undo — nothing ever leaves your PC.",
        "agent-xctl.webp",
        "100% Offline Handheld Agent XCTL inside X20 Pro frame",
    )
    slices["spot_3.svg"], _ = _render_spotlight(
        y_offsets["spot_3"],
        total_h,
        softboxes,
        beams,
        "c_s3",
        "HIGHLIGHT 03 // TUNING & PADDLE TIMELINES",
        "Stick & Trigger Curves, Buttons & Rear Paddle Macros",
        "M1–M4 / M1–M6 PADDLES",
        "Sculpt left/right sticks and triggers with Default, Quick, Slow, Smooth, and Fine presets or custom points,",
        "listen-to-bind front & rear buttons, and build timed macro sequences with instant local draft previews.",
        "curves.webp",
        "Stick and trigger response curves, buttons, and macros in X20CTL",
    )
    slices["spot_4.svg"], _ = _render_spotlight(
        y_offsets["spot_4"],
        total_h,
        softboxes,
        beams,
        "c_s4",
        "HIGHLIGHT 04 // SYSTEM-WIDE SOFTWARE BRIDGE",
        "PC Mode — Xbox 360 / PS4, 64-Key Keyboard, Mouse & 4-Zone Vibration",
        "4 PLAYERS · VIEW + MENU PAUSE",
        "Run per-game PC Mode profiles via ViGEmBus + HidHide (Windows) or evdev/uinput (Linux): 64-key Keyboard chains,",
        "Controller as Mouse (M1–M7, 2–30 Hz turbo, View + Y toggle), and 4-motor Vibration up to 200% rumble boost.",
        "mouse.webp",
        "PC Mode Mouse, Keyboard sequences, Xbox 360 / PS4 and 4-zone Vibration in X20CTL",
    )

    slices["themes.svg"], _ = _render_themes(y_offsets["themes"], total_h, softboxes, beams)
    slices["shots_head.svg"], _ = _render_shots_head(y_offsets["shots_head"], total_h, softboxes, beams)

    s_sr1, _, _ = _emit_button_row(
        "shots_r1",
        y_offsets["shots_r1"],
        50,
        total_h,
        softboxes,
        beams,
        [
            ("Live RGB Effects", "Open full-res Live RGB Effects screenshot", False),
            ("Buttons & Paddles", "Open full-res Buttons and Rear Paddles screenshot", False),
            ("Keyboard 64-Key", "Open full-res Keyboard Sequences screenshot", False),
            ("4-Zone Vibration", "Open full-res 4-Zone Vibration screenshot", False),
        ],
        btn_widths=[216, 222, 218, 218],
        gap=10,
    )
    slices.update(s_sr1)

    s_sr2, _, _ = _emit_button_row(
        "shots_r2",
        y_offsets["shots_r2"],
        56,
        total_h,
        softboxes,
        beams,
        [
            ("Stick Diagnostics", "Open full-res Stick Diagnostics screenshot", False),
            ("Controller Check", "Open full-res Controller Check screenshot", False),
            ("Linux Studio", "Open full-res Linux Studio screenshot", False),
        ],
        btn_widths=[220, 220, 216],
        gap=10,
    )
    slices.update(s_sr2)

    slices["models.svg"], _ = _render_models(y_offsets["models"], total_h, softboxes, beams)
    slices["download_head.svg"], _ = _render_download_head(y_offsets["download_head"], total_h, softboxes, beams, tag)

    s_win, _, _ = _emit_button_row(
        "dl_win",
        y_offsets["dl_win"],
        54,
        total_h,
        softboxes,
        beams,
        [
            (f"X20CTL-{ver}-windows-x64-setup.exe", f"Download X20CTL-{ver}-windows-x64-setup.exe", True),
            (f"X20CTL-{ver}-windows-x64.exe (Portable)", f"Download X20CTL-{ver}-windows-x64.exe", False),
        ],
        btn_widths=[447, 447],
        gap=10,
    )
    slices.update(s_win)

    s_lin1, _, _ = _emit_button_row(
        "dl_lin1",
        y_offsets["dl_lin1"],
        52,
        total_h,
        softboxes,
        beams,
        [
            (f"X20CTL-{ver}-linux-x86_64.AppImage", f"Download X20CTL-{ver}-linux-x86_64.AppImage", False),
            (f"X20CTL-{ver}-linux-x86_64.flatpak", f"Download X20CTL-{ver}-linux-x86_64.flatpak", False),
            (f"X20CTL-{ver}-linux-check.zip", f"Download X20CTL-{ver}-linux-check.zip", False),
        ],
        btn_widths=[312, 302, 270],
        gap=10,
    )
    slices.update(s_lin1)

    s_lin2, _, _ = _emit_button_row(
        "dl_lin2",
        y_offsets["dl_lin2"],
        52,
        total_h,
        softboxes,
        beams,
        [
            (f"X20CTL-{ver}-linux-x64.tar.gz", f"Download X20CTL-{ver}-linux-x64.tar.gz", False),
            (f"X20CTL-{ver}-linux-musl-x64.tar.gz", f"Download X20CTL-{ver}-linux-musl-x64.tar.gz", False),
            ("SHA256SUMS.txt", "Download SHA256SUMS.txt", False),
        ],
        btn_widths=[306, 336, 242],
        gap=10,
    )
    slices.update(s_lin2)

    slices["download_foot.svg"], _ = _render_download_foot(y_offsets["download_foot"], total_h, softboxes, beams)

    s_dmeta, _, _ = _emit_button_row(
        "dl_meta",
        y_offsets["dl_meta"],
        52,
        total_h,
        softboxes,
        beams,
        [
            ("SignPath Foundation", "SignPath Foundation", False),
            ("Linux user guide", "Linux user guide", False),
            ("Version numbers", "Version numbers", False),
            ("All releases", "All releases", False),
        ],
        btn_widths=[224, 214, 214, 222],
        gap=10,
    )
    slices.update(s_dmeta)

    slices["reverse_eng.svg"], _ = _render_reverse_eng(y_offsets["reverse_eng"], total_h, softboxes, beams)

    s_re, _, _ = _emit_button_row(
        "re_links",
        y_offsets["re_links"],
        52,
        total_h,
        softboxes,
        beams,
        [
            ("Protocol reference", "Protocol reference", False),
            ("Reverse-engineering log", "Reverse-engineering log", False),
            ("Controller scan kit", "Controller scan kit", False),
            ("Research policy", "Research policy", False),
        ],
        btn_widths=[214, 240, 210, 210],
        gap=10,
    )
    slices.update(s_re)

    slices["local_first.svg"], _ = _render_local_first(y_offsets["local_first"], total_h, softboxes, beams)

    s_loc, _, _ = _emit_button_row(
        "local_links",
        y_offsets["local_links"],
        52,
        total_h,
        softboxes,
        beams,
        [
            ("Privacy policy", "Privacy policy", False),
            ("Controller troubleshooting", "Controller troubleshooting", False),
        ],
        btn_widths=[240, 280],
        gap=10,
    )
    slices.update(s_loc)

    slices["credits.svg"], _ = _render_credits(y_offsets["credits"], total_h, softboxes, beams)

    s_cred, _, _ = _emit_button_row(
        "cred_links",
        y_offsets["cred_links"],
        52,
        total_h,
        softboxes,
        beams,
        [
            ("EasyGamepad", "EasyGamepad", False),
            ("Monratis", "Monratis", False),
            ("ApexSenseBridge", "ApexSenseBridge", False),
            ("Support on Ko-fi", "Support on Ko-fi", False),
        ],
        btn_widths=[212, 204, 232, 226],
        gap=10,
    )
    slices.update(s_cred)

    slices["license_note.svg"], _ = _render_license_note(y_offsets["license_note"], total_h, softboxes, beams)

    s_lic, _, _ = _emit_button_row(
        "lic_links",
        y_offsets["lic_links"],
        52,
        total_h,
        softboxes,
        beams,
        [
            ("GPL-3.0-or-later", "GPL-3.0-or-later", False),
            ("NOTICE", "NOTICE", False),
            ("Third-party notices", "Third-party notices", False),
        ],
        btn_widths=[224, 180, 220],
        gap=10,
    )
    slices.update(s_lic)

    slices["end.svg"], _ = _render_end(y_offsets["end"], total_h, softboxes, beams, tag)

    written: list[Path] = []
    for fname, content in slices.items():
        ET.fromstring(content)
        out_path = BG_DIR / fname
        out_path.write_text(content, encoding="utf-8")
        written.append(out_path)

    # Companion headers for guide docs
    guides = [
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
    for path, content in guides:
        ET.fromstring(content)
        path.write_text(content, encoding="utf-8")
        written.append(path)

    if social_preview:
        build_social_preview(tag)
        written.append(SOCIAL_PREVIEW_PATH)

    return written


def main() -> None:
    parser = argparse.ArgumentParser(description="Build full-page Obsidian Hardware Showroom README artwork.")
    parser.add_argument("--tag", default="v0.025", help="Release tag (e.g. v0.025)")
    parser.add_argument("--social-preview", action="store_true", help="Also render assets/social-preview.png (1280x640)")
    args = parser.parse_args()
    written = generate_all(args.tag, social_preview=args.social_preview)
    bg_count = len(list(BG_DIR.glob("*.svg")))
    print(f"Generated {len(written)} Obsidian assets ({bg_count} bg slices) for {args.tag}.")


if __name__ == "__main__":
    main()

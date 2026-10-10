"""Build transparent, separately movable controller layers for the native WPF app.

Inputs are the existing per-model assets under src/assets/controllers (the same art,
silhouettes, stick-cap crops and trigger-cap polygons the older React renderer used).
Nothing in the source folders is modified. Outputs go to
src/assets/controllers/<model>/native/ plus src/assets/controllers/native-layers.json.

Every layer is a full 1536x1024 RGBA canvas in the source coordinate system, so the
native renderer only applies transforms; no offsets need to be tracked.

Every photo is relit first (owner direction 10 Oct 2026): the advertising rim light baked into the
source shots - blue along the left edges, orange along the right - is neutralised near the silhouette,
and one consistent cool-white light is added along the upper edges, so all seven read as one set shot
in the same studio. The controllers' own RGB LEDs keep their true colours. Needs numpy, Pillow, scipy.

    python tools/build_native_controller_layers.py
"""
import json
import math
import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src" / "assets" / "controllers"
MODELS = ["x20", "x20_pro", "x15", "x10", "x05", "x05_pro", "d10"]
W, H = 1536, 1024
SS = 4  # supersampling for anti-aliased masks


def path_points(d):
    """Flatten an SVG path made of M/L/C/Z commands into polygons."""
    tokens = re.findall(r"[MLCZmlcz]|-?\d+(?:\.\d+)?", d)
    polys, cur, i, cmd, pos = [], [], 0, None, (0.0, 0.0)
    while i < len(tokens):
        t = tokens[i]
        if t.isalpha():
            cmd = t.upper(); i += 1
            if cmd == "Z":
                if cur: polys.append(cur); cur = []
            continue
        if cmd == "M":
            if cur: polys.append(cur)
            pos = (float(tokens[i]), float(tokens[i + 1])); cur = [pos]; i += 2; cmd = "L"
        elif cmd == "L":
            pos = (float(tokens[i]), float(tokens[i + 1])); cur.append(pos); i += 2
        elif cmd == "C":
            p1 = (float(tokens[i]), float(tokens[i + 1])); p2 = (float(tokens[i + 2]), float(tokens[i + 3])); p3 = (float(tokens[i + 4]), float(tokens[i + 5])); i += 6
            p0 = pos
            for s in range(1, 13):
                u = s / 12; a = (1 - u) ** 3; b = 3 * (1 - u) ** 2 * u; c = 3 * (1 - u) * u ** 2; e = u ** 3
                cur.append((a * p0[0] + b * p1[0] + c * p2[0] + e * p3[0], a * p0[1] + b * p1[1] + c * p2[1] + e * p3[1]))
            pos = p3
        else:
            i += 1
    if cur: polys.append(cur)
    return polys


def poly_mask(polys, feather=1.1, grow=0):
    m = Image.new("L", (W * SS, H * SS), 0)
    dr = ImageDraw.Draw(m)
    for p in polys:
        if len(p) >= 3:
            dr.polygon([(x * SS, y * SS) for x, y in p], fill=255)
    m = m.resize((W, H), Image.LANCZOS)
    if grow < 0:
        m = m.filter(ImageFilter.MinFilter(1 - 2 * grow))
    elif grow > 0:
        m = m.filter(ImageFilter.MaxFilter(1 + 2 * grow))
    return m.filter(ImageFilter.GaussianBlur(feather)) if feather else m


def ellipse_mask(cx, cy, rx, ry, feather=1.4):
    m = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(m).ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS], fill=255)
    return m.resize((W, H), Image.LANCZOS).filter(ImageFilter.GaussianBlur(feather))


def background_key(img, mask):
    """Remove leftover backdrop inside the silhouette edge: pixels that are both near the
    silhouette boundary and close to the sampled backdrop colour fade out."""
    a = np.asarray(img.convert("RGB")).astype(np.float32)
    corners = np.concatenate([a[:24, :24].reshape(-1, 3), a[:24, -24:].reshape(-1, 3), a[-24:, :24].reshape(-1, 3), a[-24:, -24:].reshape(-1, 3)])
    bg = np.median(corners, axis=0)
    dist = np.sqrt(((a - bg) ** 2).sum(axis=2))
    m = np.asarray(mask).astype(np.float32) / 255
    edge = np.asarray(mask.filter(ImageFilter.MinFilter(15))).astype(np.float32) / 255  # interior core
    band = (m > 0) & (edge < 1)  # only the outer ~7px band is eligible
    keep = np.clip((dist - 10) / 26, 0, 1)
    m = np.where(band, m * np.maximum(keep, edge), m)
    return Image.fromarray((m * 255).astype(np.uint8))


def relight(img, mask):
    """Studio relight within the silhouette. Near the edge (a band about 7% of the width deep) a blue cast
    on left-facing parts and an orange cast on right-facing parts are pulled to a faintly cool neutral of
    the same brightness; very bright, saturated pixels are LEDs and keep their colour. Then a thin
    cool-white rim follows the upper edges, fading down the sides."""
    rgb = np.asarray(img.convert("RGB")).astype(np.float32) / 255
    inside = np.asarray(mask).astype(np.float32) / 255 > .5
    dist = ndimage.distance_transform_edt(inside)
    band = np.clip(1 - dist / (W * .075), 0, 1) ** 1.15
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    x = np.linspace(0, 1, W)[None, :]
    left = np.clip((.6 - x) / .2, 0, 1); right = np.clip((x - .4) / .2, 0, 1)  # blue rim lights the left, orange the right
    orange = np.clip((r - b) * 2.2, 0, 1) * np.clip((r - g + .1) * 2, 0, 1) * right
    blue = np.clip((b - r) * 2.2, 0, 1) * np.clip((b - g + .15) * 2, 0, 1) * left
    # LEDs never sit on the silhouette edge itself; bright saturated pixels hugging the edge are the rim light
    led = np.clip((mx - .82) / .12, 0, 1) * np.clip((sat - .55) / .2, 0, 1) * np.clip((dist - 8) / 10, 0, 1)
    cast = np.clip((orange + blue) * np.clip(sat * 2.2, 0, 1) * 1.35, 0, 1) * band * (1 - led)
    lum = (.30 * r + .55 * g + .15 * b)[..., None]
    neutral = np.concatenate([lum * .96, lum * .99, lum * 1.05], -1) * .92
    out = rgb * (1 - cast[..., None]) + neutral * cast[..., None]
    # the painted cool-white rim along the upper edges was removed (owner, 10 Oct 2026: it read as a foggy, low-quality
    # edge); ControllerOutline now draws a crisp rim on the traced silhouette instead
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8))


def clean_edge(keyed, outline):
    """Smooth cut-out edges (owner report 10 Oct 2026: "pixelated around the edges"). The colour key leaves speckled,
    stair-stepped alpha along the silhouette; a small median removes the speckle, a light blur anti-aliases it, and
    the clean supersampled outline caps it so nothing grows past the real edge."""
    k = keyed.filter(ImageFilter.MedianFilter(5)).filter(ImageFilter.GaussianBlur(.9))
    k = np.asarray(k).astype(np.float32) / 255
    o = np.asarray(outline).astype(np.float32) / 255
    m = np.minimum(k, o)
    m = np.clip((m - .04) / .92, 0, 1)  # a firm, still soft, edge: no faint grey skirt around the body
    return Image.fromarray((m * 255).astype(np.uint8))


def with_alpha(img, mask):
    a = np.asarray(img.convert("RGB")).astype(np.float32)
    m = np.asarray(mask)
    # defringe: edge pixels take their colour from just inside the body, so the backdrop or rim light that was
    # mixed into them can't show as a light or dark line around the controller
    core = m > 250
    if core.any():
        dist, (iy, ix) = ndimage.distance_transform_edt(~core, return_indices=True)
        inner = a[iy, ix]
        w = np.clip(dist / 3.0, 0, 1)[..., None] * (m < 250)[..., None]
        a = a * (1 - w) + inner * w
    a = a.astype(np.uint8)
    a[m == 0] = 0  # fully transparent pixels carry no colour, keeping layers small
    return Image.fromarray(np.dstack([a, m]), "RGBA")


def hologram(img, mask, polys):
    """Model-specific hologram line art: the photo's own surface contours (edge detection inside the
    silhouette) plus a bright silhouette rim, as luminous cyan on transparency."""
    g = np.asarray(img.convert("L").filter(ImageFilter.GaussianBlur(1.6))).astype(np.float32)
    gx = np.zeros_like(g); gy = np.zeros_like(g)
    gx[:, 1:-1] = g[:, 2:] - g[:, :-2]; gy[1:-1, :] = g[2:, :] - g[:-2, :]
    mag = np.sqrt(gx * gx + gy * gy)
    inner = np.asarray(mask.filter(ImageFilter.MinFilter(9))).astype(np.float32) / 255
    detail = np.clip((mag - 9) / 30, 0, 1) * inner
    detail = np.asarray(Image.fromarray((detail * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(.6))).astype(np.float32) / 255
    rim = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(rim)
    for p in polys:
        if len(p) >= 3:
            d.line([(x * SS, y * SS) for x, y in p] + [(p[0][0] * SS, p[0][1] * SS)], fill=255, width=3 * SS, joint="curve")
    rim = np.asarray(rim.resize((W, H), Image.LANCZOS)).astype(np.float32) / 255
    lines = np.clip(detail * .55 + rim, 0, 1)
    glow = np.asarray(Image.fromarray((lines * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(5))).astype(np.float32) / 255
    fill = (np.asarray(mask).astype(np.float32) / 255) * .10  # faint glass body
    alpha = np.clip(lines + glow * .7 + fill, 0, 1)
    core = np.clip(lines * 1.2, 0, 1)[..., None]
    base = np.array([70, 150, 255], np.float32); hot = np.array([205, 235, 255], np.float32)
    rgb = base * (1 - core) + hot * core
    return Image.fromarray(np.dstack([rgb.astype(np.uint8), (alpha * 255).astype(np.uint8)]), "RGBA")


def main():
    lighting = json.loads((ASSETS / "lighting.json").read_text())
    solids = json.loads((ASSETS / "trigger-solids.json").read_text())
    motion = json.loads((ASSETS / "trigger-motion.json").read_text())
    framing = json.loads((ASSETS / "framing.json").read_text())
    manifest = {"sourceSize": [W, H], "models": {}}
    for model in MODELS:
        src = ASSETS / model
        out = src / "native"
        out.mkdir(exist_ok=True)
        geo = json.loads((src / "geometry.json").read_text())
        front_outline = poly_mask(path_points(lighting["models"][model]["front"]))
        back_outline = poly_mask(path_points(lighting["models"][model]["back"]))
        front_mask = clean_edge(background_key(Image.open(src / "controller.png"), front_outline), front_outline)
        back_mask = clean_edge(background_key(Image.open(src / "controller-rear.png"), back_outline), back_outline)

        full = relight(Image.open(src / "controller.png"), front_mask)
        with_alpha(full, front_mask).save(out / "front-full.png", optimize=True)
        hologram(full, front_mask, path_points(lighting["models"][model]["front"])).save(out / "holo-front.png", optimize=True)
        hologram(Image.open(src / "controller-rear.png"), back_mask, path_points(lighting["models"][model]["back"])).save(out / "holo-rear.png", optimize=True)
        with_alpha(relight(Image.open(src / "controller-base.png"), front_mask), front_mask).save(out / "front-body.png", optimize=True)

        sticks = {}
        for side in ("left", "right"):
            s = geo["sticks"][side]; cap = s["cap"]
            cx, cy = cap["x"] * W, cap["y"] * H
            rx, ry = cap["w"] * W / 2, cap["h"] * H / 2
            sock = (s["x"] * W, s["y"] * H)
            crop = with_alpha(full, ellipse_mask(cx, cy, rx * .97, ry * .97))
            layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            # place the cap texture so its centre sits on the fixed socket centre
            layer.alpha_composite(crop, (int(round(sock[0] - cx)), int(round(sock[1] - cy))))
            layer.save(out / f"stick-{side[0]}.png", optimize=True)
            sticks[side] = {"center": [round(sock[0], 2), round(sock[1], 2)], "travel": round(s["radius"] * W * .62, 2), "capRadius": round(max(rx, ry), 2)}

        rear = relight(Image.open(src / "controller-rear.png"), back_mask)
        with_alpha(rear, back_mask).save(out / "rear-full.png", optimize=True)
        with_alpha(relight(Image.open(src / "controller-rear-trigger-base.png"), back_mask), back_mask).save(out / "rear-body.png", optimize=True)
        triggers = {}
        for key in ("LT", "RT"):
            pts = [tuple(p) for p in solids["models"][model][key]]
            layer = with_alpha(rear, poly_mask([pts], feather=.8, grow=1))
            layer.save(out / f"trigger-{key[0].lower()}.png", optimize=True)
            m = motion["models"][model][key]
            (x1, y1), (x2, y2) = m["hinge"]
            triggers[key] = {"hinge": [[x1, y1], [x2, y2]], "restAngle": m["restAngle"], "travelAngle": m["travelAngle"],
                             "bounds": [round(min(p[0] for p in pts), 1), round(min(p[1] for p in pts), 1), round(max(p[0] for p in pts), 1), round(max(p[1] for p in pts), 1)]}
        manifest["models"][model] = {"sticks": sticks, "triggers": triggers, "framing": framing["models"][model]}
        print(model, "ok")
    (ASSETS / "native-layers.json").write_text(json.dumps(manifest, indent=1))


if __name__ == "__main__":
    main()

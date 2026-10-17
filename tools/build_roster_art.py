"""Rebuilds the controller artwork from the owner's balanced product photographs (12 Oct 2026), exactly the way the
Dune was done (tools/add_dune_controller.py): each photograph is lifted off its white background and placed on the
1536 x 1024 art canvas; every stick cap is lifted onto its own layer with a dark socket left in the body, so the cap
really moves; each trigger is lifted off the rear photograph and the shell behind it is filled in; hologram line art,
silhouettes, control shapes, layer data and the catalog's visual block are written from the numbers in
tools/roster_art_data.py.

    python tools/build_roster_art.py PHOTOS_DIR x20            # one model
    python tools/build_roster_art.py PHOTOS_DIR all
    python tools/build_roster_art.py PHOTOS_DIR x20 --preview  # only draw the measured shapes on the photographs
"""
import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import add_dune_controller as dune          # cut(), place(), circle(), rounded(), ellipse(), edit()
import build_controller_outlines as outlines  # trace()
import build_native_controller_layers as art  # hologram(), path_points()
from roster_art_data import DATA

REPO = Path(__file__).resolve().parents[1]
CONTROLLERS = REPO / "src" / "assets" / "controllers"
W, H = 1536, 1024
HEIGHT = 880  # the controller's height on the canvas; the width is capped at 1300 like every other model


def cut(path, threshold=250, erode=2):
    """The photographs sit on pure white (255) and the products can be white too, so only what is connected to the
    border and within a hair of pure white is backdrop; a white body keeps its shading. A soft 1 px edge."""
    rgba = np.array(Image.open(path).convert("RGBA"))
    rgb = rgba[:, :, :3]
    light = (rgb.min(axis=2) >= threshold).astype(np.uint8)
    n, labels = cv2.connectedComponents(light, connectivity=4)
    border = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))) - {0}
    background = np.isin(labels, list(border)) & (light > 0)
    mask = (~background).astype(np.uint8) * 255
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
    count, comp, stats, _ = cv2.connectedComponentsWithStats(mask)
    if count > 1:  # keep the controller, drop specks
        keep = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
        mask = np.where(comp == keep, 255, 0).astype(np.uint8)
    for _ in range(erode):
        mask = cv2.erode(mask, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3)))
    alpha = cv2.GaussianBlur(mask, (0, 0), 1.0)
    return np.dstack([rgb, alpha])


def poly_shape(points):
    xs = [p[0] for p in points]; ys = [p[1] for p in points]
    path = "M" + " L".join(f"{x:g} {y:g}" for x, y in points) + " Z"
    return {"bounds": [min(xs), min(ys), max(xs) - min(xs), max(ys) - min(ys)], "path": path}


def ellipse_shape(cx, cy, rx, ry):
    return {"bounds": [cx - rx, cy - ry, 2 * rx, 2 * ry], "path": dune.ellipse(cx, cy, rx, ry)}


def soft_ellipse(cx, cy, rx, ry, feather=1.2):
    m = np.zeros((H * 4, W * 4), np.uint8)
    cv2.ellipse(m, (int(round(cx * 4)), int(round(cy * 4))), (int(round(rx * 4)), int(round(ry * 4))), 0, 0, 360, 255, -1, cv2.LINE_AA)
    m = cv2.resize(m, (W, H), interpolation=cv2.INTER_AREA)
    return cv2.GaussianBlur(m, (0, 0), feather)


def layer(image, mask):
    out = image.copy()
    out[:, :, 3] = (image[:, :, 3].astype(np.float32) * mask / 255).astype(np.uint8)
    out[out[:, :, 3] == 0] = 0
    return out


def write(image, path):
    """Windows now and then refuses a file it is still scanning (EINVAL); wait a moment and write it again."""
    import time
    for attempt in range(8):
        try:
            image.save(path, optimize=True); return
        except OSError:
            if attempt == 7: raise
            time.sleep(.4)


def save(array, model, name):
    write(Image.fromarray(array, "RGBA"), CONTROLLERS / model / "native" / name)


# ---------------------------------------------------------------------------------------------------------------
def photographs(model, photos):
    """Step 1: lift both photographs off their backdrop, place them, write the full plates."""
    d = DATA[model]; (CONTROLLERS / model / "native").mkdir(parents=True, exist_ok=True)
    plates = {}
    for view, name in (("Front", "front"), ("Back", "rear")):
        placed = dune.place(cut(str(Path(photos) / f"{d['photo']}{view}.png"), erode=d.get("erode", 2)), HEIGHT)
        save(placed, model, f"{name}-full.png"); save(placed, model, f"{name}-body.png")
        plates[name] = placed
    # the legacy paths (web UI, catalog asset keys) follow the same plates
    for src, dst in ((plates["front"], "controller.png"), (plates["rear"], "controller-rear.png")):
        write(Image.fromarray(src, "RGBA"), CONTROLLERS / model / dst)
    return plates


def silhouette(model):
    """Step 2: trace this model's silhouettes from its new plates into outlines.json and lighting.json."""
    target = CONTROLLERS / "outlines.json"
    data = json.loads(target.read_text(encoding="utf-8"))
    entry = {}
    for view, name in (("front", "front-full.png"), ("back", "rear-full.png")):
        path = outlines.trace(CONTROLLERS / model / "native" / name)
        if path: entry[view] = path
    data["models"][model] = entry
    target.write_text(json.dumps(data, indent=1), encoding="utf-8")
    dune.edit(CONTROLLERS / "lighting.json", 2, lambda lighting: lighting["models"].__setitem__(model, {"front": entry["front"], "back": entry["back"]}))
    return entry


def parts(model):
    """Step 3: caps onto their own layers with a socket left behind; triggers off the rear photograph."""
    d = DATA[model]
    front = np.array(Image.open(CONTROLLERS / model / "native" / "front-full.png"))
    body = front.copy()
    yy, xx = np.mgrid[0:H, 0:W]
    for side, name in (("left", "stick-l.png"), ("right", "stick-r.png")):
        x, y, r = d["sticks"][side]
        save(layer(front, soft_ellipse(x, y, r, r - 2)), model, name)
        dist = np.sqrt((xx - x) ** 2 + (yy - y) ** 2) / (r + 6)
        well = np.clip(30 - 20 * dist, 8, 30)[..., None] * np.array([1.0, 1.0, 1.1])
        k = (soft_ellipse(x, y, r + 4, r + 2) / 255.0)[..., None]
        body[:, :, :3] = (body[:, :, :3] * (1 - k) + well * k).astype(np.uint8)
    save(body, model, "front-body.png")
    # the X20's own web plates reuse the socket body
    if (CONTROLLERS / model / "controller-base.png").exists(): write(Image.fromarray(body, "RGBA"), CONTROLLERS / model / "controller-base.png")

    rear = np.array(Image.open(CONTROLLERS / model / "native" / "rear-full.png"))
    shell = rear.copy()
    for key, name in (("LT", "trigger-l.png"), ("RT", "trigger-r.png")):
        (x, y, rx, ry), _ = d["triggers"][key]
        save(layer(rear, soft_ellipse(x, y, rx, ry)), model, name)
        hole = (soft_ellipse(x, y, rx + 3, ry + 3) > 20).astype(np.uint8) * 255
        filled = cv2.inpaint(np.ascontiguousarray(shell[:, :, :3]), hole, 9, cv2.INPAINT_TELEA)
        shell[:, :, :3] = np.where(hole[..., None] > 0, (filled * .7).astype(np.uint8), shell[:, :, :3])
    save(shell, model, "rear-body.png")
    if (CONTROLLERS / model / "controller-rear-trigger-base.png").exists(): write(Image.fromarray(shell, "RGBA"), CONTROLLERS / model / "controller-rear-trigger-base.png")


def hologram(model, entry):
    for view, name, holo in (("front", "front-full.png", "holo-front.png"), ("back", "rear-full.png", "holo-rear.png")):
        photo = Image.open(CONTROLLERS / model / "native" / name)
        write(art.hologram(photo, photo.getchannel("A"), art.path_points(entry[view])), CONTROLLERS / model / "native" / holo)


def layout(model):
    """Step 4: the data the pages read."""
    d = DATA[model]
    n = lambda x, y: {"x": x / W, "y": y / H}
    # the picture frame the pages fit: the opaque bounds of each plate
    framing = {}
    for view, name in (("front", "front-full.png"), ("back", "rear-full.png")):
        alpha = np.array(Image.open(CONTROLLERS / model / "native" / name))[:, :, 3]
        ys, xs = np.nonzero(alpha > 128)
        framing[view] = [int(xs.min()), int(ys.min()), int(xs.max() - xs.min()), int(ys.max() - ys.min())]

    def shapes(data):
        old = data["models"][model]
        front = {k: dune.circle(*v) for k, v in d["face"].items()} | {k: dune.rounded(*v, r=14) for k, v in d["dpad"].items()}
        back = {}
        for k, v in d["back"].items():
            back[k] = ellipse_shape(*v) if k in ("LT", "RT") else dune.rounded(*v, r=14)
        macros = {k: poly_shape(v) for k, v in d["macros"].items()}
        for section, new in (("front", front), ("back", back), ("macros", macros)):
            missing = set(old.get(section, {})) - set(new)
            if missing: print(f"  ! {model}: no measurement for {section} {sorted(missing)}")
        data["models"][model] = {"front": front, "back": back, "macros": macros}
    dune.edit(CONTROLLERS / "control-shapes.json", 2, shapes)

    def layers(data):
        sticks = {s: {"center": [float(x), float(y)], "travel": round(.75 * r, 1), "capRadius": float(r)} for s, (x, y, r) in d["sticks"].items()}
        hinged = {}
        for key, ((cx, cy, rx, ry), (h1, h2)) in d["triggers"].items():
            hinged[key] = {"hinge": [list(h1), list(h2)], "restAngle": 32, "travelAngle": 28,
                           "bounds": [cx - rx, cy - ry, cx + rx, cy + ry]}
        data["models"][model] = {"sticks": sticks, "triggers": {"LT": hinged["LT"], "RT": hinged["RT"]}, "framing": framing}
    dune.edit(CONTROLLERS / "native-layers.json", 1, layers)

    hap = CONTROLLERS / model / "haptics-geometry.json"
    keys = list(json.loads(hap.read_text(encoding="utf-8")).get("contours", {}))
    contours = {k: dune.ellipse(*d["grips"][k]) for k in keys if k in d["grips"]}
    for k in keys:
        if k not in d["grips"]: print(f"  ! {model}: no haptic contour for {k}")
    hap.write_text(json.dumps({"viewBox": [0, 0, W, H], "contours": contours}, indent=2) + "\n", encoding="utf-8")

    def catalog(data):
        item = next(m for m in data if m["id"] == model); v = item["visual"]
        sticks = {s: {**n(x, y), "radius": r / W, "cap": {**n(x, y), "w": 2 * r / W, "h": 2 * r / H}} for s, (x, y, r) in d["sticks"].items()}
        buttons = dict(v["buttons"])  # the shoulder placeholders stay where the pages already draw them
        for k, (x, y, _) in d["face"].items(): buttons[k] = n(x, y)
        for k, (x, y, w, h) in d["dpad"].items(): buttons[k] = n(x + w / 2, y + h / 2)
        v["sticks"] = sticks; v["buttons"] = buttons
        v["motors"] = [{**m, **n(*({**{k: (g[0], g[1]) for k, g in d["grips"].items()}, **d.get("motor_points", {})}[m["id"]]))} for m in v["motors"]]
        v["rgb"] = [{**n(x, y), "w": .14, "h": .21} for x, y in d["rgb"]]
        if "screen" in d: v["screen"] = d["screen"]
    dune.edit(REPO / "x20ctl" / "controllers" / "catalog.json", 2, catalog)

    if model == "x20":  # the one model with its own rear contour file: labels M1..M4 by the owner's rear numbering
        names = {"M1": "M1", "M2": "M4", "M3": "M2", "M4": "M3"}
        controls = [{"slot": slot, "points": [{"x": round(x / W, 8), "y": round(y / H, 8)} for x, y in d["macros"][names[slot]]]} for slot in ("M1", "M2", "M3", "M4")]
        (CONTROLLERS / "x20" / "rear-geometry.json").write_text(json.dumps({"asset": "controller-rear.png", "aspect": 1.5, "viewLabel": "Back view", "controls": controls}, indent=2) + "\n", encoding="utf-8")


def preview(model, out):
    """Draw every measured shape on the placed plates, for checking by eye."""
    d = DATA[model]
    for view, name in (("front", f"{model}-front.png"), ("back", f"{model}-back.png")):
        im = Image.open(Path(out) / name).convert("RGBA")  # the placed plates written by the measuring step
        bg = Image.new("RGBA", im.size, (70, 80, 100, 255)); bg.alpha_composite(im)
        g = ImageDraw.Draw(bg)
        if view == "front":
            for s, (x, y, r) in d["sticks"].items(): g.ellipse([x - r, y - r, x + r, y + r], outline=(255, 0, 255, 255), width=3)
            for k, (x, y, r) in d["face"].items(): g.ellipse([x - r, y - r, x + r, y + r], outline=(255, 255, 0, 255), width=3); g.text((x - 6, y - 6), k[:2], fill=(255, 255, 0, 255))
            for k, (x, y, w, h) in d["dpad"].items(): g.rectangle([x, y, x + w, y + h], outline=(0, 255, 255, 255), width=3)
            for k, (x, y) in d.get("extra", {}).items(): g.ellipse([x - 20, y - 20, x + 20, y + 20], outline=(0, 255, 0, 255), width=3); g.text((x - 8, y - 6), k[:2], fill=(0, 255, 0, 255))
            for k, (cx, cy, rx, ry) in d["grips"].items(): g.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], outline=(255, 128, 0, 255), width=2)
        else:
            for k, v in d["back"].items():
                if k in ("LT", "RT"): cx, cy, rx, ry = v; g.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], outline=(255, 255, 0, 255), width=3)
                else: x, y, w, h = v; g.rectangle([x, y, x + w, y + h], outline=(0, 255, 255, 255), width=3)
                g.text((v[0] + 4, v[1] + 4), k, fill=(255, 255, 255, 255))
            for k, p in d["macros"].items(): g.line(p + [p[0]], fill=(255, 0, 255, 255), width=3); g.text((p[0][0], p[0][1] + 10), k, fill=(255, 255, 255, 255))
        bg.convert("RGB").save(Path(out) / f"{model}-{view}-measured.png")


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    photos, which = sys.argv[1], sys.argv[2]
    models = list(DATA) if which == "all" else [which]
    for model in models:
        if "--preview" in sys.argv:
            preview(model, sys.argv[sys.argv.index("--preview") + 1] if len(sys.argv) > sys.argv.index("--preview") + 1 else "."); continue
        print("==", model)
        photographs(model, photos)
        entry = silhouette(model)
        parts(model)
        hologram(model, entry)
        layout(model)
        print("  done")


if __name__ == "__main__":
    main()

"""Adds the EasySMX Dune (owner photos, 10 Oct 2026) as an art-and-layout placeholder model "dune".

Step 1 (this file, `cut`): lift each photo off its background (its own alpha when it has one, else a white key) and place it in the 1536x1024 art space at the
same scale the other controllers use. Usage:
    python tools/add_dune_controller.py cut FRONT_IMAGE REAR_IMAGE
    python tools/build_controller_outlines.py
    python tools/add_dune_controller.py layout
    python tools/add_dune_controller.py parts
Writes src/assets/controllers/dune/native/{front-full,front-body,rear-full,rear-body}.png plus empty stick and trigger
layers (the photos have the caps and triggers baked in, so nothing moves separately), and the legacy-path copies
controller.png / controller-rear.png.
"""
import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / "src" / "assets" / "controllers" / "dune"
W, H = 1536, 1024


def cut(path: str) -> np.ndarray:
    """RGBA with the white backdrop keyed out: flood from the border over near-white, then a soft 1.5 px edge."""
    rgba = np.array(Image.open(path).convert("RGBA"))
    if rgba[:, :, 3].min() < 250:  # the photo is already cut out
        return rgba
    rgb = rgba[:, :, :3]
    h, w = rgb.shape[:2]
    light = (rgb.min(axis=2) >= 232) & (rgb.max(axis=2).astype(int) - rgb.min(axis=2) <= 18)
    seeds = np.zeros((h + 2, w + 2), np.uint8)
    region = light.astype(np.uint8)
    _, labels = cv2.connectedComponents(region, connectivity=4)
    border = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))) - {0}
    background = np.isin(labels, list(border)) & light
    mask = (~background).astype(np.uint8) * 255
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
    # keep the largest piece and fill its holes
    count, comp, stats, _ = cv2.connectedComponentsWithStats(mask)
    if count > 1:
        keep = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
        mask = np.where(comp == keep, 255, 0).astype(np.uint8)
    # erode a hair so no white rim survives, then feather
    mask = cv2.erode(mask, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3)))
    alpha = cv2.GaussianBlur(mask, (0, 0), 1.1)
    del seeds
    return np.dstack([rgb, alpha])


def place(rgba: np.ndarray, height: int) -> np.ndarray:
    """Scale to the given controller height and centre it on the 1536x1024 canvas (bottom-weighted like the others)."""
    ys, xs = np.nonzero(rgba[:, :, 3] > 8)
    crop = rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    scale = min(height / crop.shape[0], 1300 / crop.shape[1])
    crop = cv2.resize(crop, (round(crop.shape[1] * scale), round(crop.shape[0] * scale)), interpolation=cv2.INTER_AREA)
    canvas = np.zeros((H, W, 4), np.uint8)
    x = (W - crop.shape[1]) // 2
    y = (H - crop.shape[0]) // 2 + 20
    canvas[y:y + crop.shape[0], x:x + crop.shape[1]] = crop
    return canvas


def save(array: np.ndarray, name: str) -> None:
    Image.fromarray(array, "RGBA").save(ROOT / "native" / name, optimize=True)


def main() -> None:
    if len(sys.argv) != 4 or sys.argv[1] != "cut":
        sys.exit(__doc__)
    (ROOT / "native").mkdir(parents=True, exist_ok=True)
    front = place(cut(sys.argv[2]), 800)
    rear = place(cut(sys.argv[3]), 800)
    for name, art in (("front-full.png", front), ("front-body.png", front), ("rear-full.png", rear), ("rear-body.png", rear)):
        save(art, name)
    empty = np.zeros((H, W, 4), np.uint8)
    for name in ("stick-l.png", "stick-r.png", "trigger-l.png", "trigger-r.png"):
        save(empty, name)
    Image.fromarray(front, "RGBA").save(ROOT / "controller.png", optimize=True)
    Image.fromarray(rear, "RGBA").save(ROOT / "controller-rear.png", optimize=True)
    for label, art in (("front", front), ("rear", rear)):
        ys, xs = np.nonzero(art[:, :, 3] > 128)
        print(label, "bounds", xs.min(), ys.min(), xs.max(), ys.max())


# ----- step 2, `layout`: the data every page reads, measured on the placed art (1536x1024) -----
REPO = Path(__file__).resolve().parents[1]
FRONT = {  # centre x, y and radius of each round control, read off the placed front photo
    "Y": (1022, 245, 31), "X": (962, 295, 31), "B": (1080, 295, 31), "A": (1022, 347, 31),
    "SELECT": (655, 190, 19), "START": (880, 190, 19),
}
DPAD = {"DPAD_UP": (603, 362, 48, 46), "DPAD_DOWN": (603, 452, 48, 46), "DPAD_LEFT": (555, 406, 46, 48), "DPAD_RIGHT": (653, 406, 46, 48)}
STICKS = {"left": (500, 284, 57), "right": (900, 430, 57)}  # the cap heads, measured on the photo
REAR = {"RB": (368, 140, 137, 58), "RT": (333, 163, 167, 104), "LB": (1031, 140, 137, 58), "LT": (1036, 163, 167, 104)}
# M3/M4 are labelled on the grips; M1/M2 are the two pieces either side of the top centre block (owner, 10 Oct 2026)
PADDLES = {"M4": (445, 480, 100, 155), "M3": (990, 480, 100, 155), "M2": (500, 133, 117, 54), "M1": (920, 133, 117, 54)}
TRIGGERS = {"RT": ((418, 215, 86, 54), ((340, 172), (500, 165))), "LT": ((1118, 215, 86, 54), ((1196, 172), (1036, 165)))}  # ellipse, hinge on its top edge
GRIPS = {"left_grip": (345, 578, 100, 188), "right_grip": (1191, 578, 100, 188), "left_trigger": (485, 150, 70, 22), "right_trigger": (1050, 150, 70, 22)}


def circle(cx, cy, r):
    return {"bounds": [cx - r, cy - r, 2 * r, 2 * r], "path": f"M{cx - r} {cy} A{r} {r} 0 1 0 {cx + r} {cy} A{r} {r} 0 1 0 {cx - r} {cy} Z"}


def rounded(x, y, w, h, r=14):
    return {"bounds": [x, y, w, h], "path": f"M{x + r} {y} L{x + w - r} {y} Q{x + w} {y} {x + w} {y + r} L{x + w} {y + h - r} Q{x + w} {y + h} {x + w - r} {y + h} L{x + r} {y + h} Q{x} {y + h} {x} {y + h - r} L{x} {y + r} Q{x} {y} {x + r} {y} Z"}


def ellipse(cx, cy, rx, ry):
    return f"M{cx - rx} {cy} A{rx} {ry} 0 1 0 {cx + rx} {cy} A{rx} {ry} 0 1 0 {cx - rx} {cy} Z"


def edit(path: Path, indent: int, change) -> None:
    """Load, change and write back a JSON file in its own format (indent, CRLF), so the diff is only the Dune."""
    raw = path.read_bytes().decode("utf-8")
    data = json.loads(raw)
    change(data)
    text = json.dumps(data, indent=indent, ensure_ascii=False)
    if "\r\n" in raw:
        text = text.replace("\n", "\r\n")
    path.write_bytes((text + ("\r\n" if raw.endswith("\r\n") else "\n" if raw.endswith("\n") else "")).encode("utf-8"))


def layout() -> None:
    controllers = REPO / "src" / "assets" / "controllers"
    n = lambda x, y: {"x": x / W, "y": y / H}

    def shapes(data):
        front = {k: circle(*v) for k, v in FRONT.items()} | {k: rounded(*v) for k, v in DPAD.items()}
        data["models"]["dune"] = {"front": front, "back": {k: rounded(*v, r=20) for k, v in REAR.items()}, "macros": {k: rounded(*v, r=26) for k, v in PADDLES.items()}}
    edit(controllers / "control-shapes.json", 2, shapes)

    def layers(data):
        sticks = {side: {"center": [float(x), float(y)], "travel": 40.0, "capRadius": float(r)} for side, (x, y, r) in STICKS.items()}
        hinged = lambda key: {"hinge": [list(TRIGGERS[key][1][0]), list(TRIGGERS[key][1][1])], "restAngle": 32, "travelAngle": 28,
                              "bounds": [TRIGGERS[key][0][0] - TRIGGERS[key][0][2], TRIGGERS[key][0][1] - TRIGGERS[key][0][3], TRIGGERS[key][0][0] + TRIGGERS[key][0][2], TRIGGERS[key][0][1] + TRIGGERS[key][0][3]]}
        data["models"]["dune"] = {"sticks": sticks, "triggers": {"LT": hinged("LT"), "RT": hinged("RT")}, "framing": {"front": [221, 113, 1092, 838], "back": [136, 112, 1264, 838]}}
    edit(controllers / "native-layers.json", 1, layers)

    haptics = {"viewBox": [0, 0, W, H], "contours": {k: ellipse(*v) for k, v in GRIPS.items()}}
    (ROOT / "haptics-geometry.json").write_text(json.dumps(haptics, indent=2) + "\n", encoding="utf-8")

    outlines = json.loads((controllers / "outlines.json").read_text(encoding="utf-8"))["models"]["dune"]
    # hologram line art, made the same way as every other model's
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import build_native_controller_layers as art
    for view, name, holo in (("front", "front-full.png", "holo-front.png"), ("back", "rear-full.png", "holo-rear.png")):
        photo = Image.open(ROOT / "native" / name)
        art.hologram(photo, photo.getchannel("A"), art.path_points(outlines[view])).save(ROOT / "native" / holo, optimize=True)
    edit(controllers / "lighting.json", 2, lambda data: data["models"].__setitem__("dune", {"front": outlines["front"], "back": outlines["back"]}))

    def catalog(data):
        data[:] = [m for m in data if m["id"] != "dune"]
        buttons = {k: n(x, y) for k, (x, y, _) in FRONT.items()} | {k: n(x + w / 2, y + h / 2) for k, (x, y, w, h) in DPAD.items()}
        buttons |= {"LB": n(470, 150), "RB": n(1066, 150), "LT": n(470, 128), "RT": n(1066, 128)}
        sticks = {side: {**n(x, y), "radius": r / W, "cap": {**n(x, y), "w": 2 * r / W, "h": 2 * r / H}} for side, (x, y, r) in STICKS.items()}
        data.append({
            "id": "dune", "name": "Dune", "macroSlots": ["M1", "M2", "M3", "M4"], "backend": None, "visible": True, "placeholder": True,
            "softwareControl": "unverified", "macroPlacement": "rear",
            "sources": ["Owner product photos, 10 Oct 2026", "Owner research notes, 10 Oct 2026"],
            "hardware": {"rgb": True, "vibration": True, "triggerHaptics": True, "motion": None, "display": True, "sticks": "TMR", "triggers": None},
            "visual": {"asset": "dune/controller.png", "baseAsset": "dune/controller.png", "aspect": 1.5, "viewLabel": "Front view", "sticks": sticks, "buttons": buttons,
                       "motors": [{"id": k, **n(x, y), "kind": "grip" if "grip" in k else "trigger"} for k, (x, y, _, _) in GRIPS.items()], "rgb": [], "screen": None},
            "availability": "preview",
            "known": ["Owner photos: front in its charging dock, rear with M3/M4 on the grips and M1/M2 beside the top centre block.", "Research notes: TMR sticks, 8000 Hz (8K edition), 4 programmable buttons, grip and trigger vibration, 1.3 inch LCD."],
            "missing": ["USB and receiver identities.", "Any configuration protocol."],
        })
    edit(REPO / "x20ctl" / "controllers" / "catalog.json", 2, catalog)
    print("dune layout written")

def parts() -> None:
    """Moving parts, like every other model (owner, 10 Oct 2026): each stick cap is lifted onto its own layer and the
    body gets a dark socket where it sat; each trigger is lifted off the rear photo onto its own layer, and the shell
    behind it is filled in so a pressed (foreshortened) trigger never shows a hole."""
    def soft_ellipse(cx, cy, rx, ry, feather=1.2):
        m = np.zeros((H * 4, W * 4), np.uint8)
        cv2.ellipse(m, (cx * 4, cy * 4), (rx * 4, ry * 4), 0, 0, 360, 255, -1, cv2.LINE_AA)
        m = cv2.resize(m, (W, H), interpolation=cv2.INTER_AREA)
        return cv2.GaussianBlur(m, (0, 0), feather)

    def layer(art, mask):
        out = art.copy(); out[:, :, 3] = (art[:, :, 3].astype(np.float32) * mask / 255).astype(np.uint8)
        out[out[:, :, 3] == 0] = 0
        return out

    front = np.array(Image.open(ROOT / "native" / "front-full.png"))
    body = front.copy()
    for side, name in (("left", "stick-l.png"), ("right", "stick-r.png")):
        x, y, r = STICKS[side]
        cap = soft_ellipse(x, y, r, r - 2)
        save(layer(front, cap), name)
        # the socket: a dark well, a little lighter in the middle, so a tilted cap shows depth instead of a hole
        yy, xx = np.mgrid[0:H, 0:W]; d = np.sqrt((xx - x) ** 2 + (yy - y) ** 2) / (r + 6)
        well = np.clip(30 - 20 * d, 8, 30)[..., None] * np.array([1.0, 1.0, 1.1])
        k = (soft_ellipse(x, y, r + 4, r + 2) / 255.0)[..., None]
        body[:, :, :3] = (body[:, :, :3] * (1 - k) + well * k).astype(np.uint8)
    save(body, "front-body.png")

    rear = np.array(Image.open(ROOT / "native" / "rear-full.png"))
    shell = rear.copy()
    for key, name in (("LT", "trigger-l.png"), ("RT", "trigger-r.png")):
        (x, y, rx, ry), _ = TRIGGERS[key]
        piece = soft_ellipse(x, y, rx, ry)
        save(layer(rear, piece), name)
        hole = (soft_ellipse(x, y, rx + 3, ry + 3) > 20).astype(np.uint8) * 255
        filled = cv2.inpaint(np.ascontiguousarray(shell[:, :, :3]), hole, 9, cv2.INPAINT_TELEA)
        shell[:, :, :3] = np.where(hole[..., None] > 0, (filled * .7).astype(np.uint8), shell[:, :, :3])  # a shade darker: it sits behind
    save(shell, "rear-body.png")
    print("dune parts written")


if __name__ == "__main__":
    if len(sys.argv) == 2 and sys.argv[1] == "layout":
        layout()
    elif len(sys.argv) == 2 and sys.argv[1] == "parts":
        parts()
    else:
        main()

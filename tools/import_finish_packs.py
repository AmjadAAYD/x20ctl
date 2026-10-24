"""Places the owner's controller finish packs (24 Oct 2026) onto the app's 1536 x 1024 art canvas.

Each pack (controller-animation-layers.zip, one folder per finish) holds front-full, front-no-sticks, stick-l, stick-r,
rear-full, rear-no-triggers, trigger-rt and trigger-lt on its own canvas. Every view is fitted onto the model's existing
art with a scale and an offset per view (no stretch): the front by the pack's stick pivots onto the ones measured in
native-layers.json, the rear by its silhouette. The button shapes, vibration contours, stick pivots and trigger hinges
already measured for that model stay where they are; the rim outlines are retraced from the new originals. The rear's screen-left trigger is RT, which the app calls trigger-r.

    python tools/import_finish_packs.py PACKS_DIR [--x20pro-white PAIR.png] [--report out.json]
    python tools/import_finish_packs.py --tidy-only    # re-tidy the colours on disk and retrace their rims

The original finish of each model replaces native/; the other finishes go to <model>/finishes/<id>/. The fit is checked
by silhouette overlap and by where each pack's stick pivots land against native-layers.json; both go in the report.
Needs numpy, Pillow and OpenCV.
"""
import argparse
import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_roster_art as roster  # silhouette() retraces outlines.json and lighting.json
from build_roster_art import write  # retries a save Windows briefly refuses

REPO = Path(__file__).resolve().parents[1]
CONTROLLERS = REPO / "src" / "assets" / "controllers"
W, H = 1536, 1024

# pack -> (model, finish id, name shown in the app, swatch). The first finish of each model is its original.
PACKS = [
    ("x20-original", "x20", "original", "White", "#e9eaee"),
    ("x20-black", "x20", "black", "Black", "#202228"),
    ("x05_pro-original", "x05_pro", "original", "Black", "#1f2024"),
    ("x05pro-white", "x05_pro", "white", "White", "#e6e8ee"),
    ("x05pro-purple", "x05_pro", "purple", "Aurora Purple", "#4a3fa0"),
    ("x10-original", "x10", "original", "Silver and purple", "#aeb6c4"),
    ("x10-silver", "x10", "silver", "Silver and black", "#c9cbd0"),
    ("d10-original", "d10", "original", "Space Black", "#26272b"),
    ("d10-purple", "d10", "purple", "Farout Purple", "#b48ce6"),
    ("d10-green", "d10", "green", "Wirtanen Green", "#9db33c"),
    ("x15-original", "x15", "original", "Mecha", "#c9b6d8"),
    ("x15-starfield", "x15", "starfield", "Starfield", "#f1f1f1"),
    ("x15-wukong", "x15", "wukong", "Wukong", "#d9cfb8"),
    ("dune-original", "dune", "original", "Gold", "#e0aa18"),
    ("dune-black", "dune", "black", "Black", "#1c1c20"),
]
# X20 Pro keeps its current art as the original finish; white comes from a front + rear product pair, not a pack.
KEEP = {"x20_pro": ("original", "Grey", "#4a4d54")}
# the colour a model is shown in until its player picks one (owner, 24 Oct 2026: the Dune shows black)
DEFAULT = {"dune": "black"}
WHITE_PRO = ("x20_pro", "white", "White", "#e4e6ea")
LAYERS = {"front": [("front-full", "front-full"), ("front-no-sticks", "front-body"), ("stick-l", "stick-l"), ("stick-r", "stick-r")],
          "rear": [("rear-full", "rear-full"), ("rear-no-triggers", "rear-body"), ("trigger-rt", "trigger-r"), ("trigger-lt", "trigger-l")]}


def rgba(path):
    return np.array(Image.open(path).convert("RGBA"))


def matrix(s, tx, ty):
    return np.array([[s, 0, tx], [0, s, ty]], np.float32)


def iou(source_alpha, target_alpha, m):
    placed = cv2.warpAffine(source_alpha, m, (W, H), flags=cv2.INTER_LINEAR) > 128
    goal = target_alpha > 128
    return float((placed & goal).sum() / (placed | goal).sum())


def pivot_fit(pivots, centres):
    """Scale and offset (no rotation, no stretch) that put the pack's stick pivots on the model's measured ones."""
    p = np.array(pivots, float); q = np.array(centres, float)
    pc, qc = p - p.mean(0), q - q.mean(0)
    s = (pc * qc).sum() / (pc * pc).sum(); t = q.mean(0) - s * p.mean(0)
    return matrix(s, t[0], t[1])


def silhouette_fit(source_alpha, target_alpha, scale=None):
    """Scale and offset that overlay two silhouettes best, by searching on quarter-size masks and refining.
    With scale given, only the offset is searched (a rear half shares its front half's scale)."""
    def box(a):
        ys, xs = np.nonzero(a > 128); return xs.min(), ys.min(), xs.max(), ys.max()
    sx0, sy0, sx1, sy1 = box(source_alpha); tx0, ty0, tx1, ty1 = box(target_alpha)
    s0 = scale or ((tx1 - tx0) / (sx1 - sx0) + (ty1 - ty0) / (sy1 - sy0)) / 2
    q = .25
    src = cv2.resize(source_alpha, None, fx=q, fy=q, interpolation=cv2.INTER_AREA)
    goal = cv2.resize(target_alpha, (int(W * q), int(H * q)), interpolation=cv2.INTER_AREA) > 128
    def score(s, tx, ty):
        placed = cv2.warpAffine(src, matrix(s, tx * q, ty * q), (goal.shape[1], goal.shape[0]), flags=cv2.INTER_LINEAR) > 128
        return (placed & goal).sum() / max(1, (placed | goal).sum())
    best = (s0, (tx0 + tx1) / 2 - s0 * (sx0 + sx1) / 2, (ty0 + ty1) / 2 - s0 * (sy0 + sy1) / 2)
    best_score = score(*best)
    for ds, dt in ((.02, 16), (.006, 4), (.002, 1)):
        improved = True
        while improved:
            improved = False
            for s in ((best[0],) if scale else (best[0] - ds, best[0], best[0] + ds)):
                for dx in (-dt, 0, dt):
                    for dy in (-dt, 0, dt):
                        cand = (s, best[1] + dx, best[2] + dy); v = score(*cand)
                        if v > best_score + 1e-6: best, best_score, improved = cand, v, True
    return matrix(*best)


def place(image, m):
    """Premultiplied resample so transparent pixels never bleed a colour fringe into the edge."""
    a = image[:, :, 3:4].astype(np.float32) / 255
    pre = np.concatenate([image[:, :, :3].astype(np.float32) * a, a * 255], 2)
    out = cv2.warpAffine(pre, m, (W, H), flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_CONSTANT, borderValue=0)
    out = np.clip(out, 0, 255); alpha = out[:, :, 3:4] / 255
    rgb = np.where(alpha > 1e-3, out[:, :, :3] / np.maximum(alpha, 1e-3), 0)
    result = np.concatenate([np.clip(rgb, 0, 255), out[:, :, 3:4]], 2).round().astype(np.uint8)
    result[result[:, :, 3] == 0] = 0
    return result


def point(m, p):
    return [float(m[0, 0] * p[0] + m[0, 1] * p[1] + m[0, 2]), float(m[1, 0] * p[0] + m[1, 1] * p[1] + m[1, 2])]


def save(array, folder, name):
    folder.mkdir(parents=True, exist_ok=True); write(Image.fromarray(array, "RGBA"), folder / f"{name}.png")


def import_pack(pack_dir, model, finish, report):
    """Every finish's front is pinned by its stick pivots to the model's measured ones, so each control shape stays on its
    control. Its rear is fitted to the rear silhouette: the app's for an original, the new original's for any other."""
    folder = CONTROLLERS / model / ("native" if finish == "original" else f"finishes/{finish}")
    anchors = json.loads((pack_dir / "anchors.json").read_text(encoding="utf-8"))
    sticks = {s["layer"]: (s["pivot"]["x"], s["pivot"]["y"]) for s in anchors["sticks"]}
    centres = LAYOUT[model]["sticks"]
    front_alpha = rgba(pack_dir / "front-full.png")[:, :, 3]; rear_alpha = rgba(pack_dir / "rear-full.png")[:, :, 3]
    # every finish is pinned by its own stick pivots: the sticks, and the controls between them, are what must line up
    front = pivot_fit([sticks["stick-l"], sticks["stick-r"]], [centres["left"]["center"], centres["right"]["center"]])
    rear = silhouette_fit(rear_alpha, TARGETS[model]["rear"])
    entry = {}
    for view, m in (("front", front), ("rear", rear)):
        for source, name in LAYERS[view]: save(place(rgba(pack_dir / f"{source}.png"), m), folder, name)
        entry[view] = {"iou": round(iou(front_alpha if view == "front" else rear_alpha, TARGETS[model][view], m), 4),
                       "matrix": [[round(float(v), 4) for v in row] for row in m]}
    entry["pivots"] = {k: [round(v, 1) for v in point(front, p)] for k, p in sticks.items()}
    entry["pivotError"] = round(max(float(np.hypot(*(np.array(point(front, sticks[k])) - centres[side]["center"])))
                                    for k, side in (("stick-l", "left"), ("stick-r", "right"))), 1)
    report[f"{model}/{finish}"] = entry
    if finish == "original":
        # the finishes of this model are fitted onto this one
        TARGETS[model] = {v: rgba(folder / f"{v}-full.png")[:, :, 3] for v in ("front", "rear")}


def feature_fit(source, target):
    """Scale and offset from matching details (SIFT on grey, RANSAC): two colours of one product shot share every edge
    and label, while their silhouettes need not match exactly. Falls back to the silhouette fit with too few matches."""
    g1 = cv2.cvtColor(source[:, :, :3], cv2.COLOR_RGB2GRAY); g2 = cv2.cvtColor(target[:, :, :3], cv2.COLOR_RGB2GRAY)
    sift = cv2.SIFT_create(4000)
    k1, d1 = sift.detectAndCompute(g1, (source[:, :, 3] > 200).astype(np.uint8)); k2, d2 = sift.detectAndCompute(g2, (target[:, :, 3] > 200).astype(np.uint8))
    good = [a for a, b in cv2.BFMatcher().knnMatch(d1, d2, k=2) if a.distance < .8 * b.distance]
    if len(good) >= 12:
        m, inliers = cv2.estimateAffinePartial2D(np.float32([k1[a.queryIdx].pt for a in good]), np.float32([k2[a.trainIdx].pt for a in good]),
                                                 method=cv2.RANSAC, ransacReprojThreshold=4)
        if m is not None and inliers.sum() >= 10:
            s = float(np.hypot(m[0, 0], m[1, 0]))  # drop the stray rotation the match carries (under 0.3 degrees)
            return matrix(s, m[0, 2], m[1, 2]), int(inliers.sum())
    return silhouette_fit(source[:, :, 3], target[:, :, 3]), 0


def cut_white(image):
    """Lifts a product shot off a plain white backdrop: everything connected to the border that is near-white goes."""
    rgb = image[:, :, :3].astype(np.int16)
    near = (np.abs(rgb - 255).max(2) < 14).astype(np.uint8)
    flood = near.copy(); mask = np.zeros((flood.shape[0] + 2, flood.shape[1] + 2), np.uint8)
    for x, y in ((0, 0), (flood.shape[1] - 1, 0), (0, flood.shape[0] - 1), (flood.shape[1] - 1, flood.shape[0] - 1)):
        if flood[y, x] == 1: cv2.floodFill(flood, mask, (x, y), 2)
    body = (flood != 2).astype(np.uint8) * 255
    body = cv2.morphologyEx(body, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    count, labels, stats, _ = cv2.connectedComponentsWithStats(body)
    keep = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA])); body = np.where(labels == keep, 255, 0).astype(np.uint8)
    body = cv2.GaussianBlur(cv2.erode(body, np.ones((3, 3), np.uint8)), (0, 0), .8)
    out = image.copy(); out[:, :, 3] = body; return out


def tidy(model, finish, reach=9):
    """Drops what a neighbouring picture left glued to the shell. Any colour loses long thin strips that an opening
    (a disc of radius reach) parts from the body, like the sliver beside the X05 Pro purple rear; real details are short
    and stay. The white X20 Pro is the black one's shell, so it is also held inside the black's outline, which takes off
    its own front grip that stood beside the rear in the source picture. A rear shell is mirror-symmetric, so a solid
    piece with no twin across the middle goes too (what of that grip lay inside the black's outline, the purple tab).
    Thin unmatched edges are the shell's own slight asymmetry and stay."""
    folder = CONTROLLERS / model / "finishes" / finish
    removed = {}
    for view in ("front", "rear"):
        alpha = rgba(folder / f"{view}-full.png")[:, :, 3]; solid = (alpha > 40).astype(np.uint8)
        opened = cv2.morphologyEx(solid, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * reach + 1, 2 * reach + 1)))
        count, labels, stats, _ = cv2.connectedComponentsWithStats(opened)
        body = (labels == 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))).astype(np.uint8)
        rest = solid & ~cv2.dilate(body, np.ones((5, 5), np.uint8))
        count, labels, stats, _ = cv2.connectedComponentsWithStats(rest)
        strips = [i for i in range(1, count) if max(stats[i, 2], stats[i, 3]) >= 80 and stats[i, 4] >= 300]
        drop = cv2.dilate(np.isin(labels, strips).astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
        drop &= ~(body > 0)
        if view == "rear":
            drop |= unmirrored(solid)
        if (model, finish) == WHITE_PRO[:2]:
            drop |= cv2.dilate((TARGETS[model][view] > 0).astype(np.uint8), np.ones((5, 5), np.uint8)) == 0
        drop &= alpha > 0
        if drop.sum() < 50: continue
        removed[view] = int(drop.sum())
        for name in [f"{view}-full", f"{view}-body"] + list(("stick-l", "stick-r") if view == "front" else ("trigger-l", "trigger-r")):
            layer = rgba(folder / f"{name}.png"); layer[drop] = 0; save(layer, folder, name)
    return removed


def unmirrored(solid):
    """Solid pieces with nothing opposite them across the shell's middle, found from the upper third, which no stray reaches."""
    rows = np.nonzero(solid.any(1))[0]; top = solid[rows.min():rows.min() + (rows.max() - rows.min()) // 3]
    xs = np.nonzero(top.any(0))[0]; axis = (xs.min() + xs.max()) / 2
    source = np.arange(solid.shape[1]); target = np.round(2 * axis - source).astype(int); inside = (target >= 0) & (target < solid.shape[1])
    mirrored = np.zeros_like(solid); mirrored[:, target[inside]] = solid[:, source[inside]]
    lone = (solid & (cv2.dilate(mirrored, np.ones((9, 9), np.uint8)) == 0)).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(lone)
    pieces = [i for i in range(1, count) if stats[i, 2] >= 12 and stats[i, 4] >= 300 and stats[i, 4] >= .25 * stats[i, 2] * stats[i, 3]]
    return cv2.dilate(np.isin(labels, pieces).astype(np.uint8), np.ones((5, 5), np.uint8)) > 0


def import_white_pro(pair_path, report):
    """The white X20 Pro is one picture holding the front (left half) and the rear (right half). Each half is lifted off
    its backdrop and fitted onto the black X20 Pro by their shared details. Its caps and triggers are cut along the black
    finish's own layers; their sockets are darkened like the packs'."""
    model, finish = WHITE_PRO[0], WHITE_PRO[1]
    pair = rgba(pair_path); half = pair.shape[1] // 2
    folder = CONTROLLERS / model / "finishes" / finish
    native = CONTROLLERS / model / "native"
    entry = {}
    for view, crop in (("front", pair[:, :half]), ("rear", pair[:, half:])):
        cut = cut_white(np.ascontiguousarray(crop))
        target = TARGETS[model][view]
        m, matches = feature_fit(cut, rgba(native / f"{'front' if view == 'front' else 'rear'}-full.png"))
        full = place(cut, m)
        if view == "front":
            # the two views touch in the source picture, so the front's right grip is cut at the split. The shell is
            # mirror-symmetric there: that strip is rebuilt from the white's own left grip, inside the black's outline.
            edge = int(m[0, 0] * half + m[0, 2]) - 44
            black = rgba(native / "front-full.png"); xs = np.nonzero(black[:, :, 3].max(0) > 128)[0]; axis = (xs.min() + xs.max()) / 2
            mirrored = full[:, ::-1]; shift = int(round(2 * axis - (W - 1)))
            mirrored = np.roll(mirrored, shift, axis=1)
            k = np.clip((np.arange(W) - edge) / 24, 0, 1)[None, :, None]
            rebuilt = mirrored.copy(); rebuilt[:, :, 3] = np.minimum(mirrored[:, :, 3], black[:, :, 3])
            full = (full * (1 - k) + rebuilt * k).astype(np.uint8); full[full[:, :, 3] == 0] = 0
            entry["rebuiltFromX"] = edge
        save(full, folder, f"{view}-full")
        parts = ("stick-l", "stick-r") if view == "front" else ("trigger-l", "trigger-r")
        body = full.copy()
        for part in parts:
            cap = np.minimum(rgba(native / f"{part}.png")[:, :, 3], full[:, :, 3])
            piece = full.copy(); piece[:, :, 3] = cap; piece[cap == 0] = 0
            save(piece, folder, part)
            # a socket under the moving piece: the shell darkened toward its centre, as in every pack
            k = cv2.GaussianBlur(cv2.dilate(cap, np.ones((9, 9), np.uint8)).astype(np.float32) / 255, (0, 0), 3)[..., None]
            body[:, :, :3] = (body[:, :, :3] * (1 - k * .82)).astype(np.uint8)
        save(body, folder, f"{view}-body")
        entry[view] = {"iou": round(iou(cut[:, :, 3], target, m), 4), "matches": matches, "matrix": [[round(float(v), 4) for v in row] for row in m]}
    report[f"{model}/{finish}"] = entry


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("packs", type=Path, nargs="?")
    parser.add_argument("--x20pro-white", type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--tidy-only", action="store_true", help="only tidy the colours already imported and retrace their rims")
    args = parser.parse_args()
    global TARGETS, LAYOUT
    LAYOUT = json.loads((CONTROLLERS / "native-layers.json").read_text(encoding="utf-8"))["models"]
    models = sorted({p[1] for p in PACKS} | set(KEEP))
    TARGETS = {m: {"front": rgba(CONTROLLERS / m / "native" / "front-full.png")[:, :, 3], "rear": rgba(CONTROLLERS / m / "native" / "rear-full.png")[:, :, 3]} for m in models}
    report = {}
    # with --tidy-only the colours already on disk are kept, the white X20 Pro among them
    white = bool(args.x20pro_white) or (args.tidy_only and (CONTROLLERS / WHITE_PRO[0] / "finishes" / WHITE_PRO[1]).is_dir())
    for pack, model, finish, _, _ in ([] if args.tidy_only else PACKS):
        print(pack, "->", model, finish, flush=True); import_pack(args.packs / pack, model, finish, report)
    # the rims, clips and 3D walls follow the new originals (the old Dune silhouette still held its charging dock)
    if not args.tidy_only:
        for model in sorted({p[1] for p in PACKS}): roster.silhouette(model)
        if args.x20pro_white:
            print("x20pro white", flush=True); import_white_pro(args.x20pro_white, report)
    extra = [(m, f) for _, m, f, _, _ in PACKS if f != "original"] + ([WHITE_PRO[:2]] if white else [])
    for model, finish in extra:
        removed = tidy(model, finish)
        if removed: print("tidied", f"{model}/{finish}", removed, flush=True); report.setdefault(f"{model}/{finish}", {})["tidied"] = removed
    # a colour's shell can be cut a little differently from its original: each gets its own rim, keyed "model/colour"
    outlines_file = CONTROLLERS / "outlines.json"; data = json.loads(outlines_file.read_text(encoding="utf-8"))
    for model, finish in extra:
        folder = CONTROLLERS / model / "finishes" / finish
        data["models"][f"{model}/{finish}"] = {"front": roster.outlines.trace(folder / "front-full.png"), "back": roster.outlines.trace(folder / "rear-full.png")}
    outlines_file.write_text(json.dumps(data, indent=1), encoding="utf-8")
    # the finish list the apps read: id, name, swatch; the original first
    finishes = {}
    for model in models:
        rows = [{"id": f, "name": n, "swatch": s} for _, m, f, n, s in PACKS if m == model]
        if model in KEEP: rows.insert(0, dict(zip(("id", "name", "swatch"), KEEP[model])))
        if model == WHITE_PRO[0] and white: rows.append(dict(zip(("id", "name", "swatch"), WHITE_PRO[1:])))
        finishes[model] = rows
    (CONTROLLERS / "finishes.json").write_text(json.dumps({"models": finishes, "default": DEFAULT}, indent=1) + "\n", encoding="utf-8")
    if args.report: args.report.write_text(json.dumps(report, indent=1), encoding="utf-8")
    for key, entry in report.items():
        if "front" in entry: print(key, entry["front"]["iou"], entry["rear"]["iou"], entry.get("pivotError", ""))


TARGETS, LAYOUT = {}, {}
if __name__ == "__main__":
    main()

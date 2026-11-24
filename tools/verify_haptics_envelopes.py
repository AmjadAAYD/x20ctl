"""Held-out envelope verifier for X20Ctl HapticsModelWeights.json.

Runs fresh unseen synthetic test scenarios through the exact 28-feature FFT pipeline
and the exported JSON weights to verify false-positive suppression, silence gating,
and accurate multi-head separation across music and game SFX scenarios.
"""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
from typing import Dict, Tuple

import numpy as np

from train_haptics_model import (
    BitExactSpectrumAnalyzer,
    FRAME_HOP,
    HEAD_NAMES,
    _synth_scenario,
)


def infer_json(weights: dict, features: np.ndarray) -> np.ndarray:
    w1 = np.asarray(weights["w1"], dtype=np.float64)  # (H, F)
    b1 = np.asarray(weights["b1"], dtype=np.float64)  # (H,)
    w2 = np.asarray(weights["w2"], dtype=np.float64)  # (O, H)
    b2 = np.asarray(weights["b2"], dtype=np.float64)  # (O,)

    # Force silence floor matching C# classifier
    if features[8] < 0.015:
        return np.zeros(len(HEAD_NAMES), dtype=np.float64)

    h = np.maximum(0.0, w1 @ features + b1)
    logits = w2 @ h + b2
    return 1.0 / (1.0 + np.exp(-np.clip(logits, -40.0, 40.0)))


def eval_scenario(weights: dict, kind: str, seed: int) -> Dict[str, Tuple[float, float]]:
    rng = np.random.default_rng(seed)
    preds = []
    for _ in range(3):
        left, right, _ = _synth_scenario(rng, kind, duration_sec=3.0)
        analyzer = BitExactSpectrumAnalyzer()
        n_frames = len(left) // FRAME_HOP
        for fi in range(n_frames):
            i0 = fi * FRAME_HOP
            feat = analyzer.process_chunk(left[i0 : i0 + FRAME_HOP], right[i0 : i0 + FRAME_HOP])
            if fi >= 4:
                preds.append(infer_json(weights, feat))
    arr = np.stack(preds, axis=0)
    return {name: (float(arr[:, idx].mean()), float(arr[:, idx].max())) for idx, name in enumerate(HEAD_NAMES)}


def verify_real_audio(weights: dict, data_dir: Path) -> int:
    cache_path = data_dir / "features_cache_v1.npz"
    if not cache_path.exists():
        print(f"[SKIP] Real-audio cache not found at {cache_path}")
        return 0

    c = np.load(cache_path, allow_pickle=False)
    mus_x = c["mus_val_x"]
    mus_y = c["mus_val_y"]
    mus_tags = c["mus_val_tags"]
    esc_x = c["esc_val_x"]
    esc_roles = c["esc_val_roles"]

    w1 = np.asarray(weights["w1"], dtype=np.float64)
    b1 = np.asarray(weights["b1"], dtype=np.float64)
    w2 = np.asarray(weights["w2"], dtype=np.float64)
    b2 = np.asarray(weights["b2"], dtype=np.float64)

    def infer_batch(x: np.ndarray) -> np.ndarray:
        h = np.maximum(0.0, x.astype(np.float64) @ w1.T + b1)
        logits = h @ w2.T + b2
        out = 1.0 / (1.0 + np.exp(-np.clip(logits, -40.0, 40.0)))
        out[x[:, 8] < 0.015] = 0.0
        return out

    mus_p = infer_batch(mus_x)
    esc_p = infer_batch(esc_x)

    inst_mask = mus_tags == 1
    vox_act_mask = (mus_tags == 2) & (mus_y[:, 0] > 0.50)
    full_bass_mask = (mus_tags == 0) & (mus_y[:, 1] > 0.50)
    esc_neg_mask = esc_roles == "neg"
    esc_eng_mask = (esc_roles == "engine") & (esc_x[:, 8] > 0.15)
    esc_gravel_mask = (esc_roles == "gravel") & (esc_x[:, 8] > 0.15)

    real_checks = [
        (float(mus_p[inst_mask, 0].mean()) < 0.12, f"Real MUSDB18 instrumental vocal mean={mus_p[inst_mask, 0].mean():.3f} (< 0.12)"),
        (float(mus_p[vox_act_mask, 0].mean()) > 0.55, f"Real MUSDB18 active vocal mean={mus_p[vox_act_mask, 0].mean():.3f} (> 0.55)"),
        (float(mus_p[full_bass_mask, 1].mean()) > 0.55, f"Real MUSDB18 active bass mean={mus_p[full_bass_mask, 1].mean():.3f} (> 0.55)"),
        (float(esc_p[esc_neg_mask, 0].mean()) < 0.06, f"Real ESC-50 Fold-5 hard-negatives vocal mean={esc_p[esc_neg_mask, 0].mean():.3f} (< 0.06)"),
        (float(np.percentile(esc_p[esc_eng_mask, 7], 90)) > 0.45, f"Real ESC-50 Fold-5 engine p90={np.percentile(esc_p[esc_eng_mask, 7], 90):.3f} (> 0.45)"),
        (float(np.percentile(esc_p[esc_gravel_mask, 6], 90)) > 0.45, f"Real ESC-50 Fold-5 rain/fire/gravel p90={np.percentile(esc_p[esc_gravel_mask, 6], 90):.3f} (> 0.45)"),
    ]

    fails = 0
    for ok, msg in real_checks:
        print(f"[{'PASS' if ok else 'FAIL'}] {msg}")
        if not ok:
            fails += 1
    return fails


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--real-data", default="", help="Optional path to scratch haptics-datasets dir")
    args = parser.parse_args()

    weights_path = Path(__file__).resolve().parent.parent / "desktop-dotnet" / "X20Ctl.Zone" / "Lighting" / "HapticsModelWeights.json"
    weights = json.loads(weights_path.read_text(encoding="utf-8"))

    checks = [
        ("silence", 101, lambda s: max(mx for _, mx in s.values()) < 0.02, "silence max < 0.02"),
        ("pure_instrumental", 202, lambda s: s["Vocal"][0] < 0.10, "instrumental vocal mean < 0.10"),
        ("vocal_plus_music", 303, lambda s: s["Vocal"][1] > 0.65 and s["Vocal"][0] > 0.20, "vocal_plus_music peak Vocal > 0.65"),
        ("gunshot", 404, lambda s: s["Gunshot"][1] > 0.55 and s["Vocal"][0] < 0.08, "gunshot peak > 0.55 and low vocal bleed"),
        ("explosion", 505, lambda s: s["Explosion"][1] > 0.55, "explosion peak > 0.55"),
        ("gravel", 606, lambda s: s["GravelSurface"][1] > 0.55 and s["Vocal"][0] < 0.08, "gravel peak > 0.55"),
        ("boost_surge", 707, lambda s: s["BoostSurge"][1] > 0.55, "boost_surge peak > 0.55"),
        ("tire_screech", 808, lambda s: s["TireScreech"][1] > 0.55, "tire_screech peak > 0.55"),
        ("melee_clash", 909, lambda s: s["MeleeClash"][1] > 0.55, "melee_clash peak > 0.55"),
        ("dialogue", 1010, lambda s: s["Dialogue"][1] > 0.55 and s["Drums"][0] < 0.15, "dialogue peak > 0.55 without false drums"),
    ]

    failed = 0
    for kind, seed, predicate, desc in checks:
        stats = eval_scenario(weights, kind, seed)
        ok = predicate(stats)
        top = sorted(((k, v[1]) for k, v in stats.items()), key=lambda kv: kv[1], reverse=True)[:3]
        top_str = ", ".join(f"{k}={v:.2f}" for k, v in top)
        vox_mean = stats["Vocal"][0]
        print(f"[{'PASS' if ok else 'FAIL'}] {desc} | VocalMean={vox_mean:.3f} | Top=[{top_str}]")
        if not ok:
            failed += 1

    if args.real_data:
        failed += verify_real_audio(weights, Path(args.real_data))

    if failed:
        print(f"Verification FAILED ({failed} checks failed)")
        return 1
    print("All held-out envelope verification checks PASSED.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

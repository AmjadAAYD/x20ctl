"""Real-audio GPU trainer for X20Ctl Universal Haptics classifier.

Combines:
1. MUSDB18 studio multitrack stems (100 train songs, 50 held-out test songs)
   producing full mixes, pure instrumental hard negatives, vocal-forward mixes,
   and rhythm-only mixes with exact frame-level RMS ground-truth stem targets.
2. ESC-50 real environmental & SFX WAV recordings (folds 1-4 train, fold 5 test)
   covering fireworks/impacts, thunder, engines, rain/gravel textures, sirens,
   metallic/glass clashes, non-singing human sounds, and hard-negative noises.
3. Targeted game SFX clips (gunshot impulse, nitro boost surge, tire screech,
   gravel curbs, melee clash, dialogue, silence) from train_haptics_model.

Trains a compact 28 -> 96 -> 12 MLP on CUDA (or CPU fallback) and exports compact
JSON weights embedded directly into X20Ctl.Zone/Lighting/HapticsModelWeights.json.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import subprocess
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import imageio_ffmpeg
import numpy as np
import torch
import torch.nn as nn
from scipy.io import wavfile
from scipy.signal import resample_poly

from train_haptics_model import (
    BitExactSpectrumAnalyzer,
    FEATURE_COUNT,
    FRAME_HOP,
    HEAD_COUNT,
    HEAD_NAMES,
    SAMPLE_RATE,
    _pan_stereo,
    _synth_scenario,
)

HIDDEN_UNITS = 128


def _gate(rms: np.ndarray, lo: float, hi: float) -> np.ndarray:
    return np.clip((rms - lo) / max(1e-6, hi - lo), 0.0, 1.0)


def _decode_mp4_stream(ffmpeg_exe: str, mp4_path: Path, stream_idx: int) -> np.ndarray:
    cmd = [
        ffmpeg_exe,
        "-v",
        "error",
        "-i",
        str(mp4_path),
        "-map",
        f"0:a:{stream_idx}",
        "-f",
        "f32le",
        "-acodec",
        "pcm_f32le",
        "-ac",
        "2",
        "-ar",
        str(SAMPLE_RATE),
        "pipe:1",
    ]
    res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if res.returncode != 0 or not res.stdout:
        raise RuntimeError(f"ffmpeg failed for {mp4_path.name} stream {stream_idx}: {res.stderr.decode('utf-8', 'ignore')}")
    arr = np.frombuffer(res.stdout, dtype=np.float32)
    n = (len(arr) // 2) * 2
    return arr[:n].reshape(-1, 2).copy()


def _decode_musdb_track(ffmpeg_exe: str, mp4_path: Path) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    # MUSDB18 stem MP4 layout: 0=mixture, 1=drums, 2=bass, 3=other, 4=vocals
    drums = _decode_mp4_stream(ffmpeg_exe, mp4_path, 1)
    bass = _decode_mp4_stream(ffmpeg_exe, mp4_path, 2)
    other = _decode_mp4_stream(ffmpeg_exe, mp4_path, 3)
    vocals = _decode_mp4_stream(ffmpeg_exe, mp4_path, 4)
    min_len = min(len(drums), len(bass), len(other), len(vocals))
    return drums[:min_len], bass[:min_len], other[:min_len], vocals[:min_len]


def _extract_mix_features(
    drums: np.ndarray,
    bass: np.ndarray,
    other: np.ndarray,
    vocals: np.ndarray,
    d_gain: float,
    b_gain: float,
    o_gain: float,
    v_gain: float,
    master_gain: float,
) -> Tuple[np.ndarray, np.ndarray]:
    mix = (drums * d_gain + bass * b_gain + other * o_gain + vocals * v_gain) * master_gain
    peak = float(np.max(np.abs(mix)))
    norm_s = (0.96 / peak) if peak > 0.96 else 1.0
    if norm_s != 1.0:
        mix *= norm_s
        d_s, b_s, o_s, v_s = d_gain * master_gain * norm_s, b_gain * master_gain * norm_s, o_gain * master_gain * norm_s, v_gain * master_gain * norm_s
    else:
        d_s, b_s, o_s, v_s = d_gain * master_gain, b_gain * master_gain, o_gain * master_gain, v_gain * master_gain

    n_frames = len(mix) // FRAME_HOP
    analyzer = BitExactSpectrumAnalyzer(SAMPLE_RATE)
    feats = []
    tgts = []

    for fi in range(n_frames):
        i0 = fi * FRAME_HOP
        i1 = i0 + FRAME_HOP
        l = mix[i0:i1, 0].astype(np.float64)
        r = mix[i0:i1, 1].astype(np.float64)
        feat = analyzer.process_chunk(l, r)

        if fi < 3:
            continue

        d_rms = math.sqrt(float(np.mean((drums[i0:i1] * d_s) ** 2)))
        b_rms = math.sqrt(float(np.mean((bass[i0:i1] * b_s) ** 2)))
        o_rms = math.sqrt(float(np.mean((other[i0:i1] * o_s) ** 2)))
        v_rms = math.sqrt(float(np.mean((vocals[i0:i1] * v_s) ** 2)))

        y = np.zeros(HEAD_COUNT, dtype=np.float32)
        if v_gain > 0.0:
            v_gate = float(_gate(np.array([v_rms]), 0.010, 0.055)[0])
            v_dom = v_rms / (v_rms + 0.35 * o_rms + 0.22 * d_rms + 0.012)
            y[0] = float(np.clip(v_dom * v_gate * 1.15, 0.0, 1.0))

        b_gate = float(_gate(np.array([b_rms]), 0.012, 0.075)[0])
        y[1] = float(np.clip((b_rms / (b_rms + 0.32 * o_rms + 0.015)) * b_gate * 1.12, 0.0, 1.0))

        d_gate = float(_gate(np.array([d_rms]), 0.010, 0.065)[0])
        y[2] = float(np.clip((d_rms / (d_rms + 0.32 * o_rms + 0.015)) * d_gate * 1.15, 0.0, 1.0))

        o_gate = float(_gate(np.array([o_rms]), 0.010, 0.055)[0])
        y[3] = float(np.clip((o_rms / (o_rms + 0.35 * v_rms + 0.015)) * o_gate * 1.12, 0.0, 1.0))

        feats.append(feat)
        tgts.append(y)

    if not feats:
        return np.empty((0, FEATURE_COUNT), dtype=np.float32), np.empty((0, HEAD_COUNT), dtype=np.float32)
    return np.stack(feats), np.stack(tgts)


def _process_musdb_split(stems_dir: Path, seed: int) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Returns (X, Y, tag_ids) where tag_ids: 0=full_mix, 1=instrumental_only, 2=vocal_forward, 3=rhythm_only."""
    mp4_files = sorted(stems_dir.glob("*.stem.mp4"))
    if not mp4_files:
        raise FileNotFoundError(f"No *.stem.mp4 found in {stems_dir}")

    ffmpeg_exe = imageio_ffmpeg.get_ffmpeg_exe()
    rng = np.random.default_rng(seed)

    print(f"Decoding {len(mp4_files)} MUSDB18 stem files from {stems_dir.name}...")
    with ThreadPoolExecutor(max_workers=8) as pool:
        decoded = list(pool.map(lambda p: _decode_musdb_track(ffmpeg_exe, p), mp4_files))

    xs: List[np.ndarray] = []
    ys: List[np.ndarray] = []
    tags: List[np.ndarray] = []

    for drums, bass, other, vocals in decoded:
        views = [
            (1.0, 1.0, 1.0, 1.0, float(rng.uniform(0.55, 1.0)), 0),    # Real full mix
            (1.0, 1.0, 1.05, 0.0, float(rng.uniform(0.50, 1.0)), 1),   # Real instrumental only (hard negative for vocals)
            (0.25, 0.25, 0.25, 1.15, float(rng.uniform(0.50, 0.95)), 2),  # Vocal-forward / acoustic
            (1.0, 1.0, 0.0, 0.0, float(rng.uniform(0.50, 0.95)), 3),   # Pure drums + bass
        ]
        for dg, bg, og, vg, mg, tag in views:
            fx, fy = _extract_mix_features(drums, bass, other, vocals, dg, bg, og, vg, mg)
            if len(fx) > 0:
                xs.append(fx)
                ys.append(fy)
                tags.append(np.full(len(fx), tag, dtype=np.int32))

    return np.concatenate(xs, axis=0), np.concatenate(ys, axis=0), np.concatenate(tags, axis=0)


ESC_HEAD_MAP: Dict[str, str] = {
    "fireworks": "impulse_boom",
    "thunderstorm": "thunder",
    "engine": "engine",
    "chainsaw": "engine",
    "helicopter": "engine",
    "airplane": "engine",
    "train": "engine",
    "crackling_fire": "gravel",
    "rain": "gravel",
    "wind": "gravel",
    "footsteps": "gravel",
    "pouring_water": "gravel",
    "sea_waves": "gravel",
    "siren": "screech",
    "car_horn": "screech",
    "hand_saw": "screech",
    "glass_breaking": "clash",
    "church_bells": "clash",
    "clapping": "clash",
    "door_wood_knock": "clash",
    "can_opening": "clash",
    "laughing": "dialogue",
    "coughing": "dialogue",
    "crying_baby": "dialogue",
    "breathing": "dialogue",
    "snoring": "dialogue",
}


def _find_esc_root(esc_base: Path) -> Path:
    for candidate in [esc_base, esc_base / "ESC-50-master"]:
        if (candidate / "meta" / "esc50.csv").exists():
            return candidate
    hits = list(esc_base.rglob("esc50.csv"))
    if hits:
        return hits[0].parent.parent
    raise FileNotFoundError(f"esc50.csv not found under {esc_base}")


def _process_esc50(esc_base: Path, target_folds: set[int], seed: int) -> Tuple[np.ndarray, np.ndarray, List[str]]:
    root = _find_esc_root(esc_base)
    csv_path = root / "meta" / "esc50.csv"
    audio_dir = root / "audio"
    rng = np.random.default_rng(seed)

    rows = []
    with csv_path.open("r", encoding="utf-8") as fh:
        for r in csv.DictReader(fh):
            if int(r["fold"]) in target_folds:
                rows.append((r["filename"], r["category"]))

    print(f"Processing {len(rows)} ESC-50 real recordings for folds {sorted(target_folds)}...")
    xs: List[np.ndarray] = []
    ys: List[np.ndarray] = []
    cats: List[str] = []

    for fname, category in rows:
        sr, data = wavfile.read(audio_dir / fname)
        if data.dtype == np.int16:
            wav = data.astype(np.float64) / 32768.0
        elif data.dtype == np.int32:
            wav = data.astype(np.float64) / 2147483648.0
        else:
            wav = data.astype(np.float64)
        if wav.ndim > 1:
            wav = np.mean(wav, axis=1)

        if sr != SAMPLE_RATE:
            g = math.gcd(SAMPLE_RATE, sr)
            wav = resample_poly(wav, SAMPLE_RATE // g, sr // g)

        peak = float(np.max(np.abs(wav)))
        if peak > 0.01:
            wav = wav / peak * float(rng.uniform(0.35, 0.90))

        left, right = _pan_stereo(wav, float(rng.uniform(-0.80, 0.80)))
        n_frames = len(left) // FRAME_HOP
        analyzer = BitExactSpectrumAnalyzer(SAMPLE_RATE)
        role = ESC_HEAD_MAP.get(category, "neg")
        prev_rms = 0.0

        for fi in range(n_frames):
            i0 = fi * FRAME_HOP
            i1 = i0 + FRAME_HOP
            l_seg, r_seg = left[i0:i1], right[i0:i1]
            feat = analyzer.process_chunk(l_seg, r_seg)
            rms = math.sqrt(float(np.mean((0.5 * (l_seg + r_seg)) ** 2)))
            act = float(np.clip((rms - 0.008) / 0.055, 0.0, 1.0))
            onset = float(np.clip((rms - prev_rms * 1.15) / 0.035, 0.0, 1.0))
            prev_rms = rms

            if fi < 3:
                continue

            y = np.zeros(HEAD_COUNT, dtype=np.float32)
            if act > 0.02:
                if role == "impulse_boom":
                    y[4] = onset
                    y[5] = act * 0.80
                    y[1] = act * 0.45
                elif role == "thunder":
                    y[5] = act * 0.92
                    y[1] = act * 0.65
                elif role == "engine":
                    y[7] = act * 0.92
                elif role == "gravel":
                    y[6] = act * 0.88
                elif role == "screech":
                    y[9] = act * 0.88
                elif role == "clash":
                    y[10] = max(onset, act * 0.68)
                elif role == "dialogue":
                    y[11] = act * 0.88

            xs.append(feat)
            ys.append(y)
            cats.append(role)

    return np.stack(xs), np.stack(ys), cats


def _build_synth_sfx_supplement(seed: int, clips_per_kind: int) -> Tuple[np.ndarray, np.ndarray]:
    rng = np.random.default_rng(seed)
    kinds = [
        "silence",
        "gunshot",
        "explosion",
        "gravel",
        "engine",
        "boost_surge",
        "tire_screech",
        "melee_clash",
        "dialogue",
    ]
    xs: List[np.ndarray] = []
    ys: List[np.ndarray] = []
    for kind in kinds:
        for _ in range(clips_per_kind):
            left, right, per_sample_y = _synth_scenario(rng, kind, duration_sec=3.0)
            analyzer = BitExactSpectrumAnalyzer(SAMPLE_RATE)
            n_frames = len(left) // FRAME_HOP
            for fi in range(n_frames):
                i0 = fi * FRAME_HOP
                feat = analyzer.process_chunk(left[i0 : i0 + FRAME_HOP], right[i0 : i0 + FRAME_HOP])
                y_frame = np.max(per_sample_y[i0 : i0 + FRAME_HOP], axis=0)
                if fi >= 3:
                    xs.append(feat)
                    ys.append(y_frame)
    return np.stack(xs), np.stack(ys)


class RealHapticsMlp(nn.Module):
    def __init__(self) -> None:
        super().__init__()
        self.fc1 = nn.Linear(FEATURE_COUNT, HIDDEN_UNITS)
        self.act = nn.ReLU()
        self.fc2 = nn.Linear(HIDDEN_UNITS, HEAD_COUNT)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.fc2(self.act(self.fc1(x)))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-dir", required=True, help="Scratch directory containing musdb18/ and esc50/")
    parser.add_argument("--no-cache", action="store_true")
    args = parser.parse_args()

    data_dir = Path(args.data_dir)
    cache_path = data_dir / "features_cache_v1.npz"

    if cache_path.exists() and not args.no_cache:
        print(f"Loading cached real-audio feature tensors from {cache_path}...")
        c = np.load(cache_path, allow_pickle=False)
        train_x, train_y = c["train_x"], c["train_y"]
        mus_val_x, mus_val_y, mus_val_tags = c["mus_val_x"], c["mus_val_y"], c["mus_val_tags"]
        esc_val_x, esc_val_y, esc_val_roles = c["esc_val_x"], c["esc_val_y"], c["esc_val_roles"].tolist()
    else:
        mus_tr_x, mus_tr_y, _ = _process_musdb_split(data_dir / "musdb18" / "train", seed=1001)
        mus_val_x, mus_val_y, mus_val_tags = _process_musdb_split(data_dir / "musdb18" / "test", seed=2002)

        esc_tr_x, esc_tr_y, _ = _process_esc50(data_dir / "esc50", {1, 2, 3, 4}, seed=3003)
        esc_val_x, esc_val_y, esc_val_roles = _process_esc50(data_dir / "esc50", {5}, seed=4004)

        sfx_tr_x, sfx_tr_y = _build_synth_sfx_supplement(seed=5005, clips_per_kind=12)
        sfx_val_x, sfx_val_y = _build_synth_sfx_supplement(seed=6006, clips_per_kind=4)

        train_x = np.concatenate([mus_tr_x, esc_tr_x, sfx_tr_x], axis=0).astype(np.float32)
        train_y = np.concatenate([mus_tr_y, esc_tr_y, sfx_tr_y], axis=0).astype(np.float32)
        np.savez_compressed(
            cache_path,
            train_x=train_x,
            train_y=train_y,
            mus_val_x=mus_val_x.astype(np.float32),
            mus_val_y=mus_val_y.astype(np.float32),
            mus_val_tags=mus_val_tags,
            esc_val_x=esc_val_x.astype(np.float32),
            esc_val_y=esc_val_y.astype(np.float32),
            esc_val_roles=np.array(esc_val_roles),
            sfx_val_x=sfx_val_x.astype(np.float32),
            sfx_val_y=sfx_val_y.astype(np.float32),
        )
        print(f"Saved feature cache to {cache_path}")

    val_x = np.concatenate([mus_val_x, esc_val_x], axis=0).astype(np.float32)
    val_y = np.concatenate([mus_val_y, esc_val_y], axis=0).astype(np.float32)

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"Training RealHapticsMlp ({FEATURE_COUNT} -> {HIDDEN_UNITS} -> {HEAD_COUNT}) on {device} | Train={train_x.shape}, Val={val_x.shape}")

    torch.manual_seed(42)
    model = RealHapticsMlp().to(device)
    epochs = 260
    opt = torch.optim.AdamW(model.parameters(), lr=4.5e-3, weight_decay=8e-5)
    sched = torch.optim.lr_scheduler.CosineAnnealingLR(opt, T_max=epochs)

    # Calibrated loss & sampling: boost active Bass & Drums recall on dense studio mixes while strictly
    # keeping instrumental hard-negative false-vocal rate low and SFX classes isolated.
    pos_w = torch.tensor([1.02, 2.70, 1.55, 1.30, 1.15, 1.35, 1.60, 1.25, 1.35, 1.25, 1.00, 1.15], device=device)

    # Extra per-sample boost for pure-music instrumental hard negatives & active stems
    inst_hard_neg = ((train_y[:, 0] < 0.02) & (train_y[:, 1:4].max(axis=1) > 0.15)).astype(np.float64)
    active_mus = (train_y[:, :4].max(axis=1) > 0.08).astype(np.float64)
    active_sfx = (train_y[:, 4:].max(axis=1) > 0.08).astype(np.float64)
    row_w = 1.0 + 2.2 * active_mus + 2.4 * inst_hard_neg + 1.6 * active_sfx
    row_probs = torch.from_numpy((row_w / row_w.sum()).astype(np.float32)).to(device)

    tx = torch.from_numpy(train_x).to(device)
    ty = torch.from_numpy(train_y).to(device)
    vx = torch.from_numpy(val_x).to(device)
    vy = torch.from_numpy(val_y).to(device)

    batch_size = 2048
    n_steps = max(1, len(tx) // batch_size)
    best_loss = float("inf")
    best_state: Dict[str, torch.Tensor] = {}

    for epoch in range(1, epochs + 1):
        model.train()
        for _ in range(n_steps):
            idx = torch.multinomial(row_probs, batch_size, replacement=True)
            logits = model(tx[idx])
            probs = torch.sigmoid(logits)
            bce_val = nn.functional.binary_cross_entropy_with_logits(logits, ty[idx], pos_weight=pos_w)
            mse_val = nn.functional.mse_loss(probs, ty[idx])
            loss = 0.62 * bce_val + 0.38 * mse_val
            opt.zero_grad()
            loss.backward()
            opt.step()
        sched.step()

        if epoch % 5 == 0 or epoch == epochs:
            model.eval()
            with torch.no_grad():
                vlogits = model(vx)
                vprobs = torch.sigmoid(vlogits)
                vloss = float((0.62 * nn.functional.binary_cross_entropy_with_logits(vlogits, vy) + 0.38 * nn.functional.mse_loss(vprobs, vy)).item())
                if vloss < best_loss:
                    best_loss = vloss
                    best_state = {k: v.detach().cpu().clone() for k, v in model.state_dict().items()}

    model.cpu().load_state_dict(best_state)
    model.eval()

    with torch.no_grad():
        mus_preds = torch.sigmoid(model(torch.from_numpy(mus_val_x))).numpy()

    inst_mask = mus_val_tags == 1
    vox_fwd_mask = (mus_val_tags == 2) & (mus_val_y[:, 0] > 0.45)
    print(f"Best held-out loss: {best_loss:.4f}")
    print(f"  Real MUSDB18 Test (50 unseen tracks) Instrumental-Only VocalMean = {mus_preds[inst_mask, 0].mean():.4f}")
    print(f"  Real MUSDB18 Test (50 unseen tracks) Vocal-Forward Active Mean  = {mus_preds[vox_fwd_mask, 0].mean():.4f}")
    for i, name in enumerate(["Vocal", "Bass", "Drums", "Instruments"]):
        mae = float(np.mean(np.abs(mus_preds[:, i] - mus_val_y[:, i])))
        pos = mus_val_y[:, i] > 0.40
        neg = mus_val_y[:, i] < 0.05
        pm = float(mus_preds[pos, i].mean()) if np.any(pos) else 0.0
        nm = float(mus_preds[neg, i].mean()) if np.any(neg) else 0.0
        print(f"  MUSDB {name:<13} MAE={mae:.3f} PosMean={pm:.3f} NegMean={nm:.3f}")

    weights_obj = {
        "version": 2,
        "trainedOn": "MUSDB18-stems + ESC-50-folds1to4 + SFX-supplement",
        "featureCount": FEATURE_COUNT,
        "hiddenUnits": HIDDEN_UNITS,
        "heads": HEAD_NAMES,
        "w1": [[round(float(v), 5) for v in row] for row in model.fc1.weight.detach().numpy()],
        "b1": [round(float(v), 5) for v in model.fc1.bias.detach().numpy()],
        "w2": [[round(float(v), 5) for v in row] for row in model.fc2.weight.detach().numpy()],
        "b2": [round(float(v), 5) for v in model.fc2.bias.detach().numpy()],
    }

    out_path = Path(__file__).resolve().parent.parent / "desktop-dotnet" / "X20Ctl.Zone" / "Lighting" / "HapticsModelWeights.json"
    out_path.write_text(json.dumps(weights_obj, separators=(",", ":")), encoding="utf-8")
    print(f"Exported real-audio weights to {out_path} ({out_path.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

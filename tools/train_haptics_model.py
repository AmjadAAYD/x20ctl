"""Offline trainer for X20Ctl Universal Haptics audio & SFX classifier.

Generates diverse labeled 48 kHz stereo training and held-out validation scenarios
matching X20Ctl's exact 1024-point Mid-Side FFT feature pipeline (28 features),
trains a compact 2-layer MLP (28 -> 48 -> 12) in PyTorch, validates vocal false-positive
suppression and game SFX accuracy, and exports compact weights JSON embedded in X20Ctl.Zone.
"""

from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Dict, List, Tuple

import numpy as np
import torch
import torch.nn as nn

SAMPLE_RATE = 48000
FFT_SIZE = 1024
FRAME_HOP = 960  # 20 ms -> 50 Hz frame rate
FEATURE_COUNT = 28
HIDDEN_UNITS = 48
HEAD_NAMES = [
    "Vocal",
    "Bass",
    "Drums",
    "Instruments",
    "Gunshot",
    "Explosion",
    "GravelSurface",
    "EngineRumble",
    "BoostSurge",
    "TireScreech",
    "MeleeClash",
    "Dialogue",
]
HEAD_COUNT = len(HEAD_NAMES)
MAX_LOG_SPAN = math.log2(12000.0 / 20.0)


def _clamp(v: float, lo: float, hi: float) -> float:
    return hi if v > hi else (lo if v < lo else v)


def _lerp_exp(cur: float, tgt: float, dt: float, tau: float) -> float:
    alpha = 1.0 - math.exp(-dt / max(0.005, tau))
    return cur + (tgt - cur) * alpha


def _smooth(cur: float, tgt: float, dt: float, att: float, rel: float) -> float:
    tau = att if tgt > cur else rel
    alpha = 1.0 - math.exp(-dt / max(0.005, tau))
    return _clamp(cur + (tgt - cur) * alpha, 0.0, 1.0)


def _update_track_ref(cur: float, peak: float, dt: float, audible: bool, floor: float) -> float:
    if peak > cur:
        return _smooth(cur, peak, dt, 0.15, 0.15)
    decay_tau = 14.0 if audible else 3.0
    return max(floor, _smooth(cur, max(floor, peak), dt, decay_tau, decay_tau))


def _update_agc(cur: float, val: float, dt: float, audible: bool, floor: float, decay_sec: float) -> float:
    if val > cur:
        return _smooth(cur, val, dt, 0.08, 0.08)
    decay_tau = decay_sec if audible else 1.5
    return max(floor, _smooth(cur, max(floor, val), dt, decay_tau, decay_tau))


class BitExactSpectrumAnalyzer:
    """Python mirror of C# AudioSpectrumAnalyzer for exact 28-feature parity."""

    def __init__(self, sample_rate: int = SAMPLE_RATE) -> None:
        self.sr = sample_rate
        self.hann = 0.5 * (1.0 - np.cos(2.0 * np.pi * np.arange(FFT_SIZE) / (FFT_SIZE - 1)))
        self.mono_ring = np.zeros(FFT_SIZE, dtype=np.float64)
        self.side_ring = np.zeros(FFT_SIZE, dtype=np.float64)
        self.ring_pos = 0
        self.t_sec = 0.0
        self.last_beat_t = -10.0
        self.last_treble_t = -10.0
        self.first_frame = True

        self.track_ref = 0.05
        self.mid_vocal_ref = 0.04
        self.agc_bass = 0.04
        self.agc_mid = 0.04
        self.agc_treble = 0.04
        self.agc_vocal = 0.03
        self.agc_inst = 0.035
        self.agc_stereo = 0.04
        self.agc_bands = np.array([0.04, 0.04, 0.04, 0.035, 0.035, 0.03, 0.028, 0.025], dtype=np.float64)
        self.smooth_bands = np.zeros(8, dtype=np.float64)
        self.prev_band_norm = np.zeros(8, dtype=np.float64)

        self.bass_avg = 0.0
        self.treble_avg = 0.0
        self.bass_floor = 0.0
        self.level_floor = 0.0
        self.prev_center_rms = 0.0
        self.prev_centroid = 0.5

        self.smooth_level = 0.0
        self.smooth_bass = 0.0
        self.smooth_mid = 0.0
        self.smooth_treble = 0.0
        self.smooth_left = 0.0
        self.smooth_right = 0.0
        self.smooth_centroid = 0.5
        self.smooth_energy = 0.0
        self.smooth_vocal = 0.0
        self.smooth_vocal_warm = 0.0
        self.smooth_vocal_air = 0.0
        self.smooth_inst = 0.0
        self.smooth_bass_punch = 0.0
        self.smooth_dynamic = 0.0
        self.smooth_flatness = 0.0
        self.smooth_flux = 0.0
        self.smooth_coherence = 0.0
        self.smooth_crest = 0.0
        self.smooth_high_res = 0.0
        self.smooth_centroid_vel = 0.0
        self.beat_pulse = 0.0
        self.treble_transient = 0.0
        self.stereo_pan = 0.0

        # Precompute frequency masks for bins 1..511
        bins = np.arange(1, FFT_SIZE // 2)
        self.freqs = bins * (self.sr / float(FFT_SIZE))
        valid = (self.freqs >= 20.0) & (self.freqs <= 14000.0)
        self.valid_idx = np.where(valid)[0]
        self.vf = self.freqs[self.valid_idx]
        self.m_12k = self.vf <= 12000.0
        self.log_pos_12k = np.clip(np.log2(self.vf[self.m_12k] / 20.0) / MAX_LOG_SPAN, 0.0, 1.0)
        self.m_hires = (self.vf >= 1800.0) & (self.vf <= 4800.0)
        self.band_masks = [
            self.vf < 85.0,
            (self.vf >= 85.0) & (self.vf < 220.0),
            (self.vf >= 220.0) & (self.vf < 450.0),
            (self.vf >= 450.0) & (self.vf < 1100.0),
            (self.vf >= 1100.0) & (self.vf < 2800.0),
            (self.vf >= 2800.0) & (self.vf < 5500.0),
            (self.vf >= 5500.0) & (self.vf < 9500.0),
            self.vf >= 9500.0,
        ]
        self.band_scales = np.array([1.45, 1.65, 1.90, 2.15, 2.40, 2.75, 3.15, 3.60], dtype=np.float64)
        self.m_bass = self.vf < 240.0
        self.m_mid = (self.vf >= 240.0) & (self.vf < 2200.0)
        self.m_treble = self.vf >= 2200.0
        self.m_vox = (self.vf >= 260.0) & (self.vf <= 3600.0)
        self.m_vox_warm = self.m_vox & (self.vf < 1150.0)
        self.m_vox_air = self.m_vox & (self.vf >= 1150.0)
        self.m_inst_side = (self.vf >= 160.0) & (self.vf <= 8000.0)
        self.m_inst_sparkle = self.vf > 3400.0

    def process_chunk(self, left: np.ndarray, right: np.ndarray) -> np.ndarray:
        n = len(left)
        dt = float(np.clip(n / float(self.sr), 0.002, 0.25))
        self.t_sec += dt

        left_c = np.clip(left, -1.0, 1.0)
        right_c = np.clip(right, -1.0, 1.0)
        mono = 0.5 * (left_c + right_c)
        side = 0.5 * (left_c - right_c)

        idx = (self.ring_pos + np.arange(n)) & (FFT_SIZE - 1)
        self.mono_ring[idx] = mono
        self.side_ring[idx] = side
        self.ring_pos = (self.ring_pos + n) & (FFT_SIZE - 1)

        raw_rms = float(np.sqrt(np.mean(mono * mono)))
        left_rms = float(np.sqrt(np.mean(left_c * left_c)))
        right_rms = float(np.sqrt(np.mean(right_c * right_c)))
        lr_sum = left_rms + right_rms
        target_pan = _clamp((right_rms - left_rms) / lr_sum, -1.0, 1.0) if lr_sum > 0.002 else 0.0
        self.stereo_pan = _lerp_exp(self.stereo_pan, target_pan, dt, 0.09)

        order = (self.ring_pos + np.arange(FFT_SIZE)) & (FFT_SIZE - 1)
        scale = 4.0 / FFT_SIZE
        m_fft = np.fft.rfft(self.mono_ring[order] * self.hann)[1 : FFT_SIZE // 2]
        s_fft = np.fft.rfft(self.side_ring[order] * self.hann)[1 : FFT_SIZE // 2]
        mm = np.abs(m_fft)[self.valid_idx] * scale
        sm = np.abs(s_fft)[self.valid_idx] * scale

        mag_sq = mm * mm + sm * sm
        tot_mag = np.sqrt(mag_sq)

        tot_w_sq = float(np.sum(mag_sq[self.m_12k]))
        centroid = _clamp(float(np.sum(self.log_pos_12k * mag_sq[self.m_12k])) / tot_w_sq, 0.0, 1.0) if tot_w_sq > 1e-8 else self.smooth_centroid

        msq_12k = mag_sq[self.m_12k]
        full_bins_n = len(msq_12k)
        full_geom = math.exp(float(np.mean(np.log(msq_12k + 1e-10)))) if full_bins_n > 0 else 0.0
        full_arith = float(np.mean(msq_12k)) if full_bins_n > 0 else 0.0
        raw_flatness = _clamp(full_geom / full_arith, 0.0, 1.0) if full_arith > 1e-10 else 0.0

        f_mid = float(np.sum(mm[self.m_12k]))
        f_side = float(np.sum(sm[self.m_12k]))
        raw_coherence = _clamp(f_mid / (f_mid + f_side), 0.0, 1.0) if (f_mid + f_side) > 1e-6 else 0.0

        t_12k = tot_mag[self.m_12k]
        full_mean = float(np.mean(t_12k)) if full_bins_n > 0 else 0.0
        full_max = float(np.max(t_12k)) if full_bins_n > 0 else 0.0
        raw_crest = _clamp((full_max / full_mean - 2.0) / 14.0, 0.0, 1.0) if full_mean > 1e-6 else 0.0

        hi_t = tot_mag[self.m_hires]
        hi_mean = float(np.mean(hi_t)) if len(hi_t) > 0 else 0.0
        hi_max = float(np.max(hi_t)) if len(hi_t) > 0 else 0.0
        raw_high_res = _clamp((hi_max / hi_mean - 2.5) / 10.0, 0.0, 1.0) if hi_mean > 1e-6 else 0.0

        raw_bands = np.zeros(8, dtype=np.float64)
        for i, mask in enumerate(self.band_masks):
            raw_bands[i] = float(np.sqrt(np.mean(mag_sq[mask])) * self.band_scales[i]) if np.any(mask) else 0.0

        raw_bass = float(np.sqrt(np.mean(mag_sq[self.m_bass])) * 1.45)
        raw_mid = float(np.sqrt(np.mean(mag_sq[self.m_mid])) * 1.90)
        raw_treble = float(np.sqrt(np.mean(mag_sq[self.m_treble])) * 2.95)

        vox_mm = mm[self.m_vox]
        vox_sm = sm[self.m_vox]
        vox_n = len(vox_mm)
        ratio = vox_sm / (vox_mm + 1e-6)
        center_mask = np.clip(1.0 - ratio * 1.18, 0.0, 1.0)
        center_mag = vox_mm * center_mask
        center_sq = center_mag * center_mag
        mm_sq = vox_mm * vox_mm

        vox_mean = float(np.mean(vox_mm))
        vox_crest = float(np.max(vox_mm)) / vox_mean if vox_mean > 1e-6 else 0.0
        geom_m = math.exp(float(np.mean(np.log(mm_sq + 1e-10))))
        arith_m = float(np.mean(mm_sq))
        sfm = _clamp(geom_m / arith_m, 0.0, 1.0) if arith_m > 1e-10 else 1.0
        harmonicity = _clamp((0.34 - sfm) / 0.24, 0.0, 1.0)
        crest_score = _clamp((vox_crest - 2.1) / 2.2, 0.0, 1.0)
        tonality = _clamp(harmonicity * (0.45 + 0.55 * crest_score), 0.0, 1.0)

        vox_center_rms = float(np.sqrt(np.mean(center_sq)))
        in_band_side_rms = float(np.sqrt(np.mean(vox_sm * vox_sm * 2.0)))
        center_dom = _clamp((vox_center_rms / (vox_center_rms + in_band_side_rms + 1e-6) - 0.34) / 0.40, 0.0, 1.0)
        mod_bonus = _clamp(1.0 + abs(vox_center_rms - self.prev_center_rms) * 12.0, 1.0, 1.25)
        self.prev_center_rms = vox_center_rms
        bass_supp = 0.30 if (tonality < 0.35 and raw_bass > raw_mid * 3.5) else 1.0
        vox_clarity = _clamp(tonality * center_dom * bass_supp * mod_bonus, 0.0, 1.0)

        # Warm/Air indexing on full array
        all_ratio = sm / (mm + 1e-6)
        all_center_sq = (mm * np.clip(1.0 - all_ratio * 1.18, 0.0, 1.0)) ** 2
        raw_vox_warm = float(np.sqrt(np.mean(all_center_sq[self.m_vox_warm])) * 2.55)
        raw_vox_air = float(np.sqrt(np.mean(all_center_sq[self.m_vox_air])) * 2.85)
        inst_side_rms = float(np.sqrt(np.mean((sm[self.m_inst_side] ** 2) * 2.0)))
        inst_sparkle_rms = float(np.sqrt(np.mean(mag_sq[self.m_inst_sparkle])))
        raw_inst = math.sqrt(inst_side_rms * inst_side_rms * 1.65 + raw_bass * raw_bass * 0.25 + inst_sparkle_rms * inst_sparkle_rms * 1.15)

        audible = (
            raw_rms >= 0.0022
            or left_rms >= 0.0022
            or right_rms >= 0.0022
            or raw_bass >= 0.003
            or raw_mid >= 0.003
            or raw_treble >= 0.003
        )

        raw_peak_band = max(raw_rms * 1.25, raw_bass, raw_mid, raw_treble)
        self.track_ref = _update_track_ref(self.track_ref, raw_peak_band, dt, audible, 0.05)
        self.mid_vocal_ref = _update_track_ref(self.mid_vocal_ref, max(raw_mid, raw_vox_warm, raw_vox_air), dt, audible, 0.035)

        self.agc_bass = _update_agc(self.agc_bass, raw_bass, dt, audible, 0.04, 9.0)
        self.agc_mid = _update_agc(self.agc_mid, raw_mid, dt, audible, 0.035, 9.0)
        self.agc_treble = _update_agc(self.agc_treble, raw_treble, dt, audible, 0.03, 9.0)
        self.agc_vocal = _update_agc(self.agc_vocal, max(raw_vox_warm, raw_vox_air), dt, audible, 0.028, 7.0)
        self.agc_inst = _update_agc(self.agc_inst, raw_inst, dt, audible, 0.035, 8.5)
        self.agc_stereo = _update_agc(self.agc_stereo, max(left_rms, right_rms), dt, audible, 0.035, 4.5)

        flux_acc = 0.0
        for i in range(8):
            self.agc_bands[i] = _update_agc(self.agc_bands[i], float(raw_bands[i]), dt, audible, 0.038 if i <= 2 else 0.028, 8.0)
            b_denom = max(0.025, self.track_ref * 0.45 + self.agc_bands[i] * 0.55)
            norm_b = _clamp(float(raw_bands[i]) / b_denom, 0.0, 1.0) if audible else 0.0
            diff = norm_b - self.prev_band_norm[i]
            if diff > 0.0:
                flux_acc += diff
            self.prev_band_norm[i] = norm_b
            self.smooth_bands[i] = _smooth(self.smooth_bands[i], norm_b, dt, 0.020, 0.10)

        raw_flux = _clamp(flux_acc / 2.2, 0.0, 1.0) if (audible and not self.first_frame) else 0.0
        self.smooth_flux = _smooth(self.smooth_flux, raw_flux, dt, 0.012, 0.070)
        self.smooth_flatness = _smooth(self.smooth_flatness, raw_flatness if audible else 0.0, dt, 0.025, 0.090)
        self.smooth_coherence = _smooth(self.smooth_coherence, raw_coherence if audible else 0.0, dt, 0.030, 0.100)
        self.smooth_crest = _smooth(self.smooth_crest, raw_crest if audible else 0.0, dt, 0.020, 0.090)
        self.smooth_high_res = _smooth(self.smooth_high_res, raw_high_res if audible else 0.0, dt, 0.018, 0.085)

        denom_bass = max(0.03, self.track_ref * 0.52 + self.agc_bass * 0.48)
        denom_mid = max(0.03, self.track_ref * 0.52 + self.agc_mid * 0.48)
        denom_treble = max(0.025, self.track_ref * 0.48 + self.agc_treble * 0.52)
        denom_vocal = max(0.025, self.mid_vocal_ref * 0.42 + self.agc_vocal * 0.58)
        denom_inst = max(0.03, self.track_ref * 0.50 + self.agc_inst * 0.50)

        norm_bass = _clamp(raw_bass / denom_bass, 0.0, 1.0) if audible else 0.0
        norm_mid = _clamp(raw_mid / denom_mid, 0.0, 1.0) if audible else 0.0
        norm_treble = _clamp(raw_treble / denom_treble, 0.0, 1.0) if audible else 0.0
        norm_vw = _clamp(raw_vox_warm / denom_vocal, 0.0, 1.0) * vox_clarity if audible else 0.0
        norm_va = _clamp(raw_vox_air / denom_vocal, 0.0, 1.0) * vox_clarity if audible else 0.0
        norm_inst = _clamp(raw_inst / denom_inst, 0.0, 1.0) if audible else 0.0
        norm_side = _clamp(in_band_side_rms / max(0.025, self.mid_vocal_ref * 0.50 + self.agc_inst * 0.50), 0.0, 1.0) if audible else 0.0
        target_level = _clamp(max(norm_bass * 0.50 + norm_mid * 0.30 + norm_treble * 0.25, norm_bass, norm_mid * 0.90), 0.0, 1.0) if audible else 0.0

        self.bass_floor = _lerp_exp(self.bass_floor, norm_bass, dt, 0.75)
        self.level_floor = _lerp_exp(self.level_floor, target_level, dt, 0.80)

        self.smooth_bass = _smooth(self.smooth_bass, norm_bass, dt, 0.024, 0.12)
        self.smooth_mid = _smooth(self.smooth_mid, norm_mid, dt, 0.028, 0.13)
        self.smooth_treble = _smooth(self.smooth_treble, norm_treble, dt, 0.022, 0.11)
        self.smooth_left = _smooth(self.smooth_left, _clamp(left_rms / self.agc_stereo, 0.0, 1.0) if audible else 0.0, dt, 0.028, 0.13)
        self.smooth_right = _smooth(self.smooth_right, _clamp(right_rms / self.agc_stereo, 0.0, 1.0) if audible else 0.0, dt, 0.028, 0.13)
        self.smooth_level = _smooth(self.smooth_level, target_level, dt, 0.028, 0.13)
        self.smooth_energy = _smooth(self.smooth_energy, target_level, dt, 0.45, 1.20)

        if audible:
            next_c = _clamp(centroid if self.first_frame else _lerp_exp(self.smooth_centroid, centroid, dt, 0.05), 0.0, 1.0)
            raw_vel = 0.0 if self.first_frame else _clamp((next_c - self.prev_centroid) / max(0.015, dt) * 0.25, -1.0, 1.0)
            self.smooth_centroid = next_c
            self.prev_centroid = next_c
            self.smooth_centroid_vel = _lerp_exp(self.smooth_centroid_vel, raw_vel, dt, 0.06)
        else:
            self.smooth_centroid_vel = _lerp_exp(self.smooth_centroid_vel, 0.0, dt, 0.06)

        bleed = norm_side * 0.22 + 0.04
        clean_warm = max(0.0, norm_vw - bleed)
        clean_air = max(0.0, norm_va - bleed * 0.90)
        clean_vox = max(clean_warm, clean_air * 0.92)
        self.smooth_vocal_warm = _smooth(self.smooth_vocal_warm, clean_warm, dt, 0.020, 0.085)
        self.smooth_vocal_air = _smooth(self.smooth_vocal_air, clean_air, dt, 0.018, 0.080)
        self.smooth_vocal = _smooth(self.smooth_vocal, clean_vox, dt, 0.020, 0.085)
        self.smooth_inst = _smooth(self.smooth_inst, norm_inst, dt, 0.028, 0.12)

        avg_alpha = 1.0 - math.exp(-dt / 0.35)
        bass_thresh = max(0.012, self.bass_avg * 1.38)
        if audible and raw_bass > bass_thresh and (self.t_sec - self.last_beat_t) >= 0.16:
            r = raw_bass / bass_thresh
            self.beat_pulse = 1.0 if (self.first_frame or r >= 1.55) else _clamp(0.42 + (r - 1.0) * 1.05, 0.42, 1.0)
            self.last_beat_t = self.t_sec
        else:
            self.beat_pulse *= math.exp(-dt / 0.11)
            if self.beat_pulse < 0.005:
                self.beat_pulse = 0.0
        self.bass_avg += (raw_bass - self.bass_avg) * avg_alpha

        bass_excess = _clamp((norm_bass - self.bass_floor * 0.72) * 2.1, 0.0, 1.0)
        self.smooth_bass_punch = _smooth(self.smooth_bass_punch, max(self.beat_pulse, bass_excess), dt, 0.018, 0.10)

        lvl_lift = max(0.0, target_level - self.level_floor * 0.75)
        tgt_dyn = _clamp(self.smooth_level * 0.42 + lvl_lift * 1.35 + self.beat_pulse * 0.42, 0.0, 1.0) if audible else 0.0
        self.smooth_dynamic = _smooth(self.smooth_dynamic, tgt_dyn, dt, 0.024, 0.12)

        tr_alpha = 1.0 - math.exp(-dt / 0.24)
        tr_thresh = max(0.009, self.treble_avg * 1.36)
        if audible and raw_treble > tr_thresh and (self.t_sec - self.last_treble_t) >= 0.09:
            self.treble_transient = 1.0
            self.last_treble_t = self.t_sec
        else:
            self.treble_transient *= math.exp(-dt / 0.085)
            if self.treble_transient < 0.005:
                self.treble_transient = 0.0
        self.treble_avg += (raw_treble - self.treble_avg) * tr_alpha

        self.first_frame = False

        feat = np.array([
            self.smooth_bands[0],
            self.smooth_bands[1],
            self.smooth_bands[2],
            self.smooth_bands[3],
            self.smooth_bands[4],
            self.smooth_bands[5],
            self.smooth_bands[6],
            self.smooth_bands[7],
            self.smooth_level,
            self.smooth_bass,
            self.smooth_mid,
            self.smooth_treble,
            self.beat_pulse,
            self.treble_transient,
            self.smooth_centroid,
            self.smooth_energy,
            self.smooth_vocal,
            self.smooth_vocal_warm,
            self.smooth_vocal_air,
            self.smooth_inst,
            self.smooth_bass_punch,
            self.smooth_dynamic,
            self.smooth_flatness,
            self.smooth_flux,
            self.smooth_coherence,
            self.smooth_high_res,
            self.smooth_crest,
            _clamp(self.smooth_centroid_vel * 0.5 + 0.5, 0.0, 1.0),
        ], dtype=np.float32)
        return np.clip(feat, 0.0, 1.0)


# ---- Realistic stem & game-SFX scenario synthesis ----

def _pan_stereo(mono: np.ndarray, pan: float) -> Tuple[np.ndarray, np.ndarray]:
    # Constant-power pan in [-1, 1]
    theta = (pan + 1.0) * 0.25 * math.pi
    return mono * math.cos(theta), mono * math.sin(theta)


def _synth_formant_voice(
    rng: np.random.Generator,
    n_samples: int,
    f0_base: float,
    syllable_hz: float,
    speaking: bool = False,
) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
    t = np.arange(n_samples, dtype=np.float64) / SAMPLE_RATE
    # Pitch contour + vibrato
    vib = 0.018 * np.sin(2.0 * np.pi * (5.3 + rng.uniform(-0.8, 0.8)) * t)
    f0 = f0_base * (1.0 + 0.14 * np.sin(2.0 * np.pi * 0.8 * t + rng.uniform(0, 6)) + vib)
    if speaking:
        f0 *= 1.0 + 0.18 * np.sin(2.0 * np.pi * 2.1 * t)
    phase = np.cumsum(2.0 * np.pi * f0 / SAMPLE_RATE)

    # Rich glottal harmonic stack (1..12 harmonics) shaped by dynamic vowel formants
    f1 = 550.0 + 220.0 * np.sin(2.0 * np.pi * syllable_hz * 0.7 * t)
    f2 = 1550.0 + 620.0 * np.cos(2.0 * np.pi * syllable_hz * 0.5 * t)
    f3 = 2650.0 + 250.0 * np.sin(2.0 * np.pi * 1.1 * t)
    sig = np.zeros(n_samples, dtype=np.float64)
    for h in range(1, 13):
        hf = h * f0
        roll = 1.0 / (h ** 0.85)
        g1 = np.exp(-0.5 * ((hf - f1) / 140.0) ** 2)
        g2 = 0.75 * np.exp(-0.5 * ((hf - f2) / 210.0) ** 2)
        g3 = 0.42 * np.exp(-0.5 * ((hf - f3) / 300.0) ** 2)
        sig += np.sin(h * phase) * roll * (0.20 + g1 + g2 + g3)

    # Syllabic amplitude envelope with crisp pauses / breaths
    syl_wave = np.sin(2.0 * np.pi * syllable_hz * t + rng.uniform(0, 6))
    if speaking:
        env = np.clip((syl_wave - 0.12) / 0.88, 0.0, 1.0) ** 1.3
        # Periodic sentence pauses
        env *= np.where((t % 1.8) < 1.25, 1.0, 0.0)
    else:
        env = np.clip((syl_wave + 0.35) / 1.35, 0.0, 1.0) ** 1.1
        # Add breathy sibilance at syllable transitions
        sib = rng.normal(0.0, 0.04, n_samples) * np.exp(-8.0 * np.abs(syl_wave))
        sig += sib

    sig = sig / (np.max(np.abs(sig)) + 1e-6) * env * rng.uniform(0.28, 0.55)
    # Mostly centered + subtle stereo room spread
    reverb = np.roll(sig, 640) * 0.08
    left = sig * 0.94 + reverb
    right = sig * 0.94 - reverb * 0.75
    return left, right, env


def _synth_drums(rng: np.random.Generator, n_samples: int, bpm: float) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    t = np.arange(n_samples, dtype=np.float64) / SAMPLE_RATE
    step_sec = 60.0 / bpm / 2.0  # 8th-note grid
    left = np.zeros(n_samples, dtype=np.float64)
    right = np.zeros(n_samples, dtype=np.float64)
    bass_env = np.zeros(n_samples, dtype=np.float64)
    drum_env = np.zeros(n_samples, dtype=np.float64)

    n_steps = int(n_samples / SAMPLE_RATE / step_sec) + 1
    for s in range(n_steps):
        idx0 = int(s * step_sec * SAMPLE_RATE)
        if idx0 >= n_samples:
            break
        # Kick on even 8ths
        if s % 2 == 0:
            dur = min(int(0.22 * SAMPLE_RATE), n_samples - idx0)
            kt = np.arange(dur, dtype=np.float64) / SAMPLE_RATE
            kf = 48.0 + 115.0 * np.exp(-kt * 26.0)
            k = np.sin(np.cumsum(2.0 * np.pi * kf / SAMPLE_RATE)) * np.exp(-kt * 11.0) * rng.uniform(0.45, 0.75)
            left[idx0 : idx0 + dur] += k
            right[idx0 : idx0 + dur] += k
            bass_env[idx0 : idx0 + dur] = np.maximum(bass_env[idx0 : idx0 + dur], np.exp(-kt * 9.0))
            drum_env[idx0 : idx0 + dur] = np.maximum(drum_env[idx0 : idx0 + dur], np.exp(-kt * 12.0))
        # Snare on 2 and 4
        if s % 4 == 2:
            dur = min(int(0.14 * SAMPLE_RATE), n_samples - idx0)
            st = np.arange(dur, dtype=np.float64) / SAMPLE_RATE
            sn = (rng.normal(0.0, 0.4, dur) + 0.35 * np.sin(2.0 * np.pi * 195.0 * st)) * np.exp(-st * 20.0)
            left[idx0 : idx0 + dur] += sn * 0.8
            right[idx0 : idx0 + dur] += sn * 0.85
            drum_env[idx0 : idx0 + dur] = np.maximum(drum_env[idx0 : idx0 + dur], np.exp(-st * 16.0))
        # Closed hi-hat
        if s % 1 == 0:
            dur = min(int(0.045 * SAMPLE_RATE), n_samples - idx0)
            ht = np.arange(dur, dtype=np.float64) / SAMPLE_RATE
            hat = rng.normal(0.0, 0.14, dur) * np.sin(2.0 * np.pi * 8200.0 * ht) * np.exp(-ht * 55.0)
            pan = rng.uniform(-0.4, 0.4)
            hl, hr = _pan_stereo(hat, pan)
            left[idx0 : idx0 + dur] += hl
            right[idx0 : idx0 + dur] += hr
            drum_env[idx0 : idx0 + dur] = np.maximum(drum_env[idx0 : idx0 + dur], np.exp(-ht * 35.0) * 0.55)

    return left, right, bass_env, drum_env


def _synth_instruments(rng: np.random.Generator, n_samples: int, bass_follow_bpm: float) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    t = np.arange(n_samples, dtype=np.float64) / SAMPLE_RATE
    left = np.zeros(n_samples, dtype=np.float64)
    right = np.zeros(n_samples, dtype=np.float64)
    # Continuous bassline
    root_f = rng.choice([45.0, 55.0, 62.0, 73.0])
    bass_line = np.sin(2.0 * np.pi * root_f * t) * 0.32 + 0.14 * np.sin(2.0 * np.pi * root_f * 2.0 * t)
    bass_mod = 0.45 + 0.55 * (0.5 + 0.5 * np.sin(2.0 * np.pi * (bass_follow_bpm / 60.0) * 0.5 * t))
    bass_sig = bass_line * bass_mod * rng.uniform(0.4, 0.85)
    left += bass_sig
    right += bass_sig

    # Tricky wide OR centered guitar/piano/synth harmonic chords & arpeggios (Vocal MUST stay 0!)
    chord_freqs = [220.0, 277.18, 329.63, 440.0, 659.25]
    arp_rate = rng.uniform(2.0, 5.0)
    pluck = np.exp(-((t * arp_rate) % 1.0) * 6.5)
    inst_l = np.zeros(n_samples, dtype=np.float64)
    inst_r = np.zeros(n_samples, dtype=np.float64)
    for k, cf in enumerate(chord_freqs):
        detune = 1.0 + rng.uniform(-0.008, 0.008)
        w = np.sin(2.0 * np.pi * cf * detune * t) + 0.45 * np.sin(2.0 * np.pi * cf * 2.0 * t) + 0.20 * np.sin(2.0 * np.pi * cf * 3.0 * t)
        pan = rng.choice([-0.8, 0.8, -0.45, 0.45, 0.05])
        wl, wr = _pan_stereo(w * (0.10 if k > 2 else 0.15) * pluck, pan)
        inst_l += wl
        inst_r += wr

    inst_env = np.clip(np.abs(inst_l) + np.abs(inst_r), 0.0, 1.0)
    inst_env = np.minimum(1.0, inst_env * 2.4)
    left += inst_l
    right += inst_r
    return left, right, np.clip(bass_mod * 0.75, 0.0, 1.0), inst_env


def _synth_scenario(rng: np.random.Generator, kind: str, duration_sec: float = 3.5) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
    n = int(duration_sec * SAMPLE_RATE)
    t = np.arange(n, dtype=np.float64) / SAMPLE_RATE
    left = np.zeros(n, dtype=np.float64)
    right = np.zeros(n, dtype=np.float64)
    targets = np.zeros((n, HEAD_COUNT), dtype=np.float32)  # per-sample targets aggregated per frame

    if kind == "silence":
        noise = rng.normal(0.0, 0.0004, n)
        return noise, noise.copy(), targets

    if kind == "pure_instrumental":
        bpm = rng.uniform(85.0, 145.0)
        dl, dr, b_env, d_env = _synth_drums(rng, n, bpm)
        il, ir, ib_env, i_env = _synth_instruments(rng, n, bpm)
        left = dl + il
        right = dr + ir
        targets[:, 1] = np.maximum(b_env, ib_env).astype(np.float32)
        targets[:, 2] = d_env.astype(np.float32)
        targets[:, 3] = i_env.astype(np.float32)

    elif kind == "solo_vocal":
        f0 = rng.uniform(115.0, 310.0)
        vl, vr, venv = _synth_formant_voice(rng, n, f0, rng.uniform(2.5, 5.2), speaking=False)
        left, right = vl, vr
        targets[:, 0] = np.clip(venv * 1.15, 0.0, 1.0).astype(np.float32)

    elif kind == "vocal_plus_music":
        bpm = rng.uniform(88.0, 138.0)
        dl, dr, b_env, d_env = _synth_drums(rng, n, bpm)
        il, ir, ib_env, i_env = _synth_instruments(rng, n, bpm)
        f0 = rng.uniform(125.0, 300.0)
        vl, vr, venv = _synth_formant_voice(rng, n, f0, rng.uniform(2.8, 5.0), speaking=False)
        mix_scale = rng.uniform(0.45, 0.85)
        left = (dl + il) * mix_scale + vl * 1.15
        right = (dr + ir) * mix_scale + vr * 1.15
        targets[:, 0] = np.clip(venv * 1.15, 0.0, 1.0).astype(np.float32)
        targets[:, 1] = np.maximum(b_env, ib_env).astype(np.float32) * mix_scale
        targets[:, 2] = d_env.astype(np.float32) * mix_scale
        targets[:, 3] = i_env.astype(np.float32) * mix_scale

    elif kind == "gunshot":
        bed = rng.normal(0.0, 0.008, n)
        left += bed
        right += bed
        shots = [0.25, 0.95, 1.45, 1.72, 2.55]
        for s_t in shots:
            idx0 = int(s_t * SAMPLE_RATE)
            dur = min(int(0.18 * SAMPLE_RATE), n - idx0)
            gt = np.arange(dur, dtype=np.float64) / SAMPLE_RATE
            crack = rng.normal(0.0, 0.90, dur) * np.exp(-gt * 58.0)
            thump = np.sin(2.0 * np.pi * (140.0 * np.exp(-gt * 18.0)) * gt) * np.exp(-gt * 26.0) * 0.55
            shot = np.clip(crack + thump, -1.0, 1.0)
            sl, sr_c = _pan_stereo(shot, rng.uniform(-0.35, 0.45))
            left[idx0 : idx0 + dur] += sl
            right[idx0 : idx0 + dur] += sr_c
            targets[idx0 : idx0 + dur, 4] = np.maximum(targets[idx0 : idx0 + dur, 4], np.exp(-gt * 28.0).astype(np.float32))

    elif kind == "explosion":
        blasts = [0.35, 1.90]
        for b_t in blasts:
            idx0 = int(b_t * SAMPLE_RATE)
            dur = min(int(1.10 * SAMPLE_RATE), n - idx0)
            et = np.arange(dur, dtype=np.float64) / SAMPLE_RATE
            sub = np.sin(2.0 * np.pi * (48.0 * np.exp(-et * 1.8) + 22.0) * et) * np.exp(-et * 3.2) * 0.85
            debris = rng.normal(0.0, 0.55, dur) * np.exp(-et * 4.5)
            blast = np.clip(sub + debris, -1.0, 1.0)
            bl, br = _pan_stereo(blast, rng.uniform(-0.7, 0.7))
            left[idx0 : idx0 + dur] += bl
            right[idx0 : idx0 + dur] += br
            env = np.exp(-et * 3.0).astype(np.float32)
            targets[idx0 : idx0 + dur, 5] = np.maximum(targets[idx0 : idx0 + dur, 5], env)
            targets[idx0 : idx0 + dur, 1] = np.maximum(targets[idx0 : idx0 + dur, 1], env * 0.7)

    elif kind == "gravel":
        # Rough broadband crackling gravel + rolling low chassis rumble
        raw_n = rng.normal(0.0, 0.28, n)
        # Random micro-pebble impacts every ~15-40 ms
        impulses = (rng.random(n) > 0.996).astype(np.float64) * rng.uniform(0.35, 0.9, n)
        crackle = raw_n + np.convolve(impulses, np.exp(-np.arange(180) / 35.0), mode="same")
        chassis = np.sin(2.0 * np.pi * rng.uniform(62.0, 98.0) * t) * 0.22
        env = np.where((t >= 0.2) & (t <= 3.1), 1.0, 0.0)
        pan = rng.uniform(-0.75, 0.75)
        gl, gr = _pan_stereo((crackle * 0.65 + chassis) * env, pan)
        left, right = gl, gr
        targets[:, 6] = (env * 0.90).astype(np.float32)

    elif kind == "engine":
        rpm_f = rng.uniform(65.0, 130.0) + 55.0 * np.sin(2.0 * np.pi * 0.35 * t)
        ph = np.cumsum(2.0 * np.pi * rpm_f / SAMPLE_RATE)
        eng = 0.42 * np.sin(ph) + 0.30 * np.sin(2.0 * ph) + 0.18 * np.sin(3.0 * ph) + 0.10 * np.sin(4.0 * ph)
        env = np.where((t >= 0.15) & (t <= 3.2), 1.0, 0.0)
        left = eng * env * 0.65
        right = eng * env * 0.65
        targets[:, 7] = (env * 0.90).astype(np.float32)

    elif kind == "boost_surge":
        # Rising turbine whine + sub-bass swell
        engine_base = 0.28 * np.sin(2.0 * np.pi * 95.0 * t)
        boost_mask = np.where((t >= 0.5) & (t <= 2.3), 1.0, 0.0)
        local_t = np.clip((t - 0.5) / 1.6, 0.0, 1.0)
        whine_f = 550.0 + 3400.0 * local_t
        whine_ph = np.cumsum(2.0 * np.pi * whine_f / SAMPLE_RATE)
        whine = (0.35 * np.sin(whine_ph) + 0.16 * np.sin(2.0 * whine_ph)) * boost_mask
        sub_thrust = 0.45 * np.sin(2.0 * np.pi * 52.0 * t) * boost_mask
        sig = engine_base + whine + sub_thrust
        left = sig * 0.8
        right = sig * 0.8
        targets[:, 7] = 0.45
        targets[:, 8] = (boost_mask * 0.92).astype(np.float32)

    elif kind == "tire_screech":
        f1 = rng.uniform(2100.0, 3400.0)
        f2 = f1 * rng.uniform(1.18, 1.32)
        env = np.where((t >= 0.25) & (t <= 2.9), 1.0, 0.0) * (0.7 + 0.3 * np.sin(2.0 * np.pi * 7.0 * t))
        squeal = (0.38 * np.sin(2.0 * np.pi * f1 * t) + 0.28 * np.sin(2.0 * np.pi * f2 * t)) * env
        sl, sr_c = _pan_stereo(squeal, rng.uniform(-0.5, 0.5))
        left = sl + rng.normal(0.0, 0.03, n)
        right = sr_c + rng.normal(0.0, 0.03, n)
        targets[:, 9] = (np.where((t >= 0.25) & (t <= 2.9), 0.92, 0.0)).astype(np.float32)

    elif kind == "melee_clash":
        hits = [0.30, 1.15, 2.10, 2.85]
        for h_t in hits:
            idx0 = int(h_t * SAMPLE_RATE)
            dur = min(int(0.32 * SAMPLE_RATE), n - idx0)
            mt = np.arange(dur, dtype=np.float64) / SAMPLE_RATE
            clash = (
                0.35 * np.sin(2.0 * np.pi * 1680.0 * mt)
                + 0.32 * np.sin(2.0 * np.pi * 2540.0 * mt)
                + 0.24 * np.sin(2.0 * np.pi * 3690.0 * mt)
                + rng.normal(0.0, 0.25, dur) * np.exp(-mt * 40.0)
            ) * np.exp(-mt * 11.0)
            ml, mr = _pan_stereo(clash, rng.uniform(-0.4, 0.4))
            left[idx0 : idx0 + dur] += ml
            right[idx0 : idx0 + dur] += mr
            targets[idx0 : idx0 + dur, 10] = np.maximum(targets[idx0 : idx0 + dur, 10], np.exp(-mt * 9.0).astype(np.float32))

    elif kind == "dialogue":
        f0 = rng.uniform(105.0, 245.0)
        vl, vr, venv = _synth_formant_voice(rng, n, f0, rng.uniform(3.2, 5.5), speaking=True)
        left = vl + rng.normal(0.0, 0.004, n)
        right = vr + rng.normal(0.0, 0.004, n)
        targets[:, 11] = np.clip(venv * 1.10, 0.0, 1.0).astype(np.float32)

    return np.clip(left, -1.0, 1.0), np.clip(right, -1.0, 1.0), targets


def build_dataset(seed: int, clips_per_kind: int) -> Tuple[np.ndarray, np.ndarray]:
    rng = np.random.default_rng(seed)
    kinds = [
        "silence",
        "pure_instrumental",
        "pure_instrumental",
        "solo_vocal",
        "vocal_plus_music",
        "vocal_plus_music",
        "gunshot",
        "explosion",
        "gravel",
        "engine",
        "boost_surge",
        "tire_screech",
        "melee_clash",
        "dialogue",
    ]
    all_x: List[np.ndarray] = []
    all_y: List[np.ndarray] = []

    for kind in kinds:
        for _ in range(clips_per_kind):
            left, right, per_sample_y = _synth_scenario(rng, kind, duration_sec=3.2)
            analyzer = BitExactSpectrumAnalyzer()
            n_frames = len(left) // FRAME_HOP
            # Skip first 3 warm-up frames so ring buffer is populated
            for fi in range(n_frames):
                i0 = fi * FRAME_HOP
                i1 = i0 + FRAME_HOP
                feat = analyzer.process_chunk(left[i0:i1], right[i0:i1])
                y_frame = np.max(per_sample_y[i0:i1], axis=0)
                if fi >= 3:
                    all_x.append(feat)
                    all_y.append(y_frame)

    return np.stack(all_x).astype(np.float32), np.stack(all_y).astype(np.float32)


class HapticsMlp(nn.Module):
    def __init__(self) -> None:
        super().__init__()
        self.fc1 = nn.Linear(FEATURE_COUNT, HIDDEN_UNITS)
        self.act = nn.ReLU()
        self.fc2 = nn.Linear(HIDDEN_UNITS, HEAD_COUNT)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.fc2(self.act(self.fc1(x)))


def main() -> int:
    torch.manual_seed(42)
    print("Generating synthetic 48 kHz training and held-out validation corpora...")
    train_x, train_y = build_dataset(seed=20261124, clips_per_kind=10)
    val_x, val_y = build_dataset(seed=99887766, clips_per_kind=4)
    print(f"Dataset shapes -> Train: {train_x.shape}, Val: {val_x.shape}")

    model = HapticsMlp()
    opt = torch.optim.AdamW(model.parameters(), lr=3.5e-3, weight_decay=2e-4)
    bce = nn.BCEWithLogitsLoss()
    mse = nn.MSELoss()

    tx = torch.from_numpy(train_x)
    ty = torch.from_numpy(train_y)
    vx = torch.from_numpy(val_x)
    vy = torch.from_numpy(val_y)

    batch_size = 512
    n_train = len(tx)
    best_loss = float("inf")
    best_state: Dict[str, torch.Tensor] = {}

    for epoch in range(1, 91):
        model.train()
        perm = torch.randperm(n_train)
        for start in range(0, n_train, batch_size):
            idx = perm[start : start + batch_size]
            logits = model(tx[idx])
            probs = torch.sigmoid(logits)
            # Combine BCE and MSE for calibrated continuous probability envelopes
            loss = 0.65 * bce(logits, ty[idx]) + 0.35 * mse(probs, ty[idx])
            opt.zero_grad()
            loss.backward()
            opt.step()

        model.eval()
        with torch.no_grad():
            vlogits = model(vx)
            vprobs = torch.sigmoid(vlogits)
            vloss = float((0.65 * bce(vlogits, vy) + 0.35 * mse(vprobs, vy)).item())
            if vloss < best_loss:
                best_loss = vloss
                best_state = {k: v.detach().clone() for k, v in model.state_dict().items()}

    model.load_state_dict(best_state)
    model.eval()

    with torch.no_grad():
        preds = torch.sigmoid(model(vx)).numpy()

    print(f"Best held-out loss: {best_loss:.4f}")
    for i, name in enumerate(HEAD_NAMES):
        pos = val_y[:, i] > 0.45
        neg = val_y[:, i] < 0.05
        pos_mean = float(preds[pos, i].mean()) if np.any(pos) else 0.0
        neg_mean = float(preds[neg, i].mean()) if np.any(neg) else 0.0
        mae = float(np.mean(np.abs(preds[:, i] - val_y[:, i])))
        print(f"  {name:<15} MAE={mae:.3f}  PosMean={pos_mean:.3f}  NegMean={neg_mean:.3f}")

    # Export clean compact JSON for embedded C# zero-alloc inference
    weights_obj = {
        "version": 1,
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
    print(f"Exported weights to {out_path} ({out_path.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

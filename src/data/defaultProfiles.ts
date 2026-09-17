import { Profile, KeyName, StickDirection } from '../types/gamepad';

export const DEFAULT_KEY_REMAPS: Record<KeyName, KeyName> = {
  DPAD_UP: 'DPAD_UP',
  DPAD_DOWN: 'DPAD_DOWN',
  DPAD_LEFT: 'DPAD_LEFT',
  DPAD_RIGHT: 'DPAD_RIGHT',
  A: 'A',
  B: 'B',
  X: 'X',
  Y: 'Y',
  LB: 'LB',
  RB: 'RB',
  LT: 'LT',
  RT: 'RT',
  L3: 'L3',
  R3: 'R3',
  SELECT: 'SELECT',
  START: 'START',
  CAPTURE: 'CAPTURE',
  TURBO: 'TURBO',
};

export const DEFAULT_CURVE_CONFIG = {
  innerDeadzone: 5,
  outerDeadzone: 98,
  p1: { x: 33, y: 33 },
  p2: { x: 66, y: 66 },
  preset: 'linear' as const,
};

export const DEFAULT_PROFILES: Profile[] = [
  {
    id: 'default-stock',
    name: 'Standard X20',
    createdAt: 1710000000000,
    remaps: { ...DEFAULT_KEY_REMAPS },
    macros: {
      M1: [
        { id: '1', buttons: ['A'], leftStick: StickDirection.NEUTRAL, rightStick: StickDirection.NEUTRAL, durationMs: 40, intervalMs: 20 },
        { id: '2', buttons: ['B'], leftStick: StickDirection.NEUTRAL, rightStick: StickDirection.NEUTRAL, durationMs: 40, intervalMs: 20 },
      ],
      M2: [
        { id: '3', buttons: ['LT', 'RT'], leftStick: StickDirection.NEUTRAL, rightStick: StickDirection.NEUTRAL, durationMs: 60, intervalMs: 30 },
      ],
      M3: [],
      M4: [],
    },
    vibration: 70,
    idleTimeoutMinutes: 10,
    stickCurves: {
      left: { ...DEFAULT_CURVE_CONFIG },
      right: { ...DEFAULT_CURVE_CONFIG },
    },
    triggerCurves: {
      left: { ...DEFAULT_CURVE_CONFIG },
      right: { ...DEFAULT_CURVE_CONFIG },
    },
  },
  {
    id: 'competitive-fps',
    name: 'FPS Fast Triggers',
    createdAt: 1710100000000,
    remaps: {
      ...DEFAULT_KEY_REMAPS,
      L3: 'LB',
      R3: 'RB',
    },
    macros: {
      M1: [{ id: 'm1-1', buttons: ['A'], leftStick: StickDirection.NEUTRAL, rightStick: StickDirection.NEUTRAL, durationMs: 30, intervalMs: 15 }],
      M2: [{ id: 'm2-1', buttons: ['B'], leftStick: StickDirection.NEUTRAL, rightStick: StickDirection.NEUTRAL, durationMs: 30, intervalMs: 15 }],
      M3: [{ id: 'm3-1', buttons: ['X'], leftStick: StickDirection.NEUTRAL, rightStick: StickDirection.NEUTRAL, durationMs: 30, intervalMs: 15 }],
      M4: [{ id: 'm4-1', buttons: ['Y'], leftStick: StickDirection.NEUTRAL, rightStick: StickDirection.NEUTRAL, durationMs: 30, intervalMs: 15 }],
    },
    vibration: 35,
    idleTimeoutMinutes: 15,
    stickCurves: {
      left: { innerDeadzone: 3, outerDeadzone: 99, p1: { x: 25, y: 35 }, p2: { x: 70, y: 80 }, preset: 'aggressive' },
      right: { innerDeadzone: 2, outerDeadzone: 100, p1: { x: 20, y: 40 }, p2: { x: 65, y: 85 }, preset: 'aggressive' },
    },
    triggerCurves: {
      left: { innerDeadzone: 1, outerDeadzone: 70, p1: { x: 10, y: 80 }, p2: { x: 40, y: 100 }, preset: 'instant' },
      right: { innerDeadzone: 1, outerDeadzone: 70, p1: { x: 10, y: 80 }, p2: { x: 40, y: 100 }, preset: 'instant' },
    },
  },
  {
    id: 'fighting-special',
    name: 'Fighting Game Combo',
    createdAt: 1710200000000,
    remaps: { ...DEFAULT_KEY_REMAPS },
    macros: {
      M1: [
        { id: 'f-1', buttons: ['DPAD_DOWN'], leftStick: StickDirection.DOWN, rightStick: StickDirection.NEUTRAL, durationMs: 35, intervalMs: 16 },
        { id: 'f-2', buttons: ['DPAD_DOWN', 'DPAD_RIGHT'], leftStick: StickDirection.DOWN_RIGHT, rightStick: StickDirection.NEUTRAL, durationMs: 35, intervalMs: 16 },
        { id: 'f-3', buttons: ['DPAD_RIGHT'], leftStick: StickDirection.RIGHT, rightStick: StickDirection.NEUTRAL, durationMs: 35, intervalMs: 16 },
        { id: 'f-4', buttons: ['X'], leftStick: StickDirection.NEUTRAL, rightStick: StickDirection.NEUTRAL, durationMs: 45, intervalMs: 20 },
      ],
      M2: [],
      M3: [],
      M4: [],
    },
    vibration: 80,
    idleTimeoutMinutes: 30,
    stickCurves: {
      left: { ...DEFAULT_CURVE_CONFIG },
      right: { ...DEFAULT_CURVE_CONFIG },
    },
    triggerCurves: {
      left: { ...DEFAULT_CURVE_CONFIG },
      right: { ...DEFAULT_CURVE_CONFIG },
    },
  },
];

const STORAGE_KEY = 'x20ctl_profiles_v1';
const ACTIVE_PROFILE_KEY = 'x20ctl_active_profile_id';

export function loadProfiles(): Profile[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      if (Array.isArray(parsed) && parsed.length > 0) {
        return parsed;
      }
    }
  } catch (err) {
    console.error('Failed to load profiles from local storage', err);
  }
  return DEFAULT_PROFILES;
}

export function saveProfiles(profiles: Profile[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(profiles));
  } catch (err) {
    console.error('Failed to save profiles to local storage', err);
  }
}

export function getActiveProfileId(): string {
  try {
    return localStorage.getItem(ACTIVE_PROFILE_KEY) || DEFAULT_PROFILES[0].id;
  } catch {
    return DEFAULT_PROFILES[0].id;
  }
}

export function setActiveProfileId(id: string): void {
  try {
    localStorage.setItem(ACTIVE_PROFILE_KEY, id);
  } catch {}
}

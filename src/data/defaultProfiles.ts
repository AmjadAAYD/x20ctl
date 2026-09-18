import { Profile, KeyName } from "../types/gamepad";

export const DEFAULT_KEY_REMAPS = Object.fromEntries(
  [
    "A",
    "B",
    "X",
    "Y",
    "LB",
    "RB",
    "LT",
    "RT",
    "L3",
    "R3",
    "SELECT",
    "START",
    "DPAD_UP",
    "DPAD_DOWN",
    "DPAD_LEFT",
    "DPAD_RIGHT",
  ].map((key) => [key, key]),
) as Record<KeyName, KeyName>;

export const DEFAULT_CURVE_CONFIG = {
  innerDeadzone: 0,
  outerDeadzone: 100,
  p1: { x: 100 / 3, y: 100 / 3 },
  p2: { x: 200 / 3, y: 200 / 3 },
  preset: "linear" as const,
};

// An offline editing template, never presented as the controller's settings.
export function newProfile(name = "Untitled setup"): Profile {
  return {
    schemaVersion: 2,
    id: crypto.randomUUID(),
    name,
    createdAt: Date.now(),
    remaps: { ...DEFAULT_KEY_REMAPS },
    macros: { M1: [], M2: [], M3: [], M4: [] },
    macroLoops: { M1: 0, M2: 0, M3: 0, M4: 0 },
    vibration: 70,
    idleTimeoutMinutes: 10,
    stickCurves: {
      left: structuredClone(DEFAULT_CURVE_CONFIG),
      right: structuredClone(DEFAULT_CURVE_CONFIG),
    },
    triggerCurves: {
      left: structuredClone(DEFAULT_CURVE_CONFIG),
      right: structuredClone(DEFAULT_CURVE_CONFIG),
    },
  };
}

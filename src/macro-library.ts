import type { MacroStep, KeyName } from "./types/gamepad";

// The existing X20 editor format. Preview models use it for local drafts only.
export const MACRO_ENTRY_LIMIT = 47;
export const MACRO_INPUTS: KeyName[] = [
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
  "DPAD_UP",
  "DPAD_DOWN",
  "DPAD_LEFT",
  "DPAD_RIGHT",
  "SELECT",
  "START",
];
export interface SavedMacro {
  id: string;
  format: "x20ctl-macro";
  version: 1;
  name: string;
  sourceModel: string;
  loopMs: number;
  steps: MacroStep[];
}

export function countWireEntries(steps: MacroStep[]) {
  return steps.reduce(
    (count, step) => count + 1 + (step.intervalMs > 0 ? 1 : 0),
    0,
  );
}

export function validateMacroSteps(value: unknown): MacroStep[] {
  if (
    !Array.isArray(value) ||
    !value.length ||
    value.length > MACRO_ENTRY_LIMIT
  )
    throw new Error("Choose a nonempty sequence with at most 47 entries.");
  const steps = value.map((raw) => {
    if (!raw || typeof raw !== "object") throw new Error("Invalid macro step.");
    if (
      Object.keys(raw).some(
        (key) =>
          ![
            "id",
            "buttons",
            "leftStick",
            "rightStick",
            "durationMs",
            "intervalMs",
          ].includes(key),
      )
    )
      throw new Error("This step contains unsupported actions.");
    const step = raw as MacroStep;
    if (
      !Array.isArray(step.buttons) ||
      step.buttons.some((key) => !MACRO_INPUTS.includes(key)) ||
      new Set(step.buttons).size !== step.buttons.length
    )
      throw new Error("Unsupported or repeated macro input.");
    for (const direction of [step.leftStick, step.rightStick])
      if (!Number.isInteger(direction) || direction < 0 || direction > 8)
        throw new Error(
          "Stick directions must be neutral or one of eight headings.",
        );
    for (const [time, minimum] of [
      [step.durationMs, 5],
      [step.intervalMs, 0],
    ])
      if (
        !Number.isInteger(time) ||
        time < minimum ||
        time > 327675 ||
        time % 5 !== 0
      )
        throw new Error(
          "Macro timing must use 5 ms increments within the supported range.",
        );
    return {
      id: typeof step.id === "string" ? step.id : "",
      buttons: [...step.buttons],
      leftStick: step.leftStick,
      rightStick: step.rightStick,
      durationMs: step.durationMs,
      intervalMs: step.intervalMs,
    };
  });
  if (countWireEntries(steps) > MACRO_ENTRY_LIMIT)
    throw new Error(
      "This sequence exceeds 47 entries. Each nonzero pause uses another entry.",
    );
  return steps;
}

export function validateSavedMacro(value: unknown): SavedMacro {
  if (!value || typeof value !== "object")
    throw new Error("Invalid macro file.");
  const macro = value as SavedMacro;
  if (
    Object.keys(macro).some(
      (key) =>
        ![
          "id",
          "format",
          "version",
          "name",
          "sourceModel",
          "loopMs",
          "steps",
        ].includes(key),
    )
  )
    throw new Error("This file contains unsupported macro options.");
  if (macro.format !== "x20ctl-macro" || macro.version !== 1)
    throw new Error("Choose an X20CTL macro file (version 1).");
  if (
    typeof macro.name !== "string" ||
    !macro.name.trim() ||
    macro.name.length > 80
  )
    throw new Error("Macro names must contain 1–80 characters.");
  if (typeof macro.sourceModel !== "string" || !macro.sourceModel)
    throw new Error("Macro source model is missing.");
  if (
    !Number.isInteger(macro.loopMs) ||
    macro.loopMs < 0 ||
    macro.loopMs > 20475 ||
    macro.loopMs % 5 !== 0
  )
    throw new Error(
      "Loop interval must use 5 ms increments between 0 and 20475 ms.",
    );
  return {
    id: typeof macro.id === "string" ? macro.id : "",
    format: "x20ctl-macro",
    version: 1,
    name: macro.name.trim(),
    sourceModel: macro.sourceModel,
    loopMs: macro.loopMs,
    steps: validateMacroSteps(macro.steps),
  };
}

export function cloneMacroSteps(steps: MacroStep[]): MacroStep[] {
  return validateMacroSteps(steps).map((step) => ({
    ...step,
    id: crypto.randomUUID(),
    buttons: [...step.buttons],
  }));
}

export function setMacroPauses(
  steps: MacroStep[],
  pauseMs: number,
): MacroStep[] {
  if (
    !Number.isInteger(pauseMs) ||
    pauseMs < 0 ||
    pauseMs > 327675 ||
    pauseMs % 5 !== 0
  )
    throw new Error("Pauses must use 5 ms increments.");
  return validateMacroSteps(
    steps.map((step) => ({ ...step, intervalMs: pauseMs })),
  );
}

import lighting from "./assets/controllers/lighting.json";
import type { ControllerId } from "./controllers";

export const controllerSilhouette = (model: ControllerId, view: "front" | "back") => lighting.models[model][view];
export const LIGHTING_VIEWBOX = `0 0 ${lighting.sourceSize.join(" ")}`;
export const LIGHTING_CLIP_SCALE = `scale(${1 / lighting.sourceSize[0]} ${1 / lighting.sourceSize[1]})`;

/** UI-only speed/brightness: never a motor command or hardware capability. */
export function vibrationPresentation(value: number) {
  const strength = Number.isFinite(value) ? Math.max(0, Math.min(100, value)) : 0;
  const curve = lighting.speedCurve;
  const index = Math.max(1, curve.findIndex(([point]) => point >= strength));
  const [low, slow] = curve[index - 1];
  const [high, fast] = curve[index];
  return { strength, period: slow + (fast - slow) * (strength - low) / (high - low), opacity: .82 + .14 * Math.sqrt(strength / 100) };
}

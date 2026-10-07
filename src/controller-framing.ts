import type { CSSProperties } from "react";
import framing from "./assets/controllers/framing.json";
import type { ControllerId } from "./controllers";

/** Measured shell bounds exclude background padding. Scale the entire source
 * plane, including its controls and effects, without changing photo geometry. */
export function controllerFraming(model: ControllerId, view: "front" | "back"): CSSProperties {
  const [sourceWidth, sourceHeight] = framing.sourceSize;
  const [x, y, width, height] = framing.models[model][view];
  const scale = Math.min(framing.coverage * sourceWidth / width, framing.coverage * sourceHeight / height);
  return {
    width: `${scale * 100}%`,
    left: `${(0.5 - scale * ((x + width / 2) / sourceWidth - 0.5)) * 100}%`,
    top: `${(0.5 - scale * ((y + height / 2) / sourceHeight - 0.5)) * 100}%`,
  };
}

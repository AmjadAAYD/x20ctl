import motion from "./assets/controllers/trigger-motion.json" with { type: "json" };
import type { ControllerId } from "./controllers";

export type TriggerKey = "LT" | "RT";
export const triggerStrength = (value: number) => Number.isFinite(value) ? Math.max(0, Math.min(1, value)) : 0;

/** A downward press about the body-side shaft, with positive camera depth:
 * the free edge approaches the viewer rather than folding away. The shaft
 * stays stationary. Photo-derived angles are not hardware calibration. */
export function triggerPose(model: ControllerId, key: TriggerKey, value: number) {
  const press = triggerStrength(value);
  const { hinge, restAngle, travelAngle } = motion.models[model][key];
  const [[x, y], [endX, endY]] = hinge;
  const length = Math.hypot(endX - x, endY - y);
  const ux = (endX - x) / length, uy = (endY - y) / length;
  const angle = travelAngle * press;
  const projected = Math.cos((restAngle + angle) * Math.PI / 180) / Math.cos(restAngle * Math.PI / 180);
  const a = ux * ux + projected * uy * uy;
  const b = ux * uy * (1 - projected);
  const d = uy * uy + projected * ux * ux;
  const matrix = [a, b, b, d, x - a * x - b * y, y - b * x - d * y];
  // Both photo-space normals point down, despite the mirrored shaft ordering.
  const nx = key === "RT" ? -uy : uy, ny = key === "RT" ? ux : -ux;
  const depthPerPixel = (Math.sin((restAngle + angle) * Math.PI / 180) - Math.sin(restAngle * Math.PI / 180))
    / Math.cos(restAngle * Math.PI / 180);
  const depth = [-nx * depthPerPixel, -ny * depthPerPixel];
  const perspective = 750;
  // Translation is handled by CSS transform-origin, keeping the shaft fixed
  // at every rendered size. cqw scales camera distance with the image plane.
  const spatial = [a, b, depth[0], 0, b, d, depth[1], 0, 0, 0, 1, 0, 0, 0, 0, 1];
  return {
    press, angle, hinge, matrix, depth, perspective,
    transform: `matrix(${matrix.join(" ")})`,
    cssTransform: press === 0 ? "none" : `perspective(${perspective / 1536 * 100}cqw) matrix3d(${spatial.join(",")})`,
    transformOrigin: `${x / 1536 * 100}% ${y / 1024 * 100}%`,
  };
}

import type { ControllerId } from "./controllers";

export interface HapticsGeometry {
  viewBox: [number, number, number, number];
  contours: Record<string, string>;
}

const geometries = import.meta.glob("./assets/controllers/*/haptics-geometry.json", {
  eager: true, import: "default",
}) as Record<string, HapticsGeometry>;

/** Contours are traced in photo coordinates and share the artwork's image plane. */
export const controllerHaptics = (model: ControllerId) =>
  geometries[`./assets/controllers/${model}/haptics-geometry.json`];

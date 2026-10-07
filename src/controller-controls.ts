import shapes from "./assets/controllers/control-shapes.json";
import type { ControllerId } from "./controllers";
import type { KeyName } from "./types/gamepad";

export interface ControlShape {
  bounds: number[];
  path: string;
  transform?: string;
}
type ModelControls = { front: Partial<Record<KeyName, ControlShape>>; back: Partial<Record<KeyName, ControlShape>>; macros: Record<string, ControlShape> };
export const controlShapes = (model: ControllerId) => shapes.models[model] as ModelControls;
export const CONTROL_SOURCE_SIZE = shapes.sourceSize;
export const isRearControl = (key: KeyName) => ["LB", "LT", "RB", "RT"].includes(key);

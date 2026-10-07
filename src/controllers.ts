import catalog from "../x20ctl/controllers/catalog.json";
import type { KeyName } from "./types/gamepad";

export type ControllerId = "x20" | "x20_pro" | "x05" | "x05_pro" | "x10" | "d10" | "x15";
export type Point = { x: number; y: number };
export interface ControllerProfile {
  id: ControllerId;
  name: string;
  macroSlots: string[];
  backend: string | null;
  visible: boolean;
  placeholder: boolean;
  softwareControl: string;
  availability?: "configuration" | "preview" | "input_experimental" | "unavailable";
  inputBackend?: string | null;
  connectionModes?: string[];
  macroPlacement?: string;
  sources?: string[];
  hardware: {
    rgb: boolean | null;
    vibration: boolean;
    triggerHaptics: boolean;
    motion: boolean | null;
    display: boolean;
    sticks: string;
    triggers: string;
  };
  visual: {
    asset: string;
    baseAsset: string | null;
    aspect: number;
    assetAspect?: number;
    artworkBounds?: Point & { w: number; h: number };
    viewLabel?: string;
    sticks: {
      left: Point & { radius: number; cap: (Point & { w: number; h: number }) | null };
      right: Point & { radius: number; cap: (Point & { w: number; h: number }) | null };
    };
    buttons: Partial<Record<KeyName, Point>>;
    motors: (Point & { id: string; kind: string })[];
    rgb: (Point & { w: number; h: number })[];
    screen:
      | (Point & {
          w: number;
          h: number;
          interactive: boolean;
          customContent: boolean;
        })
      | null;
  };
}
export const controllers = catalog as ControllerProfile[];
export const controller = (id: ControllerId) =>
  controllers.find((item) => item.id === id)!;
export const unavailableController = (id: ControllerId) => controller(id).availability === "unavailable";
const assets = import.meta.glob("./assets/controllers/*/*.{png,jpg,webp}", {
  eager: true,
  import: "default",
}) as Record<string, string>;
export const controllerImage = (profile: ControllerProfile) =>
  assets[`./assets/controllers/${profile.visual.asset}`];
export const controllerBaseImage = (profile: ControllerProfile) =>
  profile.visual.baseAsset
    ? assets[`./assets/controllers/${profile.visual.baseAsset}`]
    : controllerImage(profile);

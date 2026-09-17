// Protocol and Gamepad types for x20ctl

export enum KeyCode {
  DPAD_UP = 0x0001,
  DPAD_DOWN = 0x0002,
  DPAD_LEFT = 0x0004,
  DPAD_RIGHT = 0x0008,
  A = 0x0010,
  B = 0x0020,
  X = 0x0040,
  Y = 0x0080,
  LB = 0x0100,
  RB = 0x0200,
  LT = 0x0400,
  RT = 0x0800,
  L3 = 0x1000,
  R3 = 0x2000,
  SELECT = 0x4000,
  START = 0x8000,
  CAPTURE = 0x010000,
  TURBO = 0x020000,
}

export type KeyName =
  | 'DPAD_UP'
  | 'DPAD_DOWN'
  | 'DPAD_LEFT'
  | 'DPAD_RIGHT'
  | 'A'
  | 'B'
  | 'X'
  | 'Y'
  | 'LB'
  | 'RB'
  | 'LT'
  | 'RT'
  | 'L3'
  | 'R3'
  | 'SELECT'
  | 'START'
  | 'CAPTURE'
  | 'TURBO';

export const KEY_LABELS: Record<KeyName, string> = {
  DPAD_UP: 'D-pad Up',
  DPAD_DOWN: 'D-pad Down',
  DPAD_LEFT: 'D-pad Left',
  DPAD_RIGHT: 'D-pad Right',
  A: 'A',
  B: 'B',
  X: 'X',
  Y: 'Y',
  LB: 'LB',
  RB: 'RB',
  LT: 'LT',
  RT: 'RT',
  L3: 'L3 (Left Stick)',
  R3: 'R3 (Right Stick)',
  SELECT: 'Select / Back',
  START: 'Start',
  CAPTURE: 'C (Capture)',
  TURBO: 'T (Turbo)',
};

export enum StickDirection {
  NEUTRAL = 0,
  UP = 1,
  UP_RIGHT = 2,
  RIGHT = 3,
  DOWN_RIGHT = 4,
  DOWN = 5,
  DOWN_LEFT = 6,
  LEFT = 7,
  UP_LEFT = 8,
}

export const STICK_DIRECTION_NAMES: Record<StickDirection, string> = {
  [StickDirection.NEUTRAL]: 'Neutral',
  [StickDirection.UP]: 'Up',
  [StickDirection.UP_RIGHT]: 'Up-Right',
  [StickDirection.RIGHT]: 'Right',
  [StickDirection.DOWN_RIGHT]: 'Down-Right',
  [StickDirection.DOWN]: 'Down',
  [StickDirection.DOWN_LEFT]: 'Down-Left',
  [StickDirection.LEFT]: 'Left',
  [StickDirection.UP_LEFT]: 'Up-Left',
};

export interface MacroStep {
  id: string;
  buttons: KeyName[];
  leftStick: StickDirection;
  rightStick: StickDirection;
  durationMs: number;
  intervalMs: number;
}

export interface CurveConfig {
  innerDeadzone: number; // 0 - 50
  outerDeadzone: number; // 50 - 100
  p1: { x: number; y: number }; // 0 - 100
  p2: { x: number; y: number }; // 0 - 100
  preset: 'linear' | 'aggressive' | 'relaxed' | 'instant' | 'custom';
}

export interface Profile {
  id: string;
  name: string;
  createdAt: number;
  remaps: Record<KeyName, KeyName>;
  macros: {
    M1: MacroStep[];
    M2: MacroStep[];
    M3: MacroStep[];
    M4: MacroStep[];
  };
  vibration: number; // 0 - 100
  idleTimeoutMinutes: number; // 5, 10, 15, 30, 0
  stickCurves: {
    left: CurveConfig;
    right: CurveConfig;
  };
  triggerCurves: {
    left: CurveConfig;
    right: CurveConfig;
  };
}

export type ConnectionType = 'dongle' | 'bluetooth' | 'wired';

export type ControllerPreset = 'x20-pro-black' | 'x20-pro-white' | 'x05';

export interface ControllerPresetInfo {
  id: ControllerPreset;
  name: string;
  badge: string;
  description: string;
}

export const CONTROLLER_PRESETS: ControllerPresetInfo[] = [
  {
    id: 'x20-pro-black',
    name: 'X20 Pro Black',
    badge: 'Smoked Polycarbonate',
    description: 'Translucent smoked faceplate, CNC knurled collars, OLED screen',
  },
  {
    id: 'x20-pro-white',
    name: 'X20 Pro White',
    badge: 'Frost White',
    description: 'Frosted translucent shell, CNC knurled collars, light grey grips',
  },
  {
    id: 'x05',
    name: 'X05',
    badge: 'Stealth Black',
    description: 'Matte black body, top rainbow RGB lightbar, RGB halo D-pad',
  },
];

export interface ControllerSlot {
  slot: number; // 0 - 3 (P1 - P4)
  connected: boolean;
  name: string; // Default or custom name
  customName?: string;
  batteryLevel: number; // 1 to 4 bars (EasySMX hardware indicator)
  isCharging: boolean;
  firmwareVersion: string;
  hardwareType: string;
  connectionMode: ConnectionType;
  activeProfileId: string;
}

export interface LiveGamepadState {
  buttons: Record<KeyName, boolean>;
  rawButtons?: Record<KeyName, boolean>;
  leftStick: { x: number; y: number };
  rightStick: { x: number; y: number };
  leftTrigger: number; // 0.0 - 1.0
  rightTrigger: number; // 0.0 - 1.0
  pollingRateHz: number;
  peakPollingRateHz?: number;
  avgPollingRateHz?: number;
  packetIntervalMs?: number;
  jitterMs?: number;
  packetCount?: number;
  trail: { left: Array<{ x: number; y: number }>; right: Array<{ x: number; y: number }> };
}

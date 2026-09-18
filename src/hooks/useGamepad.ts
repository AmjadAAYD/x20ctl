import { useEffect, useState } from "react";
import { KeyName, LiveGamepadState } from "../types/gamepad";
import { request } from "../native";

export const emptyInput = (): LiveGamepadState => ({
  buttons: {} as Record<KeyName, boolean>,
  leftStick: { x: 0, y: 0 },
  rightStick: { x: 0, y: 0 },
  leftTrigger: 0,
  rightTrigger: 0,
  pollingRateHz: 0,
  trail: { left: [], right: [] },
});

export function useGamepad(enabled: boolean, onDisconnect: () => void) {
  const [liveState, setLiveState] = useState(emptyInput);
  const [hardwareDetected, setHardwareDetected] = useState(false);
  const [slot, setSlot] = useState<number | null>(null);
  useEffect(() => {
    if (!enabled) return;
    let stopped = false;
    let timer: ReturnType<typeof setTimeout>;
    async function poll() {
      try {
        const data = await request<{
          connected: boolean;
          input: null | {
            slot: number;
            buttons: KeyName[];
            leftStick: { x: number; y: number };
            rightStick: { x: number; y: number };
            leftTrigger: number;
            rightTrigger: number;
          };
        }>("input");
        if (stopped) return;
        if (!data.connected) onDisconnect();
        setHardwareDetected(!!data.input);
        setSlot(data.input?.slot ?? null);
        const input = data.input;
        setLiveState((previous) =>
          input
            ? {
                ...emptyInput(),
                ...input,
                buttons: Object.fromEntries(
                  input.buttons.map((key) => [key, true]),
                ) as Record<KeyName, boolean>,
                trail: {
                  left: [...previous.trail.left, input.leftStick].slice(-25),
                  right: [...previous.trail.right, input.rightStick].slice(-25),
                },
              }
            : emptyInput(),
        );
      } catch {
        if (!stopped) {
          setHardwareDetected(false);
          setLiveState(emptyInput());
          onDisconnect();
        }
      }
      if (!stopped) timer = setTimeout(poll, 33);
    }
    void poll();
    return () => {
      stopped = true;
      clearTimeout(timer);
    };
  }, [enabled, onDisconnect]);
  return { liveState, hardwareDetected, slot };
}

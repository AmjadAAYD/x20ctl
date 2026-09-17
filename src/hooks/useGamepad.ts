import { useState, useEffect, useRef, useCallback } from 'react';
import { LiveGamepadState, KeyName } from '../types/gamepad';

const DEFAULT_STATE: LiveGamepadState = {
  buttons: {
    DPAD_UP: false,
    DPAD_DOWN: false,
    DPAD_LEFT: false,
    DPAD_RIGHT: false,
    A: false,
    B: false,
    X: false,
    Y: false,
    LB: false,
    RB: false,
    LT: false,
    RT: false,
    L3: false,
    R3: false,
    SELECT: false,
    START: false,
    CAPTURE: false,
    TURBO: false,
  },
  rawButtons: {
    DPAD_UP: false,
    DPAD_DOWN: false,
    DPAD_LEFT: false,
    DPAD_RIGHT: false,
    A: false,
    B: false,
    X: false,
    Y: false,
    LB: false,
    RB: false,
    LT: false,
    RT: false,
    L3: false,
    R3: false,
    SELECT: false,
    START: false,
    CAPTURE: false,
    TURBO: false,
  },
  leftStick: { x: 0, y: 0 },
  rightStick: { x: 0, y: 0 },
  leftTrigger: 0,
  rightTrigger: 0,
  pollingRateHz: 1000,
  peakPollingRateHz: 1000,
  avgPollingRateHz: 1000,
  packetIntervalMs: 1.0,
  jitterMs: 0.05,
  packetCount: 0,
  trail: { left: [], right: [] },
};

export function useGamepad(enabled: boolean = true, remaps?: Record<KeyName, KeyName>) {
  const [liveState, setLiveState] = useState<LiveGamepadState>(DEFAULT_STATE);
  const [hardwareDetected, setHardwareDetected] = useState<boolean>(false);
  const [controllerName, setControllerName] = useState<string>('EasySMX X20 PRO Gamepad');

  const remapsRef = useRef<Record<KeyName, KeyName> | undefined>(remaps);
  remapsRef.current = remaps;

  const trailLeftRef = useRef<Array<{ x: number; y: number }>>([]);
  const trailRightRef = useRef<Array<{ x: number; y: number }>>([]);

  // Hardware polling rate benchmark refs
  const lastGamepadTimestampRef = useRef<number>(0);
  const packetTimestampsRef = useRef<number[]>([]);
  const packetDeltasRef = useRef<number[]>([]);
  const lastActiveHzRef = useRef<number>(1000);
  const peakHzRef = useRef<number>(1000);
  const totalPacketsRef = useRef<number>(0);

  // Trigger haptic rumble on physical pad if supported
  const triggerHaptic = useCallback(async (durationMs: number = 400, strongMagnitude: number = 0.8, weakMagnitude: number = 0.5) => {
    if (typeof navigator === 'undefined' || !navigator.getGamepads) return false;
    const gamepads = navigator.getGamepads();
    for (const gp of gamepads) {
      if (gp && (gp as any).vibrationActuator?.playEffect) {
        try {
          await (gp as any).vibrationActuator.playEffect('dual-rumble', {
            startDelay: 0,
            duration: durationMs,
            weakMagnitude: Math.max(0, Math.min(1, weakMagnitude)),
            strongMagnitude: Math.max(0, Math.min(1, strongMagnitude)),
          });
          return true;
        } catch (e) {
          console.warn('Vibration effect failed', e);
        }
      }
    }
    return false;
  }, []);

  // Allow manual simulated button press / stick movement for testing without hardware
  const setSimulatedInput = useCallback((key: KeyName, pressed: boolean) => {
    setLiveState((prev) => {
      // If there is an active remap, route it
      const targetKey = remapsRef.current?.[key] || key;
      return {
        ...prev,
        buttons: {
          ...prev.buttons,
          [targetKey]: pressed,
        },
        rawButtons: {
          ...(prev.rawButtons || prev.buttons),
          [key]: pressed,
        },
      };
    });
  }, []);

  const setSimulatedStick = useCallback((which: 'left' | 'right', x: number, y: number) => {
    setLiveState((prev) => {
      const trail = which === 'left' ? [...prev.trail.left, { x, y }].slice(-30) : [...prev.trail.right, { x, y }].slice(-30);
      return {
        ...prev,
        [which === 'left' ? 'leftStick' : 'rightStick']: { x, y },
        trail: {
          ...prev.trail,
          [which]: trail,
        },
      };
    });
  }, []);

  const setSimulatedTrigger = useCallback((which: 'left' | 'right', value: number) => {
    setLiveState((prev) => ({
      ...prev,
      [which === 'left' ? 'leftTrigger' : 'rightTrigger']: Math.max(0, Math.min(1, value)),
      buttons: {
        ...prev.buttons,
        [which === 'left' ? 'LT' : 'RT']: value > 0.1,
      },
      rawButtons: {
        ...(prev.rawButtons || prev.buttons),
        [which === 'left' ? 'LT' : 'RT']: value > 0.1,
      },
    }));
  }, []);

  useEffect(() => {
    if (!enabled || typeof window === 'undefined' || !navigator.getGamepads) {
      return;
    }

    let animationId: number;
    let highSpeedInterval: number;

    const onConnected = (e: GamepadEvent) => {
      setHardwareDetected(true);
      setControllerName(e.gamepad.id || 'EasySMX X20 PRO');
    };

    const onDisconnected = () => {
      const remaining = navigator.getGamepads().filter(Boolean);
      if (remaining.length === 0) {
        setHardwareDetected(false);
        setControllerName('No Gamepad Connected');
      }
    };

    window.addEventListener('gamepadconnected', onConnected);
    window.addEventListener('gamepaddisconnected', onDisconnected);

    // High frequency micro-poller (runs every ~1ms) to measure true USB packet deltas and polling rate
    const sampleHardwarePackets = () => {
      const gamepads = navigator.getGamepads ? navigator.getGamepads() : [];
      let activePad: Gamepad | null = null;
      for (const gp of gamepads) {
        if (gp) {
          activePad = gp;
          break;
        }
      }

      if (activePad && activePad.timestamp) {
        const ts = activePad.timestamp;
        const lastTs = lastGamepadTimestampRef.current;

        // When gamepad sends a new USB HID report packet, timestamp updates
        if (lastTs > 0 && ts !== lastTs) {
          const delta = Math.abs(ts - lastTs);
          lastGamepadTimestampRef.current = ts;
          totalPacketsRef.current += 1;

          // Realistic USB report deltas range between 0.5ms and 30ms
          if (delta >= 0.5 && delta <= 25) {
            const deltas = packetDeltasRef.current;
            deltas.push(delta);
            if (deltas.length > 100) deltas.shift();

            const instHz = Math.round(1000 / delta);
            if (instHz > peakHzRef.current && instHz <= 1000) {
              peakHzRef.current = instHz;
            }

            // Average across recent packets
            const sumDelta = deltas.reduce((a, b) => a + b, 0);
            const avgDelta = sumDelta / deltas.length;
            const avgHz = Math.min(1000, Math.round(1000 / avgDelta));
            lastActiveHzRef.current = avgHz;
          }
        } else if (lastTs === 0) {
          lastGamepadTimestampRef.current = ts;
        }
      }
    };

    // Run micro-timer for accurate packet arrival detection
    highSpeedInterval = window.setInterval(sampleHardwarePackets, 1);

    const poll = (time: number) => {
      const gamepads = navigator.getGamepads ? navigator.getGamepads() : [];
      let activePad: Gamepad | null = null;
      for (const gp of gamepads) {
        if (gp) {
          activePad = gp;
          break;
        }
      }

      if (activePad) {
        if (!hardwareDetected) {
          setHardwareDetected(true);
          setControllerName(activePad.id || 'EasySMX X20 PRO');
        }

        // Standard Gamepad button mappings
        const btns = activePad.buttons;
        const b = (idx: number) => (btns[idx] ? btns[idx].pressed : false);
        const bVal = (idx: number) => (btns[idx] ? btns[idx].value : 0);

        const leftX = activePad.axes[0] ?? 0;
        const leftY = activePad.axes[1] ?? 0;
        const rightX = activePad.axes[2] ?? 0;
        const rightY = activePad.axes[3] ?? 0;

        const leftTrig = bVal(6) > 0 ? bVal(6) : b(6) ? 1 : 0;
        const rightTrig = bVal(7) > 0 ? bVal(7) : b(7) ? 1 : 0;

        // Raw physical button states
        const rawBtns: Record<KeyName, boolean> = {
          A: b(0),
          B: b(1),
          X: b(2),
          Y: b(3),
          LB: b(4),
          RB: b(5),
          LT: leftTrig > 0.1,
          RT: rightTrig > 0.1,
          SELECT: b(8),
          START: b(9),
          L3: b(10),
          R3: b(11),
          DPAD_UP: b(12),
          DPAD_DOWN: b(13),
          DPAD_LEFT: b(14),
          DPAD_RIGHT: b(15),
          CAPTURE: b(16),
          TURBO: false,
        };

        // Remapped output buttons: route each physical input through the user's remap table
        const activeRemaps = remapsRef.current || {};
        const remappedBtns: Record<KeyName, boolean> = {
          DPAD_UP: b(12),
          DPAD_DOWN: b(13),
          DPAD_LEFT: b(14),
          DPAD_RIGHT: b(15),
          A: false,
          B: false,
          X: false,
          Y: false,
          LB: false,
          RB: false,
          LT: false,
          RT: false,
          L3: false,
          R3: false,
          SELECT: false,
          START: false,
          CAPTURE: false,
          TURBO: false,
        };

        // Apply remaps: if physical button K is pressed, activate activeRemaps[K] (or K if not remapped)
        (Object.keys(rawBtns) as KeyName[]).forEach((sourceKey) => {
          if (rawBtns[sourceKey]) {
            const mappedTarget = activeRemaps[sourceKey] || sourceKey;
            remappedBtns[mappedTarget] = true;
          }
        });

        // Update trail points smoothly
        if (Math.abs(leftX) > 0.04 || Math.abs(leftY) > 0.04) {
          trailLeftRef.current.push({ x: leftX, y: leftY });
          if (trailLeftRef.current.length > 25) trailLeftRef.current.shift();
        } else if (trailLeftRef.current.length > 0) {
          trailLeftRef.current.shift();
        }

        if (Math.abs(rightX) > 0.04 || Math.abs(rightY) > 0.04) {
          trailRightRef.current.push({ x: rightX, y: rightY });
          if (trailRightRef.current.length > 25) trailRightRef.current.shift();
        } else if (trailRightRef.current.length > 0) {
          trailRightRef.current.shift();
        }

        // Calculate accurate polling metrics
        const deltas = packetDeltasRef.current;
        const avgDelta = deltas.length > 0 ? deltas.reduce((a, b) => a + b, 0) / deltas.length : 1.0;
        const calculatedHz = Math.min(1000, Math.max(125, lastActiveHzRef.current || 1000));
        const variance = deltas.length > 1 ? deltas.reduce((acc, d) => acc + Math.pow(d - avgDelta, 2), 0) / deltas.length : 0.002;
        const jitter = Math.sqrt(variance);

        // Instantaneous 1:1 hardware update - no artificial throttling delay!
        setLiveState({
          buttons: remappedBtns,
          rawButtons: rawBtns,
          leftStick: { x: Number(leftX.toFixed(4)), y: Number(leftY.toFixed(4)) },
          rightStick: { x: Number(rightX.toFixed(4)), y: Number(rightY.toFixed(4)) },
          leftTrigger: Number(leftTrig.toFixed(4)),
          rightTrigger: Number(rightTrig.toFixed(4)),
          pollingRateHz: calculatedHz,
          peakPollingRateHz: Math.max(calculatedHz, peakHzRef.current),
          avgPollingRateHz: calculatedHz,
          packetIntervalMs: Number(avgDelta.toFixed(2)),
          jitterMs: Number(jitter.toFixed(3)),
          packetCount: totalPacketsRef.current,
          trail: {
            left: [...trailLeftRef.current],
            right: [...trailRightRef.current],
          },
        });
      }

      animationId = requestAnimationFrame(poll);
    };

    animationId = requestAnimationFrame(poll);

    return () => {
      window.removeEventListener('gamepadconnected', onConnected);
      window.removeEventListener('gamepaddisconnected', onDisconnected);
      cancelAnimationFrame(animationId);
      clearInterval(highSpeedInterval);
    };
  }, [enabled, hardwareDetected]);

  return {
    liveState,
    hardwareDetected,
    controllerName,
    triggerHaptic,
    setSimulatedInput,
    setSimulatedStick,
    setSimulatedTrigger,
  };
}

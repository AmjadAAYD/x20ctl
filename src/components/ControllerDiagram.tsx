import { ControllerCanvas } from "./ControllerCanvas";
import type { KeyName, LiveGamepadState, ControllerPreset } from "../types/gamepad";
export function ControllerDiagram({ liveState, onButtonClick, selectedKey, className = "" }: {
  liveState: LiveGamepadState; onButtonClick?: (key: KeyName) => void;
  selectedKey?: KeyName | null; className?: string; themeAccent?: string; preset?: ControllerPreset;
}) {
  return <ControllerCanvas className={className} buttons={liveState.buttons} leftStick={liveState.leftStick} rightStick={liveState.rightStick} onSelect={onButtonClick} selectedKey={selectedKey} />;
}

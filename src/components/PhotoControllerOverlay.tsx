import { useId, type KeyboardEvent, type PointerEvent } from "react";
import { pointerStickVector } from "../controller-preview";
import { KEY_LABELS, type KeyName } from "../types/gamepad";
import "./photo-controller.css";

type Vector = { x: number; y: number };
type StickKey = "L3" | "R3";

interface PhotoControllerOverlayProps {
  selectedKey: KeyName;
  pressedButtons: Partial<Record<KeyName, boolean>>;
  leftStick: Vector;
  rightStick: Vector;
  previewEnabled: boolean;
  onSelect: (key: KeyName) => void;
  onStickPreview: (key: StickKey, value: Vector | null) => void;
}

const faceButtons = [
  { key: "Y", x: 525, y: 99, color: "#f8d82a" },
  { key: "X", x: 478, y: 143, color: "#23bfff" },
  { key: "B", x: 571, y: 144, color: "#ff515b" },
  { key: "A", x: 526, y: 185, color: "#3ff493" },
] as const;

const dpadButtons = [
  { key: "DPAD_UP", x: 248, y: 198 },
  { key: "DPAD_LEFT", x: 216, y: 228 },
  { key: "DPAD_RIGHT", x: 280, y: 228 },
  { key: "DPAD_DOWN", x: 248, y: 258 },
] as const;

const shoulderButtons = [
  { key: "LT", x: 166, y: 33 },
  { key: "LB", x: 135, y: 53 },
  { key: "RT", x: 535, y: 33 },
  { key: "RB", x: 565, y: 53 },
] as const;

export function PhotoControllerOverlay({
  selectedKey,
  pressedButtons,
  leftStick,
  rightStick,
  previewEnabled,
  onSelect,
  onStickPreview,
}: PhotoControllerOverlayProps) {
  const gradientId = useId().replace(/:/g, "");

  function selectWithKeyboard(event: KeyboardEvent<SVGGElement>, key: KeyName) {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      onSelect(key);
    }
  }

  function moveStick(event: PointerEvent<SVGGElement>, key: StickKey, center: Vector) {
    if (!previewEnabled || !(event.buttons & 1)) return;
    const svg = event.currentTarget.ownerSVGElement;
    if (!svg) return;
    const bounds = svg.getBoundingClientRect();
    const scale = Math.min(bounds.width / 700, bounds.height / 455);
    const x = (event.clientX - bounds.left - (bounds.width - 700 * scale) / 2) / scale;
    const y = (event.clientY - bounds.top - (bounds.height - 455 * scale) / 2) / scale;
    onStickPreview(key, pointerStickVector(x, y, center.x, center.y, 42));
  }

  function stick(key: StickKey, center: Vector, value: Vector) {
    const selected = selectedKey === key;
    const pressed = pressedButtons[key];
    return (
      <g
        key={key}
        className={`photo-stick-control${selected ? " is-selected" : ""}${pressed ? " is-pressed" : ""}`}
        role="button"
        tabIndex={0}
        aria-label={`Select ${KEY_LABELS[key]}`}
        aria-pressed={selected}
        transform={`translate(${center.x} ${center.y})`}
        onClick={() => onSelect(key)}
        onKeyDown={(event) => selectWithKeyboard(event, key)}
        onPointerDown={(event) => {
          onSelect(key);
          if (previewEnabled) {
            event.currentTarget.setPointerCapture(event.pointerId);
            moveStick(event, key, center);
          }
        }}
        onPointerMove={(event) => moveStick(event, key, center)}
        onPointerUp={() => onStickPreview(key, null)}
        onPointerCancel={() => onStickPreview(key, null)}
        onLostPointerCapture={() => onStickPreview(key, null)}
      >
        <circle className="photo-stick-hit" r="54" />
        <circle className="photo-stick-base" r="43" fill={`url(#${gradientId}-base)`} />
        <circle className="photo-stick-ring" r="48" />
        <g transform={`translate(${value.x * 17} ${value.y * 17})`}>
          <circle className="photo-stick-cap-shadow" r="36" />
          <circle className="photo-stick-cap" r="33" fill={`url(#${gradientId}-cap)`} />
          <circle className="photo-stick-cap-sheen" r="28" />
          <circle className="photo-stick-cap-center" r="21" />
        </g>
      </g>
    );
  }

  return (
    <svg
      className="photo-control-overlay"
      viewBox="0 0 700 455"
      role="group"
      aria-label="Interactive X20 controller controls"
      data-left-x={leftStick.x.toFixed(3)}
      data-left-y={leftStick.y.toFixed(3)}
      data-right-x={rightStick.x.toFixed(3)}
      data-right-y={rightStick.y.toFixed(3)}
    >
      <defs>
        <radialGradient id={`${gradientId}-base`} cx="50%" cy="50%" r="75%">
          <stop offset="0" stopColor="#161e29" />
          <stop offset=".7" stopColor="#080d16" />
          <stop offset="1" stopColor="#2e3947" />
        </radialGradient>
        <radialGradient id={`${gradientId}-cap`} cx="38%" cy="28%" r="75%">
          <stop offset="0" stopColor="#424c5a" />
          <stop offset=".46" stopColor="#202936" />
          <stop offset="1" stopColor="#080d17" />
        </radialGradient>
      </defs>
      {shoulderButtons.map(({ key, x, y }) => {
        const selected = selectedKey === key;
        return (
          <g
            key={key}
            className={`photo-shoulder-control${selected ? " is-selected" : ""}${pressedButtons[key] ? " is-pressed" : ""}`}
            role="button"
            tabIndex={0}
            aria-label={`Select ${KEY_LABELS[key]}`}
            aria-pressed={selected}
            onClick={() => onSelect(key)}
            onKeyDown={(event) => selectWithKeyboard(event, key)}
          >
            <rect x={x - 27} y={y - 16} width="54" height="32" rx="12" />
          </g>
        );
      })}
      {dpadButtons.map(({ key, x, y }) => {
        const selected = selectedKey === key;
        return (
          <g
            key={key}
            className={`photo-dpad-control${selected ? " is-selected" : ""}${pressedButtons[key] ? " is-pressed" : ""}`}
            role="button"
            tabIndex={0}
            aria-label={`Select ${KEY_LABELS[key]}`}
            aria-pressed={selected}
            onClick={() => onSelect(key)}
            onKeyDown={(event) => selectWithKeyboard(event, key)}
          >
            <rect x={x - 15} y={y - 15} width="30" height="30" rx="7" />
          </g>
        );
      })}
      {faceButtons.map(({ key, x, y, color }) => {
        const selected = selectedKey === key;
        return (
          <g
            key={key}
            className={`photo-face-control${selected ? " is-selected" : ""}${pressedButtons[key] ? " is-pressed" : ""}`}
            role="button"
            tabIndex={0}
            aria-label={`Select ${KEY_LABELS[key]}`}
            aria-pressed={selected}
            style={{ color }}
            onClick={() => onSelect(key)}
            onKeyDown={(event) => selectWithKeyboard(event, key)}
          >
            <circle cx={x} cy={y} r="24" />
          </g>
        );
      })}
      {stick("L3", { x: 180, y: 134 }, leftStick)}
      {stick("R3", { x: 439, y: 229 }, rightStick)}
    </svg>
  );
}

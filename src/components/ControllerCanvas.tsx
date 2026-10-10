import { useId, type CSSProperties, type PointerEvent } from "react";
import { controllerHaptics } from "../controller-haptics";
import { controllerFraming } from "../controller-framing";
import { vibrationPresentation } from "../controller-lighting";
import { ControllerLighting } from "./ControllerLighting";
import { controlShapes } from "../controller-controls";
import { PhotoControl } from "./PhotoControl";
import {
  controller,
  controllerBaseImage,
  controllerImage,
  type ControllerId,
  type Point,
} from "../controllers";
import type { KeyName } from "../types/gamepad";
import { KEY_LABELS } from "../types/gamepad";
import "./controller-canvas.css";
import { ControllerOutline } from './ControllerOutline';

export interface ControllerCanvasProps {
  presentation?: 'outline' | 'photo';
  model?: ControllerId;
  buttons?: Partial<Record<KeyName, boolean>>;
  leftStick?: Point;
  rightStick?: Point;
  leftTrigger?: number;
  rightTrigger?: number;
  selectedKey?: KeyName | null;
  onSelect?: (key: KeyName) => void;
  onStickPreview?: (key: "L3" | "R3", value: Point | null) => void;
  previewEnabled?: boolean;
  motorPower?: number;
  disabled?: boolean;
  className?: string;
  framing?: "inset" | "detail";
  lighting?: "quiet" | "interactive" | "vibration";
  emphasis?: "default" | "selected" | "connected";
}
const position = (point: Point): CSSProperties => ({
  left: `${point.x * 100}%`,
  top: `${point.y * 100}%`,
});

export function ControllerCanvas({
  presentation = 'outline',
  model = "x20",
  buttons = {},
  leftStick = { x: 0, y: 0 },
  rightStick = { x: 0, y: 0 },
  leftTrigger = 0,
  rightTrigger = 0,
  selectedKey,
  onSelect,
  onStickPreview,
  previewEnabled = false,
  motorPower = 0,
  disabled = false,
  className = "",
  framing = "inset",
  lighting = "quiet",
  emphasis = "default",
}: ControllerCanvasProps) {
  const profile = controller(model);
  const haptics = controllerHaptics(model);
  const motorClipId = useId().replace(/:/g, "");
  const { strength, period, opacity } = vibrationPresentation(motorPower);
  const image = controllerImage(profile);
  const artworkBounds = profile.visual.artworkBounds;
  const planeAspect = artworkBounds
    ? (profile.visual.assetAspect ?? profile.visual.aspect) * artworkBounds.w / artworkBounds.h
    : profile.visual.aspect;
  const productPhotoStyle: CSSProperties | undefined = artworkBounds
    ? {
        left: `${-artworkBounds.x / artworkBounds.w * 100}%`,
        top: `${-artworkBounds.y / artworkBounds.h * 100}%`,
        width: `${100 / artworkBounds.w}%`,
        height: `${100 / artworkBounds.h}%`,
      }
    : undefined;
  const inactive = disabled;
  if (inactive) selectedKey = null;
  const move = (
    event: PointerEvent<HTMLButtonElement>,
    key: "L3" | "R3",
    center: Point,
    radius: number,
  ) => {
    if (!previewEnabled || inactive || !(event.buttons & 1)) return;
    const bounds = event.currentTarget.parentElement!.getBoundingClientRect();
    const x =
      (event.clientX - bounds.left - center.x * bounds.width) /
      (radius * bounds.width);
    const y =
      (event.clientY - bounds.top - center.y * bounds.height) /
      (radius * bounds.width);
    const length = Math.max(1, Math.hypot(x, y));
    onStickPreview?.(key, { x: x / length, y: y / length });
  };
  return (
    <div
      className={`controller-canvas ${className}`}
      data-model={model}
      data-framing={framing}
      data-lighting={lighting}
      data-emphasis={emphasis}
      data-preview={profile.placeholder}
      data-presentation={presentation}
      data-left-x={inactive ? "0.000" : leftStick.x.toFixed(3)}
      data-left-y={inactive ? "0.000" : leftStick.y.toFixed(3)}
      data-right-x={inactive ? "0.000" : rightStick.x.toFixed(3)}
      data-right-y={inactive ? "0.000" : rightStick.y.toFixed(3)}
      data-motor-power={motorPower}
      style={{ aspectRatio: profile.visual.aspect }}
      role="group"
      aria-label={`${profile.name} controller`}
    >
      <div
        className={`controller-image-plane ${artworkBounds ? "is-product-plane" : ""}`}
        style={{
          aspectRatio: planeAspect,
          width: `${84 * Math.min(1, planeAspect / profile.visual.aspect)}%`,
          ...(framing === "detail" ? controllerFraming(model, "front") : {}),
        }}
      >
        {presentation === 'outline' && <ControllerOutline model={model} buttons={inactive ? {} : buttons} leftStick={inactive ? { x: 0, y: 0 } : leftStick}
          rightStick={inactive ? { x: 0, y: 0 } : rightStick} selectedKey={selectedKey} leftTrigger={leftTrigger} rightTrigger={rightTrigger} />}
        <img
          className="controller-photo"
          src={disabled ? image : controllerBaseImage(profile)}
          data-art-layer={profile.visual.baseAsset ? (disabled ? "complete" : "stickless-base") : "product-photo"}
          style={{ ...productPhotoStyle, clipPath: `url(#${motorClipId}-shell)` }}
          alt={`Front view of EasySMX ${profile.name}`}
          draggable={false}
        />
        <img className="controller-photo controller-photo-ambient"
          src={disabled ? image : controllerBaseImage(profile)} style={productPhotoStyle}
          alt="" aria-hidden="true" draggable={false} />
        <ControllerLighting model={model} view="front" id={motorClipId} sweep={lighting === "interactive"} />
        {!disabled &&
          Object.entries(controlShapes(model).front).map(([name, shape]) => {
            const key = name as KeyName;
            return (
              <PhotoControl
                key={key}
                shape={shape!}
                disabled={inactive || !onSelect}
                className="controller-control"
                label={`Select ${KEY_LABELS[key]}`}
                selected={selectedKey === key}
                pressed={!!buttons[key] && !inactive}
                onSelect={onSelect ? () => onSelect(key) : undefined}
              />
            );
          })}
        {!disabled &&
          (["left", "right"] as const).map((side) => {
            const key = side === "left" ? "L3" : "R3";
            const center = profile.visual.sticks[side];
            const value = inactive
              ? { x: 0, y: 0 }
              : side === "left"
                ? leftStick
                : rightStick;
            // Crop coordinates sample the source texture only. Placement and
            // deflection use the independently measured center of the fixed ring.
            const cap = center.cap;
            const capStyle: CSSProperties | undefined = cap ? {
              left: "50%",
              top: "50%",
              width: `${(cap.w / (center.radius * 2)) * 100}%`,
              height: `${(cap.h / (center.radius * 2 * planeAspect)) * 100}%`,
              backgroundImage: `url(${image})`,
              backgroundSize: `${100 / cap.w}% ${100 / cap.h}%`,
              backgroundPosition: `${((cap.x - cap.w / 2) / (1 - cap.w)) * 100}% ${((cap.y - cap.h / 2) / (1 - cap.h)) * 100}%`,
              transform: `translate(${value.x * 18}%, ${value.y * 18}%)`,
            } : undefined;
            return (
              <button
                type="button"
                key={key}
                disabled={inactive || !onSelect}
                className={`controller-stick ${cap ? "" : "controller-stick-marker-target"} ${selectedKey === key ? "is-selected" : ""} ${buttons[key] && !inactive ? "is-pressed" : ""}`}
                style={{
                  ...position(center),
                  width: `${center.radius * 200}%`,
                  aspectRatio: 1,
                }}
                aria-label={`Select ${KEY_LABELS[key]}`}
                aria-pressed={selectedKey === key}
                onClick={() => onSelect?.(key)}
                onPointerDown={(event) => {
                  if (previewEnabled) {
                    event.currentTarget.setPointerCapture(event.pointerId);
                    move(event, key, center, center.radius);
                  }
                }}
                onPointerMove={(event) =>
                  move(event, key, center, center.radius)
                }
                onPointerUp={() => onStickPreview?.(key, null)}
                onPointerCancel={() => onStickPreview?.(key, null)}
                onLostPointerCapture={() => onStickPreview?.(key, null)}
              >
                {cap && presentation === 'photo' ? (
                  <span className="controller-stick-cap" style={capStyle} />
                ) : (value.x !== 0 || value.y !== 0) && (
                  <span
                    className="controller-stick-marker"
                    aria-hidden="true"
                    style={{ left: `${50 + value.x * 30}%`, top: `${50 + value.y * 30}%` }}
                  />
                )}
              </button>
            );
          })}
        {profile.visual.screen && (
          <span
            className="controller-screen"
            style={{
              ...position(profile.visual.screen),
              width: `${profile.visual.screen.w * 100}%`,
              height: `${profile.visual.screen.h * 100}%`,
            }}
            aria-label="On-controller display; custom content unverified"
          />
        )}
        {strength > 0 && haptics && <svg
          className="controller-haptics-overlay" viewBox={haptics.viewBox.join(" ")}
          preserveAspectRatio="none" aria-hidden="true" focusable="false"
          style={{ "--motor-period": `${period}s`, "--motor-opacity": opacity,
            "--motor-gradient": `url(#${motorClipId}-motor-colors)` } as CSSProperties}>
          <defs>
            <linearGradient id={`${motorClipId}-motor-colors`} x1="0" x2="1" y1="0" y2="0">
              <stop offset="0" stopColor="#42c8ff" /><stop offset=".35" stopColor="#9d84ff" />
              <stop offset=".65" stopColor="#ff85cf" /><stop offset="1" stopColor="#ffb34d" />
            </linearGradient>
          </defs>
          {profile.visual.motors.map((zone) => {
            const contour = haptics.contours[zone.id];
            if (!contour) return null;
            const clipId = `${motorClipId}-${zone.id}`;
            return <g key={zone.id} data-motor={zone.id}
              className={`controller-motor controller-motor-${zone.kind}`}
              style={{ "--motor-color": zone.id.startsWith("left") ? "#62d7ff" : "#ffb55c" } as CSSProperties}>
              <defs><clipPath id={clipId}><path d={contour} /></clipPath></defs>
              <g clipPath={`url(#${clipId})`}>
                <path className="motor-surface" d={contour} />
                <path className="motor-contour motor-contour-layer" d={contour} />
                <path className="motor-contour-shadow" d={contour} />
                <path className="motor-contour" d={contour} />
                <path className="motor-wave motor-wave-glow" d={contour} pathLength="100" />
                <path className="motor-wave motor-wave-glow motor-wave-echo" d={contour} pathLength="100" />
                <path className="motor-wave" d={contour} pathLength="100" />
                <path className="motor-wave motor-wave-echo" d={contour} pathLength="100" />
                <path className="motor-wave motor-wave-core" d={contour} pathLength="100" />
              </g>
            </g>;
          })}
        </svg>}
      </div>
    </div>
  );
}

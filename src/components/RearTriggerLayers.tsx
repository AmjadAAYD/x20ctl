import { controlShapes } from "../controller-controls";
import { controllerSilhouette, LIGHTING_CLIP_SCALE } from "../controller-lighting";
import { triggerPose, type TriggerKey } from "../controller-triggers";
import { useState } from "react";
import { solidTriggerOutline, triggerSolidFrame } from "../controller-trigger-solids";
import { TriggerVolume } from "./TriggerVolume";
import type { ControllerId } from "../controllers";
import type { KeyName } from "../types/gamepad";

const bases = import.meta.glob("../assets/controllers/*/controller-rear-trigger-base.png", { eager: true, import: "default" }) as Record<string, string>;
const keys: TriggerKey[] = ["LT", "RT"];

/** Original photographic caps above a reconstructed socket. No duplicate baked caps. */
export function RearTriggerLayers({ model, original, id, leftTrigger, rightTrigger, selectedKey }: {
  model: ControllerId; original: string; id: string; leftTrigger: number; rightTrigger: number; selectedKey?: KeyName | null;
}) {
  const [volumeReady,setVolumeReady]=useState(false);
  const shapes = controlShapes(model).back;
  const holes = keys.map(key => shapes[key]!.path).join(" ");
  const base = bases[`../assets/controllers/${model}/controller-rear-trigger-base.png`];
  return <><svg className="controller-trigger-layers" viewBox="0 0 1536 1024" preserveAspectRatio="none" aria-hidden="true" focusable="false">
    <defs>
      <clipPath id={`${id}-body`} clipPathUnits="objectBoundingBox">
        <path d={`${controllerSilhouette(model, "back")} ${holes}`} transform={LIGHTING_CLIP_SCALE} clipRule="evenodd" />
      </clipPath>
      <clipPath id={`${id}-ambient`} clipPathUnits="objectBoundingBox">
        <path d={`M0 0 H1536 V1024 H0 Z ${holes}`} transform={LIGHTING_CLIP_SCALE} clipRule="evenodd" />
      </clipPath>
      {keys.map(key => {
        return <g key={key}>
          <clipPath id={`${id}-${key}`}><path d={shapes[key]!.path} /></clipPath>
        </g>;
      })}
    </defs>
    {keys.map(key => {
      const shape = shapes[key]!;
      const pose = triggerPose(model, key, key === "LT" ? leftTrigger : rightTrigger);
      return <g key={key}>
        {/* Only the socket is clipped to the stationary aperture. */}
        <g className="controller-trigger-aperture" clipPath={`url(#${id}-${key})`}>
          <image href={base} width="1536" height="1024" />
        </g>
        <g className="controller-trigger-label" opacity={pose.press > .01 || selectedKey === key ? 1 : 0}>
          <rect x={shape.bounds[0] + shape.bounds[2] / 2 - 28} y={shape.bounds[1] + shape.bounds[3] + 10} width="56" height="30" rx="8" />
          <text x={shape.bounds[0] + shape.bounds[2] / 2} y={shape.bounds[1] + shape.bounds[3] + 33} textAnchor="middle">{key}</text>
        </g>
      </g>;
    })}
  </svg><TriggerVolume model={model} original={original} leftTrigger={leftTrigger} rightTrigger={rightTrigger} onReady={setVolumeReady} />{keys.map(key => {
    const shape = shapes[key]!;
    const [x, y, , height] = shape.bounds;
    const pose = triggerPose(model, key, key === "LT" ? leftTrigger : rightTrigger);
    const solid=triggerSolidFrame(model,key);
    const outline=volumeReady ? solidTriggerOutline(model,key,pose.press) : shape.path;
    return <svg key={key} className="controller-trigger-layers controller-trigger-cap"
      viewBox="0 0 1536 1024" preserveAspectRatio="none" aria-hidden="true" focusable="false"
      data-trigger={key} data-trigger-value={pose.press.toFixed(3)}
      data-solid-depth={solid.thickness} data-solid-ready={volumeReady}
      style={{ transform: "none" }}>
      <defs>
        <clipPath id={`${id}-${key}-cap`}><path d={shape.path} /></clipPath>
        <linearGradient id={`${id}-${key}-depth`} gradientUnits="userSpaceOnUse" x1={x} x2={x} y1={y} y2={y + height}>
          <stop stopColor="#edf5ff" stopOpacity=".13" />
          <stop offset=".55" stopColor="#edf5ff" stopOpacity="0" />
          <stop offset="1" stopColor="#02050b" stopOpacity=".24" />
        </linearGradient>
      </defs>
      {!volumeReady && <>
        <image href={original} width="1536" height="1024" clipPath={`url(#${id}-${key}-cap)`} />
      </>}
      <path className={`controller-trigger-feedback ${selectedKey === key ? "is-selected" : ""}`} d={outline}
        style={{ fillOpacity: 0 }} vectorEffect="non-scaling-stroke" />
    </svg>;
  })}</>;
}

import { LIGHTING_CLIP_SCALE, LIGHTING_VIEWBOX, controllerSilhouette } from "../controller-lighting";
import type { ControllerId } from "../controllers";

/** Photo clips and a decorative outline share the original source coordinates. */
export function ControllerLighting({ model, view, id, sweep = false }: {
  model: ControllerId; view: "front" | "back"; id: string; sweep?: boolean;
}) {
  const silhouette = controllerSilhouette(model, view);
  return <>
    <svg className="controller-lighting-defs" width="0" height="0" aria-hidden="true" focusable="false">
      <defs>
        <clipPath id={`${id}-shell`} clipPathUnits="objectBoundingBox">
          <path d={silhouette} transform={LIGHTING_CLIP_SCALE} />
        </clipPath>
        <linearGradient id={`${id}-colors`} x1="0" x2="1" y1="0" y2="0">
          <stop offset="0" stopColor="#66baff" /><stop offset=".35" stopColor="#ad9aee" />
          <stop offset=".65" stopColor="#dda5ca" /><stop offset="1" stopColor="#e7b189" />
        </linearGradient>
      </defs>
    </svg>
    <svg className="controller-silhouette-overlay" viewBox={LIGHTING_VIEWBOX} preserveAspectRatio="none" aria-hidden="true" focusable="false"
      style={{ clipPath: `url(#${id}-shell)` }}>
      {sweep && <path className="controller-rim-sweep" d={silhouette} pathLength="100"
        stroke={`url(#${id}-colors)`} vectorEffect="non-scaling-stroke" />}
    </svg>
  </>;
}

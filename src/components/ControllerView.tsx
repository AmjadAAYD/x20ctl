import { useId, useState } from "react";
import { controller, type Point } from "../controllers";
import { controllerFraming } from "../controller-framing";
import { ControllerCanvas, type ControllerCanvasProps } from "./ControllerCanvas";
import { ControllerLighting } from "./ControllerLighting";
import { controlShapes } from "../controller-controls";
import { PhotoControl } from "./PhotoControl";
import { RearTriggerLayers } from "./RearTriggerLayers";
import { KEY_LABELS, type KeyName } from "../types/gamepad";
import "./controller-view.css";

export interface RearGeometry {
  asset: string;
  aspect: number;
  viewLabel: string;
  controls: { slot: string; points: Point[]; printedLabel?: boolean }[];
}
const geometries = import.meta.glob("../assets/controllers/*/rear-geometry.json", {
  eager: true, import: "default",
}) as Record<string, RearGeometry>;
const images = import.meta.glob("../assets/controllers/*/controller-rear.png", {
  eager: true, import: "default",
}) as Record<string, string>;

export function ControllerView({
  model = "x20", initialView = "front", fixedView, view: controlledView, onViewChange, selectedMacro, onMacroSelect, hideMacroControls = false, framing = "inset", lighting = "interactive", ...front
}: ControllerCanvasProps & {
  initialView?: "front" | "back";
  fixedView?: "front" | "back";
  view?: "front" | "back";
  onViewChange?: (view: "front" | "back") => void;
  selectedMacro?: string;
  onMacroSelect?: (slot: string) => void;
  hideMacroControls?: boolean;
}) {
  const [selectedView, setView] = useState(initialView);
  const lightingId = useId().replace(/:/g, "");
  const view = fixedView ?? controlledView ?? selectedView;
  const profile = controller(model);
  const geometry = geometries[`../assets/controllers/${model}/rear-geometry.json`];
  const rearImage = images[`../assets/controllers/${model}/${geometry.asset}`];
  return (
    <div className="controller-view" data-view={view} data-model={model}>
      {!fixedView && <div className="controller-view-switch" role="group" aria-label="Controller view">
        {(["front", "back"] as const).map((side) => (
          <button type="button" key={side} aria-pressed={view === side}
            onClick={() => { setView(side); onViewChange?.(side); }}>{side === "front" ? "Front View" : "Back View"}</button>
        ))}
      </div>}
      {view === "front" ? <ControllerCanvas model={model} framing={framing} lighting={lighting} {...front} /> : (
        <div className="controller-rear" data-model={model} data-framing={framing} data-lighting="rear" style={{ aspectRatio: geometry.aspect }}
          role="group" aria-label={`${profile.name} back view`}>
          <div className="controller-rear-plane" style={{ aspectRatio: geometry.aspect,
            ...(framing === "detail" ? controllerFraming(model, "back") : {}) }}>
            <img src={rearImage}
              style={{ clipPath: `url(#${lightingId}-body)` }}
              alt={`${geometry.viewLabel} of EasySMX ${profile.name}`} draggable={false} />
            <img className="controller-photo-ambient" src={rearImage}
              style={{ clipPath: `url(#${lightingId}-ambient)` }}
              alt="" aria-hidden="true" draggable={false} />
            <ControllerLighting model={model} view="back" id={lightingId} />
            <RearTriggerLayers model={model} original={rearImage} id={lightingId}
              leftTrigger={front.disabled ? 0 : front.leftTrigger ?? 0} rightTrigger={front.disabled ? 0 : front.rightTrigger ?? 0}
              selectedKey={front.selectedKey} />
            {front.onSelect && Object.entries(controlShapes(model).back).map(([key, shape]) =>
              <PhotoControl key={key} shape={shape!} className={`controller-rear-control ${key === "LT" || key === "RT" ? "is-moving-trigger" : ""}`}
                label={`Select ${KEY_LABELS[key as KeyName]}`} selected={front.selectedKey === key}
                pressed={!!front.buttons?.[key as KeyName]} disabled={front.disabled}
                onSelect={() => front.onSelect?.(key as KeyName)} />)}
            {!hideMacroControls && geometry.controls.map(({ slot, printedLabel }) => <PhotoControl key={slot}
              shape={controlShapes(model).macros[slot]} className="controller-macro-hotspot"
              label={`Open ${slot} macro`} slot={slot} text={printedLabel ? undefined : slot} selected={selectedMacro === slot}
              disabled={front.disabled} onSelect={onMacroSelect ? () => onMacroSelect(slot) : undefined} />)}
          </div>
        </div>
      )}
      {view === "back" && !hideMacroControls && <p className="controller-view-hint">
        {front.onSelect ? "Select a shoulder control to inspect its assignment." : profile.macroSlots.length ? "Select a programmable button to open its macro." : "This model has no programmable macro buttons."}
      </p>}
    </div>
  );
}

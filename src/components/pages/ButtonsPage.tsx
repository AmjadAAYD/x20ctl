import { useEffect, useRef, useState } from "react";
import {
  ArrowRight,
  Crosshair,
  Cpu,
  Keyboard,
  MousePointer2,
  RotateCcw,
  SlidersHorizontal,
} from "lucide-react";
import { ControllerView } from "../ControllerView";
import { controller, type ControllerId } from "../../controllers";
import { keyboardStickVector } from "../../controller-preview";
import { isRearControl } from "../../controller-controls";
import { triggerStrength } from "../../controller-triggers";
import { MetalSlider } from "../MetalSlider";
import {
  KeyName,
  KEY_LABELS,
  LiveGamepadState,
} from "../../types/gamepad";
import "../metal-controls.css";

interface ButtonsPageProps {
  active?: boolean;
  model?: ControllerId;
  disabled?: boolean;
  remaps: Record<KeyName, KeyName>;
  onUpdateRemap: (source: KeyName, target: KeyName) => void;
  onResetRemaps: () => void;
  liveState: LiveGamepadState;
  inputConnected?: boolean;
  onOpenMacro?: (slot: string) => void;
}
const SOURCES: KeyName[] = [
  "A",
  "B",
  "X",
  "Y",
  "LB",
  "RB",
  "LT",
  "RT",
  "L3",
  "R3",
  "DPAD_UP",
  "DPAD_DOWN",
  "DPAD_LEFT",
  "DPAD_RIGHT",
];
const TARGETS: KeyName[] = [...SOURCES, "SELECT", "START"];
const SHORT_KEYS: Partial<Record<KeyName, string>> = {
  DPAD_UP: "↑",
  DPAD_DOWN: "↓",
  DPAD_LEFT: "←",
  DPAD_RIGHT: "→",
  SELECT: "BACK",
};
const shortKey = (key: KeyName) => SHORT_KEYS[key] ?? key;
const PREVIEW_BUTTONS = Object.fromEntries(
  (Object.keys(KEY_LABELS) as KeyName[]).map((key) => [key, false]),
) as Record<KeyName, boolean>;

export function ButtonsPage({
  active = true,
  model = "x20",
  disabled = false,
  remaps,
  onUpdateRemap,
  onResetRemaps,
  liveState,
  inputConnected = false,
  onOpenMacro,
}: ButtonsPageProps) {
  const [selected, setSelected] = useState<KeyName>("A");
  const [view, setView] = useState<"front" | "back">("front");
  function selectControl(key: KeyName) {
    setSelected(key);
    setView(isRearControl(key) ? "back" : "front");
  }
  const [previewEnabled, setPreviewEnabled] = useState(false);
  const [previewTriggers, setPreviewTriggers] = useState({ left: 0, right: 0 });
  const triggerWasHeld = useRef(false);
  const [previewKeys, setPreviewKeys] = useState<ReadonlySet<string>>(() => new Set());
  const [pointerLeft, setPointerLeft] = useState<{ x: number; y: number } | null>(null);
  const [pointerRight, setPointerRight] = useState<{ x: number; y: number } | null>(null);
  useEffect(() => {
    if (!previewEnabled || !active) {
      if (!active) setPreviewEnabled(false);
      setPreviewKeys(new Set());
      setPointerLeft(null);
      setPointerRight(null);
      setPreviewTriggers({ left: 0, right: 0 });
      return;
    }
    function onKeyDown(event: globalThis.KeyboardEvent) {
      const key = event.key.toLowerCase();
      if (!["w", "a", "s", "d"].includes(key)) return;
      if (event.altKey || event.ctrlKey || event.metaKey) return;
      const target = event.target instanceof Element ? event.target : null;
      if (target?.closest("input, textarea, select, [contenteditable='true']")) return;
      event.preventDefault();
      setPreviewKeys((current) => {
        if (current.has(key)) return current;
        return new Set([...current, key]);
      });
    }
    function onKeyUp(event: globalThis.KeyboardEvent) {
      const key = event.key.toLowerCase();
      setPreviewKeys((current) => {
        if (!current.has(key)) return current;
        const next = new Set(current);
        next.delete(key);
        return next;
      });
    }
    function resetKeys() { setPreviewKeys(new Set()); }
    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("keyup", onKeyUp);
    window.addEventListener("blur", resetKeys);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("keyup", onKeyUp);
      window.removeEventListener("blur", resetKeys);
    };
  }, [previewEnabled, active]);
  const leftStick = previewEnabled ? pointerLeft ?? keyboardStickVector(previewKeys) : liveState.leftStick;
  const rightStick = previewEnabled ? pointerRight ?? { x: 0, y: 0 } : liveState.rightStick;
  const showStickValues = previewEnabled || inputConnected;
  const leftTrigger = triggerStrength(previewEnabled ? previewTriggers.left : inputConnected && active ? liveState.leftTrigger : 0);
  const rightTrigger = triggerStrength(previewEnabled ? previewTriggers.right : inputConnected && active ? liveState.rightTrigger : 0);
  useEffect(() => {
    const held = leftTrigger > .01 || rightTrigger > .01;
    if (held && !triggerWasHeld.current) setView("back");
    triggerWasHeld.current = held;
  }, [leftTrigger, rightTrigger]);
  const changed = SOURCES.filter((key) => (remaps[key] ?? key) !== key).length;
  const target = remaps[selected] ?? selected;
  return (
    <div className="mapping-workbench">
      <section className="mapping-canvas-panel">
        <header className="mapping-panel-heading">
          <div>
            <Cpu size={17} />
            <h2>Hardware canvas</h2>
          </div>
          <span className={`mapping-status ${previewEnabled ? "is-preview" : inputConnected ? "is-live" : ""}`}>
            <i />
            {previewEnabled ? "KEYBOARD PREVIEW" : inputConnected ? "LIVE INPUT" : "OFFLINE DRAFT"}
          </span>
        </header>
        <div className="mapping-canvas-toolbar">
          <span>
            EasySMX {controller(model).name}{" "}
            <small>/ interactive controls</small>
          </span>
          <div className="mapping-toolbar-actions">
            <button
              type="button"
              className="keyboard-preview-toggle"
              aria-label="Keyboard preview"
              aria-pressed={previewEnabled}
              onClick={() => setPreviewEnabled((current) => !current)}
            >
              <Keyboard size={14} /> Keyboard preview
            </button>
          </div>
        </div>
        <div className="mapping-controller-stage">
          <ControllerView
            model={model}
            framing="detail"
            view={view}
            onViewChange={setView}
            onMacroSelect={onOpenMacro}
            disabled={disabled}
            selectedKey={selected}
            buttons={previewEnabled || !inputConnected ? {} : liveState.buttons}
            leftStick={leftStick}
            rightStick={rightStick}
            leftTrigger={leftTrigger}
            rightTrigger={rightTrigger}
            previewEnabled={previewEnabled}
            onSelect={selectControl}
            onStickPreview={(key, value) => {
              if (key === "L3") setPointerLeft(value);
              else setPointerRight(value);
            }}
          />
          <div className="mapping-canvas-caption">
            <MousePointer2 size={13} />
            <span>Select a control to inspect its assignment</span>
          </div>
        </div>
        {previewEnabled && (
          <p className="keyboard-preview-note" role="status">
            Keyboard preview active. Hold WASD or drag either stick. Use the sliders to preview trigger travel. Visual test only; no controller input or settings are sent.
          </p>
        )}
        {previewEnabled && <div className="mapping-trigger-preview" role="group" aria-label="Trigger motion preview">
          {(["left", "right"] as const).map(side => <label key={side}>
            <span>{side === "left" ? "LT" : "RT"} travel <output>{Math.round(previewTriggers[side] * 100)}%</output></span>
            <MetalSlider aria-label={`${side === "left" ? "LT" : "RT"} travel preview`} value={previewTriggers[side] * 100}
              disabled={disabled} onValueChange={value => { setView("back"); setPreviewTriggers(current => ({ ...current, [side]: value / 100 })); }} />
          </label>)}
        </div>}
        <div className="mapping-input-strip">
          <div>
            <Crosshair size={18} />
            <span>
              LEFT STICK
              <strong>
                {showStickValues
                  ? `${leftStick.x.toFixed(2)} / ${leftStick.y.toFixed(2)}`
                  : "— / —"}
              </strong>
            </span>
          </div>
          <div className="mapping-trigger-readout">
            <span>
              LT
              <strong>
                {showStickValues
                  ? `${Math.round(leftTrigger * 100)}%`
                  : "—"}
              </strong>
            </span>
            <div>
              <i
                style={{
                  width: `${leftTrigger * 100}%`,
                }}
              />
            </div>
          </div>
          <div className="mapping-trigger-readout">
            <span>
              RT
              <strong>
                {showStickValues
                  ? `${Math.round(rightTrigger * 100)}%`
                  : "—"}
              </strong>
            </span>
            <div>
              <i
                style={{
                  width: `${rightTrigger * 100}%`,
                }}
              />
            </div>
          </div>
          <div>
            <Crosshair size={18} />
            <span>
              RIGHT STICK
              <strong>
                {showStickValues
                  ? `${rightStick.x.toFixed(2)} / ${rightStick.y.toFixed(2)}`
                  : "— / —"}
              </strong>
            </span>
          </div>
        </div>
        <p className="mapping-footnote">
          Illustrated controls follow gameplay input. Appearance changes this
          illustration only.
        </p>
      </section>
      <section className="mapping-inspector-panel">
        <header className="mapping-panel-heading">
          <div>
            <SlidersHorizontal size={17} />
            <h2>Button assignment</h2>
          </div>
          <span className="mapping-count">{changed} modified</span>
        </header>
        <div className="mapping-selected-control">
          <div
            className={`mapping-key-large ${liveState.buttons[selected] && inputConnected ? "is-pressed" : ""}`}
          >
            {shortKey(selected)}
          </div>
          <ArrowRight size={21} className="mapping-route-arrow" />
          <div className="mapping-target-field">
            <label htmlFor="selected-target">Assignment</label>
            <select
              id="selected-target"
              value={target}
              onChange={(e) =>
                onUpdateRemap(selected, e.target.value as KeyName)
              }
            >
              {TARGETS.map((key) => (
                <option key={key} value={key}>
                  {KEY_LABELS[key]}
                </option>
              ))}
            </select>
          </div>
        </div>
        <div className="mapping-selection-meta">
          <span>{KEY_LABELS[selected]}</span>
          <span>
            {target === selected
              ? "Standard assignment"
              : `Sends ${KEY_LABELS[target]}`}
          </span>
        </div>
        <p className="mapping-inspector-guidance">Select a control on the controller, then choose its assignment. Changes stay in your draft until applied.</p>
        <details className="mapping-all-assignments"><summary>All assignments</summary>
        <div className="mapping-table-head">
          <span>PHYSICAL INPUT</span>
          <span>OUTPUT</span>
        </div>
        <div className="mapping-assignment-list">
          {SOURCES.map((key) => (
            <div
              key={key}
              className={`mapping-assignment-row ${selected === key ? "is-selected" : ""}`}
            >
              <button
                type="button"
                aria-pressed={selected === key}
                onClick={() => selectControl(key)}
              >
                <span
                  className={`mapping-key-small ${inputConnected && liveState.buttons[key] ? "is-pressed" : ""}`}
                >
                  {shortKey(key)}
                </span>
                <span>{KEY_LABELS[key]}</span>
              </button>
              <select
                aria-label={`Remap ${KEY_LABELS[key]}`}
                value={remaps[key] ?? key}
                onFocus={() => selectControl(key)}
                onChange={(e) => onUpdateRemap(key, e.target.value as KeyName)}
              >
                {TARGETS.map((destination) => (
                  <option key={destination} value={destination}>
                    {KEY_LABELS[destination]}
                  </option>
                ))}
              </select>
            </div>
          ))}
        </div>
        </details>
        <footer className="mapping-inspector-footer">
          <p>Select and Start are output destinations only.</p>
          <button
            type="button"
            onClick={onResetRemaps}
            title="Reset draft mappings"
          >
            <RotateCcw size={14} />
            Reset mappings
          </button>
        </footer>
      </section>
    </div>
  );
}

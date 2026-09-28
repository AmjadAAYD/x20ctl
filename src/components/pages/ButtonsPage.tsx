import { useEffect, useState } from "react";
import {
  ArrowRight,
  Crosshair,
  Cpu,
  Keyboard,
  MousePointer2,
  RotateCcw,
  SlidersHorizontal,
} from "lucide-react";
import { ControllerDiagram } from "../ControllerDiagram";
import { PhotoControllerOverlay } from "../PhotoControllerOverlay";
import { keyboardStickVector } from "../../controller-preview";
import x20Hero from "../../assets/x20-hero.png";
import {
  KeyName,
  KEY_LABELS,
  LiveGamepadState,
  ControllerPreset,
} from "../../types/gamepad";
import "../metal-controls.css";

interface ButtonsPageProps {
  remaps: Record<KeyName, KeyName>;
  onUpdateRemap: (source: KeyName, target: KeyName) => void;
  onResetRemaps: () => void;
  liveState: LiveGamepadState;
  inputConnected?: boolean;
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
  remaps,
  onUpdateRemap,
  onResetRemaps,
  liveState,
  inputConnected = false,
}: ButtonsPageProps) {
  const [selected, setSelected] = useState<KeyName>("A");
  const [appearance, setAppearance] =
    useState<ControllerPreset>("x20-pro-black");
  const [previewEnabled, setPreviewEnabled] = useState(false);
  const [previewKeys, setPreviewKeys] = useState<ReadonlySet<string>>(() => new Set());
  const [pointerLeft, setPointerLeft] = useState<{ x: number; y: number } | null>(null);
  const [pointerRight, setPointerRight] = useState<{ x: number; y: number } | null>(null);
  useEffect(() => {
    if (!previewEnabled) {
      setPreviewKeys(new Set());
      setPointerLeft(null);
      setPointerRight(null);
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
  }, [previewEnabled]);
  const leftStick = previewEnabled ? pointerLeft ?? keyboardStickVector(previewKeys) : liveState.leftStick;
  const rightStick = previewEnabled ? pointerRight ?? { x: 0, y: 0 } : liveState.rightStick;
  const visualState = previewEnabled
    ? { ...liveState, leftStick, rightStick, buttons: PREVIEW_BUTTONS }
    : liveState;
  const showStickValues = previewEnabled || inputConnected;
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
            EasySMX X20 <small>/ front view</small>
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
          <div className="mapping-finish-switch" aria-label="Illustration finish">
            <button
              aria-label="Graphite illustration"
              aria-pressed={appearance === "x20-pro-black"}
              onClick={() => setAppearance("x20-pro-black")}
            >
              Graphite
            </button>
            <button
              aria-label="Silver illustration"
              aria-pressed={appearance === "x20-pro-white"}
              onClick={() => setAppearance("x20-pro-white")}
            >
              Silver
            </button>
          </div>
          </div>
        </div>
        <div className="mapping-controller-stage">
          {appearance === "x20-pro-black" && <img className="mapping-controller-photo" src={x20Hero} alt="" aria-hidden="true" />}
          <span className="mapping-corner mapping-corner-tl" />
          <span className="mapping-corner mapping-corner-tr" />
          <span className="mapping-corner mapping-corner-bl" />
          <span className="mapping-corner mapping-corner-br" />
          {appearance === "x20-pro-black" ? (
            <PhotoControllerOverlay
              selectedKey={selected}
              pressedButtons={previewEnabled || !inputConnected ? {} : liveState.buttons}
              leftStick={leftStick}
              rightStick={rightStick}
              previewEnabled={previewEnabled}
              onSelect={setSelected}
              onStickPreview={(key, value) => {
                if (key === "L3") setPointerLeft(value);
                else setPointerRight(value);
              }}
            />
          ) : (
            <ControllerDiagram
              liveState={visualState}
              selectedKey={selected}
              onButtonClick={(key) => {
                if (SOURCES.includes(key)) setSelected(key);
              }}
              preset={appearance}
            />
          )}
          <div className="mapping-canvas-caption">
            <MousePointer2 size={13} />
            <span>Select a control to inspect its assignment</span>
          </div>
        </div>
        {previewEnabled && (
          <p className="keyboard-preview-note" role="status">
            Keyboard preview active. Hold WASD for the left stick or drag either stick. Visual test only; no controller input or settings are sent.
          </p>
        )}
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
                {inputConnected
                  ? `${Math.round(liveState.leftTrigger * 100)}%`
                  : "—"}
              </strong>
            </span>
            <div>
              <i
                style={{
                  width: `${inputConnected ? liveState.leftTrigger * 100 : 0}%`,
                }}
              />
            </div>
          </div>
          <div className="mapping-trigger-readout">
            <span>
              RT
              <strong>
                {inputConnected
                  ? `${Math.round(liveState.rightTrigger * 100)}%`
                  : "—"}
              </strong>
            </span>
            <div>
              <i
                style={{
                  width: `${inputConnected ? liveState.rightTrigger * 100 : 0}%`,
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
          Illustrated controls follow XInput. Appearance changes this
          illustration only.
        </p>
      </section>
      <section className="mapping-inspector-panel">
        <header className="mapping-panel-heading">
          <div>
            <SlidersHorizontal size={17} />
            <h2>Remap inspector</h2>
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
            <label htmlFor="selected-target">OUTPUT ASSIGNMENT</label>
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
                onClick={() => setSelected(key)}
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
                onFocus={() => setSelected(key)}
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

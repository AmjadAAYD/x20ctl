import { useState } from "react";
import { ArrowRight, RotateCcw, MousePointer2 } from "lucide-react";
import { ControllerDiagram } from "../ControllerDiagram";
import {
  KeyName,
  KEY_LABELS,
  LiveGamepadState,
  ControllerPreset,
} from "../../types/gamepad";

interface ButtonsPageProps {
  remaps: Record<KeyName, KeyName>;
  onUpdateRemap: (source: KeyName, target: KeyName) => void;
  onResetRemaps: () => void;
  liveState: LiveGamepadState;
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

export function ButtonsPage({
  remaps,
  onUpdateRemap,
  onResetRemaps,
  liveState,
}: ButtonsPageProps) {
  const [selected, setSelected] = useState<KeyName>("A");
  const [appearance, setAppearance] =
    useState<ControllerPreset>("x20-pro-black");
  const changed = SOURCES.filter((key) => remaps[key] !== key).length;
  return (
    <div className="button-studio">
      <section className="controller-stage">
        <div className="stage-toolbar">
          <span className="eyebrow">CONTROLLER OVERVIEW</span>
          <div className="appearance-switch">
            <button
              aria-label="Dark illustration"
              aria-pressed={appearance === "x20-pro-black"}
              onClick={() => setAppearance("x20-pro-black")}
            >
              Dark
            </button>
            <button
              aria-label="Light illustration"
              aria-pressed={appearance === "x20-pro-white"}
              onClick={() => setAppearance("x20-pro-white")}
            >
              Light
            </button>
          </div>
        </div>
        <div className="controller-art">
          <ControllerDiagram
            liveState={liveState}
            selectedKey={selected}
            onButtonClick={(key) => {
              if (SOURCES.includes(key)) setSelected(key);
            }}
            preset={appearance}
          />
        </div>
        <div className="stage-caption">
          <MousePointer2 size={15} />
          <span>Select a button to change its assignment</span>
        </div>
        <p className="illustration-note">
          Stylized illustration. Highlights show XInput states when a gamepad is
          detected.
        </p>
        <div className="selected-mapping">
          <div className="keycap">
            {(
              {
                DPAD_UP: "↑",
                DPAD_DOWN: "↓",
                DPAD_LEFT: "←",
                DPAD_RIGHT: "→",
              } as Record<string, string>
            )[selected] ?? selected}
          </div>
          <ArrowRight size={19} />
          <div>
            <label htmlFor="selected-target">
              {KEY_LABELS[selected]} sends
            </label>
            <select
              id="selected-target"
              value={remaps[selected] ?? selected}
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
      </section>
      <section className="mapping-panel">
        <div className="mapping-header">
          <div>
            <h2>Button assignments</h2>
            <p>
              {changed
                ? `${changed} customized in this draft`
                : "Standard layout in this draft"}
            </p>
          </div>
          <button title="Reset draft mappings" onClick={onResetRemaps}>
            <RotateCcw size={16} />
          </button>
        </div>
        <div className="mapping-columns">
          <span>PHYSICAL BUTTON</span>
          <span>SENDS</span>
        </div>
        <div className="mapping-list">
          {SOURCES.map((key) => (
            <div
              key={key}
              className={`mapping-row ${selected === key ? "active" : ""}`}
            >
              <button onClick={() => setSelected(key)}>
                <span
                  className={`mini-key ${liveState.buttons[key] ? "pressed" : ""}`}
                >
                  {["A", "B", "X", "Y"].includes(key) ? key : "•"}
                </span>
                {KEY_LABELS[key]}
              </button>
              <select
                aria-label={`Remap ${KEY_LABELS[key]}`}
                value={remaps[key] ?? key}
                onFocus={() => setSelected(key)}
                onChange={(e) => onUpdateRemap(key, e.target.value as KeyName)}
              >
                {TARGETS.map((target) => (
                  <option key={target} value={target}>
                    {KEY_LABELS[target]}
                  </option>
                ))}
              </select>
            </div>
          ))}
        </div>
        <p className="mapping-note">
          Select and Start are available as destinations only. Capture and Turbo
          are controlled on the pad.
        </p>
      </section>
    </div>
  );
}

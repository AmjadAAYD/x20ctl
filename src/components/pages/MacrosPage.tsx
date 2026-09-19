import { useState } from "react";
import { Check, Layers3, ListMusic, Plus, Trash2 } from "lucide-react";
import {
  MacroStep,
  StickDirection,
  STICK_DIRECTION_NAMES,
} from "../../types/gamepad";
import {
  PianoRollModal,
  MACRO_BUTTONS,
  STICK_OPTIONS,
  StepInspector,
  createMacroStep,
  countWireEntries,
  shortKeyLabel,
} from "../PianoRollModal";
import "../metal-macros.css";

type Paddle = "M1" | "M2" | "M3" | "M4";
interface MacrosPageProps {
  loops: Record<Paddle, number>;
  onUpdateLoop: (paddle: Paddle, value: number) => void;
  macros: Record<Paddle, MacroStep[]>;
  onUpdateMacros: (paddle: Paddle, steps: MacroStep[]) => void;
  onClearMacro: (paddle: Paddle) => void;
}
const PADDLES: Paddle[] = ["M1", "M2", "M3", "M4"];

export function MacrosPage({
  loops,
  onUpdateLoop,
  macros,
  onUpdateMacros,
  onClearMacro,
}: MacrosPageProps) {
  const [paddle, setPaddle] = useState<Paddle>("M1");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [pianoOpen, setPianoOpen] = useState(false);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");
  const steps = macros[paddle] || [];
  const selected = steps.find((step) => step.id === selectedId) ?? steps[0];
  const entries = countWireEntries(steps);
  const totalMs = steps.reduce(
    (sum, step) => sum + step.durationMs + step.intervalMs,
    0,
  );

  function update(next: MacroStep[]) {
    if (countWireEntries(next) > 47) {
      setError(
        "This sequence is full. Remove a step or pause before adding another.",
      );
      return;
    }
    setError("");
    setNotice("");
    onUpdateMacros(paddle, next);
  }
  function updateStep(id: string, patch: Partial<MacroStep>) {
    update(
      steps.map((step) => (step.id === id ? { ...step, ...patch } : step)),
    );
  }
  function addStep() {
    if (entries >= 47) return;
    const step = createMacroStep(entries === 46 ? 0 : 20);
    update([...steps, step]);
    setSelectedId(step.id);
  }
  const columns = steps.length
    ? steps
    : Array.from({ length: 8 }, (_, index) => ({ id: `empty-${index}` }));

  return (
    <section className="macro-studio" aria-label="Paddle macro studio">
      <header className="macro-heading">
        <div>
          <span className="macro-eyebrow">PADDLE AUTOMATION / M1–M4</span>
          <h2>Macro studio</h2>
          <p>
            Build a sequence, adjust its timing, then apply your draft to the
            controller.
          </p>
        </div>
        <div className="macro-heading-stat">
          <span>Timing resolution</span>
          <strong>
            5 <small>ms</small>
          </strong>
        </div>
      </header>
      <div className="macro-console-bar">
        <div className="macro-paddles" role="group" aria-label="Macro paddle">
          {PADDLES.map((slot) => (
            <button
              key={slot}
              className={`macro-paddle ${paddle === slot ? "is-selected" : ""}`}
              aria-pressed={paddle === slot}
              onClick={() => {
                setPaddle(slot);
                setSelectedId(null);
                setError("");
                setNotice("");
              }}
            >
              <strong>{slot}</strong>
              <span>{macros[slot]?.length ?? 0} steps</span>
              <i aria-hidden="true" />
            </button>
          ))}
        </div>
        <div className="macro-slot-summary">
          <Layers3 size={21} />
          <div>
            <strong>{paddle} sequence</strong>
            <span>
              {steps.length
                ? `${steps.length} steps · ${totalMs.toLocaleString()} ms`
                : "Empty draft"}
            </span>
          </div>
        </div>
        <label className="macro-loop">
          <span>Loop interval</span>
          <div>
            <input
              aria-label="Macro loop interval"
              type="number"
              min={0}
              max={20475}
              step={5}
              value={loops[paddle]}
              onChange={(event) =>
                onUpdateLoop(
                  paddle,
                  Math.min(
                    20475,
                    Math.max(0, Math.round(Number(event.target.value) / 5) * 5),
                  ),
                )
              }
            />
            <span>ms</span>
          </div>
          <small>0 = run once</small>
        </label>
      </div>
      {notice && (
        <p className="macro-notice" role="status">
          <Check size={15} />
          {notice}
        </p>
      )}
      {error && (
        <p className="macro-error" role="alert">
          {error}
        </p>
      )}
      <section className="sequencer-panel">
        <header className="sequencer-heading">
          <div>
            <ListMusic size={17} />
            <strong>Step sequencer</strong>
            <span>{entries} / 47 entries</span>
          </div>
          <div className="macro-actions">
            <button className="macro-button" onClick={() => setPianoOpen(true)}>
              <ListMusic size={15} />
              Edit in Piano Roll
            </button>
            <button
              className="macro-button macro-button-primary"
              onClick={addStep}
              disabled={entries >= 47}
            >
              <Plus size={15} />
              Add Step
            </button>
          </div>
        </header>
        <div
          className="sequencer-scroll"
          tabIndex={0}
          aria-label="Macro step matrix"
        >
          <table className="sequencer-matrix">
            <thead>
              <tr>
                <th scope="col" className="sequencer-label">
                  Input / step
                </th>
                {columns.map((step, index) => (
                  <th key={step.id} scope="col">
                    <button
                      className={`sequencer-step-heading ${selected?.id === step.id ? "is-selected" : ""}`}
                      disabled={!steps.length}
                      onClick={() => setSelectedId(step.id)}
                    >
                      <strong>Step {String(index + 1).padStart(2, "0")}</strong>
                      <span>
                        {"durationMs" in step ? `${step.durationMs} ms` : "—"}
                      </span>
                    </button>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {(["leftStick", "rightStick"] as const).map((stick) => (
                <tr key={stick}>
                  <th scope="row" className="sequencer-label">
                    <span className="sequencer-input-dot" />
                    {stick === "leftStick" ? "Left stick" : "Right stick"}
                  </th>
                  {steps.length
                    ? steps.map((step, index) => (
                        <td
                          key={step.id}
                          className={
                            selected?.id === step.id ? "is-selected-column" : ""
                          }
                        >
                          <select
                            className={`sequencer-stick ${step[stick] !== StickDirection.NEUTRAL ? "is-active" : ""}`}
                            aria-label={`${stick === "leftStick" ? "Left" : "Right"} stick, step ${index + 1}`}
                            value={step[stick]}
                            onFocus={() => setSelectedId(step.id)}
                            onChange={(event) =>
                              updateStep(step.id, {
                                [stick]: Number(event.target.value),
                              })
                            }
                          >
                            {STICK_OPTIONS.map((direction) => (
                              <option key={direction} value={direction}>
                                {STICK_DIRECTION_NAMES[direction]}
                              </option>
                            ))}
                          </select>
                        </td>
                      ))
                    : columns.map((step) => (
                        <td key={step.id}>
                          <div className="sequencer-empty-cell" />
                        </td>
                      ))}
                </tr>
              ))}
              {MACRO_BUTTONS.map((key) => (
                <tr key={key}>
                  <th scope="row" className="sequencer-label">
                    <span className="sequencer-input-dot" />
                    {shortKeyLabel(key)}
                  </th>
                  {steps.length
                    ? steps.map((step, index) => (
                        <td
                          key={step.id}
                          className={
                            selected?.id === step.id ? "is-selected-column" : ""
                          }
                        >
                          <button
                            className={`sequencer-cell ${step.buttons.includes(key) ? "is-active" : ""}`}
                            aria-label={`${shortKeyLabel(key)}, step ${index + 1}`}
                            aria-pressed={step.buttons.includes(key)}
                            onClick={() => {
                              setSelectedId(step.id);
                              updateStep(step.id, {
                                buttons: step.buttons.includes(key)
                                  ? step.buttons.filter(
                                      (button) => button !== key,
                                    )
                                  : [...step.buttons, key],
                              });
                            }}
                          >
                            {step.buttons.includes(key) ? (
                              shortKeyLabel(key)
                            ) : (
                              <span aria-hidden="true">·</span>
                            )}
                          </button>
                        </td>
                      ))
                    : columns.map((step) => (
                        <td key={step.id}>
                          <div className="sequencer-empty-cell" />
                        </td>
                      ))}
                </tr>
              ))}
            </tbody>
          </table>
          {!steps.length && (
            <div className="sequencer-empty">
              <ListMusic size={28} />
              <h3>Start your {paddle} sequence</h3>
              <p>Add a step to choose buttons and stick directions.</p>
              <button
                className="macro-button macro-button-primary"
                onClick={addStep}
              >
                <Plus size={15} />
                Create first step
              </button>
            </div>
          )}
        </div>
        <footer className="sequencer-footer">
          <span>
            <i />
            {steps.length
              ? "Click a cell to toggle its input"
              : "No sequence in this draft"}
          </span>
          <span>Eight-way sticks · Digital triggers · 5 ms timing</span>
        </footer>
      </section>
      {selected && (
        <StepInspector
          step={selected}
          index={steps.findIndex((step) => step.id === selected.id)}
          onUpdate={(patch) => updateStep(selected.id, patch)}
          onDelete={() =>
            update(steps.filter((step) => step.id !== selected.id))
          }
        />
      )}
      <div className="macro-bottom">
        <p>
          A hold uses one entry. A nonzero pause uses another. Changes stay in
          your draft until you select Apply changes.
        </p>
        <button
          className="macro-button"
          disabled={!steps.length}
          onClick={() => {
            onClearMacro(paddle);
            setNotice("");
            setError("");
          }}
        >
          <Trash2 size={14} />
          Clear Slot
        </button>
      </div>
      {pianoOpen && (
        <PianoRollModal
          isOpen={pianoOpen}
          onClose={() => setPianoOpen(false)}
          paddle={paddle}
          steps={steps}
          onSaveSteps={(updated) => {
            update(updated);
            setNotice(
              `Timeline changes saved to the ${paddle} draft. Apply changes when ready.`,
            );
          }}
        />
      )}
    </section>
  );
}

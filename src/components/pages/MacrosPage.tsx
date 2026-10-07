import { useEffect, useState, type CSSProperties } from "react";
import { Check, Layers3, ListMusic, Play, Plus, Square, Trash2 } from "lucide-react";
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
import { controller, type ControllerId } from "../../controllers";
import { ControllerView } from "../ControllerView";
import { MacroLibrary } from "../MacroLibrary";

type Paddle = "M1" | "M2" | "M3" | "M4";
const STICK_SYMBOLS = ["·", "↑", "↗", "→", "↘", "↓", "↙", "←", "↖"];
interface MacrosPageProps<T extends string> {
  model?: ControllerId;
  initialSlot?: T;
  onSlotChange?: (slot: T) => void;
  draftOnly?: boolean;
  slotLabel?: string;
  slots?: T[];
  loops: Record<T, number>;
  onUpdateLoop: (paddle: T, value: number) => void;
  macros: Record<T, MacroStep[]>;
  onUpdateMacros: (paddle: T, steps: MacroStep[]) => void;
  onClearMacro: (paddle: T) => void;
}

export function MacrosPage<T extends string = Paddle>({
  model = "x20",
  initialSlot,
  onSlotChange,
  draftOnly = false,
  slotLabel = "Paddle",
  slots = controller(model).macroSlots as T[],
  loops,
  onUpdateLoop,
  macros,
  onUpdateMacros,
  onClearMacro,
}: MacrosPageProps<T>) {
  const [paddle, setPaddle] = useState<T>(initialSlot && slots.includes(initialSlot) ? initialSlot : slots[0]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [pianoOpen, setPianoOpen] = useState(false);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");
  const [playback, setPlayback] = useState<{ index: number; phase: "hold" | "gap" } | null>(null);
  const steps = macros[paddle] || [];
  const activeStep = playback?.phase === "hold" ? steps[playback.index] : undefined;
  const selected = steps.find((step) => step.id === selectedId) ?? steps[0];
  const entries = countWireEntries(steps);
  const totalMs = steps.reduce(
    (sum, step) => sum + step.durationMs + step.intervalMs,
    0,
  );

  useEffect(() => {
    if (!playback) return;
    const step = steps[playback.index];
    if (!step) { setPlayback(null); return; }
    const duration = playback.phase === "hold" ? step.durationMs : step.intervalMs;
    const timer = window.setTimeout(() => {
      if (playback.phase === "hold" && step.intervalMs > 0) setPlayback({ index: playback.index, phase: "gap" });
      else if (playback.index + 1 < steps.length) setPlayback({ index: playback.index + 1, phase: "hold" });
      else setPlayback(null);
    }, Math.max(0, duration));
    return () => window.clearTimeout(timer);
  }, [playback, steps]);

  useEffect(() => {
    const stop = () => setPlayback(null);
    const onVisibility = () => { if (document.hidden) stop(); };
    window.addEventListener("blur", stop);
    document.addEventListener("visibilitychange", onVisibility);
    return () => {
      window.removeEventListener("blur", stop);
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, []);

  function update(next: MacroStep[]) {
    setPlayback(null);
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
  function selectSlot(slot: T) {
    if (!slots.includes(slot)) return;
    setPlayback(null);
    setPaddle(slot);
    onSlotChange?.(slot);
    setSelectedId(null);
    setError("");
    setNotice("");
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
    <section className="macro-studio" aria-label={`${slotLabel} macro studio`}
      data-sequence-dense={steps.length > 12}
      data-playing-index={playback?.index ?? -1} data-preview-phase={playback?.phase ?? "idle"}>
      <header className="macro-heading">
        <div>
          <span className="macro-eyebrow">
            {slotLabel.toUpperCase()} AUTOMATION / {slots[0]}–{slots[slots.length - 1]}
          </span>
          <h2>Macro studio</h2>
          <p>
            {draftOnly
              ? "Build a local sequence and preview its timing. Hardware application is unverified."
              : "Build a sequence, adjust its timing, then apply your draft to the controller."}
          </p>
        </div>
        <div className="macro-heading-stat">
          <span>{draftOnly ? "Preview grid" : "Timing resolution"}</span>
          <strong>
            5 <small>ms</small>
          </strong>
        </div>
      </header>
      <div className="macro-controller-preview" data-playing={!!playback}>
        <ControllerView model={model} fixedView="back" framing="detail" selectedMacro={paddle}
          onMacroSelect={(slot) => selectSlot(slot as T)} disabled={false} />
        {playback && <div className="macro-preview-inputs" aria-label="Local macro preview">
          <small>Step {playback.index + 1} · local preview</small>
          {activeStep ? <>
            {activeStep.buttons.map((key) => <span className="is-active" key={key}>{shortKeyLabel(key)}</span>)}
            {activeStep.leftStick !== StickDirection.NEUTRAL && <span className="is-active">L · {STICK_DIRECTION_NAMES[activeStep.leftStick]}</span>}
            {activeStep.rightStick !== StickDirection.NEUTRAL && <span className="is-active">R · {STICK_DIRECTION_NAMES[activeStep.rightStick]}</span>}
            {!activeStep.buttons.length && activeStep.leftStick === StickDirection.NEUTRAL && activeStep.rightStick === StickDirection.NEUTRAL && <span>Neutral</span>}
          </> : <span>Pause</span>}
        </div>}
      </div>
      <div className="macro-console-bar">
        <div className="macro-paddles" role="group" aria-label={`Macro ${slotLabel.toLowerCase()}`}>
          {slots.map((slot) => (
            <button
              key={slot}
              className={`macro-paddle ${paddle === slot ? "is-selected" : ""}`}
              aria-pressed={paddle === slot}
              onClick={() => selectSlot(slot)}
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
      <section className="sequencer-panel" data-empty={!steps.length} data-dense={steps.length > 12}>
        <header className="sequencer-heading">
          <div>
            <ListMusic size={17} />
            <strong>Step sequencer</strong>
            <span>{entries} / 47 entries</span>
          </div>
          <div className="macro-actions">
            <MacroLibrary model={model} slot={paddle} slots={slots} steps={steps} loopMs={loops[paddle] || 0}
              onLoad={(next, loopMs, name) => { update(next); onUpdateLoop(paddle, loopMs); setSelectedId(next[0]?.id ?? null); setNotice(`Loaded “${name}” into the ${paddle} draft.`); }}
              onCopy={(target, next, loopMs) => { setPlayback(null); onUpdateMacros(target as T, next); onUpdateLoop(target as T, loopMs); setNotice(`Copied to the ${target} draft.`); }}
              onPauses={update} />
            <button className="macro-button" disabled={!steps.length}
              title="Visual preview in this app only; runs once without controller output"
              onClick={() => setPlayback((current) => current ? null : { index: 0, phase: "hold" })}>
              {playback ? <Square size={15} /> : <Play size={15} />}
              {playback ? "Stop preview" : "Preview sequence"}
            </button>
            <button className="macro-button" onClick={() => { setPlayback(null); setPianoOpen(true); }}>
              <ListMusic size={15} />
              Edit in Piano Roll
            </button>
            <div className="macro-step-actions">
            <button
              className="macro-button macro-button-primary"
              onClick={addStep}
              disabled={entries >= 47}
            >
              <Plus size={15} />
              Add Step
            </button>
            <button
              className="macro-button"
              title="Remove selected step"
              aria-label={selected ? `Remove Step ${steps.indexOf(selected) + 1}` : "Remove Step"}
              disabled={!selected}
              onClick={() => selected && update(steps.filter((step) => step.id !== selected.id))}
            >
              <Trash2 size={15} />
              Remove
            </button>
            </div>
          </div>
        </header>
        <div
          className="sequencer-scroll"
          tabIndex={0}
          aria-label="Macro step matrix"
        >
          <table className="sequencer-matrix" style={{ "--step-count": columns.length } as CSSProperties}>
            <colgroup>
              <col className="sequencer-input-column" />
              {columns.map((step) => <col key={step.id} />)}
            </colgroup>
            <thead>
              <tr>
                <th scope="col" className="sequencer-label">
                  Input
                </th>
                {columns.map((step, index) => (
                  <th key={step.id} scope="col">
                    <button
                      className={`sequencer-step-heading ${selected?.id === step.id ? "is-selected" : ""} ${playback?.index === index ? "is-playing" : ""}`}
                      disabled={!steps.length}
                      aria-label={`Select step ${index + 1}`}
                      aria-pressed={selected?.id === step.id}
                      title={steps[index] ? `Step ${index + 1}: hold ${steps[index].durationMs} ms, pause ${steps[index].intervalMs} ms` : "Empty step"}
                      onClick={() => setSelectedId(step.id)}
                    >
                      <strong>{String(index + 1).padStart(2, "0")}</strong>
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
                    {stick === "leftStick" ? "Left stick" : "Right stick"}
                  </th>
                  {steps.length
                    ? steps.map((step, index) => (
                        <td
                          key={step.id}
                          className={
                            `${selected?.id === step.id ? "is-selected-column" : ""} ${playback?.index === index ? "is-playing-column" : ""}`
                          }
                        >
                          <select
                            className={`sequencer-stick ${step[stick] !== StickDirection.NEUTRAL ? "is-active" : ""}`}
                            aria-label={`${stick === "leftStick" ? "Left" : "Right"} stick, step ${index + 1}`}
                            title={`${stick === "leftStick" ? "Left" : "Right"} stick: ${STICK_DIRECTION_NAMES[step[stick]]}, step ${index + 1}`}
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
                                {STICK_SYMBOLS[direction]}
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
                    {shortKeyLabel(key)}
                  </th>
                  {steps.length
                    ? steps.map((step, index) => (
                        <td
                          key={step.id}
                          className={
                            `${selected?.id === step.id ? "is-selected-column" : ""} ${playback?.index === index ? "is-playing-column" : ""}`
                          }
                        >
                          <button
                            className={`sequencer-cell ${step.buttons.includes(key) ? "is-active" : ""}`}
                            aria-label={`${shortKeyLabel(key)}, step ${index + 1}`}
                            title={`${shortKeyLabel(key)}, step ${index + 1}: ${step.buttons.includes(key) ? "on" : "off"}`}
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
                              <span aria-hidden="true">●</span>
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
              <h3>Start your {paddle} macro</h3>
              <p>Add controller inputs, stick directions, pauses and timing.</p>
              <button
                className="macro-button macro-button-primary"
                onClick={addStep}
              >
                <Plus size={15} />
                Add first step
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
            setPlayback(null);
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

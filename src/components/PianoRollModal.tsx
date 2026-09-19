import { useState, useEffect, useRef } from "react";
import {
  Check,
  Clock3,
  Pause,
  Play,
  Plus,
  RotateCcw,
  Trash2,
} from "lucide-react";
import {
  MacroStep,
  KeyName,
  KEY_LABELS,
  StickDirection,
  STICK_DIRECTION_NAMES,
} from "../types/gamepad";
import { MetalDialog } from "./MetalDialog";
import "./metal-macros.css";

export const MACRO_BUTTONS: KeyName[] = [
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
export const STICK_OPTIONS = Object.values(StickDirection).filter(
  (value): value is StickDirection => typeof value === "number",
);
export function shortKeyLabel(key: KeyName) {
  return key === "L3" || key === "R3" ? key : KEY_LABELS[key];
}
export function countWireEntries(steps: MacroStep[]) {
  return steps.reduce(
    (count, step) => count + 1 + (step.intervalMs > 0 ? 1 : 0),
    0,
  );
}
export function createMacroStep(intervalMs = 20): MacroStep {
  return {
    id: crypto.randomUUID(),
    buttons: ["A"],
    leftStick: StickDirection.NEUTRAL,
    rightStick: StickDirection.NEUTRAL,
    durationMs: 40,
    intervalMs,
  };
}

function DirectionPad({
  value,
  label,
  onChange,
}: {
  value: StickDirection;
  label: string;
  onChange: (value: StickDirection) => void;
}) {
  const layout = [
    StickDirection.UP_LEFT,
    StickDirection.UP,
    StickDirection.UP_RIGHT,
    StickDirection.LEFT,
    StickDirection.NEUTRAL,
    StickDirection.RIGHT,
    StickDirection.DOWN_LEFT,
    StickDirection.DOWN,
    StickDirection.DOWN_RIGHT,
  ];
  const symbols = ["↖", "↑", "↗", "←", "·", "→", "↙", "↓", "↘"];
  return (
    <div className="macro-direction">
      <span>{label}</span>
      <div role="group" aria-label={label}>
        {layout.map((direction, index) => (
          <button
            key={direction}
            aria-label={`${label}: ${STICK_DIRECTION_NAMES[direction]}`}
            aria-pressed={value === direction}
            className={value === direction ? "is-selected" : ""}
            onClick={() => onChange(direction)}
          >
            {symbols[index]}
          </button>
        ))}
      </div>
      <small>{STICK_DIRECTION_NAMES[value]}</small>
    </div>
  );
}

export function StepInspector({
  step,
  index,
  onUpdate,
  onDelete,
}: {
  step: MacroStep;
  index: number;
  onUpdate: (patch: Partial<MacroStep>) => void;
  onDelete: () => void;
}) {
  return (
    <section
      className="macro-inspector"
      aria-label={`Step ${index + 1} editor`}
    >
      <div className="macro-inspector-title">
        <span className="macro-eyebrow">SELECTED STEP</span>
        <strong>{String(index + 1).padStart(2, "0")}</strong>
        <button
          className="macro-button"
          title="Remove Step"
          aria-label={`Remove Step ${index + 1}`}
          onClick={onDelete}
        >
          <Trash2 size={14} />
          Remove
        </button>
      </div>
      <div className="macro-inspector-timing">
        <label>
          <span>Hold (ms)</span>
          <input
            aria-label={`Step ${index + 1} hold`}
            type="number"
            min={5}
            max={327675}
            step={5}
            value={step.durationMs}
            onChange={(event) =>
              onUpdate({
                durationMs: Math.max(
                  5,
                  Math.min(
                    327675,
                    Math.round(Number(event.target.value) / 5) * 5,
                  ),
                ),
              })
            }
          />
        </label>
        <label>
          <span>Pause (ms)</span>
          <input
            aria-label={`Step ${index + 1} pause`}
            type="number"
            min={0}
            max={327675}
            step={5}
            value={step.intervalMs}
            onChange={(event) =>
              onUpdate({
                intervalMs: Math.max(
                  0,
                  Math.min(
                    327675,
                    Math.round(Number(event.target.value) / 5) * 5,
                  ),
                ),
              })
            }
          />
        </label>
        <small>Values snap to 5 ms.</small>
      </div>
      <DirectionPad
        label="Left stick"
        value={step.leftStick}
        onChange={(value) => onUpdate({ leftStick: value })}
      />
      <DirectionPad
        label="Right stick"
        value={step.rightStick}
        onChange={(value) => onUpdate({ rightStick: value })}
      />
      <div className="macro-inspector-keys">
        <span>Active buttons</span>
        <div>
          {MACRO_BUTTONS.map((button) => (
            <button
              key={button}
              aria-pressed={step.buttons.includes(button)}
              className={`macro-key ${step.buttons.includes(button) ? "is-active" : ""}`}
              onClick={() =>
                onUpdate({
                  buttons: step.buttons.includes(button)
                    ? step.buttons.filter((key) => key !== button)
                    : [...step.buttons, button],
                })
              }
            >
              {shortKeyLabel(button)}
            </button>
          ))}
        </div>
      </div>
    </section>
  );
}

interface PianoRollModalProps {
  isOpen: boolean;
  onClose: () => void;
  paddle: "M1" | "M2" | "M3" | "M4";
  steps: MacroStep[];
  onSaveSteps: (steps: MacroStep[]) => void;
}
const TRACKS: Array<KeyName | "leftStick" | "rightStick"> = [
  "leftStick",
  "rightStick",
  ...MACRO_BUTTONS,
];

export function PianoRollModal({
  isOpen,
  onClose,
  paddle,
  steps,
  onSaveSteps,
}: PianoRollModalProps) {
  const [localSteps, setLocalSteps] = useState<MacroStep[]>(() =>
    steps.map((step) => ({ ...step, buttons: [...step.buttons] })),
  );
  const [selectedId, setSelectedId] = useState<string | null>(
    steps[0]?.id ?? null,
  );
  const [playing, setPlaying] = useState(false);
  const [playbackMs, setPlaybackMs] = useState(0);
  const [zoom, setZoom] = useState(1);
  const [error, setError] = useState("");
  const timer = useRef<number | null>(null);
  const entries = countWireEntries(localSteps);
  const selected =
    localSteps.find((step) => step.id === selectedId) ?? localSteps[0];
  let elapsed = 0;
  const positions = localSteps.map((step) => {
    const start = elapsed;
    elapsed += step.durationMs + step.intervalMs;
    return { step, start, end: start + step.durationMs };
  });
  const duration = elapsed;
  const displayMs = Math.max(500, duration);
  const timelineWidth = Math.round(780 * zoom);
  const pixelsPerMs = timelineWidth / displayMs;
  const tickMs = Math.max(5, Math.ceil(displayMs / (8 * zoom) / 5) * 5);

  useEffect(
    () => () => {
      if (timer.current !== null) window.clearInterval(timer.current);
    },
    [],
  );
  useEffect(() => {
    if (!isOpen) {
      if (timer.current !== null) window.clearInterval(timer.current);
      setPlaying(false);
    }
  }, [isOpen]);
  if (!isOpen) return null;

  function stop() {
    if (timer.current !== null) window.clearInterval(timer.current);
    timer.current = null;
    setPlaying(false);
  }
  function update(next: MacroStep[]) {
    if (countWireEntries(next) > 47) {
      setError(
        "The sequence is full. Remove a step or pause to free an entry.",
      );
      return;
    }
    stop();
    setPlaybackMs(0);
    setLocalSteps(next);
    setError("");
  }
  function togglePlay() {
    if (playing) {
      stop();
      return;
    }
    if (!duration) return;
    const started =
      performance.now() - (playbackMs >= duration ? 0 : playbackMs);
    setPlaying(true);
    timer.current = window.setInterval(() => {
      const time = performance.now() - started;
      if (time >= duration) {
        stop();
        setPlaybackMs(duration);
      } else setPlaybackMs(time);
    }, 16);
  }
  function addStep() {
    if (entries >= 47) return;
    const step = createMacroStep(entries === 46 ? 0 : 20);
    update([...localSteps, step]);
    setSelectedId(step.id);
  }

  return (
    <MetalDialog
      title={`${paddle} Piano-Roll Macro Sequencer`}
      subtitle="TIMELINE EDITOR / 5 MS GRID"
      closeLabel="Close Piano Roll"
      className="piano-dialog"
      onClose={onClose}
    >
      <div className="piano-toolbar">
        <div className="macro-actions">
          <button
            className={`macro-button ${playing ? "is-selected" : ""}`}
            onClick={togglePlay}
            disabled={!localSteps.length}
          >
            {playing ? <Pause size={14} /> : <Play size={14} />}
            {playing ? "Pause" : "Preview timeline"}
          </button>
          <button
            className="macro-button"
            aria-label="Reset Timeline"
            title="Reset Timeline"
            onClick={() => {
              stop();
              setPlaybackMs(0);
            }}
          >
            <RotateCcw size={14} />
          </button>
          <output className="piano-time">
            <Clock3 size={14} />
            {Math.round(playbackMs).toLocaleString()}{" "}
            <span>/ {duration.toLocaleString()} ms</span>
          </output>
        </div>
        <label className="piano-zoom">
          Zoom
          <select
            aria-label="Timeline zoom"
            value={zoom}
            onChange={(event) => setZoom(Number(event.target.value))}
          >
            <option value={1}>Fit</option>
            <option value={2}>2×</option>
            <option value={4}>4×</option>
          </select>
        </label>
        <button
          className="macro-button"
          disabled={entries >= 47}
          onClick={addStep}
        >
          <Plus size={14} />
          Add Step Block
        </button>
      </div>
      <div className="piano-preview-note">
        <span>
          <i /> Draft preview only. No gamepad input is sent.
        </span>
        <strong>{entries} / 47 entries</strong>
      </div>
      {error && (
        <p className="macro-error" role="alert">
          {error}
        </p>
      )}
      <div
        className="piano-track-scroll"
        tabIndex={0}
        aria-label="Piano roll timeline"
      >
        <div className="piano-track-labels">
          <div className="piano-ruler-label">INPUT</div>
          {TRACKS.map((track) => (
            <div key={track} className="piano-track-label">
              {track === "leftStick"
                ? "Left stick"
                : track === "rightStick"
                  ? "Right stick"
                  : shortKeyLabel(track)}
            </div>
          ))}
        </div>
        <div className="piano-timeline" style={{ width: timelineWidth }}>
          <div className="piano-ruler">
            {Array.from(
              { length: Math.floor(displayMs / tickMs) + 1 },
              (_, index) => (
                <span
                  key={index}
                  style={{ left: index * tickMs * pixelsPerMs }}
                >
                  {(index * tickMs).toLocaleString()}
                  <small> ms</small>
                </span>
              ),
            )}
          </div>
          <div className="piano-tracks">
            <div
              className="piano-playhead"
              style={{ transform: `translateX(${playbackMs * pixelsPerMs}px)` }}
            />
            {TRACKS.map((track) => (
              <div
                key={track}
                className="piano-track"
                style={{ backgroundSize: `${tickMs * pixelsPerMs}px 100%` }}
              >
                {positions.map(({ step, start, end }, index) => {
                  const isStick =
                    track === "leftStick" || track === "rightStick";
                  const active = isStick
                    ? step[track] !== StickDirection.NEUTRAL
                    : step.buttons.includes(track);
                  if (!active) return null;
                  const label = isStick
                    ? STICK_DIRECTION_NAMES[step[track]]
                    : shortKeyLabel(track);
                  const width = Math.max(1, step.durationMs * pixelsPerMs);
                  return (
                    <button
                      key={step.id}
                      className={`piano-note ${selected?.id === step.id ? "is-selected" : ""} ${playing && playbackMs >= start && playbackMs < end ? "is-playing" : ""}`}
                      style={{ left: start * pixelsPerMs, width }}
                      title={`Step ${index + 1}: ${label}, ${step.durationMs} ms hold, ${step.intervalMs} ms pause`}
                      aria-label={`Edit step ${index + 1}, ${label}`}
                      onClick={() => setSelectedId(step.id)}
                    >
                      {width > 44 ? label : ""}
                      {width > 115 && <small>{step.durationMs} ms</small>}
                    </button>
                  );
                })}
              </div>
            ))}
            {!localSteps.length && (
              <div className="piano-empty">
                <strong>No notes yet</strong>
                <span>Add a step block to start this sequence.</span>
              </div>
            )}
          </div>
        </div>
      </div>
      {localSteps.length > 0 && (
        <div
          className="piano-step-selector"
          role="group"
          aria-label="Select step"
        >
          {localSteps.map((step, index) => (
            <button
              key={step.id}
              className={`macro-button ${selected?.id === step.id ? "is-selected" : ""}`}
              onClick={() => setSelectedId(step.id)}
            >
              Step {index + 1}
              <span>{step.durationMs} ms</span>
            </button>
          ))}
        </div>
      )}
      {selected && (
        <StepInspector
          step={selected}
          index={localSteps.findIndex((step) => step.id === selected.id)}
          onUpdate={(patch) =>
            update(
              localSteps.map((step) =>
                step.id === selected.id ? { ...step, ...patch } : step,
              ),
            )
          }
          onDelete={() =>
            update(localSteps.filter((step) => step.id !== selected.id))
          }
        />
      )}
      <footer className="piano-footer">
        <p>
          Update the draft, then use Apply changes to send it to the controller.
        </p>
        <div className="macro-actions">
          <button className="macro-button" onClick={onClose}>
            Cancel
          </button>
          <button
            className="macro-button macro-button-primary"
            onClick={() => {
              onSaveSteps(localSteps);
              onClose();
            }}
          >
            <Check size={15} />
            Update {paddle} draft
          </button>
        </div>
      </footer>
    </MetalDialog>
  );
}

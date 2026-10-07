import { useEffect, useRef, useState } from "react";
import { BookOpen, Copy, Download, Save, Trash2, Upload } from "lucide-react";
import { controller, controllers, type ControllerId } from "../controllers";
import {
  cloneMacroSteps,
  countWireEntries,
  setMacroPauses,
  validateSavedMacro,
  type SavedMacro,
} from "../macro-library";
import { request } from "../native";
import type { MacroStep } from "../types/gamepad";
import { MetalDialog } from "./MetalDialog";
import "./macro-library.css";

const PREVIEW_STORAGE = "x20ctl-macro-library-v1";
const native = () => typeof window.pywebview?.api?.request === "function";
type LibraryResult = { entries: SavedMacro[]; path: string };

function checked(value: unknown) {
  const macro = validateSavedMacro(value);
  if (
    !controllers.some(
      (model) => model.id === macro.sourceModel && model.macroSlots.length > 0,
    )
  )
    throw new Error("Unknown macro source model.");
  return macro;
}

async function library(
  action = "load",
  payload: object = {},
): Promise<LibraryResult> {
  if (native()) return request("macro_library", { action, ...payload });
  const raw = JSON.parse(localStorage.getItem(PREVIEW_STORAGE) || "[]");
  if (!Array.isArray(raw) || raw.length > 200)
    throw new Error("Invalid macro library.");
  let entries: SavedMacro[] = raw.map(checked);
  if (action === "save") {
    if (entries.length >= 200)
      throw new Error(
        "The library holds 200 sequences. Remove one before saving another.",
      );
    entries = [
      ...entries,
      {
        ...checked((payload as { entry: unknown }).entry),
        id: crypto.randomUUID(),
      },
    ];
  } else if (action === "delete")
    entries = entries.filter(
      (entry) => entry.id !== (payload as { id: string }).id,
    );
  if (action !== "load")
    localStorage.setItem(PREVIEW_STORAGE, JSON.stringify(entries));
  return { entries, path: "Development preview storage" };
}

export function MacroLibrary({
  model,
  slot,
  slots,
  steps,
  loopMs,
  onLoad,
  onCopy,
  onPauses,
}: {
  model: ControllerId;
  slot: string;
  slots: string[];
  steps: MacroStep[];
  loopMs: number;
  onLoad: (steps: MacroStep[], loopMs: number, name: string) => void;
  onCopy: (target: string, steps: MacroStep[], loopMs: number) => void;
  onPauses: (steps: MacroStep[]) => void;
}) {
  const [open, setOpen] = useState(false);
  const [entries, setEntries] = useState<SavedMacro[]>([]);
  const [storagePath, setStoragePath] = useState("");
  const [name, setName] = useState("");
  const [target, setTarget] = useState("");
  const [pauseMs, setPauseMs] = useState(50);
  const [busy, setBusy] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const file = useRef<HTMLInputElement>(null);
  const hasSteps = steps.length > 0;
  const destinations = slots.filter((item) => item !== slot);

  useEffect(() => {
    if (!open) return;
    let active = true;
    setLoaded(false);
    setBusy(true);
    setError("");
    setNotice("");
    setName(`${controller(model).name} ${slot} sequence`);
    setTarget(destinations[0] || "");
    library()
      .then((result) => {
        if (active) {
          setEntries(result.entries.map(checked));
          setStoragePath(result.path);
          setLoaded(true);
        }
      })
      .catch((reason) => {
        if (active)
          setError(
            reason instanceof Error
              ? reason.message
              : "Could not load the library.",
          );
      })
      .finally(() => {
        if (active) setBusy(false);
      });
    return () => {
      active = false;
    };
  }, [open, model, slot]);

  async function perform(action: () => Promise<void>) {
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await action();
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : "The library operation failed.",
      );
    } finally {
      setBusy(false);
    }
  }
  function current() {
    return checked({
      format: "x20ctl-macro",
      version: 1,
      name,
      sourceModel: model,
      loopMs,
      steps,
    });
  }
  async function save(entry: SavedMacro) {
    const result = await library("save", { entry });
    setEntries(result.entries.map(checked));
    setNotice(`Saved “${entry.name}” to your library.`);
  }
  async function exportMacro(entry: SavedMacro) {
    if (native()) {
      const result = await request<{ saved: boolean } | null>("macro_export", {
        entry,
      });
      if (result) setNotice("Macro exported.");
      return;
    }
    const { id: _id, ...portable } = entry;
    const url = URL.createObjectURL(
      new Blob([JSON.stringify(portable, null, 2)], {
        type: "application/json",
      }),
    );
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "x20ctl-macro.json";
    anchor.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    setNotice("Macro exported.");
  }

  return (
    <>
      <button className="macro-button" onClick={() => setOpen(true)}>
        <BookOpen size={15} />
        Library & copy
      </button>
      {open && (
        <MetalDialog
          title="Macro library"
          subtitle={`${controller(model).name} / ${slot} · local sequences`}
          className="macro-library-dialog"
          closeLabel="Close macro library"
          onClose={() => setOpen(false)}
        >
          <p className="macro-library-description">
            Save sequences independently from setups. Loading or copying changes
            a draft; it does not send anything to a controller.
          </p>
          <div className="macro-library-current">
            <label>
              Sequence name
              <input
                aria-label="Macro library name"
                value={name}
                maxLength={80}
                onChange={(event) => setName(event.target.value)}
              />
            </label>
            <div className="macro-library-actions">
              <button
                className="macro-button macro-button-primary"
                disabled={busy || !loaded || !hasSteps || !name.trim()}
                onClick={() => perform(() => save(current()))}
              >
                <Save size={15} />
                Save copy
              </button>
              <button
                className="macro-button"
                disabled={busy || !hasSteps || !name.trim()}
                onClick={() => perform(() => exportMacro(current()))}
              >
                <Download size={15} />
                Export current
              </button>
            </div>
            <div className="macro-library-tools">
              <label>
                Copy current to
                <select
                  aria-label="Copy macro destination"
                  value={target}
                  onChange={(event) => setTarget(event.target.value)}
                >
                  {destinations.map((item) => (
                    <option key={item}>{item}</option>
                  ))}
                </select>
              </label>
              <button
                className="macro-button"
                disabled={busy || !hasSteps || !target}
                onClick={() =>
                  perform(async () => {
                    onCopy(target, cloneMacroSteps(steps), loopMs);
                    setNotice(`Copied to the ${target} draft.`);
                  })
                }
              >
                <Copy size={15} />
                Copy to slot
              </button>
              <label>
                Pause after each step
                <input
                  type="number"
                  aria-label="Fixed macro pause"
                  min={0}
                  max={327675}
                  step={5}
                  value={pauseMs}
                  onChange={(event) => setPauseMs(Number(event.target.value))}
                />
              </label>
              <button
                className="macro-button"
                disabled={busy || !hasSteps}
                onClick={() =>
                  perform(async () => {
                    onPauses(setMacroPauses(steps, pauseMs));
                    setNotice("Pauses updated in the current draft.");
                  })
                }
              >
                Set pauses
              </button>
              <button
                className="macro-button"
                disabled={busy || !hasSteps}
                onClick={() =>
                  perform(async () => {
                    onPauses(setMacroPauses(steps, 0));
                    setNotice(
                      "Pauses removed; input hold times are preserved.",
                    );
                  })
                }
              >
                Remove pauses
              </button>
            </div>
            <small>
              {countWireEntries(steps)} / 47 editor entries · 5 ms timing.
              Preview model upload limits are unverified.
            </small>
          </div>
          <div className="macro-library-heading">
            <h3>
              Saved sequences <small>{entries.length}</small>
            </h3>
            <button
              className="macro-button"
              disabled={busy || !loaded}
              onClick={() => {
                if (native())
                  perform(async () => {
                    const entry = await request<unknown>("macro_import");
                    if (entry) await save(checked(entry));
                  });
                else file.current?.click();
              }}
            >
              <Upload size={15} />
              Import macro
            </button>
          </div>
          <input
            ref={file}
            type="file"
            accept=".json,application/json"
            hidden
            onChange={(event) => {
              const selected = event.target.files?.[0];
              event.target.value = "";
              if (selected)
                perform(async () => {
                  if (selected.size > 100000)
                    throw new Error("Macro file is too large.");
                  await save(checked(JSON.parse(await selected.text())));
                });
            }}
          />
          <div className="macro-library-list">
            {!entries.length && loaded && (
              <p>
                No saved sequences yet. Build or record a sequence, then save a
                named copy here.
              </p>
            )}
            {entries.map((entry) => (
              <article key={entry.id}>
                <div>
                  <strong>{entry.name}</strong>
                  <small>
                    {controller(entry.sourceModel as ControllerId).name} ·{" "}
                    {entry.steps.length} steps · {countWireEntries(entry.steps)}{" "}
                    entries
                    {entry.loopMs
                      ? ` · loop ${entry.loopMs} ms`
                      : " · run once"}
                  </small>
                </div>
                <div className="macro-library-actions">
                  <button
                    className="macro-button"
                    disabled={busy}
                    onClick={() =>
                      perform(async () => {
                        const clean = checked(entry);
                        onLoad(
                          cloneMacroSteps(clean.steps),
                          clean.loopMs,
                          clean.name,
                        );
                        setNotice(
                          `Loaded “${clean.name}” into the ${slot} draft.${clean.sourceModel !== model ? " Hardware compatibility between models is unverified." : ""}`,
                        );
                      })
                    }
                  >
                    Load into {slot}
                  </button>
                  <button
                    className="macro-button"
                    aria-label={`Export ${entry.name}`}
                    disabled={busy}
                    onClick={() => perform(() => exportMacro(entry))}
                  >
                    <Download size={15} />
                  </button>
                  <button
                    className="macro-button"
                    aria-label={`Delete ${entry.name}`}
                    disabled={busy}
                    onClick={() =>
                      perform(async () => {
                        const result = await library("delete", {
                          id: entry.id,
                        });
                        setEntries(result.entries.map(checked));
                        setNotice(
                          `Removed “${entry.name}” from the library. Slot drafts are unchanged.`,
                        );
                      })
                    }
                  >
                    <Trash2 size={15} />
                  </button>
                </div>
              </article>
            ))}
          </div>
          {notice && (
            <p className="macro-library-notice" role="status">
              {notice}
            </p>
          )}
          {error && (
            <p className="macro-library-error" role="alert">
              {error}
            </p>
          )}
          {storagePath && (
            <p className="macro-library-location">
              Saved on this computer: <span>{storagePath}</span>
            </p>
          )}
        </MetalDialog>
      )}
    </>
  );
}

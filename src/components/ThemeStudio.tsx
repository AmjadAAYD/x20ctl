import { useState } from "react";
import { MetalDialog } from "./MetalDialog";
import {
  Appearance,
  THEMES,
  contrast,
  saveAppearance,
  validateAppearance,
} from "../theme";

const fields: { key: keyof Appearance; label: string }[] = [
  { key: "background", label: "Background" },
  { key: "surface", label: "Panel surface" },
  { key: "outline", label: "Panel outline" },
  { key: "text", label: "Text" },
  { key: "accent", label: "Accent" },
  { key: "glow", label: "Glow" },
];

export function ThemeStudio({
  value,
  onSave,
  onClose,
}: {
  value: Appearance;
  onSave: (next: Appearance) => void;
  onClose: () => void;
}) {
  const [draft, setDraft] = useState<Appearance>({ ...value });
  const [error, setError] = useState("");
  const valid = validateAppearance(draft);
  const textContrast = contrast(draft.text, draft.surface).toFixed(1);
  const accentContrast = contrast(draft.accent, draft.surface).toFixed(1);
  return (
    <MetalDialog title="Theme studio" subtitle="LOCAL APPEARANCE" onClose={onClose}>
      <div className="theme-studio">
        <p className="theme-intro">Choose a finish, then tune every color. Controller settings stay separate.</p>
        <div className="theme-presets">
          {THEMES.map((theme) => (
            <button
              key={theme.id}
              className={draft.id === theme.id ? "theme-preset selected" : "theme-preset"}
              onClick={() => { setDraft({ ...theme }); setError(""); }}
              aria-pressed={draft.id === theme.id}
            >
              <i style={{ background: theme.accent, boxShadow: `0 0 15px ${theme.glow}` }} />
              {theme.id}
            </button>
          ))}
        </div>
        <div className="theme-color-grid">
          {fields.map(({ key, label }) => (
            <label key={key}>
              <span>{label}</span>
              <input
                type="color"
                aria-label={`${label} color`}
                value={draft[key]}
                onChange={(event) =>
                  setDraft({ ...draft, id: "Custom", [key]: event.target.value })
                }
              />
              <code>{draft[key]}</code>
            </label>
          ))}
        </div>
        <label className="theme-motion">
          Motion
          <select
            aria-label="Motion level"
            value={draft.motion}
            onChange={(event) => setDraft({ ...draft, id: "Custom", motion: event.target.value as Appearance["motion"] })}
          >
            <option value="off">Off</option>
            <option value="subtle">Subtle</option>
            <option value="vivid">Vivid</option>
          </select>
        </label>
        <p className={valid ? "theme-contrast" : "theme-contrast invalid"}>
          Text contrast {textContrast}:1 · Accent contrast {accentContrast}:1
          {!valid && " · Increase contrast to save"}
        </p>
        {error && <p role="alert" className="error-text">{error}</p>}
        <div className="dialog-actions">
          <button className="button secondary" onClick={onClose}>Cancel</button>
          <button
            className="button primary"
            disabled={!valid}
            onClick={() => {
              try { saveAppearance(draft); onSave(draft); onClose(); }
              catch (reason) { setError(reason instanceof Error ? reason.message : String(reason)); }
            }}
          >Save theme</button>
        </div>
      </div>
    </MetalDialog>
  );
}

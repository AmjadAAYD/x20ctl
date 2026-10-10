import { useState } from "react";
import {
  Activity,
  Bluetooth,
  FolderOpen,
  Gamepad2,
  Heart,
  Layers3,
  Monitor,
  Power,
  SlidersHorizontal,
  Volume2,
} from "lucide-react";
import { controller, type ControllerId } from "../controllers";
import { ControllerCanvas } from "./ControllerCanvas";
import { MetalSlider } from "./MetalSlider";
import { ButtonsPage } from "./pages/ButtonsPage";
import { CurvesPage } from "./pages/CurvesPage";
import { MacrosPage } from "./pages/MacrosPage";
import { TesterPage } from "./pages/TesterPage";
import { newProfile, DEFAULT_KEY_REMAPS } from "../data/defaultProfiles";
import { emptyInput } from "../hooks/useGamepad";
import type { MacroStep } from "../types/gamepad";
import brandMark from "../assets/brand-mark.png";
import "./model-workspace.css";
import { MotionNav } from "./Motion";
import { STUDIO_SECTIONS, MissingControllerData, studioSectionLabel } from "./StudioSections";

const pages = [
  { id: "buttons", label: "Buttons", icon: Gamepad2 },
  { id: "curves", label: "Response curves", icon: SlidersHorizontal },
  { id: "macros", label: "Macros", icon: Layers3 },
  { id: "rumble", label: "Vibration", icon: Volume2 },
  { id: "lighting", label: "Lighting", icon: SlidersHorizontal },
  { id: "display", label: "Display", icon: Monitor },
  { id: "power", label: "Power & device", icon: Power },
  { id: "tester", label: "Input tester", icon: Activity },
  { id: "profiles", label: "Saved setups", icon: FolderOpen },
];

export function ModelWorkspace({
  model,
  active,
  player,
  onSwitch,
  onSupport,
  onScan,
}: {
  model: ControllerId;
  active: boolean;
  player: number;
  onSwitch: () => void;
  onSupport: () => void;
  onScan: (authorized?: boolean) => void;
}) {
  const profile = controller(model);
  const [tab, setTab] = useState("buttons");
  const [missing, setMissing] = useState<string | null>(null);
  const [macroSlot, setMacroSlot] = useState(profile.macroSlots[0]);
  const [draft, setDraft] = useState(() => ({
    ...newProfile(),
    controllerId: model,
  }));
  const [macros, setMacros] = useState<Record<string, MacroStep[]>>(() =>
    Object.fromEntries(profile.macroSlots.map((slot) => [slot, []])),
  );
  const [loops, setLoops] = useState<Record<string, number>>(() =>
    Object.fromEntries(profile.macroSlots.map((slot) => [slot, 0])),
  );
  const availablePages = STUDIO_SECTIONS;
  const input = emptyInput();
  const current = availablePages.find((page) => page.id === tab)!;
  if (!active) return null;
  return (
    <div
      className="metal-app rebranded-workspace model-workspace"
      data-page={tab}
      data-model={model}
    >
      <div className="studio-navigation-area">
        <MotionNav active={tab}>
          {availablePages.map((page) => (
            <button
              key={page.id}
              aria-label={page.label}
              title={page.label}
              aria-current={tab === page.id ? "page" : undefined}
              onClick={() => { setTab(page.id); if (page.id !== "buttons") setMissing(page.label); }}
            >
              <page.icon size={19} />
              <span>{studioSectionLabel(page.id) ?? page.label}</span>
            </button>
          ))}
        </MotionNav>
      </div>
      <header className="chassis-header">
        <div className="chassis-brand">
          <img className="brand-emblem" src={brandMark} alt="" />
          <strong>x20ctl</strong>
        </div>
        <div className="header-controller">
          <div className="header-controller-art">
            <ControllerCanvas model={model} disabled />
          </div>
          <div className="header-controller-copy">
            <strong>
              {profile.name} Controller{" "}
              <small className="studio-player-label">Player {player}</small>
            </strong>
            <div className="connection-lights">
              <span>
                <i className="led" />
                <span>
                  Config Connection <b>Not Connected</b>
                </span>
              </span>
              <span>
                <i className="led" />
                <span>
                  Gameplay <b>Not Connected</b>
                </span>
              </span>
            </div>
          </div>
        </div>
        <div className="header-actions">
        <button
          className="sidebar-support"
          onClick={onSupport}
          title="Support X20ctl"
          aria-label="Support X20ctl"
        >
          <Heart size={19} />
          <span>Support X20ctl</span>
        </button>

          <button className="button secondary" onClick={() => onScan()}>Controller scanner</button>
          <button className="button secondary" onClick={onSwitch}>
            Switch Controller
          </button>
          <button
            className="button secondary"
            disabled
            title="Hardware protocol unverified"
          >
            <Bluetooth size={15} />
            Connect controller
          </button>
        </div>
      </header>
      <main className="metal-workspace" id="workspace">
        <h1 className="workspace-page-title">{current.label}</h1>
        <div className="notice model-draft-notice" role="status">
          {profile.name} preview · configuration research in progress. Edits stay in this app session.
        </div>
        <div className="setup-toolbar">
          <div className="setup-name">
            <label htmlFor={`model-setup-${player}`}>Active Setup</label>
            <input
              id={`model-setup-${player}`}
              value={draft.name}
              maxLength={100}
              onChange={(event) =>
                setDraft((old) => ({ ...old, name: event.target.value }))
              }
            />
          </div>
          <span className="draft-indicator">Local draft · Player {player}</span>
          <button
            className="button secondary"
            disabled
            title="Persistent setups await protocol validation"
          >
            Save setup
          </button>
        </div>
        <div key={tab} className="motion-page">
          {tab === "buttons" && (
            <ButtonsPage
              active={active}
              model={model}
              onOpenMacro={(slot) => {
                if (!profile.macroSlots.includes(slot)) return;
                setMacroSlot(slot);
                setTab("macros");
              }}
              remaps={draft.remaps}
              onUpdateRemap={(source, target) =>
                setDraft((old) => ({
                  ...old,
                  remaps: { ...old.remaps, [source]: target },
                }))
              }
              onResetRemaps={() =>
                setDraft((old) => ({
                  ...old,
                  remaps: { ...DEFAULT_KEY_REMAPS },
                }))
              }
              liveState={input}
            />
          )}
          {tab === "curves" && (
            <CurvesPage
              stickCurves={draft.stickCurves}
              triggerCurves={draft.triggerCurves}
              liveState={input}
              liveConnected={false}
              onUpdateStickCurve={(side, config) =>
                setDraft((old) => ({
                  ...old,
                  stickCurves: { ...old.stickCurves, [side]: config },
                }))
              }
              onUpdateTriggerCurve={(side, config) =>
                setDraft((old) => ({
                  ...old,
                  triggerCurves: { ...old.triggerCurves, [side]: config },
                }))
              }
            />
          )}
          {tab === "macros" && profile.macroSlots.length > 0 && (
            <MacrosPage<string>
              model={model}
              initialSlot={macroSlot}
              draftOnly
              slotLabel={profile.macroPlacement === "top" ? "Top button" : "Programmable control"}
              slots={profile.macroSlots}
              loops={loops}
              macros={macros}
              onUpdateLoop={(slot, value) =>
                setLoops((old) => ({ ...old, [slot]: value }))
              }
              onUpdateMacros={(slot, steps) =>
                setMacros((old) => ({ ...old, [slot]: steps }))
              }
              onClearMacro={(slot) =>
                setMacros((old) => ({ ...old, [slot]: [] }))
              }
            />
          )}
          {tab === "rumble" && profile.hardware.vibration && (
            <section className="metal-panel model-feature model-haptics">
              <div className="panel-heading">
                <Volume2 size={17} />
                <h2>Vibration preview</h2>
                <span className="panel-code">LOCAL VISUALIZATION</span>
              </div>
              <div className="model-haptics-body">
                <ControllerCanvas
                  model={model}
                  framing="detail"
                  lighting="vibration"
                  motorPower={draft.vibration}
                  disabled
                />
                <div className="haptics-master">
                  <span className="engraved">DRAFT PREVIEW STRENGTH</span>
                  <div className="large-value">
                    {draft.vibration}
                    <small>%</small>
                  </div>
                  <MetalSlider
                    aria-label="Vibration strength"
                    value={draft.vibration}
                    tone="amber"
                    onValueChange={(value) =>
                      setDraft((old) => ({ ...old, vibration: value }))
                    }
                  />
                  <div className="scale-labels">
                    <span>OFF</span>
                    <span>50</span>
                    <span>MAXIMUM</span>
                  </div>
                  <div className="strength-presets">
                    {[0, 30, 70, 100].map((value, index) => (
                      <button
                        key={value}
                        aria-label={
                          ["Off", "Gentle", "Standard", "Maximum"][index]
                        }
                        className={value === draft.vibration ? "selected" : ""}
                        onClick={() =>
                          setDraft((old) => ({ ...old, vibration: value }))
                        }
                      >
                        {["Off", "Gentle", "Standard", "Maximum"][index]}
                        <b>{value}%</b>
                      </button>
                    ))}
                  </div>
                  <p className="fine-print">
                    {profile.hardware.triggerHaptics
                      ? "Grip and trigger zones use the same visual intensity."
                      : "Grip motor zones use the same visual intensity."}{" "}
                    No motor commands are sent; independent hardware control
                    remains unverified.
                  </p>
                </div>
              </div>
            </section>
          )}
          {tab === "lighting" && profile.hardware.rgb && (
            <section className="metal-panel model-feature">
              <div className="panel-heading">
                <h2>Lighting</h2>
              </div>
              <p>
                RGB lighting is part of this model. PC brightness, color and
                effect commands require hardware validation.
              </p>
              <button className="button primary" disabled>
                Apply lighting
              </button>
            </section>
          )}
          {tab === "display" && profile.hardware.display && (
            <section className="metal-panel model-feature">
              <div className="panel-heading">
                <h2>Display</h2>
                <span className="panel-code">ON-CONTROLLER LCD</span>
              </div>
              <p>
                The onboard display exists. PC image upload, resolution and
                animation support remain unknown.
              </p>
              <button className="button primary" disabled>
                Send to display
              </button>
            </section>
          )}
          {tab === "power" && (
            <section className="metal-panel model-feature">
              <div className="panel-heading">
                <h2>Power & device</h2>
              </div>
              <div className="model-control-grid">
                {[
                  ["Model", profile.name],
                  ["Firmware", "Unknown"],
                  ["Battery", "Not connected"],
                  ["Stick sensors", profile.hardware.sticks],
                  ["Trigger sensors", profile.hardware.triggers],
                  ["Programmable controls", String(profile.macroSlots.length)],
                  ["Lighting", profile.hardware.rgb === null ? "Unknown" : profile.hardware.rgb ? "RGB" : "None"],
                  ["Vibration", profile.hardware.vibration ? "Yes" : "None"],
                  ["Motion controls", profile.hardware.motion === null ? "Unknown" : profile.hardware.motion ? "Yes" : "None"],
                  ["Programmable placement", profile.macroPlacement ?? "Model-defined"],
                ].map(([label, value]) => (
                  <div key={label}>
                    <span>{label}</span>
                    <strong>{value}</strong>
                  </div>
                ))}
              </div>
              <button className="button secondary" disabled>
                Calibrate sticks
              </button>
              <button className="button danger" disabled>
                Reset controller
              </button>
            </section>
          )}
          {tab === "tester" && (
            <TesterPage model={model} liveState={input} inputConnected={false} />
          )}
          {tab === "profiles" && (
            <section className="metal-panel model-feature">
              <div className="panel-heading">
                <h2>Saved {profile.name} setups</h2>
              </div>
              <p>
                Your current draft is kept while switching players during this
                session. Persistent setups and hardware application await
                protocol validation.
              </p>
              <button className="button secondary" disabled>
                Import setup
              </button>
              <button className="button secondary" disabled>
                Export setup
              </button>
            </section>
          )}
        </div>
      </main>
      <footer className="chassis-footer">
        <div className="footer-status">
          <span className="led" />
          <span>Local draft · hardware protocol unverified</span>
        </div>
        <div className="footer-actions">
          <button className="button primary" disabled>
            Apply to controller
          </button>
        </div>
      </footer>
      {missing && <MissingControllerData model={profile.name} section={missing} onLater={() => setMissing(null)} onScan={() => { setMissing(null); onScan(true); }} />}
    </div>
  );
}

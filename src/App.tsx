import { useCallback, useEffect, useRef, useState } from "react";
import {
  Activity,
  ArrowDownToLine,
  ArrowUpFromLine,
  Bluetooth,
  Check,
  ChevronRight,
  CircleHelp,
  Gamepad2,
  Layers3,
  LoaderCircle,
  Monitor,
  Plus,
  Power,
  Radio,
  RefreshCw,
  Save,
  SlidersHorizontal,
  Volume2,
  Trash2,
  X,
  Battery,
  Cpu,
  FolderOpen,
  ShieldCheck,
  Clock3,
  Settings2,
} from "lucide-react";
import { MetalDialog } from "./components/MetalDialog";
import { ButtonsPage } from "./components/pages/ButtonsPage";
import { CurvesPage } from "./components/pages/CurvesPage";
import { MacrosPage } from "./components/pages/MacrosPage";
import { TesterPage } from "./components/pages/TesterPage";
import { Profile } from "./types/gamepad";
import { newProfile } from "./data/defaultProfiles";
import { request, whenNativeReady } from "./native";
import { useGamepad } from "./hooks/useGamepad";

type Tab =
  "buttons" | "curves" | "macros" | "rumble" | "power" | "tester" | "profiles";
type Category =
  | "remaps"
  | "stickCurves"
  | "triggerCurves"
  | "vibration"
  | "idleTimeoutMinutes"
  | "M1"
  | "M2"
  | "M3"
  | "M4";
interface Device {
  connected: boolean;
  name: string;
  device: { version: string };
  capabilities: Record<string, number>;
  battery: { level: number; charging: boolean } | null;
  values: Partial<Profile>;
  warnings: string[];
  motors?: number[];
}
const navigation: {
  id: Tab;
  label: string;
  icon: typeof Gamepad2;
  description: string;
}[] = [
  {
    id: "buttons",
    label: "Buttons",
    icon: Gamepad2,
    description: "Hardware mapping console",
  },
  {
    id: "curves",
    label: "Response curves",
    icon: SlidersHorizontal,
    description: "Stick and trigger response editor",
  },
  {
    id: "macros",
    label: "Macros",
    icon: Layers3,
    description: "M1–M4 paddle sequencer",
  },
  {
    id: "rumble",
    label: "Vibration",
    icon: Volume2,
    description: "Dual-motor strength control",
  },
  {
    id: "power",
    label: "Power & device",
    icon: Power,
    description: "Battery, idle timer and device configuration",
  },
  {
    id: "tester",
    label: "Input tester",
    icon: Activity,
    description: "Live stick, trigger and button input",
  },
  {
    id: "profiles",
    label: "Saved setups",
    icon: FolderOpen,
    description: "Your local setup library",
  },
];

export function App() {
  const running = useRef(false);
  const [updatesEnabled, setUpdatesEnabled] = useState(true);
  const [update, setUpdate] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [version, setVersion] = useState("3.0.0");
  const [tab, setTab] = useState<Tab>("buttons");
  const [profile, setProfile] = useState<Profile>(() => newProfile());
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [device, setDevice] = useState<Device | null>(null);
  const [dirty, setDirty] = useState<Set<Category>>(new Set());
  const [busy, setBusy] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [scanner, setScanner] = useState(false);
  const [devices, setDevices] = useState<{ address: string; name: string }[]>(
    [],
  );
  const [scanned, setScanned] = useState(false);
  const [help, setHelp] = useState(false);
  const [resetConfirm, setResetConfirm] = useState(false);
  const [recording, setRecording] = useState(false);
  const [confirmation, setConfirmation] = useState<{
    title: string;
    description: string;
    action: () => void;
  } | null>(null);
  const onDisconnect = useCallback(() => setDevice(null), []);
  const { liveState, hardwareDetected, slot } = useGamepad(ready, onDisconnect);

  const run = async (label: string, work: () => Promise<void>) => {
    if (running.current) return;
    running.current = true;
    setBusy(label);
    setError("");
    setMessage("");
    try {
      await work();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      running.current = false;
      setBusy("");
    }
  };

  useEffect(
    () =>
      whenNativeReady(() => {
        setReady(true);
        void request<{
          version: string;
          profiles: Profile[];
          warning: string | null;
          updatesEnabled: boolean;
        }>("bootstrap")
          .then((data) => {
            setUpdatesEnabled(data.updatesEnabled);
            setVersion(data.version);
            setProfiles(data.profiles);
            if (data.warning) setError(data.warning);
            if (data.updatesEnabled)
              void request<{ version: string } | null>("check_updates")
                .then((latest) => setUpdate(latest?.version ?? null))
                .catch(() => {});
          })
          .catch((e) => setError(e.message));
      }),
    [],
  );

  const adoptDevice = (next: Device) => {
    setDevice(next);
    setProfile((old) => ({ ...old, ...next.values }));
    setDirty(new Set());
    setMessage(
      next.warnings.length
        ? next.warnings.join(" · ")
        : "Settings read from your controller.",
    );
  };
  const edit = (category: Category, value: unknown) => {
    setProfile((old) =>
      category.startsWith("M")
        ? { ...old, macros: { ...old.macros, [category]: value } }
        : { ...old, [category]: value },
    );
    setDirty((old) => new Set([...old, category]));
    setProfile((old) => ({
      ...old,
      categories: [
        ...new Set([
          ...(old.categories ?? [
            "remaps",
            "stickCurves",
            "triggerCurves",
            "vibration",
            "idleTimeoutMinutes",
            "M1",
            "M2",
            "M3",
            "M4",
          ]),
          category,
        ]),
      ],
    }));
  };
  const canWrite = (category: Category) => {
    if (!device) return false;
    if (category.startsWith("M"))
      return !!(device.capabilities.macros & (1 << (Number(category[1]) - 1)));
    return Object.prototype.hasOwnProperty.call(device.values, category);
  };
  const apply = () =>
    run("Applying changes", async () => {
      for (const category of dirty) {
        if (!canWrite(category))
          throw new Error(
            `${category} is unavailable. Read the controller before applying this setting.`,
          );
      }
      const reports: string[] = [];
      for (const category of dirty) {
        const macro = category.startsWith("M");
        const result = await request<{ message: string }>("apply", {
          category,
          value: macro
            ? profile.macros[category as "M1"]
            : profile[category as keyof Profile],
          loopMs: macro ? profile.macroLoops[category as "M1"] : 0,
        });
        reports.push(`${category}: ${result.message}`);
        setDirty((old) => {
          const next = new Set(old);
          next.delete(category);
          return next;
        });
        setMessage(reports.join(" "));
      }
    });
  const scan = () =>
    run("Scanning Bluetooth", async () => {
      setDevices([]);
      setScanned(false);
      setDevices(await request("scan"));
      setScanned(true);
    });
  const persist = async (next: Profile[]) => {
    await request("save_profiles", { profiles: next });
    setProfiles(next);
  };
  const save = () =>
    run("Saving setup", async () => {
      const stored = {
        ...profile,
        name: profile.name.trim() || "Untitled setup",
      };
      await persist([...profiles.filter((p) => p.id !== stored.id), stored]);
      setMessage("Setup saved on this computer.");
    });
  const loadProfile = (saved: Profile) => {
    setProfile(structuredClone(saved));
    setDirty(
      new Set(
        (saved.categories ?? [
          "remaps",
          "stickCurves",
          "triggerCurves",
          "vibration",
          "idleTimeoutMinutes",
          "M1",
          "M2",
          "M3",
          "M4",
        ]) as Category[],
      ),
    );
    setMessage("Setup loaded into the editor. Review it before applying.");
  };

  const current = navigation.find((item) => item.id === tab)!;
  const confirmDiscard = (action: () => void) => {
    if (!dirty.size) {
      action();
      return;
    }
    setConfirmation({
      title: "Replace unsent changes?",
      description:
        "Your current draft has changes that have not been applied to the controller. Replace this draft to continue.",
      action,
    });
  };
  const createSetup = () =>
    confirmDiscard(() => {
      setProfile(newProfile());
      setDirty(new Set());
      setMessage("New local draft created.");
    });
  const importSetup = () =>
    void run("Importing setup", async () => {
      const imported = await request<Profile | null>("open_profile");
      if (imported) {
        await persist([...profiles, imported]);
        confirmDiscard(() => loadProfile(imported));
      }
    });
  const exportSetup = () =>
    void run("Exporting setup", async () => {
      const result = await request("export_profile", { profile });
      if (result) setMessage("Setup exported.");
    });
  const record = () =>
    void run(
      recording ? "Finishing recording" : "Starting recording",
      async () => {
        if (recording) {
          setRecording(false);
          const rows = await request<Profile["macros"]["M1"]>("record_stop");
          edit("M1", rows);
          setMessage("Recording placed in M1. Review before applying.");
        } else {
          await request("record_start");
          setRecording(true);
        }
      },
    );
  const battery = device?.battery;
  const changedMappings = Object.entries(profile.remaps).filter(
    ([key, value]) => key !== value,
  ).length;
  const programmedPaddles = Object.values(profile.macros).filter(
    (rows) => rows.length,
  ).length;

  return (
    <div className="metal-app">
      <a className="skip-link" href="#workspace">
        Skip to editor
      </a>
      <header className="chassis-header">
        <div className="chassis-brand">
          <div className="brand-emblem">
            <Gamepad2 size={24} strokeWidth={1.5} />
          </div>
          <div>
            <strong>x20ctl</strong>
            <span>CONTROLLER STUDIO</span>
          </div>
          <span className="edition-tag">METAL</span>
        </div>
        <div className="connection-lights" aria-label="Connection status">
          <span>
            <i className={device ? "led on" : "led"} /> CONFIG{" "}
            <b>{device ? "LINKED" : "OFFLINE"}</b>
          </span>
          <span>
            <i className={hardwareDetected ? "led on" : "led"} /> XINPUT{" "}
            <b>{hardwareDetected ? "P" + ((slot ?? 0) + 1) : "OFFLINE"}</b>
          </span>
        </div>
        <div className="header-actions">
          <button
            className="button secondary"
            disabled={!ready || !!busy}
            onClick={() => {
              setScanner(true);
              void scan();
            }}
          >
            <Bluetooth size={15} />
            {device ? "Devices" : "Connect controller"}
          </button>
          <button
            className="icon-button"
            title="Connection guide"
            aria-label="Connection guide"
            onClick={() => setHelp(true)}
          >
            <CircleHelp size={18} />
          </button>
        </div>
      </header>

      <nav className="chassis-nav" aria-label="Workspace">
        {navigation.map((item) => (
          <button
            key={item.id}
            aria-current={tab === item.id ? "page" : undefined}
            onClick={() => setTab(item.id)}
          >
            <item.icon size={16} />
            <span>{item.label}</span>
          </button>
        ))}
      </nav>

      <main id="workspace" className="metal-workspace">
        <section className="device-strip">
          <div className="device-identity">
            <div className="device-chip">
              <Cpu size={25} strokeWidth={1.3} />
            </div>
            <div>
              <span className="engraved">
                EASYSMX X20 / CONFIGURATION STUDIO
              </span>
              <h1>{current.label}</h1>
              <p>{current.description}</p>
            </div>
          </div>
          <div className="device-readouts">
            <span>
              <small>CONFIGURATION</small>
              <b>{device?.name || "Not connected"}</b>
            </span>
            <span>
              <small>FIRMWARE</small>
              <b>{device?.device.version || "—"}</b>
            </span>
            <span className="battery-readout">
              <Battery size={20} />
              <b>
                {battery ? battery.level + "/4 bars" : "—"}
                {battery?.charging ? " · Charging" : ""}
              </b>
            </span>
          </div>
        </section>

        {!ready && (
          <div className="notice">
            Desktop connection unavailable. Launch x20ctl.exe to connect your
            controller.
          </div>
        )}
        {update && (
          <div className="notice">
            Version {update} is available.
            <button
              className="button secondary"
              onClick={() => void request("open_releases")}
            >
              View release
            </button>
          </div>
        )}
        {error && (
          <div role="alert" className="notice error">
            <span>{error}</span>
            <button
              className="icon-button"
              aria-label="Dismiss error"
              onClick={() => setError("")}
            >
              <X size={16} />
            </button>
          </div>
        )}
        {message && (
          <div role="status" className="notice success">
            <span>
              <Check size={15} />
              {message}
            </span>
            <button
              className="icon-button"
              aria-label="Dismiss message"
              onClick={() => setMessage("")}
            >
              <X size={16} />
            </button>
          </div>
        )}

        <div className="setup-toolbar">
          <div className="setup-name">
            <Layers3 size={17} />
            <label htmlFor="setup-name">SETUP</label>
            <input
              id="setup-name"
              aria-label="Setup name"
              disabled={!!busy}
              value={profile.name}
              maxLength={100}
              onChange={(e) => setProfile({ ...profile, name: e.target.value })}
            />
          </div>
          <span className={"draft-indicator " + (dirty.size ? "pending" : "")}>
            {dirty.size
              ? `${dirty.size} unsent ${dirty.size === 1 ? "change" : "changes"}`
              : device
                ? "Read from device"
                : "Offline draft"}
          </span>
          <div className="toolbar-actions">
            <button
              className="button secondary compact"
              disabled={!ready || !!busy}
              onClick={save}
            >
              <Save size={14} />
              Save
            </button>
            <button
              className="icon-button"
              title="New setup"
              aria-label="New setup"
              disabled={!!busy}
              onClick={createSetup}
            >
              <Plus size={17} />
            </button>
            <button
              className="icon-button"
              title="Import setup"
              aria-label="Import setup"
              disabled={!ready || !!busy}
              onClick={importSetup}
            >
              <ArrowDownToLine size={17} />
            </button>
            <button
              className="icon-button"
              title="Export setup"
              aria-label="Export setup"
              disabled={!ready || !!busy}
              onClick={exportSetup}
            >
              <ArrowUpFromLine size={17} />
            </button>
          </div>
        </div>

        <fieldset disabled={!!busy} className="editor">
          {tab === "buttons" && (
            <ButtonsPage
              remaps={profile.remaps}
              liveState={liveState}
              inputConnected={hardwareDetected}
              onUpdateRemap={(key, target) =>
                edit("remaps", { ...profile.remaps, [key]: target })
              }
              onResetRemaps={() => edit("remaps", newProfile().remaps)}
            />
          )}
          {tab === "curves" && (
            <CurvesPage
              stickCurves={profile.stickCurves}
              triggerCurves={profile.triggerCurves}
              liveState={liveState}
              liveConnected={hardwareDetected}
              onUpdateStickCurve={(side, cfg) =>
                edit("stickCurves", { ...profile.stickCurves, [side]: cfg })
              }
              onUpdateTriggerCurve={(side, cfg) =>
                edit("triggerCurves", { ...profile.triggerCurves, [side]: cfg })
              }
            />
          )}
          {tab === "macros" && (
            <>
              <div className="record-toolbar">
                <div>
                  <span className={"led " + (recording ? "recording" : "")} />
                  <strong>
                    {recording ? "RECORDING LIVE INPUT" : "XINPUT RECORDER"}
                  </strong>
                  <span className="muted">
                    Buttons + left-stick directions · 5 ms timing
                  </span>
                </div>
                <button
                  className={"button " + (recording ? "danger" : "secondary")}
                  disabled={!ready || (!recording && !hardwareDetected)}
                  onClick={record}
                >
                  <Radio size={15} />
                  {recording ? "Stop recording → M1" : "Record to M1"}
                </button>
              </div>
              <MacrosPage
                loops={profile.macroLoops}
                onUpdateLoop={(key, value) => {
                  setProfile((old) => ({
                    ...old,
                    macroLoops: { ...old.macroLoops, [key]: value },
                  }));
                  edit(key, profile.macros[key]);
                }}
                macros={profile.macros}
                onUpdateMacros={(key, steps) => edit(key, steps)}
                onClearMacro={(key) => edit(key, [])}
              />
            </>
          )}
          {tab === "rumble" && (
            <div className="haptics-layout">
              <section className="metal-panel haptics-console">
                <div className="panel-heading">
                  <Volume2 size={17} />
                  <h2>Unified motor control</h2>
                  <span className="panel-code">HAPTICS / 01</span>
                </div>
                <div className="haptics-master">
                  <span className="engraved">STORED VIBRATION STRENGTH</span>
                  <div className="large-value">
                    {profile.vibration}
                    <small>%</small>
                  </div>
                  <input
                    aria-label="Vibration strength"
                    type="range"
                    min="0"
                    max="100"
                    value={profile.vibration}
                    onChange={(e) => edit("vibration", Number(e.target.value))}
                  />
                  <div className="scale-labels">
                    <span>OFF</span>
                    <span>50</span>
                    <span>MAXIMUM</span>
                  </div>
                  <div className="sync-caption">
                    <ShieldCheck size={15} /> Both motors receive the same
                    strength setting
                  </div>
                </div>
                <div className="motor-pair">
                  {["Left", "Right"].map((name, i) => (
                    <div className="motor-instrument" key={name}>
                      <div className="motor-disc">
                        <div className="motor-hub" />
                        <div
                          className="motor-arc"
                          style={{
                            transform: `rotate(${profile.vibration * 2.7 - 135}deg)`,
                          }}
                        />
                      </div>
                      <div>
                        <span className="engraved">{name} motor</span>
                        <b>
                          {profile.vibration}
                          <small>%</small>
                        </b>
                        <span className="muted">Draft strength</span>
                      </div>
                      <div className="motor-level">
                        <i style={{ height: `${profile.vibration}%` }} />
                      </div>
                      <span className="motor-readback">
                        Last read:{" "}
                        {device?.motors?.[i] !== undefined
                          ? device.motors[i] + "%"
                          : "—"}
                      </span>
                    </div>
                  ))}
                </div>
              </section>
              <aside className="haptics-presets metal-panel">
                <div className="panel-heading">
                  <SlidersHorizontal size={17} />
                  <h2>Strength presets</h2>
                </div>
                <div className="strength-presets">
                  {[0, 30, 70, 100].map((v, i) => (
                    <button
                      key={v}
                      className={profile.vibration === v ? "selected" : ""}
                      onClick={() => edit("vibration", v)}
                    >
                      <div>
                        <strong>
                          {["Off", "Gentle", "Standard", "Maximum"][i]}
                        </strong>
                        <small>
                          {
                            [
                              "No vibration",
                              "Light feedback",
                              "Balanced feedback",
                              "Full stored strength",
                            ][i]
                          }
                        </small>
                      </div>
                      <b>
                        {v}
                        <small>%</small>
                      </b>
                      <div className="preset-meter">
                        <i style={{ width: v + "%" }} />
                      </div>
                    </button>
                  ))}
                </div>
                <div className="panel-footnote">
                  <p>
                    This changes the strength stored on the controller. Select
                    Apply changes to send it.
                  </p>
                  <p>Games control when the motors run.</p>
                </div>
              </aside>
            </div>
          )}
          {tab === "power" && (
            <div className="power-layout">
              <section className="metal-panel battery-panel">
                <div className="panel-heading">
                  <Battery size={18} />
                  <h2>Battery status</h2>
                </div>
                <div className="battery-dial">
                  <svg viewBox="0 0 220 220" aria-hidden="true">
                    <circle cx="110" cy="110" r="92" />
                    <circle
                      className="battery-ring"
                      cx="110"
                      cy="110"
                      r="92"
                      strokeDasharray={`${battery ? (battery.level / 4) * 578 : 0} 578`}
                    />
                  </svg>
                  <div>
                    <span className="engraved">CONTROLLER REPORT</span>
                    <strong>
                      {battery ? battery.level : "—"}
                      <small>{battery ? "/4" : ""}</small>
                    </strong>
                    <span>
                      {battery
                        ? battery.charging
                          ? "Charging"
                          : "Battery bars"
                        : "Connect to read"}
                    </span>
                  </div>
                </div>
                <div className="battery-segments">
                  {[1, 2, 3, 4].map((i) => (
                    <span
                      key={i}
                      className={battery && battery.level >= i ? "filled" : ""}
                    />
                  ))}
                </div>
                <p className="panel-footnote">
                  The X20 reports four battery levels. An exact percentage or
                  remaining time is not available.
                </p>
              </section>
              <div className="power-settings">
                <section className="metal-panel">
                  <div className="panel-heading">
                    <Clock3 size={17} />
                    <h2>Auto-sleep settings</h2>
                  </div>
                  <div className="sleep-control">
                    <div>
                      <span className="engraved">IDLE TIMEOUT</span>
                      <strong>
                        {profile.idleTimeoutMinutes || "Never"}
                        {profile.idleTimeoutMinutes > 0 && (
                          <small> minutes</small>
                        )}
                      </strong>
                    </div>
                    <div className="preset-row">
                      {[5, 10, 15, 30, 0].map((v) => (
                        <button
                          key={v}
                          className={
                            profile.idleTimeoutMinutes === v ? "selected" : ""
                          }
                          onClick={() => edit("idleTimeoutMinutes", v)}
                        >
                          {v ? v + " minutes" : "Never"}
                        </button>
                      ))}
                    </div>
                    <p className="muted">
                      Powers off the controller after inactivity. Apply to store
                      this setting.
                    </p>
                  </div>
                </section>
                <section className="metal-panel">
                  <div className="panel-heading">
                    <Cpu size={17} />
                    <h2>Hardware information</h2>
                  </div>
                  <dl className="hardware-details">
                    <dt>Configuration device</dt>
                    <dd>{device?.name || "Unavailable"}</dd>
                    <dt>Firmware version</dt>
                    <dd>{device?.device.version || "Unavailable"}</dd>
                    <dt>Configuration link</dt>
                    <dd>
                      {device ? "Bluetooth LE · connected" : "Disconnected"}
                    </dd>
                    <dt>Gameplay input</dt>
                    <dd>
                      {hardwareDetected
                        ? `XInput player ${(slot ?? 0) + 1}`
                        : "Not detected"}
                    </dd>
                  </dl>
                </section>
                <section className="metal-panel reset-panel">
                  <div>
                    <h2>Reset configuration</h2>
                    <p>
                      Restore the controller's mappings, curves and macros to
                      defaults.
                    </p>
                  </div>
                  <button
                    className="button danger"
                    disabled={!device}
                    onClick={() => setResetConfirm(true)}
                  >
                    Factory reset…
                  </button>
                </section>
              </div>
            </div>
          )}
          {tab === "tester" && (
            <TesterPage
              liveState={liveState}
              inputConnected={hardwareDetected}
              inputSlot={slot}
            />
          )}
          {tab === "profiles" && (
            <section className="profile-library">
              <div className="library-heading">
                <div>
                  <h2>Local setup library</h2>
                  <p>
                    {profiles.length} saved{" "}
                    {profiles.length === 1 ? "setup" : "setups"} · Select a
                    setup to load it into the editor
                  </p>
                </div>
                <button className="button secondary" onClick={createSetup}>
                  <Plus size={16} />
                  New setup
                </button>
              </div>
              {profiles.length ? (
                <div className="library-grid">
                  {profiles.map((saved, i) => (
                    <article
                      className={
                        "metal-panel profile-card " +
                        (saved.id === profile.id ? "active" : "")
                      }
                      key={saved.id}
                    >
                      <div className="profile-card-top">
                        <div className="profile-emblem">
                          <Layers3 size={30} strokeWidth={1.2} />
                        </div>
                        <div>
                          <span className="engraved">
                            SETUP {String(i + 1).padStart(2, "0")}
                          </span>
                          <h3>{saved.name}</h3>
                          <p>
                            {saved.id === profile.id
                              ? "Open in editor"
                              : "Stored on this computer"}
                          </p>
                        </div>
                        <button
                          className="icon-button"
                          aria-label={"Delete " + saved.name}
                          onClick={() =>
                            setConfirmation({
                              title: "Delete saved setup?",
                              description: `Remove “${saved.name}” from this computer? The open draft and controller settings will stay as they are.`,
                              action: () =>
                                void run("Deleting setup", async () => {
                                  await persist(
                                    profiles.filter((p) => p.id !== saved.id),
                                  );
                                  setMessage(
                                    "Saved setup deleted. The current draft is unchanged.",
                                  );
                                }),
                            })
                          }
                        >
                          <Trash2 size={16} />
                        </button>
                      </div>
                      <div className="profile-specs">
                        <span>
                          {
                            Object.entries(saved.remaps).filter(
                              ([k, v]) => k !== v,
                            ).length
                          }{" "}
                          mappings
                        </span>
                        <span>
                          {
                            Object.values(saved.macros).filter(
                              (rows) => rows.length,
                            ).length
                          }{" "}
                          paddles
                        </span>
                        <span>{saved.vibration}% vibration</span>
                      </div>
                      <button
                        className="button secondary"
                        onClick={() => confirmDiscard(() => loadProfile(saved))}
                      >
                        Load setup
                        <ChevronRight size={15} />
                      </button>
                    </article>
                  ))}
                </div>
              ) : (
                <div className="metal-panel library-empty">
                  <FolderOpen size={44} strokeWidth={1} />
                  <h3>Your setups belong here</h3>
                  <p>
                    Save the current setup or import an existing JSON file to
                    build your library.
                  </p>
                  <div>
                    <button
                      className="button primary"
                      disabled={!ready}
                      onClick={save}
                    >
                      <Save size={16} />
                      Save current setup
                    </button>
                    <button
                      className="button secondary"
                      disabled={!ready}
                      onClick={importSetup}
                    >
                      <ArrowDownToLine size={16} />
                      Import JSON
                    </button>
                  </div>
                </div>
              )}
              <div className="metal-panel library-current">
                <span className="engraved">CURRENT DRAFT</span>
                <strong>{profile.name}</strong>
                <span>
                  {changedMappings} remaps · {programmedPaddles} configured
                  paddles
                </span>
                <button
                  className="button secondary"
                  disabled={!ready}
                  onClick={exportSetup}
                >
                  <ArrowUpFromLine size={16} />
                  Export JSON
                </button>
              </div>
            </section>
          )}
        </fieldset>
      </main>

      <footer className="chassis-footer">
        <div className="footer-status">
          {busy ? (
            <>
              <LoaderCircle className="spin" size={15} />
              <span>{busy}…</span>
            </>
          ) : (
            <>
              <span className={device ? "led on" : "led"} />
              <span>
                {device
                  ? "Configuration link ready"
                  : "Drafts stay on this computer"}
              </span>
            </>
          )}
          <span className="version">v{version}</span>
        </div>
        <div className="footer-actions">
          <button
            className="button secondary"
            disabled={!device || !!busy}
            onClick={() =>
              confirmDiscard(
                () =>
                  void run("Reading controller", async () =>
                    adoptDevice(await request("read")),
                  ),
              )
            }
          >
            <RefreshCw size={15} />
            Read controller
          </button>
          <button
            className="button primary"
            disabled={!device || !dirty.size || !!busy}
            onClick={apply}
          >
            <Check size={16} />
            Apply changes
          </button>
        </div>
      </footer>

      {scanner && (
        <MetalDialog
          title="Link your controller"
          subtitle="BLUETOOTH LE CONFIGURATION"
          closeLabel="Close devices"
          className="connection-dialog"
          onClose={() => setScanner(false)}
        >
          <div className="connection-mode">
            <Bluetooth size={15} />
            <span>Bluetooth LE settings</span>
            <small>USB / receiver input can stay connected</small>
          </div>
          <div
            className={
              "scan-radar " + (busy === "Scanning Bluetooth" ? "scanning" : "")
            }
          >
            <div className="radar-grid" />
            <div className="radar-rings" />
            {busy === "Scanning Bluetooth" && <div className="radar-sweep" />}
            <div className="radar-caption">
              <Radio size={22} />
              <strong>
                {busy === "Scanning Bluetooth"
                  ? "Scanning Bluetooth…"
                  : devices.length
                    ? `${devices.length} configuration device found`
                    : "Ready to connect"}
              </strong>
              <span>Wake your X20 and keep it nearby</span>
            </div>
          </div>
          <div className="list-caption">
            <span>AVAILABLE DEVICES</span>
            <button
              className="button secondary compact"
              disabled={!!busy}
              onClick={scan}
            >
              <RefreshCw size={13} />
              Scan again
            </button>
          </div>
          <div className="device-results">
            {devices.length ? (
              devices.map((found) => (
                <button
                  className="device-result"
                  key={found.address}
                  disabled={!!busy}
                  onClick={() =>
                    void run("Connecting and reading", async () => {
                      const next = await request<Device>("connect", {
                        address: found.address,
                      });
                      if (dirty.size) {
                        setDevice(next);
                        setMessage(
                          "Connected. Your unsent draft was preserved. Read controller to replace it.",
                        );
                      } else adoptDevice(next);
                      setScanner(false);
                    })
                  }
                >
                  <Gamepad2 size={25} />
                  <span>
                    <strong>{found.name || "Controller"}</strong>
                    <small>{found.address}</small>
                  </span>
                  <span className="device-connect-label">
                    Connect <ChevronRight size={15} />
                  </span>
                </button>
              ))
            ) : (
              <div className="scan-empty">
                {busy ? (
                  <LoaderCircle className="spin" size={20} />
                ) : (
                  <Bluetooth size={25} />
                )}
                <span>
                  {busy ||
                    (scanned
                      ? "No supported controllers found. Wake the controller and try again."
                      : "Scan for your controller's configuration connection.")}
                </span>
              </div>
            )}
          </div>
          {device && (
            <div className="connected-row">
              <span className="led on" />
              <span>{device.name} connected</span>
              <button
                className="button secondary compact"
                disabled={!!busy}
                onClick={() =>
                  void run("Disconnecting", async () => {
                    await request("disconnect");
                    setDevice(null);
                    setRecording(false);
                  })
                }
              >
                Disconnect
              </button>
            </div>
          )}
          {error && (
            <p className="error-text" role="alert">
              {error}
            </p>
          )}
          <p className="fine-print">
            The X20 configuration peripheral usually appears as Xpert2. Close
            phone configuration apps before connecting. EasySMX X05 is not
            supported.
          </p>
        </MetalDialog>
      )}
      {help && (
        <MetalDialog
          title="Two connections. One controller."
          subtitle="CONNECTION GUIDE"
          closeLabel="Close guide"
          onClose={() => setHelp(false)}
        >
          <div className="help-connections">
            <section>
              <Bluetooth size={23} />
              <h3>Configuration</h3>
              <p>
                Bluetooth LE reads and writes mappings, macros, curves and power
                settings. Connect to the Xpert2 peripheral in the device
                scanner.
              </p>
            </section>
            <section>
              <Monitor size={23} />
              <h3>Gameplay input</h3>
              <p>
                USB, the receiver or a compatible Bluetooth mode supplies XInput
                to the tester and recorder. It can work alongside the
                configuration link.
              </p>
            </section>
          </div>
          <div className="help-tip">
            Keep Bluetooth enabled, wake the controller, and disconnect any
            phone app using its settings connection.
          </div>
          <label className="update-preference">
            <Settings2 size={18} />
            <span>
              <strong>Check for app updates</strong>
              <small>Contact GitHub's release API at startup</small>
            </span>
            <input
              type="checkbox"
              role="switch"
              checked={updatesEnabled}
              disabled={!!busy}
              onChange={(e) => {
                const enabled = e.target.checked;
                void run("Saving preference", async () =>
                  setUpdatesEnabled(await request("set_updates", { enabled })),
                );
              }}
            />
          </label>
          <p className="fine-print">
            Profiles and controller settings stay on this computer. Closing the
            window keeps x20ctl in the system tray. Choose Quit from the tray
            menu to exit.
          </p>
        </MetalDialog>
      )}
      {resetConfirm && (
        <MetalDialog
          title="Reset every controller setting?"
          subtitle="CONTROLLER RESET"
          onClose={() => {
            if (!busy) setResetConfirm(false);
          }}
        >
          <p>
            This clears the controller's mappings, macros and curves. Saved
            setups on this computer are kept.
          </p>
          <div className="dialog-actions">
            <button
              className="button secondary"
              disabled={!!busy}
              onClick={() => setResetConfirm(false)}
            >
              Cancel
            </button>
            <button
              className="button danger"
              disabled={!!busy}
              onClick={() =>
                void run("Resetting controller", async () => {
                  const result = await request<{ message: string }>("reset", {
                    confirmation: "RESET",
                  });
                  setDevice(null);
                  setResetConfirm(false);
                  setMessage(result.message);
                })
              }
            >
              Reset controller
            </button>
          </div>
          {error && <p className="error-text">{error}</p>}
        </MetalDialog>
      )}
      {confirmation && (
        <MetalDialog
          title={confirmation.title}
          subtitle="UNSENT DRAFT / LOCAL STORAGE"
          onClose={() => setConfirmation(null)}
        >
          <p>{confirmation.description}</p>
          <div className="dialog-actions">
            <button
              className="button secondary"
              onClick={() => setConfirmation(null)}
            >
              Cancel
            </button>
            <button
              className="button primary"
              onClick={() => {
                const action = confirmation.action;
                setConfirmation(null);
                action();
              }}
            >
              Continue
            </button>
          </div>
        </MetalDialog>
      )}
    </div>
  );
}

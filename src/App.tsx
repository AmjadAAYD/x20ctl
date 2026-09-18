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
} from "lucide-react";
import { ButtonsPage } from "./components/pages/ButtonsPage";
import { CurvesPage } from "./components/pages/CurvesPage";
import { MacrosPage } from "./components/pages/MacrosPage";
import { TesterPage } from "./components/pages/TesterPage";
import { Profile } from "./types/gamepad";
import { newProfile } from "./data/defaultProfiles";
import { request, whenNativeReady } from "./native";
import { useGamepad } from "./hooks/useGamepad";

type Tab = "buttons" | "curves" | "macros" | "rumble" | "power" | "tester";
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
    description: "Make every button yours.",
  },
  {
    id: "curves",
    label: "Response curves",
    icon: SlidersHorizontal,
    description: "Find the response that feels right.",
  },
  {
    id: "macros",
    label: "Macros",
    icon: Layers3,
    description: "Build a sequence. Give it a paddle.",
  },
  {
    id: "rumble",
    label: "Vibration",
    icon: Volume2,
    description: "Tune the strength of your feedback.",
  },
  {
    id: "power",
    label: "Power & device",
    icon: Power,
    description: "A little control over the essentials.",
  },
  {
    id: "tester",
    label: "Input tester",
    icon: Activity,
    description: "See exactly what your controller is sending.",
  },
];

export function App() {
  const running = useRef(false);
  const [updatesEnabled, setUpdatesEnabled] = useState(true);
  const [update, setUpdate] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [version, setVersion] = useState("2.0.1");
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
    if (dirty.size && !window.confirm("Replace the current unsent draft?"))
      return;
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

  return (
    <div className="desktop-shell">
      <aside className="app-sidebar">
        <div className="brand">
          <div className="brand-symbol">
            <Gamepad2 size={25} />
          </div>
          <div>
            x20ctl<span>CONTROLLER STUDIO</span>
          </div>
        </div>
        <div className="side-label">WORKSPACE</div>
        <nav>
          {navigation.map((item) => (
            <button
              key={item.id}
              aria-current={tab === item.id ? "page" : undefined}
              onClick={() => setTab(item.id)}
            >
              <item.icon size={18} />
              <span>{item.label}</span>
              {tab === item.id && <ChevronRight size={14} />}
            </button>
          ))}
        </nav>
        <div className="side-label setup-label">
          SAVED SETUPS
          <button
            title="New setup"
            disabled={!!busy}
            onClick={() => {
              if (
                dirty.size &&
                !window.confirm("Discard unsent draft changes?")
              )
                return;
              setProfile(newProfile());
              setDirty(new Set());
            }}
          >
            <Plus size={16} />
          </button>
        </div>
        <div className="saved-list">
          {profiles.length ? (
            profiles.map((saved) => (
              <div className="saved-item" key={saved.id}>
                <button
                  disabled={!!busy}
                  key={saved.id}
                  onClick={() => loadProfile(saved)}
                >
                  <Layers3 size={15} />
                  <span>{saved.name}</span>
                </button>
                <button
                  className="delete-setup"
                  aria-label={`Delete ${saved.name}`}
                  disabled={!!busy}
                  onClick={() => {
                    if (
                      !window.confirm(
                        `Delete saved setup "${saved.name}"? The current draft will stay open.`,
                      )
                    )
                      return;
                    void run("Deleting setup", async () => {
                      await persist(profiles.filter((p) => p.id !== saved.id));
                      setMessage(
                        "Saved setup deleted. The current draft is unchanged.",
                      );
                    });
                  }}
                >
                  <Trash2 size={13} />
                </button>
              </div>
            ))
          ) : (
            <p>
              Your favorite settings,
              <br />
              ready for the next session.
            </p>
          )}
        </div>
        <div className="sidebar-bottom">
          <span className="version">v{version} · Windows desktop</span>
          <button onClick={() => setHelp(true)}>
            <CircleHelp size={16} /> Connection guide
          </button>
        </div>
      </aside>

      <div className="workspace">
        <header className="topbar">
          <div className="connection-summary">
            <span className={`status-dot ${device ? "online" : ""}`} />
            <div>
              <strong>
                {device
                  ? device.name || "Controller connected"
                  : "No controller connected"}
              </strong>
              <small>
                {device
                  ? `Bluetooth LE configuration · Firmware ${device.device.version}`
                  : "Connect via Bluetooth LE to read and apply settings"}
              </small>
            </div>
          </div>
          <button
            className="button secondary"
            disabled={!ready || !!busy}
            onClick={() => {
              setScanner(true);
              void scan();
            }}
          >
            <Bluetooth size={16} />
            {device ? "Devices" : "Connect controller"}
          </button>
        </header>
        <main>
          <div className="page-heading">
            <div>
              <div className="eyebrow">YOUR CONTROLLER, YOUR WAY</div>
              <h1>{current.label}</h1>
              <p>{current.description}</p>
            </div>
            <div className="input-badge">
              <Monitor size={15} />
              {hardwareDetected
                ? `XInput player ${(slot ?? 0) + 1} detected`
                : "No gameplay input"}
            </div>
          </div>
          {!ready && (
            <div className="notice">
              Desktop connection unavailable. Launch the packaged x20ctl
              application to configure hardware.
            </div>
          )}
          {update && (
            <div className="notice">
              Version {update} is available.
              <button onClick={() => void request("open_releases")}>
                View release
              </button>
            </div>
          )}
          {error && (
            <div role="alert" className="notice error">
              {error}
              <button aria-label="Dismiss error" onClick={() => setError("")}>
                <X size={16} />
              </button>
            </div>
          )}
          {message && (
            <div role="status" className="notice success">
              {message}
              <button
                aria-label="Dismiss message"
                onClick={() => setMessage("")}
              >
                <X size={16} />
              </button>
            </div>
          )}
          <div className="profile-bar">
            <Layers3 size={17} />
            <input
              aria-label="Setup name"
              disabled={!!busy}
              value={profile.name}
              maxLength={100}
              onChange={(e) => setProfile({ ...profile, name: e.target.value })}
            />
            <span className="draft-label">
              {dirty.size
                ? `${dirty.size} unsent ${dirty.size === 1 ? "change" : "changes"}`
                : device
                  ? "Read from device"
                  : "Offline draft"}
            </span>
            <button
              title="Save setup locally"
              disabled={!ready || !!busy}
              onClick={save}
            >
              <Save size={17} />
              <span>Save</span>
            </button>
            <button
              title="Import setup"
              disabled={!ready || !!busy}
              onClick={() =>
                void run("Importing setup", async () => {
                  const imported = await request<Profile | null>(
                    "open_profile",
                  );
                  if (imported) {
                    await persist([...profiles, imported]);
                    loadProfile(imported);
                  }
                })
              }
            >
              <ArrowDownToLine size={17} />
            </button>
            <button
              title="Export setup"
              disabled={!ready || !!busy}
              onClick={() =>
                void run("Exporting setup", async () => {
                  const result = await request("export_profile", { profile });
                  if (result) setMessage("Setup exported.");
                })
              }
            >
              <ArrowUpFromLine size={17} />
            </button>
          </div>
          <fieldset disabled={!!busy} className="editor">
            {tab === "buttons" && (
              <ButtonsPage
                remaps={profile.remaps}
                liveState={liveState}
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
                onUpdateStickCurve={(side, cfg) =>
                  edit("stickCurves", { ...profile.stickCurves, [side]: cfg })
                }
                onUpdateTriggerCurve={(side, cfg) =>
                  edit("triggerCurves", {
                    ...profile.triggerCurves,
                    [side]: cfg,
                  })
                }
              />
            )}
            {tab === "macros" && (
              <>
                <div className="macro-record">
                  <span>
                    Capture real buttons and left-stick directions. Timing uses
                    the controller’s 5 ms grid.
                  </span>
                  <button
                    className="button secondary"
                    disabled={!ready || (!recording && !hardwareDetected)}
                    onClick={() =>
                      void run(
                        recording
                          ? "Finishing recording"
                          : "Starting recording",
                        async () => {
                          if (recording) {
                            setRecording(false);
                            const rows =
                              await request<Profile["macros"]["M1"]>(
                                "record_stop",
                              );
                            edit("M1", rows);
                            setMessage(
                              "Recording placed in M1. Review before applying.",
                            );
                          } else {
                            await request("record_start");
                            setRecording(true);
                          }
                        },
                      )
                    }
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
              <section className="settings-card">
                <div className="card-icon">
                  <Volume2 size={30} />
                </div>
                <h2>Vibration strength</h2>
                <p>
                  Set the stored strength for both motors. Changes are sent when
                  you select Apply changes.
                </p>
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
                <div className="preset-row">
                  {[0, 30, 70, 100].map((v, i) => (
                    <button
                      key={v}
                      onClick={() => edit("vibration", v)}
                      className={profile.vibration === v ? "selected" : ""}
                    >
                      {["Off", "Gentle", "Standard", "Maximum"][i]}
                    </button>
                  ))}
                </div>
              </section>
            )}
            {tab === "power" && (
              <div className="power-grid">
                <section className="settings-card">
                  <Power className="accent" size={27} />
                  <h2>Sleep timer</h2>
                  <p>Turn off your controller after a period of inactivity.</p>
                  <div className="preset-row wrap">
                    {[5, 10, 15, 30, 0].map((v) => (
                      <button
                        key={v}
                        onClick={() => edit("idleTimeoutMinutes", v)}
                        className={
                          profile.idleTimeoutMinutes === v ? "selected" : ""
                        }
                      >
                        {v ? `${v} minutes` : "Never"}
                      </button>
                    ))}
                  </div>
                </section>
                <section className="settings-card">
                  <h2>Device information</h2>
                  <dl>
                    <dt>Firmware</dt>
                    <dd>{device?.device.version || "Unavailable"}</dd>
                    <dt>Configuration link</dt>
                    <dd>{device ? "Bluetooth LE" : "Disconnected"}</dd>
                    <dt>Battery estimate</dt>
                    <dd>
                      {device?.battery
                        ? `${device.battery.level}/4 bars${device.battery.charging ? " · Charging" : ""}`
                        : "Unavailable"}
                    </dd>
                  </dl>
                  <p className="fine-print">
                    Battery uses the controller’s four-step report. Intermediate
                    levels are not fully validated.
                  </p>
                </section>
                <section className="settings-card danger-card">
                  <h2>Reset controller settings</h2>
                  <p>
                    Clears saved mappings, macros, response curves and other
                    configuration. Firmware is untouched.
                  </p>
                  <button
                    className="button danger"
                    disabled={!device}
                    onClick={() => setResetConfirm(true)}
                  >
                    Factory reset…
                  </button>
                </section>
              </div>
            )}
            {tab === "tester" && <TesterPage liveState={liveState} />}
          </fieldset>
        </main>
        <footer className="actionbar">
          <span>
            {busy ? (
              <>
                <LoaderCircle className="spin" size={15} />
                {busy}…
              </>
            ) : (
              <>
                <span className={`status-dot ${device ? "online" : ""}`} />
                {device
                  ? "Configuration link ready"
                  : "Drafts stay on this computer"}
              </>
            )}
          </span>
          <div>
            <button
              className="button secondary"
              disabled={!device || !!busy}
              onClick={() => {
                if (
                  dirty.size &&
                  !window.confirm(
                    "Replace unsent changes with settings read from the controller?",
                  )
                )
                  return;
                void run("Reading controller", async () =>
                  adoptDevice(await request("read")),
                );
              }}
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
      </div>

      {scanner && (
        <div className="modal-backdrop">
          <section className="dialog">
            <button
              className="dialog-close"
              aria-label="Close devices"
              onClick={() => setScanner(false)}
            >
              <X />
            </button>
            <div className="dialog-symbol">
              <Bluetooth size={30} />
            </div>
            <div className="eyebrow">BLUETOOTH LE CONFIGURATION</div>
            <h2>Find your controller</h2>
            <p>
              Turn on Bluetooth and keep your X20 nearby. Its configuration
              peripheral usually appears as <strong>Xpert2</strong>. USB or
              receiver gameplay can stay connected.
            </p>
            <div className="device-results">
              {busy ? (
                <div className="scan-state">
                  <LoaderCircle className="spin" /> {busy}…
                </div>
              ) : devices.length ? (
                devices.map((found) => (
                  <button
                    className="device-result"
                    key={found.address}
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
                    <Gamepad2 />
                    <span>
                      <strong>{found.name || "Controller"}</strong>
                      <small>{found.address}</small>
                    </span>
                    <ChevronRight />
                  </button>
                ))
              ) : (
                <div className="scan-state">
                  {scanned
                    ? "No supported controllers found. Wake the controller and try again."
                    : "Ready to scan."}
                </div>
              )}
            </div>
            {error && (
              <p className="error-text" role="alert">
                {error}
              </p>
            )}
            <div className="dialog-actions">
              {device && (
                <button
                  className="button secondary"
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
              )}
              <button
                className="button primary"
                disabled={!!busy}
                onClick={scan}
              >
                <RefreshCw size={15} />
                Scan again
              </button>
            </div>
            <p className="fine-print">
              EasySMX X05 is not supported by the KeyLinker protocol.
            </p>
          </section>
        </div>
      )}
      {help && (
        <div className="modal-backdrop">
          <section className="dialog">
            <button
              className="dialog-close"
              aria-label="Close guide"
              onClick={() => setHelp(false)}
            >
              <X />
            </button>
            <h2>Two connections. One controller.</h2>
            <p>
              <strong>Bluetooth LE</strong> carries configuration: remaps,
              macros, curves and power settings. Use Connect controller to
              discover the separate Xpert2 peripheral.
            </p>
            <p>
              <strong>USB, the receiver or Bluetooth gameplay mode</strong>{" "}
              carries input to Windows. The tester reads XInput separately. A
              connected gameplay device does not establish the configuration
              link.
            </p>
            <p>
              Keep Bluetooth enabled, wake the controller, and disconnect any
              phone app currently using its configuration link.
            </p>
            <label className="update-preference">
              <input
                type="checkbox"
                checked={updatesEnabled}
                onChange={(e) =>
                  void run("Saving preference", async () =>
                    setUpdatesEnabled(
                      await request("set_updates", {
                        enabled: e.target.checked,
                      }),
                    ),
                  )
                }
              />
              Check GitHub for updates at startup
            </label>
            <p className="fine-print">
              Optional. Only the public release API is queried; no controller
              data or setups are uploaded. Closing the window keeps x20ctl in
              the system tray. Use the tray menu to quit.
            </p>
          </section>
        </div>
      )}
      {resetConfirm && (
        <div className="modal-backdrop">
          <section className="dialog">
            <h2>Reset every controller setting?</h2>
            <p>
              This clears the controller’s mappings, macros and curves. Saved
              setups on this computer are kept.
            </p>
            <div className="dialog-actions">
              <button
                className="button secondary"
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
          </section>
        </div>
      )}
    </div>
  );
}

import { useEffect, useState } from "react";
import { Heart, Radio } from "lucide-react";
import { controller, type ControllerId } from "../controllers";
import { request } from "../native";
import { emptyInput } from "../hooks/useGamepad";
import type { KeyName, LiveGamepadState } from "../types/gamepad";
import { KEY_LABELS } from "../types/gamepad";
import { isRearControl } from "../controller-controls";
import { ControllerView } from "./ControllerView";
import { TesterPage } from "./pages/TesterPage";
import { MetalDialog } from "./MetalDialog";
import { MotionNav } from "./Motion";
import { STUDIO_SECTIONS, MissingControllerData } from "./StudioSections";
import brandMark from "../assets/brand-mark.png";
import "./research-scanner.css";

interface Props {
  model: ControllerId;
  active: boolean;
  player: number;
  onBack: () => void;
  onSupport: () => void;
  onScan: (authorized?: boolean) => void;
}
interface InputResult {
  connected: boolean;
  source?: string;
  input: null | {
    slot: number;
    buttons: KeyName[];
    leftStick: { x: number; y: number };
    rightStick: { x: number; y: number };
    leftTrigger: number;
    rightTrigger: number;
    source: string;
  };
}

export function InputWorkspace({
  model,
  active,
  player,
  onBack,
  onSupport,
  onScan,
}: Props) {
  const profile = controller(model);
  const basicInput = profile.availability === "input_experimental";
  const [receivers, setReceivers] = useState<
    { model: string | null; status: string }[]
  >([]);
  const [tab, setTab] = useState("buttons");
  const [missing, setMissing] = useState<string | null>(null);
  const [live, setLive] = useState<LiveGamepadState>(emptyInput);
  const [connected, setConnected] = useState(false);
  const [bound, setBound] = useState(false);
  const [source, setSource] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [connectStep, setConnectStep] = useState<
    "closed" | "disconnect" | "reconnect" | "choose"
  >("closed");
  const [choices, setChoices] = useState<{ token: string; label: string }[]>(
    [],
  );
  const [view, setView] = useState<"front" | "back">("front");
  const [selected, setSelected] = useState<KeyName>("A");
  useEffect(() => {
    setBound(false);
    setConnected(false);
    setLive(emptyInput());
    setSource("");
    setReceivers([]);
    setConnectStep("closed");
    setSelected("A");
  }, [model, player]);
  useEffect(() => {
    if (!active || !basicInput || !bound) {
      setLive(emptyInput());
      setConnected(false);
      return;
    }
    let stopped = false;
    let timer: ReturnType<typeof setTimeout>;
    const poll = async () => {
      try {
        const result = await request<InputResult>("gameplay_input", { player });
        if (stopped) return;
        setConnected(result.connected && !!result.input);
        if (!result.connected) setBound(false);
        if (!result.input) setLive(emptyInput());
        else {
          const input = result.input;
          setSource(result.source || input.source);
          setLive((previous) => ({
            ...emptyInput(),
            ...input,
            buttons: Object.fromEntries(
              input.buttons.map((key) => [key, true]),
            ) as Record<KeyName, boolean>,
            trail: {
              left: [...previous.trail.left, input.leftStick].slice(-25),
              right: [...previous.trail.right, input.rightStick].slice(-25),
            },
          }));
        }
      } catch (reason) {
        if (!stopped) {
          setConnected(false);
          setBound(false);
          setLive(emptyInput());
          setError(String(reason));
        }
      }
      if (!stopped) timer = setTimeout(poll, 50);
    };
    void poll();
    return () => {
      stopped = true;
      clearTimeout(timer);
    };
  }, [active, bound, player, basicInput]);
  const perform = async (action: () => Promise<void>) => {
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setBusy(false);
    }
  };
  if (!active) return null;
  const select = (key: KeyName) => {
    setSelected(key);
    setView(isRearControl(key) ? "back" : "front");
  };
  return (
    <div
      className="metal-app rebranded-workspace model-workspace input-workspace"
      data-model={model}
      data-page={tab}
    >
      <aside className="chassis-sidebar has-sidebar-actions">
        <div className="chassis-brand">
          <img className="brand-emblem" src={brandMark} alt="" />
          <strong>x20ctl</strong>
        </div>
        <MotionNav active={tab}>
          {STUDIO_SECTIONS.map(({ id, label, icon: Icon }) => (
            <button
              key={id}
              aria-current={tab === id ? "page" : undefined}
              onClick={() => {
                setTab(id);
                if (
                  id !== "buttons" &&
                  !(basicInput && ["tester", "power"].includes(id))
                )
                  setMissing(label);
              }}
            >
              <Icon size={19} />
              <span>{label}</span>
            </button>
          ))}
          <button onClick={() => onScan()}>
            <Radio size={19} />
            <span>Controller scanner</span>
          </button>
        </MotionNav>
        <button className="sidebar-support" onClick={onSupport}>
          <Heart size={19} />
          <span>Support X20ctl</span>
        </button>
      </aside>
      <header className="chassis-header">
        <div className="header-controller-copy">
          <strong>
            {profile.name} Controller <small>Player {player}</small>
          </strong>
          <p role="status">
            {basicInput
              ? connected
                ? "Gameplay input connected"
                : "Gameplay input disconnected"
              : "Not available yet · controller preview"}
          </p>
        </div>
        <div className="header-actions">
          <button className="button secondary" onClick={onBack}>
            Switch Controller
          </button>
          {basicInput && (
            <button
              className="button primary"
              disabled={busy}
              onClick={() => {
                setError("");
                setConnectStep("disconnect");
              }}
            >
              Connect input
            </button>
          )}
        </div>
      </header>
      <main className="metal-workspace" id="workspace">
        <h1>
          {profile.name}{" "}
          {tab === "tester"
            ? "input tester"
            : tab === "power"
              ? "device & connection"
              : "controller"}
        </h1>
        <p className="notice">
          {basicInput
            ? "Your controller is supported for basic input testing, but X20CTL still needs additional captures to implement advanced trigger, macro and vibration configuration. Run Scanner to help us complete support."
            : "This controller is not available yet. More verified data is needed. You can help using the built-in scanner."}
        </p>
        {basicInput && (
          <details className="metal-panel compatibility-status">
            <summary>Known / Missing · basic input support</summary>
            <div className="compatibility-grid">
              <div>
                <h2>Known</h2>
                <ul>
                  {profile.known?.map((text) => (
                    <li key={text}>{text}</li>
                  ))}
                </ul>
                <p>
                  Input Tester maps A/B/X/Y, D-pad, LB/RB, L3/R3, Back/View,
                  Start/Menu, both sticks and LT/RT when the selected Windows
                  source reports them. Standard XInput does not expose
                  Home/Guide or independent M keys.
                </p>
                {receivers.map((item, index) => (
                  <p key={index}>
                    {item.model?.toUpperCase() ||
                      "Generic Xbox-compatible identity"}{" "}
                    · {item.status.replace(/_/g, " ")}; wireless link
                    unverified.
                  </p>
                ))}
              </div>
              <div>
                <h2>Missing</h2>
                <ul>
                  {profile.missing?.map((text) => (
                    <li key={text}>{text}</li>
                  ))}
                </ul>
                <button className="button secondary" onClick={() => onScan()}>
                  Help complete support · Run Scanner
                </button>
              </div>
            </div>
          </details>
        )}
        {error && (
          <p className="error-text" role="alert">
            {error}
          </p>
        )}
        {tab === "buttons" && (
          <section className="metal-panel input-controller-panel">
            <div className="input-controller-grid">
              <ControllerView
                model={model}
                view={view}
                onViewChange={setView}
                framing="detail"
                hideMacroControls
                lighting="interactive"
                buttons={live.buttons}
                leftStick={live.leftStick}
                rightStick={live.rightStick}
                leftTrigger={live.leftTrigger}
                rightTrigger={live.rightTrigger}
                selectedKey={basicInput ? selected : null}
                onSelect={basicInput ? select : undefined}
                disabled={!basicInput}
              />
              <div className="input-inspector">
                <span className="engraved">
                  {basicInput ? "RECEIVED INPUT" : "RESEARCH PREVIEW"}
                </span>
                <h2>
                  {basicInput ? KEY_LABELS[selected] : "Help add support"}
                </h2>
                <p>
                  {basicInput
                    ? connected
                      ? live.buttons[selected]
                        ? "Pressed"
                        : "Released"
                      : "Connect the controller to receive live input."
                    : "The picture is a preview. It does not establish software or protocol support."}
                </p>
                {basicInput && (
                  <>
                    <div className="input-value-row">
                      <span>LT</span>
                      <strong>
                        {connected
                          ? `${Math.round(live.leftTrigger * 255)} / 255`
                          : "—"}
                      </strong>
                    </div>
                    <div className="input-value-row">
                      <span>RT</span>
                      <strong>
                        {connected
                          ? `${Math.round(live.rightTrigger * 255)} / 255`
                          : "—"}
                      </strong>
                    </div>
                    <p>
                      Triggers show actual reported values. Stick orientation in
                      the HID layout still needs physical confirmation.
                    </p>
                    <p>{source || "No source selected"}</p>
                  </>
                )}
                <button className="button secondary" onClick={() => onScan()}>
                  Scan this controller
                </button>
              </div>
            </div>
          </section>
        )}
        {tab === "tester" && (
          <TesterPage
            liveState={live}
            inputConnected={connected}
            inputSlot={null}
            playerNumber={player}
            inputBackend={source || "Selected gameplay source"}
          />
        )}
        {tab === "power" && (
          <section className="metal-panel research-section">
            <h2>Selected input connection</h2>
            <p>{source || "No input source selected"}</p>
            <p>
              Firmware and hardware revision: unknown. Gameplay compatibility
              IDs are shared and cannot identify the model automatically. A
              receiver input stream does not independently prove the
              controller's wireless link status.
            </p>
            <p>
              Actual intermediate trigger values are displayed if this selected
              mode reports them. Endpoint-only readings do not prove analog
              travel.
            </p>
            <button className="button secondary" onClick={() => onScan()}>
              Collect controller data
            </button>
            {bound && (
              <button
                className="button secondary"
                onClick={() =>
                  void perform(async () => {
                    await request("input_detach", { player });
                    setBound(false);
                    setConnected(false);
                    setLive(emptyInput());
                  })
                }
              >
                Disconnect input
              </button>
            )}
          </section>
        )}
        {!["buttons", "tester", "power"].includes(tab) && (
          <section className="metal-panel research-section">
            <h2>
              {STUDIO_SECTIONS.find((section) => section.id === tab)?.label}{" "}
              needs controller data
            </h2>
            <p>
              This section is a preview until the required data is verified. No
              unverified controller commands are sent.
            </p>
            <button className="button secondary" onClick={() => onScan()}>
              Scan this controller
            </button>
          </section>
        )}
      </main>
      <footer className="chassis-footer">
        <span>
          {basicInput
            ? "Read-only gameplay input · no configuration writes"
            : "Preview only · help collect controller data"}
        </span>
        <button className="button secondary" onClick={() => onScan()}>
          Controller scanner
        </button>
      </footer>
      {connectStep !== "closed" && (
        <MetalDialog
          title={`Connect ${profile.name} input`}
          subtitle={`PLAYER ${player}`}
          onClose={() => setConnectStep("closed")}
        >
          <p>
            Close games and remappers. This selects gameplay input, not a
            configuration connection.
          </p>
          {connectStep === "disconnect" && (
            <>
              <p>
                Disconnect ONLY this controller: unplug its cable/receiver, or
                switch it off for Bluetooth.
              </p>
              <button
                className="button primary"
                disabled={busy}
                onClick={() =>
                  void perform(async () => {
                    await request("input_discover", {
                      phase: "before",
                      player,
                    });
                    setConnectStep("reconnect");
                  })
                }
              >
                Controller disconnected · continue
              </button>
            </>
          )}
          {connectStep === "reconnect" && (
            <>
              <p>
                Reconnect the same controller in its normal mode and wait for it
                to connect.
              </p>
              <button
                className="button primary"
                disabled={busy}
                onClick={() =>
                  void perform(async () => {
                    const found = await request<typeof choices>(
                      "input_discover",
                      { phase: "after", player },
                    );
                    const evidence = await request<{
                      receivers: typeof receivers;
                    }>("input_evidence", { player });
                    setReceivers(evidence.receivers);
                    setChoices(found);
                    setConnectStep("choose");
                  })
                }
              >
                Controller reconnected · find input
              </button>
            </>
          )}
          {connectStep === "choose" && (
            <>
              {choices.length ? (
                choices.map((choice) => (
                  <button
                    className="button secondary research-choice"
                    key={choice.token}
                    disabled={busy}
                    onClick={() =>
                      void perform(async () => {
                        await request("input_attach", {
                          token: choice.token,
                          player,
                        });
                        setBound(true);
                        setSource(choice.label);
                        setConnectStep("closed");
                      })
                    }
                  >
                    {choice.label} · select
                  </button>
                ))
              ) : (
                <p role="status">
                  No supported new input source appeared. Use the scanner to
                  record this mode, or retry identification.
                </p>
              )}
              <button
                className="button secondary"
                onClick={() => setConnectStep("disconnect")}
              >
                Identify again
              </button>
            </>
          )}
          {error && (
            <p role="alert" className="error-text">
              {error}
            </p>
          )}
        </MetalDialog>
      )}
      {missing && (
        <MissingControllerData
          model={profile.name}
          section={missing}
          onLater={() => setMissing(null)}
          onScan={() => {
            setMissing(null);
            onScan(true);
          }}
        />
      )}
    </div>
  );
}

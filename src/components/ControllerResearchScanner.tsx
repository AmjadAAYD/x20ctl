import { useEffect, useState } from "react";
import { request } from "../native";
import { MetalDialog } from "./MetalDialog";
import "./research-scanner.css";

interface Prompt {
  id: string;
  text: string;
  kind: "continue" | "yes" | "choice" | "text" | "file";
  choices: string[];
}
interface ScanState {
  state: string;
  reportId?: string;
  model?: string;
  prompt: Prompt | null;
  receipt: string | null;
  error?: string;
  message?: string;
  action?: string;
  duration?: number;
  samples?: number;
  size?: number;
  sha256?: string;
  files: { path: string; size: number }[];
  coverage: Record<string, { status: string; reason: string }>;
  input?: {
    leftTrigger: number;
    rightTrigger: number;
    buttons: string[];
  } | null;
}
const idle: ScanState = {
  state: "idle",
  prompt: null,
  receipt: null,
  files: [],
  coverage: {},
};
export function ControllerResearchScanner({
  model,
  preauthorized = false,
  onClose,
}: {
  model: string;
  preauthorized?: boolean;
  onClose: () => void;
}) {
  const [state, setState] = useState<ScanState>(idle);
  const [printedModel, setModel] = useState(model || "Unknown");
  const [mode, setMode] = useState("");
  const [metadata, setMetadata] = useState({
    firmware: "",
    hardwareRevision: "",
    receiverFirmware: "",
    appName: "",
    appVersion: "",
  });
  const [transport, setTransport] = useState("receiver");
  const [consent, setConsent] = useState(preauthorized);
  const [autoSubmit, setAutoSubmit] = useState(true);
  const [raw, setRaw] = useState(false);
  const [reviewed, setReviewed] = useState(false);
  const [answer, setAnswer] = useState("");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [manual, setManual] = useState(false);
  const [history, setHistory] = useState<
    { id: string; model: string; state: string; receipt: string | null }[]
  >([]);
  useEffect(() => {
    let stopped = false;
    let timer: ReturnType<typeof setTimeout>;
    const poll = async () => {
      try {
        const next = await request<ScanState>("research_status");
        if (!stopped) setState(next);
      } catch (reason) {
        if (!stopped)
          setError(reason instanceof Error ? reason.message : String(reason));
      }
      if (!stopped) timer = setTimeout(poll, 350);
    };
    void poll();
    void request<typeof history>("research_history")
      .then((rows) => {
        if (!stopped) setHistory(rows);
      })
      .catch(() => {});
    return () => {
      stopped = true;
      clearTimeout(timer);
    };
  }, []);
  useEffect(() => {
    setAnswer("");
  }, [state.prompt?.id]);
  const run = async (operation: string, payload = {}) => {
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await request(operation, payload);
      setState(await request<ScanState>("research_status"));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setBusy(false);
    }
  };
  const respond = (value: string) =>
    void run("research_answer", { promptId: state.prompt?.id, answer: value });
  const close = async () => {
    if (["collecting", "waiting", "review"].includes(state.state)) {
      try {
        await request("research_cancel");
      } catch (reason) {
        setError(String(reason));
        return;
      }
    }
    onClose();
  };
  const save = async (reportId = state.reportId) => {
    try {
      const result = await request<{ saved: boolean }>("research_export", {
        reportId,
      });
      if (result.saved) setMessage("Reviewed report ZIP saved.");
    } catch (reason) {
      setError(String(reason));
    }
  };
  const contact = async (value: string) => {
    try {
      await request("research_contact", { contact: value });
      setMessage(
        value === "email"
          ? "Email draft opened. Attach the ZIP and press Send."
          : value === "discord"
            ? "Discord opened. Add/message mistermajid and attach the ZIP."
            : "Scanner release page opened.",
      );
    } catch (reason) {
      setError(String(reason));
    }
  };
  const copyContact = async (value: string) => {
    try {
      await navigator.clipboard.writeText(value);
      setMessage("Contact copied.");
    } catch {
      setMessage(
        "Clipboard unavailable. Select and copy the displayed contact.",
      );
    }
  };
  const terminal = ["submitted", "upload_failed", "saved"].includes(
    state.state,
  );
  const setup = ["idle", "cancelled", "failed"].includes(state.state);
  return (
    <MetalDialog
      title="Controller scanner"
      subtitle="BUILT INTO X20CTL · RGB EXCLUDED"
      className="research-scanner"
      onClose={() => void close()}
    >
      {setup && (
        <div className="research-setup">
          <p>
            Normal capture records input states, available descriptors, trigger
            and rear-button behavior, and optional standard XInput vibration
            tests. It cannot see outbound USB/HID commands or another app's BLE
            traffic.
          </p>
          <details className="research-section">
            <summary>External protocol capture · optional research</summary>
            <p>
              For Windows USB/HID, use owner-installed USBPcap with Wireshark
              while an already-working official app changes one reversible
              setting, then restores it. For phone BLE configuration, collect
              the Android phone's Bluetooth HCI snoop log. Import a selected
              trace at the end. No capture driver is installed or launched here.
              For a Windows-host BLE connection, Microsoft Bluetooth Virtual
              Sniffer with Wireshark is an optional HCI capture route; default
              logs can omit payloads. A PC logger cannot see a separate phone
              connection.
            </p>
          </details>

          <p>
            Identify your controller, follow the exact button/stick/trigger
            prompts, then review and share the report. Unknown data stays
            unknown.
          </p>
          <div className="research-fields">
            <label>
              Printed model
              <input
                value={printedModel}
                maxLength={80}
                onChange={(e) => setModel(e.target.value)}
              />
            </label>
            <label>
              Connection
              <select
                value={transport}
                onChange={(e) => setTransport(e.target.value)}
              >
                <option value="receiver">Wireless USB receiver</option>
                <option value="direct_usb">USB cable</option>
                <option value="bluetooth">Bluetooth</option>
              </select>
            </label>
            <label>
              Mode, if known
              <input
                value={mode}
                maxLength={80}
                placeholder="Unknown — do not guess"
                onChange={(e) => setMode(e.target.value)}
              />
            </label>
          </div>
          <details>
            <summary>Firmware / app capture context (optional)</summary>
            <div className="research-fields">
              {(
                [
                  ["firmware", "Controller firmware"],
                  ["hardwareRevision", "Hardware revision"],
                  ["receiverFirmware", "Receiver firmware"],
                  ["appName", "Configuration app name"],
                  ["appVersion", "App version"],
                ] as const
              ).map(([key, label]) => (
                <label key={key}>
                  {label}
                  <input
                    maxLength={80}
                    value={metadata[key]}
                    onChange={(event) =>
                      setMetadata({ ...metadata, [key]: event.target.value })
                    }
                    placeholder="Unknown"
                  />
                </label>
              ))}
            </div>
          </details>
          <label className="research-check">
            <input
              type="checkbox"
              checked={autoSubmit}
              onChange={(e) => setAutoSubmit(e.target.checked)}
            />
            After review, automatically submit this report to X20CTLADMIN.
          </label>
          <label className="research-check">
            <input
              type="checkbox"
              checked={raw}
              onChange={(e) => setRaw(e.target.checked)}
            />
            Include raw gameplay HID bytes. Needed to record HID-only
            controllers; bytes can contain device-specific data. Inspect them
            before sharing.
          </label>
          <label className="research-check">
            <input
              type="checkbox"
              checked={consent}
              onChange={(e) => setConsent(e.target.checked)}
            />
            I authorize this local scan and the selected submission scope. No
            stored settings, firmware or RGB commands are sent. Short standard
            XInput motor tests require a separate confirmation during the scan.
          </label>
          <button
            className="button primary"
            disabled={busy || !consent || !printedModel.trim()}
            onClick={() =>
              void run("research_start", {
                model: printedModel,
                ...metadata,
                mode: mode || "unknown",
                transport,
                consent,
                autoSubmit,
                rawInput: raw,
              })
            }
          >
            Start guided scan
          </button>
          <p>
            Close games, Steam and remappers before input tests. Each capture
            stage can be skipped. A scan may temporarily disconnect the Studio
            input/settings connection.
          </p>
          <button
            className="button secondary"
            onClick={() => void contact("kit")}
          >
            Standalone scan kit
          </button>
        </div>
      )}
      {!setup && (
        <p className="research-status" role="status">
          {state.state === "submitting"
            ? "Submitting the reviewed report to X20CTLADMIN…"
            : state.state === "submitted"
              ? `Submitted — receipt ${state.receipt}`
              : state.state === "review"
                ? "Collection finished. Review your report before finishing."
                : state.state === "upload_failed"
                  ? "Website submission failed. Your ZIP is saved locally."
                  : state.state === "saved"
                    ? "Report saved locally. Website submission was not selected."
                    : state.message || "Follow the next guided step."}
        </p>
      )}
      {state.prompt && (
        <section className="research-prompt">
          <span className="engraved">GUIDED STEP</span>
          <h2>{state.prompt.text}</h2>
          {state.prompt.kind === "choice" &&
            state.prompt.choices.map((choice, index) => (
              <button
                key={index}
                className="button secondary research-choice"
                disabled={busy}
                onClick={() => respond(String(index))}
              >
                {choice}
              </button>
            ))}
          {state.prompt.kind === "text" && (
            <textarea
              aria-label="Scan answer"
              maxLength={1000}
              value={answer}
              onChange={(e) => setAnswer(e.target.value)}
              placeholder="Unknown / no observation is okay"
            />
          )}
          {state.prompt.kind === "file" ? (
            <div className="dialog-actions">
              <button
                className="button primary"
                disabled={busy}
                onClick={() =>
                  void run("research_attach", { promptId: state.prompt!.id })
                }
              >
                Choose file
              </button>
            </div>
          ) : (
            <div className="dialog-actions">
              {state.prompt.kind === "yes" ? (
                <button
                  className="button primary"
                  disabled={busy}
                  onClick={() => respond("yes")}
                >
                  Include this stage
                </button>
              ) : (
                state.prompt.kind !== "choice" && (
                  <button
                    className="button primary"
                    disabled={busy}
                    onClick={() =>
                      respond(
                        state.prompt!.kind === "text" ? answer : "continue",
                      )
                    }
                  >
                    Continue
                  </button>
                )
              )}
              <button
                className="button secondary"
                disabled={busy}
                onClick={() => respond("skip")}
              >
                Skip
              </button>
            </div>
          )}
        </section>
      )}
      {state.state === "collecting" && (
        <section className="research-prompt">
          <h2>{state.message || "Reading selected controller information…"}</h2>
          <p>
            {state.samples
              ? `${state.samples} input samples collected`
              : "Working locally — please wait"}
          </p>
          {state.input && (
            <p>
              LT {Math.round(state.input.leftTrigger * 255)} /255 · RT{" "}
              {Math.round(state.input.rightTrigger * 255)} /255 ·{" "}
              {state.input.buttons.join(" · ") || "No buttons pressed"}
            </p>
          )}
        </section>
      )}
      {!!Object.keys(state.coverage).length && (
        <details className="research-coverage" open={false}>
          <summary>Collection coverage and limitations</summary>
          <div>
            {Object.entries(state.coverage).map(([name, outcome]) => (
              <article key={name}>
                <strong>
                  {name === "triggers"
                    ? "Trigger readings"
                    : name === "vibration"
                      ? "Vibration feedback"
                      : name.replace(/_/g, " ")}
                </strong>
                <span>{outcome.status.replace(/_/g, " ")}</span>
                <small>{outcome.reason}</small>
              </article>
            ))}
          </div>
        </details>
      )}
      {state.state === "review" && (
        <section className="research-review">
          <h2>Review the collected files</h2>
          <button
            className="button secondary"
            onClick={() => void run("research_inspect")}
          >
            Inspect local files
          </button>
          <p>
            Raw captures may contain identifiers, pairing data, unrelated
            traffic or location metadata. Optional files can be removed before
            sending.
          </p>
          <ul>
            {state.files.map((file) => (
              <li key={file.path}>
                <span>
                  {file.path} · {(file.size / 1024).toFixed(1)} KB
                </span>
                {file.path.startsWith("attachments/") && (
                  <button
                    className="button secondary compact"
                    onClick={() =>
                      void run("research_remove", { path: file.path })
                    }
                  >
                    Remove
                  </button>
                )}
              </li>
            ))}
          </ul>
          <label className="research-check">
            <input
              type="checkbox"
              checked={reviewed}
              onChange={(e) => setReviewed(e.target.checked)}
            />
            I reviewed these files and approve the selected sharing scope.
          </label>
          <button
            className="button primary"
            disabled={busy || !reviewed}
            onClick={() => void run("research_finish", { reviewed })}
          >
            Finish scan
          </button>
        </section>
      )}
      {terminal && (
        <section className="research-handoff">
          <h2>Your report is ready</h2>
          <p>Report ID: {state.reportId}</p>
          <p>
            {state.size ? `${(state.size / 1024).toFixed(1)} KB` : ""} ·{" "}
            {state.sha256}
          </p>
          <button className="button primary" onClick={() => void save()}>
            Save result ZIP
          </button>
          <button
            className="button secondary"
            onClick={() =>
              void run("research_open_folder", { reportId: state.reportId })
            }
          >
            Open report folder
          </button>
          {state.state === "upload_failed" && (
            <button
              className="button secondary"
              disabled={busy}
              onClick={() => void run("research_retry")}
            >
              Retry website submission
            </button>
          )}
          <p>Would you also like to send this report manually to Amjad?</p>
          <div className="dialog-actions">
            <button
              className="button secondary"
              onClick={() => setManual(true)}
            >
              Send manually
            </button>
            <button
              className="button secondary"
              onClick={() => setManual(false)}
            >
              Not now
            </button>
          </div>
          {manual && (
            <div className="research-contacts">
              <p>
                Discord: <strong>mistermajid</strong>
              </p>
              <p>
                Email: <strong>aaydamjad@gmail.com</strong>
              </p>
              <button
                className="button secondary"
                onClick={() => void copyContact("mistermajid")}
              >
                Copy Discord username
              </button>
              <button
                className="button secondary"
                onClick={() => void copyContact("aaydamjad@gmail.com")}
              >
                Copy email
              </button>
              <button
                className="button secondary"
                onClick={() => void contact("email")}
              >
                Email Amjad
              </button>
              <button
                className="button secondary"
                onClick={() => void contact("discord")}
              >
                Open Discord
              </button>
              <p>
                Attach the saved ZIP yourself. Opening a draft or Discord does
                not send it.
              </p>
            </div>
          )}
        </section>
      )}
      {(error || state.error) && (
        <p className="error-text" role="alert">
          {error || state.error}
        </p>
      )}
      {message && (
        <p role="status" className="notice">
          {message}
        </p>
      )}
      {["collecting", "waiting", "review"].includes(state.state) && (
        <button
          className="button secondary"
          disabled={busy}
          onClick={() => void run("research_cancel")}
        >
          Cancel scan · keep local files
        </button>
      )}
      {!!history.length && (
        <details>
          <summary>Saved report history</summary>
          {history.map((report) => (
            <div className="research-history" key={report.id}>
              <span>
                {report.model} · {report.state} ·{" "}
                {report.receipt || "No receipt"}
              </span>
              <button
                className="button secondary compact"
                onClick={() => void save(report.id)}
              >
                Save ZIP
              </button>
            </div>
          ))}
        </details>
      )}
    </MetalDialog>
  );
}

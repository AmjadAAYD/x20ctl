import { useEffect, useState } from "react";
import { request } from "../native";
import { MetalDialog } from "./MetalDialog";

interface Preview {
  scanId: string;
  clientSubmissionId: string;
  controllerName: string;
  appVersion: string;
  scannerVersion: string;
  vid: string | null;
  pid: string | null;
  files: string[];
  size: number;
  details: Record<string, unknown>;
}

export function ReportPanel({ onClose, address, selected, onPrepareStart }: {
  onClose: () => void;
  address?: string;
  selected: boolean;
  onPrepareStart?: () => void;
}) {
  const [pending, setPending] = useState<Preview[]>([]);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [consent, setConsent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [sent, setSent] = useState("");
  useEffect(() => {
    void request<Preview[]>("report_pending").then(setPending)
      .catch((reason) => setError(reason instanceof Error ? reason.message : String(reason)));
  }, []);
  const prepare = async () => {
    setBusy(true); setError(""); setSent(""); setConsent(false);
    try {
      onPrepareStart?.();
      const result = await request<Preview>("report_prepare", address ? { address } : {});
      setPreview(result);
      setPending((old) => [result, ...old]);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally { setBusy(false); }
  };
  const send = async () => {
    if (!preview || !consent) return;
    setBusy(true); setError("");
    try {
      const result = await request<{ submissionId: string }>("report_send", { scanId: preview.scanId, consent: true });
      setSent(`Sent as ${result.submissionId}`);
      setPending((old) => old.filter((item) => item.scanId !== preview.scanId));
      setPreview(null); setConsent(false);
    } catch (reason) {
      setError(`${reason instanceof Error ? reason.message : String(reason)}. The exact ZIP and submission UUID remain local for retry.`);
    } finally { setBusy(false); }
  };
  return <MetalDialog title="Controller compatibility report" subtitle="LOCAL DIAGNOSTICS / EXPLICIT UPLOAD" onClose={onClose}>
    <p>The scanner reads standard Bluetooth LE information from your selected controller. USB/HID descriptors and input captures are unavailable when they cannot be safely tied to it. No serial number is collected.</p>
    <button className="button secondary" disabled={!selected || busy} onClick={() => void prepare()}>
      {busy ? "Working…" : "Create local report"}
    </button>
    {!selected && <p className="fine-print">Select a controller to create a new report. Existing local reports can still be reviewed below.</p>}
    {pending.length > 0 && <div><h3>Unsent local reports</h3>{pending.map((item) =>
      <button key={item.scanId} className="button secondary" disabled={busy} onClick={() => { setPreview(item); setConsent(false); setError(""); }}>
        {item.controllerName} · {Math.ceil(item.size / 1024)} KiB
      </button>)}</div>}
    {preview && <section className="metal-panel" style={{ padding: 18, marginTop: 16 }}>
      <h3>Review before sending</h3>
      <p><b>Controller:</b> {preview.controllerName}</p>
      <p><b>VID/PID:</b> {preview.vid ?? "Unavailable"} / {preview.pid ?? "Unavailable"}</p>
      <p><b>Versions:</b> X20ctl {preview.appVersion}; scanner {preview.scannerVersion}</p>
      <p><b>ZIP size:</b> approximately {Math.ceil(preview.size / 1024)} KiB</p>
      <p><b>Included files:</b> {preview.files.join(", ")}</p>
      <details><summary>Inspect diagnostic details</summary><pre style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}>{JSON.stringify(preview.details, null, 2)}</pre></details>
      <label style={{ display: "block", marginTop: 14 }}><input type="checkbox" checked={consent} disabled={busy} onChange={(event) => setConsent(event.target.checked)} /> I reviewed this report and consent to upload it to X20ADMIN.</label>
      <button className="button primary" disabled={!consent || busy} onClick={() => void send()}>Send report</button>
    </section>}
    {error && <p className="error-text" role="alert">{error}</p>}
    {sent && <p className="notice" role="status">{sent}</p>}
  </MetalDialog>;
}

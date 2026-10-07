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
  const [candidates, setCandidates] = useState<{ address: string; name: string }[]>([]);
  const [chosen, setChosen] = useState(address ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [sent, setSent] = useState("");
  useEffect(() => {
    void request<Preview[]>("report_pending").then(setPending)
      .catch((reason) => setError(reason instanceof Error ? reason.message : String(reason)));
  }, []);
  const prepare = async () => {
    setBusy(true); setError(""); setSent("");
    try {
      onPrepareStart?.();
      const result = await request<Preview>("report_prepare", chosen ? { address: chosen } : {});
      setPreview(result);
      setPending((old) => [result, ...old]);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally { setBusy(false); }
  };
  const save = async () => {
    if (!preview) return;
    setBusy(true); setError("");
    try {
      const result = await request<{ saved: boolean } | null>("report_export", { scanId: preview.scanId });
      if (result?.saved) setSent("Controller scan ZIP saved.");
    } catch (reason) { setError(reason instanceof Error ? reason.message : String(reason)); }
    finally { setBusy(false); }
  };
  const scan = async () => {
    setBusy(true); setError(""); setChosen("");
    try { setCandidates(await request<{ address: string; name: string }[]>("controller_scan")); }
    catch (reason) { setError(reason instanceof Error ? reason.message : String(reason)); }
    finally { setBusy(false); }
  };
  return <MetalDialog title="Controller Scan" subtitle="LOCAL ZIP" onClose={onClose}>
    <p>The scanner reads standard Bluetooth LE information from your selected controller. USB/HID descriptors and input captures are unavailable when they cannot be safely tied to it. No serial number is collected.</p>
    <button className="button secondary" disabled={busy} onClick={() => void scan()}>Find controller</button>
    {candidates.length > 0 && <label>Choose your controller<select value={chosen} onChange={event => setChosen(event.target.value)}><option value="">Select a device</option>{candidates.map(item => <option key={item.address} value={item.address}>{item.name || "Unnamed device"}</option>)}</select></label>}
    <button className="button secondary" disabled={(!selected && !chosen) || busy} onClick={() => void prepare()}>
      {busy ? "Working…" : "Create ZIP"}
    </button>
    {!selected && <p className="fine-print">Select a controller to create a new report. Existing local reports can still be reviewed below.</p>}
    {pending.length > 0 && <div><h3>Local controller scans</h3>{pending.map((item) =>
      <button key={item.scanId} className="button secondary" disabled={busy} onClick={() => { setPreview(item);  setError(""); }}>
        {item.controllerName} · {Math.ceil(item.size / 1024)} KiB
      </button>)}</div>}
    {preview && <section className="metal-panel" style={{ padding: 18, marginTop: 16 }}>
      <h3>Review ZIP contents</h3>
      <p><b>Controller:</b> {preview.controllerName}</p>
      <p><b>VID/PID:</b> {preview.vid ?? "Unavailable"} / {preview.pid ?? "Unavailable"}</p>
      <p><b>Versions:</b> X20ctl {preview.appVersion}; scanner {preview.scannerVersion}</p>
      <p><b>ZIP size:</b> approximately {Math.ceil(preview.size / 1024)} KiB</p>
      <p><b>Included files:</b> {preview.files.join(", ")}</p>
      <details><summary>Inspect diagnostic details</summary><pre style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}>{JSON.stringify(preview.details, null, 2)}</pre></details>
      <button className="button primary" disabled={busy} onClick={() => void save()}>Save ZIP</button>
    </section>}
    {error && <p className="error-text" role="alert">{error}</p>}
    {sent && <p className="notice" role="status">{sent}</p>}
  </MetalDialog>;
}

import { useEffect, useState } from "react";
import { ArrowLeft, Bluetooth, Cpu, Gamepad2, Radio, Search } from "lucide-react";
import { request } from "../native";
import { ProController } from "./ProController";

interface Peripheral {
  address: string;
  name: string;
  services: string[];
  rssi: number;
}
interface Inspection {
  address: string;
  verifiedModel: null;
  standardValues: Record<string, string | number | { error: string }>;
  services: { uuid: string; characteristics: { uuid: string; properties: string[] }[] }[];
}
interface HidDevice {
  Name: string;
  PNPDeviceID: string;
  Manufacturer: string;
}

export function ProWorkspace({ onBack, onTheme }: { onBack: () => void; onTheme: () => void }) {
  const [peripherals, setPeripherals] = useState<Peripheral[]>([]);
  const [inspection, setInspection] = useState<Inspection | null>(null);
  const [hid, setHid] = useState<HidDevice[]>([]);
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  useEffect(() => {
    void request<{ warning: string | null; capabilities: string[] }>("pro_bootstrap")
      .then((state) => { if (state.warning) setError(`Pro setup store: ${state.warning}`); })
      .catch((reason) => setError(reason instanceof Error ? reason.message : String(reason)));
  }, []);
  const run = async (label: string, work: () => Promise<void>) => {
    if (busy) return;
    setBusy(label); setError("");
    try { await work(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : String(reason)); }
    finally { setBusy(""); }
  };
  return (
    <div className="metal-app pro-workspace">
      <header className="chassis-header">
        <div className="chassis-brand"><div className="brand-emblem"><Gamepad2 size={24}/></div><div><strong>x20ctl</strong><span>X20 PRO / DISCOVERY</span></div></div>
        <div className="header-actions"><button className="button secondary" onClick={onBack}><ArrowLeft size={15}/>Controllers</button><button className="button secondary" onClick={onTheme}>Themes</button></div>
      </header>
      <main className="metal-workspace" id="workspace">
        <section className="device-strip"><div className="device-identity"><div className="device-chip"><Cpu size={25}/></div><div><span className="engraved">SEPARATE MODEL / READ-ONLY</span><h1>X20 Pro</h1><p>Hardware investigation workspace</p></div></div><span className="pro-status">CONFIGURATION UNVERIFIED</span></section>
        {error && <div role="alert" className="notice error">{error}</div>}
        <div className="pro-layout">
          <ProController />
          <section className="metal-panel pro-feature-panel"><div className="panel-heading"><h2>Published hardware</h2><span className="panel-code">MODEL / PRO</span></div>
            <div className="pro-feature-list">
              <div><b>06</b><span>Programmable physical buttons</span></div>
              <div><b>04</b><span>Rumble motors with instant brake</span></div>
              <div><b>TMR</b><span>Adjustable-tension contactless sticks</span></div>
              <div><b>2×</b><span>Hall analog / microswitch trigger modes</span></div>
              <div><b>LCD</b><span>On-device smart display</span></div>
            </div>
            <p className="pro-illustration-note">These are published hardware features, not readings from a connected controller.</p>
          </section>
        </div>
        <section className="metal-panel pro-discovery"><div className="panel-heading"><Radio size={17}/><h2>Read-only discovery</h2><span className="panel-code">NO SETTINGS COMMANDS</span></div>
          <p>Scan nearby BLE peripherals and Windows HID entries. Results do not identify an X20 Pro until its interface is verified.</p>
          <div className="pro-discovery-actions">
            <button className="button primary" disabled={!!busy} onClick={() => void run("Scanning BLE", async () => { setInspection(null); setPeripherals(await request<Peripheral[]>("pro_scan")); })}><Bluetooth size={16}/>{busy === "Scanning BLE" ? "Scanning…" : "Scan Bluetooth"}</button>
            <button className="button secondary" disabled={!!busy} onClick={() => void run("Reading HID inventory", async () => { setHid(await request<HidDevice[]>("pro_hid")); })}><Cpu size={16}/>List Windows HID</button>
          </div>
          <div className="pro-discovery-results"><div><h3>Observed BLE peripherals</h3>{peripherals.length ? peripherals.map((item) => <button className="pro-device-row" key={item.address} disabled={!!busy} onClick={() => void run("Inspecting standard GATT", async () => { setInspection(await request<Inspection>("pro_inspect", { address: item.address })); })}><Search size={15}/><span><b>{item.name}</b><small>{item.address} · {item.services.length} advertised services</small></span></button>) : <p>No scan result yet.</p>}</div>
            <div><h3>Windows HID inventory</h3>{hid.length ? <div className="pro-hid-list">{hid.map((item) => <div key={item.PNPDeviceID}><b>{item.Name}</b><small>{item.Manufacturer} · {item.PNPDeviceID}</small></div>)}</div> : <p>No inventory result yet.</p>}</div></div>
          {inspection && <div className="pro-inspection"><h3>Standard information from {inspection.address}</h3><p>Model remains unverified. Vendor configuration and OTA characteristics were not read.</p><div className="pro-info-grid">{Object.entries(inspection.standardValues).map(([key, value]) => <span key={key}><small>{key}</small><b>{typeof value === "object" ? value.error : value}</b></span>)}</div><details><summary>Observed GATT service IDs ({inspection.services.length})</summary><code>{inspection.services.map((service) => service.uuid).join(" · ")}</code></details></div>}
        </section>
        <section className="metal-panel pro-coming"><div className="panel-heading"><h2>Configuration</h2><span className="panel-code">COMING SOON</span></div><div className="pro-coming-grid">{["Six-button mapping", "Macros", "Four-motor haptics", "Display & lighting", "Trigger settings", "Saved Pro setups"].map((name) => <div key={name}><span>{name}</span><b>Awaiting hardware validation</b></div>)}</div><p>No X20 profile or command can be applied from this workspace. GIF support has not been established.</p></section>
      </main>
    </div>
  );
}
